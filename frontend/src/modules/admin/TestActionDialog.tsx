import { ReactNode, useState } from "react";
import { Alert, Button, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { getErrorMessage } from "../../utils/errorMessage";

interface Props {
  open: boolean;
  title: string;
  description: string;
  confirmLabel: string;
  // When set, the dialog also asks for an email address.
  addressLabel?: string;
  onClose: () => void;
  // Runs the action; the returned node (the per-recipient or per-step outcome) is shown in the dialog.
  onSubmit: (reason: string, address: string) => Promise<ReactNode>;
}

// Reason prompt shared by the test-send and probe actions. The server enforces the
// reason, the rate limit and the audit; this only collects the input and shows the outcome.
export function TestActionDialog({ open, title, description, confirmLabel, addressLabel, onClose, onSubmit }: Props) {
  const [reason, setReason] = useState("");
  const [address, setAddress] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<ReactNode>(null);

  const close = () => { setReason(""); setAddress(""); setError(null); setOutcome(null); onClose(); };

  const submit = async () => {
    setBusy(true); setError(null); setOutcome(null);
    try {
      setOutcome(await onSubmit(reason.trim(), address.trim()));
    } catch (e) {
      setError(getErrorMessage(e, "The action failed."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={close}
      maxWidth="xs"
      title={title}
      actions={
        <>
          <Button onClick={close}>Close</Button>
          <Button variant="contained" onClick={submit} disabled={busy || !reason.trim() || (addressLabel !== undefined && !address.trim())}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ pt: 1 }}>
        <Typography variant="body2">{description}</Typography>
        {addressLabel && (
          <TextField label={addressLabel} type="email" size="small" value={address} onChange={(e) => setAddress(e.target.value)} />
        )}
        <TextField
          label="Reason" size="small" required multiline minRows={2} value={reason}
          onChange={(e) => setReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
        />
        {error && <Alert severity="error">{error}</Alert>}
        {outcome}
      </Stack>
    </FloatingDialog>
  );
}
