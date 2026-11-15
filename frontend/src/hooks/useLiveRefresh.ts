import { useCallback, useEffect, useRef } from "react";
import { useLive, useLiveReconnectSubscription } from "../contexts/LiveContext";

const SIGNAL_COALESCE_MS = 1_000;

export interface UseLiveRefreshOptions {
  kind: string;
  key?: string | null;
  refetch(): void;
  fallbackMs: number;
  enabled?: boolean;
}

function isTabVisible(): boolean {
  if (typeof document === "undefined") return true;
  return !document.hidden && document.visibilityState !== "hidden";
}

export function useLiveRefresh({
  kind,
  key = null,
  refetch,
  fallbackMs,
  enabled = true
}: UseLiveRefreshOptions): void {
  const { status, subscribe } = useLive();
  const subscribeReconnect = useLiveReconnectSubscription();

  const refetchRef = useRef(refetch);
  useEffect(() => {
    refetchRef.current = refetch;
  }, [refetch]);

  const lastRefetchAtRef = useRef<number | null>(null);
  const trailingTimerRef = useRef<number | null>(null);

  const clearTrailingTimer = useCallback(() => {
    if (trailingTimerRef.current !== null) {
      window.clearTimeout(trailingTimerRef.current);
      trailingTimerRef.current = null;
    }
  }, []);

  const executeRefetch = useCallback(() => {
    clearTrailingTimer();
    lastRefetchAtRef.current = Date.now();
    refetchRef.current();
  }, [clearTrailingTimer]);

  const triggerCoalescedRefetch = useCallback(() => {
    const now = Date.now();
    const last = lastRefetchAtRef.current;
    if (last === null || now - last >= SIGNAL_COALESCE_MS) {
      executeRefetch();
      return;
    }
    if (now === last) {
      return;
    }
    if (trailingTimerRef.current === null) {
      const delay = SIGNAL_COALESCE_MS - (now - last);
      trailingTimerRef.current = window.setTimeout(() => {
        trailingTimerRef.current = null;
        lastRefetchAtRef.current = Date.now();
        refetchRef.current();
      }, delay);
    }
  }, [executeRefetch]);

  useEffect(() => {
    if (!enabled || status !== "connected") {
      clearTrailingTimer();
      return;
    }

    const unsubscribe = subscribe(kind, key ?? null, () => {
      triggerCoalescedRefetch();
    });

    return () => {
      unsubscribe();
      clearTrailingTimer();
    };
  }, [enabled, status, kind, key, subscribe, triggerCoalescedRefetch, clearTrailingTimer]);

  useEffect(() => {
    if (!enabled) return;
    return subscribeReconnect(() => {
      executeRefetch();
    });
  }, [enabled, subscribeReconnect, executeRefetch]);

  useEffect(() => {
    if (!enabled || status === "connected" || fallbackMs <= 0) return;
    const intervalId = window.setInterval(() => {
      if (isTabVisible()) {
        executeRefetch();
      }
    }, fallbackMs);
    return () => {
      window.clearInterval(intervalId);
    };
  }, [enabled, status, fallbackMs, executeRefetch]);
}
