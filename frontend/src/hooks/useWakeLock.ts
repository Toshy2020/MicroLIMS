import { useEffect } from "react";

// Minimal local typing: not every TS lib ships the Screen Wake Lock API.
interface WakeLockSentinelLike {
  release: () => Promise<void>;
}
interface WakeLockLike {
  request: (type: "screen") => Promise<WakeLockSentinelLike>;
}

function getWakeLock(): WakeLockLike | null {
  const candidate = (navigator as Navigator & { wakeLock?: WakeLockLike }).wakeLock;
  return candidate ?? null;
}

// Keeps the screen awake while `active` (for example, an open bench dialog).
// Unsupported browsers and refused requests are ignored on purpose: staying
// awake is a convenience, never a requirement. The browser drops the lock when
// the page is hidden, so it is requested again when the page is visible.
export function useWakeLock(active: boolean): void {
  useEffect(() => {
    if (!active || !getWakeLock()) return;

    let cancelled = false;
    let sentinel: WakeLockSentinelLike | null = null;

    const acquire = async () => {
      const wakeLock = getWakeLock();
      if (!wakeLock || sentinel) return;
      try {
        const granted = await wakeLock.request("screen");
        if (cancelled) {
          await granted.release().catch(() => {});
          return;
        }
        sentinel = granted;
      } catch {
        // Denied or unavailable (battery saver, background tab): carry on.
      }
    };

    const onVisibility = () => {
      if (document.visibilityState !== "visible") {
        // The browser already released it; forget the stale sentinel.
        sentinel = null;
        return;
      }
      void acquire();
    };

    void acquire();
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", onVisibility);
      const held = sentinel;
      sentinel = null;
      if (held) void held.release().catch(() => {});
    };
  }, [active]);
}
