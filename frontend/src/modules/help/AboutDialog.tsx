import { useEffect, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Typography
} from "@mui/material";
import { i18n, useTranslation } from "../../i18n";
import { systemVersionService, type ServerVersion } from "../../services/systemVersionService";
import { getErrorMessage } from "../../utils/errorMessage";
import { monospaceFontFamily } from "../../theme/palette";

interface AboutDialogProps {
  open: boolean;
  onClose: () => void;
  // Called when the closing animation has finished; the caller returns focus here.
  onExited?: () => void;
}

type ServerState =
  | { status: "loading" }
  | { status: "ready"; info: ServerVersion }
  | { status: "error"; message: string };

export function AboutDialog({ open, onClose, onExited }: AboutDialogProps) {
  const { t } = useTranslation("help");
  const [server, setServer] = useState<ServerState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    setServer({ status: "loading" });
    systemVersionService
      .get(controller.signal)
      .then((info) => {
        if (!controller.signal.aborted) setServer({ status: "ready", info });
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setServer({ status: "error", message: getErrorMessage(err, i18n.t("about.loadFailed", { ns: "help" })) });
        }
      });
    return () => controller.abort();
  }, [open, attempt]);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      fullWidth
      aria-labelledby="about-dialog-title"
      disableRestoreFocus
      slotProps={{ transition: { onExited } }}
    >
      <DialogTitle id="about-dialog-title">
        <Typography component="span" variant="h6">{t("about.title")}</Typography>
      </DialogTitle>
      <DialogContent>
        <Box component="dl" sx={{ m: 0, display: "grid", gridTemplateColumns: "auto 1fr", columnGap: 2, rowGap: 1 }}>
          <Typography component="dt" variant="body2" color="text.secondary">{t("about.applicationVersion")}</Typography>
          <Typography component="dd" variant="body2" sx={{ m: 0, fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}>
            {__APP_VERSION__}
          </Typography>
          <Typography component="dt" variant="body2" color="text.secondary">{t("about.serverVersion")}</Typography>
          <Typography component="dd" variant="body2" sx={{ m: 0, fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}>
            {server.status === "ready" ? server.info.version : server.status === "loading" ? t("about.loading") : t("about.unavailable")}
          </Typography>
          <Typography component="dt" variant="body2" color="text.secondary">{t("about.environment")}</Typography>
          <Typography component="dd" variant="body2" sx={{ m: 0 }}>
            {server.status === "ready" ? server.info.environment : server.status === "loading" ? t("about.loading") : t("about.unavailable")}
          </Typography>
        </Box>
        {server.status === "loading" && (
          <Box role="status" sx={{ display: "flex", alignItems: "center", gap: 1, mt: 2 }}>
            <CircularProgress size={16} aria-hidden />
            <Typography variant="body2">{t("about.loadingServerVersion")}</Typography>
          </Box>
        )}
        {server.status === "error" && (
          <Alert
            severity="error"
            sx={{ mt: 2 }}
            action={<Button color="inherit" size="small" onClick={() => setAttempt((n) => n + 1)}>{t("shared.retry")}</Button>}
          >
            {server.message}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} autoFocus>{t("shared.close")}</Button>
      </DialogActions>
    </Dialog>
  );
}
