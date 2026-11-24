import type { LaboratorySection } from "../services/laboratorySectionService";

// The last /org/my-sections answer, kept per user so the Laboratories menu can show on first paint.
// It is a display hint only: the fresh response replaces it on every load, the server decides access,
// it expires after MAX_AGE_MS, it is cleared at sign-out and when a fetch fails.
const PREFIX = "microlims.myLabs.v2:";
const MAX_AGE_MS = 12 * 60 * 60 * 1000;

const keyFor = (username: string) => `${PREFIX}${username}`;

function isSection(value: unknown): value is LaboratorySection {
  return typeof value === "object" && value !== null && typeof (value as { sectionCode?: unknown }).sectionCode === "string";
}

export function readCachedLabs(username: string | null | undefined, now = Date.now()): LaboratorySection[] | null {
  if (!username) return null;
  try {
    const raw = window.localStorage.getItem(keyFor(username));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as { savedAt?: unknown; labs?: unknown } | null;
    if (
      !parsed || typeof parsed.savedAt !== "number" || now - parsed.savedAt > MAX_AGE_MS || parsed.savedAt > now + 60_000 ||
      !Array.isArray(parsed.labs) || !parsed.labs.every(isSection)
    ) {
      window.localStorage.removeItem(keyFor(username));
      return null;
    }
    return parsed.labs;
  } catch {
    return null;
  }
}

export function writeCachedLabs(username: string | null | undefined, labs: LaboratorySection[], now = Date.now()) {
  if (!username) return;
  try {
    window.localStorage.setItem(keyFor(username), JSON.stringify({ savedAt: now, labs }));
  } catch {
    /* storage unavailable: the menu waits for the fetch */
  }
}

export function clearCachedLabs(username?: string | null) {
  try {
    if (username) window.localStorage.removeItem(keyFor(username));
    else for (const k of Object.keys(window.localStorage)) if (k.startsWith(PREFIX)) window.localStorage.removeItem(k);
  } catch {
    /* ignore */
  }
}

// Counts sign-ins and sign-outs in this tab. An in-flight laboratories request belongs to the session
// that started it: after a sign-out or a new sign-in (even by the same user) its answer is discarded.
// Only a number is kept here, never a token.
let labsSession = 0;
export function currentLabsSession() {
  return labsSession;
}
export function startNewLabsSession() {
  labsSession += 1;
}
