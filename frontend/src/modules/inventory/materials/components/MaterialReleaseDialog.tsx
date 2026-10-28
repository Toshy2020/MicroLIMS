import { useEffect, useState } from "react";
import { Button, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";

interface MaterialReleaseDialogProps {
  open: boolean;
  material: MaterialItem | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// Release gate for a lot in a category that needs approval before use. The server refuses the
// person who received the lot and shows its message in the signature dialog.
export function MaterialReleaseDialog({ open, material, onClose, onSuccess }: MaterialReleaseDialogProps) {
  const [decision, setDecision] = useState<"approve" | "reject">("approve");
  const [comment, setComment] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setDecision("approve");
      setComment("");
      setSignatureOpen(false);
    }
  }, [open, material]);

  if (!material) return null;

  const approved = decision === "approve";
  const trimmedComment = comment.trim();
  const label = `${material.materialName} (lot ${material.batchNumber})`;
  const meaningStatement = approved
    ? `I am releasing ${label} for use.${trimmedComment ? ` Comment: ${trimmedComment}` : ""}`
    : `I am rejecting ${label}; it can't be used. Comment: ${trimmedComment}`;

  const handleConfirmSignature = async (password: string) => {
    await MaterialService.release(material.id, { password, approved, comment: trimmedComment || null });
    setSignatureOpen(false);
    onSuccess(approved ? `${label} released for use.` : `${label} rejected.`);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`Release Lot: ${material.materialName}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button
              variant="contained"
              disabled={!approved && !trimmedComment}
              onClick={() => setSignatureOpen(true)}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            This lot can't be used until a second person releases it. Lot {material.batchNumber}
            {material.categoryName ? `, ${material.categoryName}` : ""}.
          </Typography>
          <FormControl>
            <FormLabel id="material-release-decision-label">Decision</FormLabel>
            <RadioGroup
              row
              aria-labelledby="material-release-decision-label"
              value={decision}
              onChange={(e) => setDecision(e.target.value as "approve" | "reject")}
            >
              <FormControlLabel value="approve" control={<Radio />} label="Approve" />
              <FormControlLabel value="reject" control={<Radio />} label="Reject" />
            </RadioGroup>
          </FormControl>
          <TextField
            label="Comment"
            required={!approved}
            multiline
            rows={3}
            size="small"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={approved ? "Optional" : "Say why this lot is rejected..."}
            helperText={approved ? undefined : "Required to reject."}
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
