import { useEffect, useState } from "react";
import { Button, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { SolutionPreparationService } from "../services/SolutionPreparationService";

interface Props {
  open: boolean;
  preparationId: number;
  code: string | null;
  solutionName: string;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// Release gate for a prepared solution whose recipe needs release before use. The comment
// is asked for when rejecting, but the server decides: its message (comment missing, same
// person who prepared it, wrong password) is shown in the signature dialog.
export function ReleasePreparationDialog({ open, preparationId, code, solutionName, onClose, onSuccess }: Props) {
  const [decision, setDecision] = useState<"approve" | "reject">("approve");
  const [comment, setComment] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setDecision("approve");
      setComment("");
      setSignatureOpen(false);
    }
  }, [open, preparationId]);

  const approved = decision === "approve";
  const trimmedComment = comment.trim();
  const label = `${solutionName}${code ? ` (${code})` : ""}`;
  const meaningStatement = approved
    ? `I am releasing ${label} for use.${trimmedComment ? ` Comment: ${trimmedComment}` : ""}`
    : `I am rejecting ${label}; it can't be used.${trimmedComment ? ` Comment: ${trimmedComment}` : ""}`;

  const handleConfirmSignature = async (password: string) => {
    await SolutionPreparationService.release(preparationId, {
      approved,
      comment: trimmedComment || null,
      password
    });
    setSignatureOpen(false);
    onSuccess(approved ? `${label} released for use.` : `${label} rejected.`);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`Release: ${solutionName}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" onClick={() => setSignatureOpen(true)}>
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            This solution can't be used until someone other than the preparer releases it
            {code ? ` (${code})` : ""}.
          </Typography>
          <FormControl>
            <FormLabel id="solution-release-decision-label">Decision</FormLabel>
            <RadioGroup
              row
              aria-labelledby="solution-release-decision-label"
              value={decision}
              onChange={(e) => setDecision(e.target.value as "approve" | "reject")}
            >
              <FormControlLabel value="approve" control={<Radio />} label="Approve" />
              <FormControlLabel value="reject" control={<Radio />} label="Reject" />
            </RadioGroup>
          </FormControl>
          <TextField
            label="Comment"
            multiline
            rows={3}
            size="small"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={approved ? "Optional" : "Say why this solution is rejected..."}
            helperText={approved ? undefined : "Needed to reject."}
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
