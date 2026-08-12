import { useEffect, useRef, useState } from "react";
import { IncubationLock } from "../types/testWorkflowTypes";

// Option B countdown: the submit control stays visible but disabled,
// showing a live remaining-time readout, unlocking itself the instant
// the window ends - never hidden, mirroring the existing
// useIdleTimeout ref-held-interval pattern. The server is still the
// source of truth (every Submit* call is re-validated server-side);
// this only avoids the analyst needing to reload the page to see the
// button re-enable.
export function useIncubationCountdown(lock: IncubationLock | null, onUnlocked?: () => void) {
  const [remainingSeconds, setRemainingSeconds] = useState(lock?.remainingSeconds ?? 0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const firedRef = useRef(false);

  useEffect(() => {
    setRemainingSeconds(lock?.remainingSeconds ?? 0);
    firedRef.current = false;
    if (intervalRef.current) clearInterval(intervalRef.current);

    if (!lock || !lock.isLocked) return;

    const endTime = new Date(lock.incubationEndUtc).getTime();
    intervalRef.current = setInterval(() => {
      const remaining = Math.max(0, Math.ceil((endTime - Date.now()) / 1000));
      setRemainingSeconds(remaining);
      if (remaining === 0 && !firedRef.current) {
        firedRef.current = true;
        onUnlocked?.();
      }
    }, 1000);

    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lock?.incubationEndUtc, lock?.isLocked]);

  const isLocked = !!lock?.isLocked && remainingSeconds > 0;
  const formatted = formatCountdown(remainingSeconds);
  return { isLocked, remainingSeconds, formatted };
}

function formatCountdown(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  if (h > 0) return `${h}h ${m}m ${s}s`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}
