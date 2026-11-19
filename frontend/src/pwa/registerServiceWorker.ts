import { useSyncExternalStore } from "react";

type ReloadFn = (reloadPage?: boolean) => Promise<void> | void;

interface UpdateState {
  updateAvailable: boolean;
  applyUpdate: () => void;
}

let updateAvailable = false;
let pendingReload: ReloadFn | null = null;
let registered = false;
const listeners = new Set<() => void>();

const applyUpdate = (): void => {
  if (!pendingReload) return;
  void pendingReload(true);
};

let snapshot: UpdateState = {
  updateAvailable: false,
  applyUpdate,
};

function notify(): void {
  snapshot = { updateAvailable, applyUpdate };
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

function getSnapshot(): UpdateState {
  return snapshot;
}

/**
 * Registers the service worker in production builds.
 * Deliberately a no-op in dev mode, tests, and environments without serviceWorker support.
 */
export function registerServiceWorker(): void {
  if (
    registered ||
    import.meta.env.DEV ||
    import.meta.env.MODE === "test" ||
    typeof navigator === "undefined" ||
    !("serviceWorker" in navigator)
  ) {
    return;
  }
  registered = true;
  void import("virtual:pwa-register").then(({ registerSW }) => {
    const updateSW = registerSW({
      onNeedRefresh() {
        pendingReload = updateSW;
        updateAvailable = true;
        notify();
      },
    });
  });
}

/**
 * Subscribes to service-worker update availability and provides a manual reload trigger.
 */
export function useServiceWorkerUpdate(): UpdateState {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

/**
 * Test helper to simulate a waiting service worker without mocking UI components or hooks.
 */
export function setPendingUpdateForTest(reload: ReloadFn): void {
  pendingReload = reload;
  updateAvailable = true;
  notify();
}

/**
 * Test helper to reset service-worker update state between tests.
 */
export function resetServiceWorkerStateForTest(): void {
  updateAvailable = false;
  pendingReload = null;
  registered = false;
  notify();
}
