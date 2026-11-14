import { useCallback, useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { draftId, draftStore, type Draft } from "../services/drafts/draftStore";

// Shared behaviour of every bench form that keeps unsaved entries on the tablet:
// save locally while the analyst types, offer the entry back after an interruption,
// and report the submit outcome. A draft is never a record and is never sent by this
// hook: only the form's own Save button submits.
//
// Security rules (Review Focus 1-3):
//  - Only the fields `pick` returns are ever stored. Every call passes an explicit
//    allow-list; there is no deny-list. A password, signature comment or token must
//    never be in `pick`'s result.
//  - Drafts are looked up by the signed-in user's id, so another user on the same
//    tablet is never offered, and never able to open, this user's draft.
//  - `draftRecordVersion` is the version of the record the draft was started
//    against. Send it as If-Match on Save; on a 409 call `onRejected`, which keeps
//    the draft. Never retry with a fresh version automatically.

const WRITE_DELAY_MS = 400;
const LATE_SAVE_MS = 5 * 60 * 1000;
export const DRAFT_STARTED_HEADER = "X-Draft-Started-At";

export interface BenchDraftOptions<T> {
  formKey: string;
  recordKey: string;
  // The version of the record as the form loaded it.
  recordVersion: number | null;
  title: string;
  values: T;
  // Allow-list of the fields that may be stored.
  pick(values: T): Partial<T>;
  enabled: boolean;
}

export interface BenchDraft<T> {
  // A draft found for this user and record. Offered, never applied by the hook.
  restored: Partial<T> | null;
  restoredStartedAt: string | null;
  // Resolves the offer and returns the offered values for the form to apply.
  acceptRestore(): Partial<T> | null;
  discard(): Promise<void>;
  onSubmitted(): Promise<void>;
  onRejected(reason: string): Promise<void>;
  // { "X-Draft-Started-At": ... } once the draft is more than 5 minutes old, else {}.
  submitHeaders(): Record<string, string>;
  // The version to send as If-Match: the one the draft (or, without a draft, the form) started with.
  draftRecordVersion: number | null;
  storageAvailable: boolean;
}

interface Loaded {
  key: string;
  available: boolean;
  draft?: Draft<unknown>;
}

interface Session {
  key: string;
  baseline: string | null;
  dirty: boolean;
  submitted: boolean;
  startedAt: string | null;
  version: number | null;
}

export function useBenchDraft<T>(opts: BenchDraftOptions<T>): BenchDraft<T> {
  const { userId } = useAuth();
  const location = useLocation();
  const { formKey, recordKey, enabled } = opts;
  const active = enabled && userId != null;
  const key = userId == null ? "" : draftId(userId, formKey, recordKey);
  const pickedJson = JSON.stringify(opts.pick(opts.values));

  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [choice, setChoice] = useState<{ key: string; kind: "accepted" | "discarded" } | null>(null);
  const [writeFailedKey, setWriteFailedKey] = useState<string | null>(null);

  const session = useRef<Session | null>(null);
  if (session.current?.key !== key) {
    session.current = { key, baseline: null, dirty: false, submitted: false, startedAt: null, version: opts.recordVersion };
  }

  const latest = useRef({ ...opts, userId, key, route: location.pathname + location.search, pickedJson });
  latest.current = { ...opts, userId, key, route: location.pathname + location.search, pickedJson };

  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef(false);

  const here = loaded?.key === key ? loaded : null;
  const ready = active && here !== null && here.available && writeFailedKey !== key;
  const offered = here?.draft && choice?.key !== key ? here.draft : null;

  // Load the signed-in user's own draft for this record, once per record.
  useEffect(() => {
    if (!active || userId == null) return;
    let cancelled = false;
    void (async () => {
      if (!(await draftStore.available())) {
        if (!cancelled) setLoaded({ key, available: false });
        return;
      }
      const draft = await draftStore.get<unknown>(userId, formKey, recordKey).catch(() => undefined);
      if (cancelled) return;
      const s = session.current;
      if (draft && s?.key === key) {
        s.startedAt = draft.startedAtDevice;
        s.version = draft.recordVersion;
      }
      setLoaded({ key, available: true, draft });
    })();
    return () => {
      cancelled = true;
    };
  }, [active, userId, formKey, recordKey, key]);

  const persist = useCallback(async (state: "open" | "rejected", rejectReason?: string) => {
    const l = latest.current;
    const s = session.current;
    if (l.userId == null || !s || s.key !== l.key) return;
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    pending.current = false;
    const now = new Date().toISOString();
    s.startedAt ??= now;
    const draft: Draft<unknown> = {
      id: l.key,
      userId: l.userId,
      formKey: l.formKey,
      recordKey: l.recordKey,
      recordVersion: s.version,
      title: l.title,
      route: l.route,
      values: l.pick(l.values),
      startedAtDevice: s.startedAt,
      updatedAtDevice: now,
      state,
      ...(rejectReason === undefined ? {} : { rejectReason })
    };
    try {
      await draftStore.put(draft);
    } catch {
      // The tablet's storage failed: stop saving and tell the form (storageAvailable false).
      setWriteFailedKey(l.key);
    }
  }, []);

  const flush = useCallback(() => {
    if (pending.current) void persist("open");
  }, [persist]);

  // A form that is closed takes its baseline again when it reopens, so a discard followed by a
  // reset of the form does not look like new entries.
  useEffect(() => {
    const s = session.current;
    if (!active && s) s.baseline = null;
  }, [active]);

  // Save the picked fields shortly after each change; the first value is the baseline.
  useEffect(() => {
    const s = session.current;
    if (!active || !s) return;
    if (s.baseline === null) s.baseline = pickedJson;
    if (pickedJson !== s.baseline && !s.dirty) {
      s.dirty = true;
      // The tablet's own clock, as stated. Kept if an older draft is loaded later.
      s.startedAt ??= new Date().toISOString();
    }
    if (!ready || !s.dirty || s.submitted || offered) return;
    pending.current = true;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void persist("open"), WRITE_DELAY_MS);
  }, [active, ready, pickedJson, offered, persist]);

  // Do not lose the last typed values when the page is hidden or closed.
  useEffect(() => {
    const onHide = () => {
      if (document.visibilityState === "hidden") flush();
    };
    document.addEventListener("visibilitychange", onHide);
    window.addEventListener("pagehide", flush);
    return () => {
      document.removeEventListener("visibilitychange", onHide);
      window.removeEventListener("pagehide", flush);
      flush();
    };
  }, [flush]);

  const stopPending = () => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    pending.current = false;
  };

  const acceptRestore = (): Partial<T> | null => {
    if (!offered) return null;
    setChoice({ key, kind: "accepted" });
    return offered.values as Partial<T>;
  };

  const discard = async (): Promise<void> => {
    stopPending();
    const s = session.current;
    if (s?.key === key) {
      s.startedAt = null;
      s.version = opts.recordVersion;
      s.dirty = false;
      s.baseline = pickedJson;
    }
    setChoice({ key, kind: "discarded" });
    if (ready) await draftStore.remove(key);
  };

  const onSubmitted = async (): Promise<void> => {
    stopPending();
    const s = session.current;
    if (s?.key === key) {
      s.submitted = true;
      s.startedAt = null;
    }
    if (ready) await draftStore.remove(key);
  };

  const onRejected = async (reason: string): Promise<void> => {
    if (!ready) return;
    await persist("rejected", reason);
  };

  const submitHeaders = (): Record<string, string> => {
    const startedAt = session.current?.key === key ? session.current.startedAt : null;
    if (!startedAt) return {};
    const started = Date.parse(startedAt);
    if (Number.isNaN(started) || Date.now() - started <= LATE_SAVE_MS) return {};
    return { [DRAFT_STARTED_HEADER]: startedAt };
  };

  const draftVersion = choice?.key === key && choice.kind === "discarded" ? opts.recordVersion : (here?.draft?.recordVersion ?? session.current?.version ?? null);

  return {
    restored: offered ? (offered.values as Partial<T>) : null,
    restoredStartedAt: offered ? offered.startedAtDevice : null,
    acceptRestore,
    discard,
    onSubmitted,
    onRejected,
    submitHeaders,
    draftRecordVersion: draftVersion,
    storageAvailable: here ? here.available && writeFailedKey !== key : true
  };
}
