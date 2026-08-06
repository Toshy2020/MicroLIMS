import { useCallback, useEffect, useRef, useState } from "react";

// GMP control: an unattended logged-in terminal breaks attributability,
// so this has no "never log me out" escape hatch - only activity or the
// explicit "Stay signed in" click resets the clock.
const IDLE_TIMEOUT_MS = 20 * 60 * 1000;
const WARNING_COUNTDOWN_SECONDS = 60;
const ACTIVITY_EVENTS = ["mousemove", "keydown", "click", "scroll"] as const;

export function useIdleTimeout(onTimeout: () => void) {
  const [showWarning, setShowWarning] = useState(false);
  const [secondsRemaining, setSecondsRemaining] = useState(WARNING_COUNTDOWN_SECONDS);

  const idleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const countdownIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const onTimeoutRef = useRef(onTimeout);
  onTimeoutRef.current = onTimeout;

  const clearTimers = useCallback(() => {
    if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
    if (countdownIntervalRef.current) clearInterval(countdownIntervalRef.current);
    idleTimerRef.current = null;
    countdownIntervalRef.current = null;
  }, []);

  const startCountdown = useCallback(() => {
    setShowWarning(true);
    setSecondsRemaining(WARNING_COUNTDOWN_SECONDS);
    countdownIntervalRef.current = setInterval(() => {
      setSecondsRemaining((prev) => {
        if (prev <= 1) {
          clearTimers();
          onTimeoutRef.current();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [clearTimers]);

  const resetIdleTimer = useCallback(() => {
    clearTimers();
    setShowWarning(false);
    idleTimerRef.current = setTimeout(startCountdown, IDLE_TIMEOUT_MS);
  }, [clearTimers, startCountdown]);

  useEffect(() => {
    resetIdleTimer();
    ACTIVITY_EVENTS.forEach((evt) => window.addEventListener(evt, resetIdleTimer));
    return () => {
      ACTIVITY_EVENTS.forEach((evt) => window.removeEventListener(evt, resetIdleTimer));
      clearTimers();
    };
  }, [resetIdleTimer, clearTimers]);

  return { showWarning, secondsRemaining, stayLoggedIn: resetIdleTimer };
}
