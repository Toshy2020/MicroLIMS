import { useEffect, useId, useState, type FormEvent } from "react";
import { Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from "@mui/material";
import { toast } from "sonner";
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
      toast.success(`Company account link reset for ${user.username}.`);
      onRelinked();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not re-link the user."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="xs" fullWidth aria-describedby={messageId}>
      <DialogTitle>Re-link company account</DialogTitle>
      <DialogContent>
        <Box component="form" id={formId} onSubmit={submit} noValidate>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography id={messageId} variant="body2">
              Re-linking <strong>{user?.fullName} (@{user?.username})</strong> removes the current link to the company account.
              The user must be linked again before signing in with the company login.
            </Typography>
            <TextField
              label="Reason" size="small" multiline minRows={2} value={reason} required fullWidth
              onChange={(e) => setReason(e.target.value)} disabled={submitting}
              placeholder="Why the link is being reset"
            />
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting} autoFocus>Cancel</Button>
        <Button
          type="submit" form={formId} variant="contained" disabled={submitting || !trimmed}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          Re-link
        </Button>
      </DialogActions>
    </Dialog>
  );
}
