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
import { toast } from "sonner";
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
      toast.success(`Second factor reset for ${user.username}.`);
      onReset();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not reset the second factor."));
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
        Reset second factor
      </DialogTitle>
      <DialogContent>
        <Box component="form" id={formId} onSubmit={handleSubmit} noValidate>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography id={messageId} variant="body2">
              Resetting the second factor for{" "}
              <strong>
                {user?.fullName} (@{user?.username})
              </strong>{" "}
              removes their authenticator app or hardware token, deletes all recovery codes, forgets all trusted
              computers, and signs out all active sessions. They will have to set up a new second factor at next
              sign-in with no fresh grace period.
            </Typography>
            <TextField
              label="Reason"
              size="small"
              multiline
              minRows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={submitting}
              required
              fullWidth
              placeholder="Why the second factor is being reset"
            />
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting} autoFocus>
          Cancel
        </Button>
        <Button
          type="submit"
          form={formId}
          variant="contained"
          color="error"
          disabled={submitting || !trimmedReason}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          Reset second factor
        </Button>
      </DialogActions>
    </Dialog>
  );
}
