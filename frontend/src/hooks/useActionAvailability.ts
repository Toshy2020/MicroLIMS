import { useCallback, useEffect, useMemo, useState } from "react";
import {
  actionAvailabilityService,
  type ActionAvailabilityDto,
  type AvailabilityKind
} from "../services/actionAvailabilityService";
import { i18n } from "../i18n";
import { SERVER_MESSAGES_NAMESPACE, translateServerDetail } from "../i18n/serverMessages";
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

  // The interface language the reasons are shown in, and the language whose serverMessages file is loaded.
  const [language, setLanguage] = useState(i18n.language);
  const [loadedLanguage, setLoadedLanguage] = useState<string | null>(null);
  useEffect(() => {
    const onChange = (lng: string) => setLanguage(lng);
    i18n.on("languageChanged", onChange);
    return () => i18n.off("languageChanged", onChange);
  }, []);

  const hasCodes = !!entries?.some((e) => !e.allowed && e.reasonCode);
  useEffect(() => {
    if (!hasCodes) return;
    let cancelled = false;
    // A failed load only leaves the English reasons in place; it never fails the availability load.
    i18n
      .loadNamespaces(SERVER_MESSAGES_NAMESPACE)
      .catch(() => undefined)
      .then(() => {
        if (!cancelled) setLoadedLanguage(language);
      });
    return () => {
      cancelled = true;
    };
  }, [hasCodes, language]);
  const translate = hasCodes && loadedLanguage === language;

  const get = useCallback(
    (action: string): ActionAvailabilityEntry | null => {
      if (loading || failed || !entries) return null;
      const found = entries.find((e) => e.action === action);
      if (!found) return null;
      if (found.allowed) return { allowed: true };
      const english = found.reason ?? undefined;
      if (!translate || !found.reasonCode) return { allowed: false, reason: english };
      return {
        allowed: false,
        reason: translateServerDetail({
          code: found.reasonCode,
          params: found.reasonParams ?? undefined,
          message: english ?? ""
        })
      };
    },
    [entries, loading, failed, translate, language]
  );

  return useMemo(() => ({ get, loading, failed, refresh }), [get, loading, failed, refresh]);
}
