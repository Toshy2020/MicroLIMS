import { useState, useEffect } from "react";
import { Button, TextField, Stack } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { MaterialMasterService, MaterialMasterEntry, MATERIAL_MASTER_CODE_PREFIX } from "./services/MaterialMasterService";

interface ChangeMaterialMasterCodeDialogProps {
  open: boolean;
  entry: MaterialMasterEntry | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ChangeMaterialMasterCodeDialog({ open, entry, onClose, onSuccess }: ChangeMaterialMasterCodeDialogProps) {
  const [newCode, setNewCode] = useState("");
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setNewCode("");
      setReason("");
      setSignatureOpen(false);
    }
  }, [open, entry]);

  if (!entry) return null;

  const prefix = MATERIAL_MASTER_CODE_PREFIX[entry.category];
  const trimmedNewCode = newCode.trim().toUpperCase();
  const trimmedReason = reason.trim();

  const meaningStatement = `I am changing the code of "${entry.name}" from ${entry.code} to ${trimmedNewCode}. Stock lots already received keep the code they were received with. Reason: ${trimmedReason}`;

  const handleConfirmSignature = async (password: string) => {
    await MaterialMasterService.changeCode(entry.id, trimmedNewCode, trimmedReason, password);
    setSignatureOpen(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`Change Code: ${entry.name}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button
              variant="contained"
              disabled={!trimmedNewCode || !trimmedReason}
              onClick={() => setSignatureOpen(true)}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Current Code" value={entry.code} slotProps={{ input: { readOnly: true } }} size="small" />
          <TextField
            label="New Code"
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value.toUpperCase())}
            placeholder={`e.g. ${prefix}011`}
            helperText={`Must start with "${prefix}" and be unique across all labs`}
            size="small"
            autoFocus
          />
          <TextField
            label="Reason"
            required
            multiline
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Enter the justification for changing this code..."
            size="small"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
