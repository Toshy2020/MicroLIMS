import { useCallback, useEffect, useRef, useState } from "react";
import axios from "axios";
import { myWorkService } from "../services/myWorkService";
import type { MyWorkResponse } from "../myWorkTypes";

const POLL_INTERVAL_MS = 60_000;

function isAbortError(err: unknown): boolean {
  return axios.isCancel(err) || (err instanceof DOMException && err.name === "AbortError");
}

export interface UseMyWorkResult {
  data: MyWorkResponse | null;
  loading: boolean;
  refreshing: boolean;
  error: unknown;
  reload: () => void;
}

export function useMyWork(lab?: string): UseMyWorkResult {
  const [data, setData] = useState<MyWorkResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const controllerRef = useRef<AbortController | null>(null);
  const hasLoadedOnceRef = useRef(false);

  const fetchWork = useCallback(async () => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;

    if (hasLoadedOnceRef.current) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    try {
      const response = await myWorkService.get(lab, controller.signal);
      if (controller.signal.aborted) return;
      hasLoadedOnceRef.current = true;
      setData(response);
      setError(null);
    } catch (err: unknown) {
      if (controller.signal.aborted || isAbortError(err)) return;
      setError(err);
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [lab]);

  useEffect(() => {
    void fetchWork();
    return () => {
      controllerRef.current?.abort();
    };
  }, [fetchWork]);

  useEffect(() => {
    const handleFocus = () => {
      void fetchWork();
    };
    window.addEventListener("focus", handleFocus);

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void fetchWork();
      }
    }, POLL_INTERVAL_MS);

    return () => {
      window.removeEventListener("focus", handleFocus);
      window.clearInterval(intervalId);
    };
  }, [fetchWork]);

  const reload = useCallback(() => {
    void fetchWork();
  }, [fetchWork]);

  return { data, loading, refreshing, error, reload };
}
