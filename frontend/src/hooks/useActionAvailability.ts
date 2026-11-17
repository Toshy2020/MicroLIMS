import { useCallback, useEffect, useMemo, useState } from "react";
import {
  actionAvailabilityService,
  type ActionAvailabilityDto,
  type AvailabilityKind
} from "../services/actionAvailabilityService";
import { useRecordChanged } from "./useRecordChanged";

export interface ActionAvailabilityEntry {
  allowed: boolean;
  reason?: string;
}

export interface ActionAvailability {
  // null while loading, when the load failed, or when the server does not list the action: the button then
  // stays usable and the server decides on click.
  get(action: string): ActionAvailabilityEntry | null;
  loading: boolean;
  failed: boolean;
  refresh(): void;
}

// The record types the live hub lets a client watch; the others have no signal to follow.
const LIVE_TYPE: Partial<Record<AvailabilityKind, string>> = {
  sample: "sample",
  "test-order": "testOrder"
};

export function useActionAvailability(
  kind: AvailabilityKind,
  id: number | null,
  sectionId?: number | null
): ActionAvailability {
  // The last answer, tagged with the record it is for: a refresh keeps showing it until the new one arrives,
  // so a gated button does not flip to enabled while it reloads.
  const key = `${kind}:${id}:${sectionId ?? ""}`;
  const [result, setResult] = useState<{ key: string; list: ActionAvailabilityDto[] | null } | null>(null);
  const [version, setVersion] = useState(0);

  const refresh = useCallback(() => setVersion((v) => v + 1), []);

  useEffect(() => {
    if (id == null) return;
    const controller = new AbortController();
    actionAvailabilityService
      .get(kind, id, sectionId, controller.signal)
      .then((list) => {
        if (!controller.signal.aborted) setResult({ key, list });
      })
      .catch(() => {
        if (!controller.signal.aborted) setResult({ key, list: null });
      });
    return () => controller.abort();
  }, [kind, id, sectionId, key, version]);

  const current = id != null && result?.key === key ? result : null;
  const loading = id != null && current === null;
  const failed = current !== null && current.list === null;
  const entries = current?.list ?? null;

  // Someone else changed the record: the reasons may have changed with it.
  const live = useRecordChanged(LIVE_TYPE[kind] ?? "", LIVE_TYPE[kind] ? id : null);
  const { changed, dismiss } = live;
  useEffect(() => {
    if (!changed) return;
    dismiss();
    refresh();
  }, [changed, dismiss, refresh]);

  const get = useCallback(
    (action: string): ActionAvailabilityEntry | null => {
      if (loading || failed || !entries) return null;
      const found = entries.find((e) => e.action === action);
      if (!found) return null;
      return found.allowed ? { allowed: true } : { allowed: false, reason: found.reason ?? undefined };
    },
    [entries, loading, failed]
  );

  return useMemo(() => ({ get, loading, failed, refresh }), [get, loading, failed, refresh]);
}
