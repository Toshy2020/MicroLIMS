import { useSyncExternalStore } from "react";

let openCount = 0;
const listeners = new Set<() => void>();

function notify(): void {
  for (const listener of listeners) {
    listener();
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): boolean {
  return openCount > 0;
}

/**
 * Increments the count of currently open BenchDialog instances.
 */
export function incrementOpenBenchDialogs(): () => void {
  openCount += 1;
  notify();
  let decremented = false;
  return () => {
    if (decremented) return;
    decremented = true;
    openCount = Math.max(0, openCount - 1);
    notify();
  };
}

/**
 * Returns true while at least one BenchDialog is open.
 */
export function useBenchDialogOpen(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
