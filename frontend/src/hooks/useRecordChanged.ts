import { useCallback, useEffect, useState } from "react";
import { useOptionalAuth } from "../contexts/AuthContext";
import { useLive, type LiveSignal } from "../contexts/LiveContext";

// A signal from the current user that arrives within this window of this tab's own
// successful save is that save echoing back, not a change by someone else.
export const OWN_SAVE_WINDOW_MS = 5000;

const ownSaves = new Map<string, number>();
const ownSaveListeners = new Set<(key: string) => void>();

function recordKey(type: string, id: number): string {
  return `${type}:${id}`;
}

/**
 * Marks a successful save made by THIS tab. Save paths call it after the server accepted the
 * write. A signal for the record that the same user's other tab produced is not affected.
 */
export function noteOwnSave(type: string, id: number | null | undefined): void {
  if (id == null) return;
  const key = recordKey(type, id);
  ownSaves.set(key, Date.now());
  for (const listener of [...ownSaveListeners]) listener(key);
}

function wasOwnSaveWithinWindow(key: string, arrivedAt: number): boolean {
  const savedAt = ownSaves.get(key);
  return savedAt !== undefined && Math.abs(arrivedAt - savedAt) <= OWN_SAVE_WINDOW_MS;
}

interface ChangedState {
  changed: boolean;
  byMe: boolean;
  at: string | null;
  // Arrival time in ms and whether the actor was this user: kept so a save that finishes after
  // its own signal already arrived can still withdraw the banner.
  arrivedAt: number | null;
}

const UNCHANGED: ChangedState = { changed: false, byMe: false, at: null, arrivedAt: null };

export interface RecordChangedState {
  changed: boolean;
  byMe: boolean;
  at: string | null;
  dismiss(): void;
}

/**
 * Watches one record and reports that someone else changed it. It only reports: it never reloads
 * and never touches the caller's form values. Unwatches when the id changes or on unmount.
 */
export function useRecordChanged(type: string, id: number | null): RecordChangedState {
  const { watchRecord, subscribe } = useLive();
  const userId = useOptionalAuth()?.userId ?? null;
  const [state, setState] = useState<ChangedState>(UNCHANGED);

  useEffect(() => {
    setState(UNCHANGED);
    if (id == null) return;

    const key = recordKey(type, id);
    const unwatch = watchRecord(type, id);
    const onSignal = (signal: LiveSignal) => {
      // The server key is `{type}:{id}`, or `record:{type}:{id}` from the data-layer queue.
      if (signal.key !== key && signal.key !== `record:${key}`) return;
      const arrivedAt = Date.now();
      const mine = userId !== null && signal.actorUserId === userId;
      if (mine && wasOwnSaveWithinWindow(key, arrivedAt)) return;
      setState({ changed: true, byMe: mine, at: new Date(arrivedAt).toISOString(), arrivedAt });
    };
    const unsubscribe = subscribe("record", null, onSignal);

    // The save response can arrive after its own signal: withdraw a banner it caused.
    const onOwnSave = (savedKey: string) => {
      if (savedKey !== key) return;
      setState((current) =>
        current.changed && current.byMe && current.arrivedAt !== null &&
        Math.abs(current.arrivedAt - Date.now()) <= OWN_SAVE_WINDOW_MS
          ? UNCHANGED
          : current
      );
    };
    ownSaveListeners.add(onOwnSave);

    return () => {
      ownSaveListeners.delete(onOwnSave);
      unsubscribe();
      unwatch();
    };
  }, [type, id, userId, watchRecord, subscribe]);

  const dismiss = useCallback(() => setState(UNCHANGED), []);

  return { changed: state.changed, byMe: state.byMe, at: state.at, dismiss };
}
