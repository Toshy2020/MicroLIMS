import { useState, useEffect } from "react";
import { Button, TextField, Stack } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { ReferenceOrganismService, ReferenceOrganism, REFERENCE_ORGANISM_CODE_PREFIX } from "./services/ReferenceOrganismService";

interface ChangeReferenceOrganismCodeDialogProps {
  open: boolean;
  strain: ReferenceOrganism | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ChangeReferenceOrganismCodeDialog({ open, strain, onClose, onSuccess }: ChangeReferenceOrganismCodeDialogProps) {
  const [newCode, setNewCode] = useState("");
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setNewCode("");
      setReason("");
      setSignatureOpen(false);
    }
  }, [open, strain]);

  if (!strain) return null;

  const trimmedNewCode = newCode.trim().toUpperCase();
  const trimmedReason = reason.trim();

  const meaningStatement = `I am changing the code of "${strain.name}" from ${strain.code} to ${trimmedNewCode}. Stock lots and cryovials already received keep the code they were received with. Reason: ${trimmedReason}`;

  const handleConfirmSignature = async (password: string) => {
    await ReferenceOrganismService.changeCode(strain.id, trimmedNewCode, trimmedReason, password);
    setSignatureOpen(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`Change Code: ${strain.name}`}
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
          <TextField label="Current Code" value={strain.code} slotProps={{ input: { readOnly: true } }} size="small" />
          <TextField
            label="New Code"
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value.toUpperCase())}
            placeholder={`e.g. ${REFERENCE_ORGANISM_CODE_PREFIX}011`}
            helperText={`Must start with "${REFERENCE_ORGANISM_CODE_PREFIX}" and be unique across all labs`}
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
