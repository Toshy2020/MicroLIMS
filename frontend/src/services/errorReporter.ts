import { apiClient, getLastCorrelationId } from "./apiClient";

// Reports client-side crashes to the backend error monitoring page.
//
// Every path here is fire-and-forget and swallows its own failures. The
// app is already broken when this runs; a reporting error must not become
// a second crash, and must not surface any UI - the house pattern is a
// local <Alert severity=...> owned by the component that failed, never a
// global toast (Frozen Principle #3 - the frontend decides nothing).

export type ClientErrorSource = "errorBoundary" | "windowError" | "unhandledRejection";

interface ClientErrorReport {
  message: string;
  errorType?: string;
  stack?: string;
  componentStack?: string;
  route?: string;
  userAgent?: string;
  correlationId?: string;
  source: ClientErrorSource;
}

// Mirrors the server-side caps so an oversized payload is trimmed here
// rather than rejected there - a truncated report beats no report.
const MESSAGE_MAX = 2000;
const STACK_MAX = 8000;
const ROUTE_MAX = 500;
const USER_AGENT_MAX = 400;

// A crash loop can fire window.onerror hundreds of times a second. The
// backend rate limiter is the real guard; this stops us generating the
// flood in the first place, which also keeps the browser responsive.
const MAX_REPORTS_PER_SESSION = 25;
const DUPLICATE_WINDOW_MS = 10_000;

// React re-reports a boundary-caught error to window.onerror, and in a
// production build that re-report arrives FIRST. Reporting from the window
// handlers immediately would therefore let the thin entry win the
// de-duplication and discard the boundary's Critical one with its
// component stack. Holding window reports back briefly lets the boundary
// claim the key first; a real window error, with no boundary involved,
// is merely reported a fraction of a second later.
const WINDOW_HANDLER_DEFER_MS = 150;

let reportsSent = 0;
const recentlyReported = new Map<string, number>();

function clamp(value: string | undefined | null, max: number): string | undefined {
  if (!value) return undefined;
  const trimmed = String(value).trim();
  if (!trimmed) return undefined;
  return trimmed.length <= max ? trimmed : trimmed.slice(0, max);
}

// location.pathname only. A full URL carries the query string, and a
// filtered list view puts sample references and result values in there -
// ErrorLog is prunable non-GxP operational data and must not become a
// shadow copy of laboratory records.
function currentRoute(): string | undefined {
  try {
    return clamp(window.location?.pathname, ROUTE_MAX);
  } catch {
    return undefined;
  }
}

function isDuplicate(key: string): boolean {
  const now = Date.now();

  for (const [seen, at] of recentlyReported) {
    if (now - at > DUPLICATE_WINDOW_MS) recentlyReported.delete(seen);
  }

  const last = recentlyReported.get(key);
  if (last !== undefined && now - last < DUPLICATE_WINDOW_MS) return true;

  recentlyReported.set(key, now);
  return false;
}

export function reportClientError(input: {
  error: unknown;
  source: ClientErrorSource;
  componentStack?: string;
  deferMs?: number;
}): void {
  if (input.deferMs) {
    window.setTimeout(() => reportClientError({ ...input, deferMs: 0 }), input.deferMs);
    return;
  }

  try {
    if (reportsSent >= MAX_REPORTS_PER_SESSION) return;

    const error = input.error;
    const asError = error instanceof Error ? error : undefined;

    const message =
      clamp(asError?.message, MESSAGE_MAX) ??
      clamp(typeof error === "string" ? error : undefined, MESSAGE_MAX) ??
      "Unknown client error";

    const route = currentRoute();

    // Keyed WITHOUT the source on purpose. React re-reports an error its
    // boundary already handled to window.onerror - in production builds as
    // well as development - so one render crash arrives here twice, via
    // two different sources. Including the source would file it as two
    // Incidents, which is exactly the "one thing went wrong" premise this
    // system exists to uphold. The boundary reports first, so the richer
    // Critical entry with the component stack is the one that survives.
    if (isDuplicate(`${message}|${route ?? ""}`)) return;

    reportsSent += 1;

    const payload: ClientErrorReport = {
      message,
      errorType: clamp(asError?.name, 200),
      stack: clamp(asError?.stack, STACK_MAX),
      componentStack: clamp(input.componentStack, STACK_MAX),
      route,
      userAgent: clamp(navigator?.userAgent, USER_AGENT_MAX),
      // Ties this crash to the backend error from the same user action.
      // Undefined when no API call has failed recently, in which case the
      // server falls back to this request's own correlation id.
      correlationId: getLastCorrelationId(),
      source: input.source
    };

    // Deliberately not awaited, and errors are dropped: the report is
    // best-effort. A 429 from the rate limiter is an expected outcome
    // here, not a problem to surface.
    void apiClient.post("/system/client-errors", payload).catch(() => undefined);
  } catch {
    // Reporting must never throw into the caller - an ErrorBoundary that
    // throws while handling an error takes down the whole tree.
  }
}

// Registers window-level handlers for errors React never sees: async
// callbacks, event handlers, and rejected promises. Safe to call twice.
let handlersInstalled = false;

export function installGlobalErrorHandlers(): void {
  if (handlersInstalled || typeof window === "undefined") return;
  handlersInstalled = true;

  window.addEventListener("error", (event) => {
    // Resource load failures (a missing image, a chunk that 404s) also
    // raise "error" but carry no Error object and are not app crashes.
    if (!event.error) return;
    reportClientError({
      error: event.error,
      source: "windowError",
      deferMs: WINDOW_HANDLER_DEFER_MS
    });
  });

  window.addEventListener("unhandledrejection", (event) => {
    reportClientError({
      error: event.reason,
      source: "unhandledRejection",
      deferMs: WINDOW_HANDLER_DEFER_MS
    });
  });
}
