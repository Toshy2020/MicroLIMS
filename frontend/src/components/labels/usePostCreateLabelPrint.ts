import { useCallback, useEffect, useRef, useState } from "react";
import { labelService, type LabelTarget } from "../../services/labelService";

/**
 * Checks whether newly created records can be printed without blocking or
 * delaying the surrounding save flow. Opens PrintLabelsDialog only when the
 * server confirms at least one target is printable.
 */
export function usePostCreateLabelPrint() {
  const [targets, setTargets] = useState<LabelTarget[]>([]);
  const controllerRef = useRef<AbortController | null>(null);
  const requestIdRef = useRef(0);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      controllerRef.current?.abort();
    };
  }, []);

  const offerPrint = useCallback((candidates: readonly LabelTarget[]) => {
    const valid = candidates.filter((t) => t.entityId > 0 && t.code.trim().length > 0);
    if (valid.length === 0) return;

    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    const requestId = ++requestIdRef.current;

    labelService
      .getStatus(valid, controller.signal)
      .then((statuses) => {
        if (controller.signal.aborted || !mountedRef.current || requestIdRef.current !== requestId) {
          return;
        }
        if (statuses.some((s) => s.canPrint)) {
          setTargets(valid);
        }
      })
      .catch(() => {
        // Post-creation label printing is optional; errors or denied status must not interrupt creation.
      });
  }, []);

  const closeDialog = useCallback(() => {
    setTargets([]);
  }, []);

  return {
    dialogOpen: targets.length > 0,
    targets,
    offerPrint,
    closeDialog
  };
}
