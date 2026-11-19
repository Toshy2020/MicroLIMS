import { useEffect, useId, useState, type FormEvent } from "react";
import { Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { UserRecord, UserService } from "../services/UserService";

export interface RelinkDirectoryDialogProps {
  open: boolean;
  user: UserRecord | null;
  onClose: () => void;
  onRelinked: () => void;
}

/** Clears a user's company account link so it can be linked again; a reason is mandatory. */
export function RelinkDirectoryDialog({ open, user, onClose, onRelinked }: RelinkDirectoryDialogProps) {
  const { t } = useTranslation("users");
  const { t: tc } = useTranslation("common");
  const formId = useId();
  const messageId = useId();
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) { setReason(""); setSubmitting(false); }
  }, [open, user?.id]);

  const trimmed = reason.trim();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!user || !trimmed || submitting) return;
    setSubmitting(true);
    try {
      await UserService.relinkDirectory(user.id, trimmed);
      toast.success(t("relink.success", { username: user.username }));
      onRelinked();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("relink.error")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="xs" fullWidth aria-describedby={messageId}>
      <DialogTitle>{t("relink.title")}</DialogTitle>
      <DialogContent>
        <Box component="form" id={formId} onSubmit={submit} noValidate>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography id={messageId} variant="body2">
              <Trans
                i18nKey="relink.message"
                ns="users"
                values={{ fullName: user?.fullName ?? "", username: user?.username ?? "" }}
                components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <TextField
              label={t("fields.reason")} size="small" multiline minRows={2} value={reason} required fullWidth
              onChange={(e) => setReason(e.target.value)} disabled={submitting}
              placeholder={t("relink.reasonPlaceholder")}
            />
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting} autoFocus>{tc("actions.cancel")}</Button>
        <Button
          type="submit" form={formId} variant="contained" disabled={submitting || !trimmed}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("relink.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
