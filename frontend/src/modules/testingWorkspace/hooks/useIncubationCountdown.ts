import { useState, useEffect, useMemo } from "react";
import { formatRemainingCountdown, formatCountdownCompact } from "../utils/countdownFormatter";

interface UseIncubationCountdownOptions {
  minReadyAt?: Date | string | null;
  isIncubating?: boolean;
  overridden?: boolean;
  fallbackRemainingSeconds?: number | null;
}

export function useIncubationCountdown({
  minReadyAt,
  isIncubating = false,
  overridden = false,
  fallbackRemainingSeconds
}: UseIncubationCountdownOptions) {
  const [nowMs, setNowMs] = useState<number>(() => Date.now());

  const minReadyDate = useMemo(() => {
    if (!minReadyAt) return null;
    const parsed = minReadyAt instanceof Date ? minReadyAt : new Date(minReadyAt);
    return isNaN(parsed.getTime()) ? null : parsed;
  }, [minReadyAt]);

  // Tick every second while incubating and not overridden
  useEffect(() => {
    if (overridden) return;
    if (!isIncubating && !minReadyDate) return;

    const interval = setInterval(() => {
      setNowMs(Date.now());
    }, 1000);

    return () => clearInterval(interval);
  }, [isIncubating, minReadyDate, overridden]);

  const remainingSeconds = useMemo(() => {
    if (overridden) return 0;
    if (minReadyDate) {
      return Math.max(0, Math.floor((minReadyDate.getTime() - nowMs) / 1000));
    }
    if (fallbackRemainingSeconds != null && fallbackRemainingSeconds > 0) {
      return fallbackRemainingSeconds;
    }
    return 0;
  }, [overridden, minReadyDate, nowMs, fallbackRemainingSeconds]);

  const isTimeReady = useMemo(() => {
    if (overridden) return true;
    if (!isIncubating) return true;
    if (minReadyDate) {
      return nowMs >= minReadyDate.getTime();
    }
    // If actively incubating but no minReadyDate was found:
    if (fallbackRemainingSeconds != null) {
      return fallbackRemainingSeconds <= 0;
    }
    // Safe default for active incubation: keep locked until server or date confirms
    return false;
  }, [overridden, isIncubating, minReadyDate, nowMs, fallbackRemainingSeconds]);

  return {
    nowMs,
    minReadyDate,
    remainingSeconds,
    isTimeReady,
    formattedRemaining: formatRemainingCountdown(remainingSeconds),
    formattedCompact: formatCountdownCompact(remainingSeconds)
  };
}
