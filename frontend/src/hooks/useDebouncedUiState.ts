import { useCallback, useEffect, useRef } from "react";
import { useUiState } from "./useUiState";

export const PERSIST_DEBOUNCE_MS = 500;

// A stored interface choice (last laboratory, last tab). Reads come from the
// shared per-user store (which already falls back to this browser when the
// store is unreachable). Writes are debounced and run after render, so a
// choice never blocks the page; a pending write is sent when the page closes.
export function useDebouncedUiState(key: string): { value: string | null; save: (next: string) => void } {
  const { value, set } = useUiState(key);
  const setRef = useRef(set);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef<string | null>(null);

  useEffect(() => {
    setRef.current = set;
  }, [set]);

  const flush = useCallback(() => {
    if (timer.current !== null) clearTimeout(timer.current);
    timer.current = null;
    const next = pending.current;
    pending.current = null;
    // The choice is a convenience: a refused or failed save must not interrupt the page.
    if (next !== null) void setRef.current(next).catch(() => undefined);
  }, []);

  const save = useCallback((next: string) => {
    pending.current = next;
    if (timer.current !== null) clearTimeout(timer.current);
    timer.current = setTimeout(flush, PERSIST_DEBOUNCE_MS);
  }, [flush]);

  useEffect(() => flush, [flush]);

  return { value, save };
}
