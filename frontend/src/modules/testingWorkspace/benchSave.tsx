import { useEffect, useState } from "react";
import { Alert, Button } from "@mui/material";
import { i18n as appI18n, useTranslation } from "../../i18n";
import { isNetworkError } from "../../services/networkError";
import { OFFLINE_MESSAGE } from "../../components/bench/ConnectionBanner";

// How a save that did not reach MicroLIMS ended, for the bench forms that keep a local draft.
export type SaveFailure =
  | { kind: "network" }
  | { kind: "conflict"; message: string }
  | { kind: "other"; message: string | undefined };

export function serverMessage(error: unknown): string | undefined {
  const message = (error as { response?: { data?: { message?: unknown } } } | null)?.response?.data?.message;
  return typeof message === "string" && message !== "" ? message : undefined;
}

export function classifySaveFailure(error: unknown): SaveFailure {
  if (isNetworkError(error)) return { kind: "network" };
  const status = (error as { response?: { status?: unknown } } | null)?.response?.status;
  if (status === 409) {
    return { kind: "conflict", message: serverMessage(error) ?? appI18n.t("testingWorkspace:benchSave.conflictFallback") };
  }
  return { kind: "other", message: serverMessage(error) };
}

// The optional last argument of a save call: the headers only when the draft is
// old enough to carry X-Draft-Started-At, so a prompt save is called exactly as before.
export function draftHeaderArgs(headers: Record<string, string>): [] | [{ headers: Record<string, string> }] {
  return Object.keys(headers).length > 0 ? [{ headers }] : [];
}

// True after a save found no connection. Cleared by the browser's "online" event or by
// the analyst choosing to try again; it never sends anything by itself.
export function useKeptOffline(): [boolean, (value: boolean) => void] {
  const [offline, setOffline] = useState(false);
  useEffect(() => {
    if (!offline) return;
    const back = () => setOffline(false);
    window.addEventListener("online", back);
    return () => window.removeEventListener("online", back);
  }, [offline]);
  return [offline, setOffline];
}

export function OfflineDraftNotice({ onTryAgain }: { onTryAgain: () => void }) {
  const { t } = useTranslation("testingWorkspace");
  return (
    <Alert
      severity="warning"
      sx={{ mb: 2 }}
      action={
        <Button color="inherit" size="small" onClick={onTryAgain}>
          {t("benchSave.tryAgain")}
        </Button>
      }
    >
      {OFFLINE_MESSAGE}
    </Alert>
  );
}
