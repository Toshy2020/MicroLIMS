import { useState } from "react";
import { Stack, Button, TextField, Alert } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { ApprovalService, ApprovalDecision } from "./services/ApprovalService";

export function DecisionDialog({ open, testOrderId, onClose }: { open: boolean; testOrderId: number | null; onClose: () => void }) {
  const [comment, setComment] = useState("");
  const [error, setError] = useState<string | null>(null);

  const decide = async (decision: ApprovalDecision) => {
    if (!testOrderId) return;
    setError(null);
    try {
      await ApprovalService.decide(testOrderId, decision, comment);
      onClose();
    } catch {
      setError("This decision requires a documented comment, or the test order isn't ready for a decision yet.");
    }
  };

  return (
    <FloatingDialog open={open} title="Decision" onClose={onClose}>
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField label="Comment / Justification" multiline rows={2} value={comment} onChange={(e) => setComment(e.target.value)} />
        <Stack direction="row" spacing={1} flexWrap="wrap">
          <Button variant="contained" color="success" onClick={() => decide("Approve")}>Approve</Button>
          <Button variant="outlined" color="error" onClick={() => decide("Reject")}>Reject</Button>
          <Button variant="outlined" onClick={() => decide("RetestRetainedSample")}>Retest Retained Sample</Button>
          <Button variant="outlined" onClick={() => decide("NewSampleRequest")}>New Sample Request</Button>
          <Button variant="outlined" color="warning" onClick={() => decide("Investigation")}>Investigation</Button>
          <Button variant="outlined" color="warning" onClick={() => decide("OOSInvestigation")}>OOS Investigation</Button>
        </Stack>
      </Stack>
    </FloatingDialog>
  );
}
