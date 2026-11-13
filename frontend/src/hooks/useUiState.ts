import { useCallback, useEffect, useSyncExternalStore } from "react";
import { uiStateService, type UiStateMap } from "../services/uiStateService";

// Per-user interface preferences kept on the server, with a localStorage
// fallback when the server store cannot be reached. One shared request serves
// every component that uses the hook; it is cancelled when the last one unmounts.

type Status = "idle" | "loading" | "ready" | "failed";

interface Snapshot {
  status: Status;
  map: UiStateMap;
}

const IDLE: Snapshot = { status: "idle", map: {} };

let snapshot: Snapshot = IDLE;
let owner: string | null = null;
let controller: AbortController | null = null;
let subscribers = 0;
const listeners = new Set<() => void>();

function emit(next: Snapshot) {
  snapshot = next;
  listeners.forEach((l) => l());
}

function currentOwner(): string {
  try {
    return localStorage.getItem("microlims_user_id") ?? "anonymous";
  } catch {
    // Storage blocked: every session shares the "anonymous" bucket.
    return "anonymous";
  }
}

function storageKey(key: string): string {
  return `microlims.uiState.${currentOwner()}.${key}`;
}

function readLocal(key: string): string | null {
  try {
    return localStorage.getItem(storageKey(key));
  } catch {
    // Storage unavailable (private window, blocked site data): behave as "not set".
    return null;
  }
}

function writeLocal(key: string, value: string | null): boolean {
  try {
    if (value === null) localStorage.removeItem(storageKey(key));
    else localStorage.setItem(storageKey(key), value);
    return true;
  } catch {
    return false;
  }
}

function load() {
  const ownerAtStart = currentOwner();
  owner = ownerAtStart;
  const mine = new AbortController();
  controller = mine;
  emit({ status: "loading", map: {} });
  uiStateService
    .getAll(mine.signal)
    .then((map) => {
      if (!mine.signal.aborted) emit({ status: "ready", map });
    })
    .catch(() => {
      // The store is unreachable: fall back to this browser's own copy.
      if (!mine.signal.aborted) emit({ status: "failed", map: {} });
    });
}

function acquire() {
  subscribers += 1;
  // A different user signed in on this page: never show the previous user's values.
  if (snapshot.status !== "idle" && owner !== currentOwner()) {
    controller?.abort();
    snapshot = IDLE;
  }
  if (snapshot.status === "idle") load();
}

function release() {
  subscribers -= 1;
  if (subscribers === 0 && snapshot.status === "loading") {
    controller?.abort();
    controller = null;
    snapshot = IDLE;
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot() {
  return snapshot;
}

// For tests: forget everything held between renders.
export function resetUiStateCache() {
  controller?.abort();
  controller = null;
  subscribers = 0;
  owner = null;
  snapshot = IDLE;
  listeners.clear();
}

export interface UiStateHandle {
  value: string | null;
  loaded: boolean;
  set(value: string): Promise<void>;
  remove(): Promise<void>;
}

export function useUiState(key: string): UiStateHandle {
  const snap = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);

  useEffect(() => {
    acquire();
    return release;
  }, []);

  const loaded = snap.status === "ready" || snap.status === "failed";
  const value = snap.status === "ready" ? (snap.map[key] ?? null) : snap.status === "failed" ? readLocal(key) : null;

  const set = useCallback(async (next: string) => {
    try {
      await uiStateService.set(key, next);
      // Not loaded yet, or running on the local copy: the next load shows the saved value.
      if (snapshot.status === "ready") emit({ status: "ready", map: { ...snapshot.map, [key]: next } });
    } catch (err) {
      if (!writeLocal(key, next)) throw err;
      emit({ status: "failed", map: snapshot.map });
    }
  }, [key]);

  const remove = useCallback(async () => {
    try {
      await uiStateService.remove(key);
      if (snapshot.status === "ready") {
        const rest = { ...snapshot.map };
        delete rest[key];
        emit({ status: "ready", map: rest });
      }
    } catch (err) {
      if (!writeLocal(key, null)) throw err;
      emit({ status: "failed", map: snapshot.map });
    }
  }, [key]);

  return { value, loaded, set, remove };
}
