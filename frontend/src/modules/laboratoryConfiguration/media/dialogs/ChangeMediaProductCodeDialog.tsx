import { useState, useEffect } from "react";
import { Button, TextField, Stack } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { masterDataOptions, MediaProductOption } from "../../../../services/masterDataOptions";

interface ChangeMediaProductCodeDialogProps {
  open: boolean;
  product: MediaProductOption | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ChangeMediaProductCodeDialog({ open, product, onClose, onSuccess }: ChangeMediaProductCodeDialogProps) {
  const [newCode, setNewCode] = useState("");
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setNewCode("");
      setReason("");
      setSignatureOpen(false);
    }
  }, [open, product]);

  if (!product) return null;

  const yy = String(new Date().getFullYear()).slice(-2);
  const trimmedNewCode = newCode.trim();
  const trimmedReason = reason.trim();

  const meaningStatement = `I am changing the code of media product "${product.name}" from ${product.code} to ${trimmedNewCode}. Lots prepared from now on will be numbered ${trimmedNewCode}/01/${yy} onwards. Reason: ${trimmedReason}`;

  const handleConfirmSignature = async (password: string) => {
    await masterDataOptions.changeMediaProductCode(
      product.id,
      trimmedNewCode,
      trimmedReason,
      password
    );
    setSignatureOpen(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`Change Code: ${product.name}`}
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
          <TextField
            label="Current Code"
            value={product.code}
            slotProps={{ input: { readOnly: true } }}
            size="small"
          />
          <TextField
            label="New Code"
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value)}
            placeholder="e.g. TSA2"
            helperText="2-10 letters, digits, dot or hyphen"
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
            placeholder="Enter the justification for changing this product code..."
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
