import { Alert, Button } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";
import { useTranslation } from "../i18n";

// Shown where a supporting list failed to load. Without it, screens stored
// the failure as an empty list, so "could not load" read as "none exist" -
// no archived copies, no autoclaves, no history - which on a GMP record is
// a false statement rather than a missing one.
export function LoadErrorAlert({ message, onRetry, sx }: { message: string; onRetry?: () => void; sx?: SxProps<Theme> }) {
  const { t } = useTranslation();
  return (
    <Alert
      severity="error"
      sx={sx}
      action={onRetry ? <Button color="inherit" size="small" onClick={onRetry}>{t("actions.retry")}</Button> : undefined}
    >
      {message}
    </Alert>
  );
}

// Names every list a form could not load (see useLoadFailures). Renders
// nothing when all loaded.
export function LoadFailuresAlert({
  failed,
  retryHint,
  sx
}: {
  failed: string[];
  retryHint?: string;
  sx?: SxProps<Theme>;
}) {
  const { t } = useTranslation();
  if (failed.length === 0) return null;
  return (
    <LoadErrorAlert
      sx={sx}
      message={t("components.loadErrorAlert.message", {
        count: failed.length,
        lists: failed.join(t("listSeparator", { defaultValue: ", " })),
        hint: retryHint ?? t("components.loadErrorAlert.defaultHint")
      })}
    />
  );
}
