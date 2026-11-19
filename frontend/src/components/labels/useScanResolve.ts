import { useCallback, useEffect, useRef, useState } from "react";
import axios from "axios";
import { labelService, type ScanResolveResponse } from "../../services/labelService";

function isAbortError(err: unknown): boolean {
  return (
    axios.isCancel(err) ||
    ((err instanceof DOMException || err instanceof Error) &&
      (err.name === "AbortError" || err.name === "CanceledError"))
  );
}

/**
 * Resolves a scanned barcode/code via `labelService.resolveScan`.
 * Aborts any previous in-flight request when a new call starts or on unmount;
 * an aborted or superseded call resolves to `null`.
 */
export function useScanResolve(): {
  resolve(value: string): Promise<ScanResolveResponse | null>;
  pending: boolean;
} {
  const [pending, setPending] = useState(false);
  const controllerRef = useRef<AbortController | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      controllerRef.current?.abort();
      controllerRef.current = null;
    };
  }, []);

  const resolve = useCallback(async (value: string): Promise<ScanResolveResponse | null> => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setPending(true);

    try {
      const response = await labelService.resolveScan(value, controller.signal);
      if (!mountedRef.current || controller.signal.aborted || controllerRef.current !== controller) {
        return null;
      }
      return response;
    } catch (err: unknown) {
      if (
        !mountedRef.current ||
        controller.signal.aborted ||
        controllerRef.current !== controller ||
        isAbortError(err)
      ) {
        return null;
      }
      throw err;
    } finally {
      if (mountedRef.current && controllerRef.current === controller) {
        controllerRef.current = null;
        setPending(false);
      }
    }
  }, []);

  return { resolve, pending };
}
