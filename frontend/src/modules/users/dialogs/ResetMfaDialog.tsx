import { useEffect, useId, useState, type FormEvent } from "react";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { UserRecord, UserService } from "../services/UserService";

export interface ResetMfaDialogProps {
  open: boolean;
  user: UserRecord | null;
  onClose: () => void;
  onReset: () => void;
}

/** Destructive confirmation dialog for resetting another user's second factor with mandatory reason. */
export function ResetMfaDialog({ open, user, onClose, onReset }: ResetMfaDialogProps) {
  const { t } = useTranslation("users");
  const { t: tc } = useTranslation("common");
  const formId = useId();
  const messageId = useId();
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) {
      setReason("");
      setSubmitting(false);
    }
  }, [open, user?.id]);

  const trimmedReason = reason.trim();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!user || !trimmedReason || submitting) return;
    setSubmitting(true);
    try {
      await UserService.resetMfa(user.id, trimmedReason);
      toast.success(t("resetMfa.success", { username: user.username }));
      onReset();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("resetMfa.error")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      maxWidth="xs"
      fullWidth
      aria-describedby={messageId}
      role="alertdialog"
    >
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <WarningAmberRoundedIcon color="error" aria-hidden="true" />
        {t("resetMfa.title")}
      </DialogTitle>
      <DialogContent>
        <Box component="form" id={formId} onSubmit={handleSubmit} noValidate>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography id={messageId} variant="body2">
              <Trans
                i18nKey="resetMfa.message"
                ns="users"
                values={{ fullName: user?.fullName ?? "", username: user?.username ?? "" }}
                components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <TextField
              label={t("fields.reason")}
              size="small"
              multiline
              minRows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={submitting}
              required
              fullWidth
              placeholder={t("resetMfa.reasonPlaceholder")}
            />
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting} autoFocus>
          {tc("actions.cancel")}
        </Button>
        <Button
          type="submit"
          form={formId}
          variant="contained"
          color="error"
          disabled={submitting || !trimmedReason}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("resetMfa.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
