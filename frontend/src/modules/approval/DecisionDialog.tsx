import { useEffect, useState } from "react";
import { Stack, Button, TextField, Typography, Divider, Box } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ApprovalService, ApprovalDecision } from "./services/ApprovalService";
import { SignaturesService, SignatureTrailEntry } from "../../services/signaturesService";

const DECISION_LABELS: Record<ApprovalDecision, string> = {
  Approve: "I am approving this result.",
  Reject: "I am rejecting this result.",
  RetestRetainedSample: "I am ordering a retest of the retained sample.",
  NewSampleRequest: "I am requesting a new sample.",
  Investigation: "I am ordering an investigation.",
  OOSInvestigation: "I am ordering an OOS investigation."
};

export function DecisionDialog({ open, testOrderId, onClose }: { open: boolean; testOrderId: number | null; onClose: () => void }) {
  const [comment, setComment] = useState("");
  const [pendingDecision, setPendingDecision] = useState<ApprovalDecision | null>(null);
  const [trail, setTrail] = useState<SignatureTrailEntry[]>([]);

  useEffect(() => {
    if (open && testOrderId) {
      SignaturesService.getTrail("TestOrder", testOrderId).then(setTrail).catch(() => setTrail([]));
    }
  }, [open, testOrderId]);

  const confirm = async (password: string) => {
    if (!testOrderId || !pendingDecision) return;
    await ApprovalService.decide(testOrderId, pendingDecision, comment, password);
    setComment("");
    setPendingDecision(null);
    onClose();
  };

  return (
    <>
      <FloatingDialog open={open && !pendingDecision} title="Decision" onClose={onClose}>
        <Stack spacing={2}>
          <TextField label="Comment / Justification" multiline rows={2} value={comment} onChange={(e) => setComment(e.target.value)} />
          <Stack direction="row" spacing={1} flexWrap="wrap">
            <Button variant="contained" color="success" onClick={() => setPendingDecision("Approve")}>Approve</Button>
            <Button variant="outlined" color="error" onClick={() => setPendingDecision("Reject")}>Reject</Button>
            <Button variant="outlined" onClick={() => setPendingDecision("RetestRetainedSample")}>Retest Retained Sample</Button>
            <Button variant="outlined" onClick={() => setPendingDecision("NewSampleRequest")}>New Sample Request</Button>
            <Button variant="outlined" color="warning" onClick={() => setPendingDecision("Investigation")}>Investigation</Button>
            <Button variant="outlined" color="warning" onClick={() => setPendingDecision("OOSInvestigation")}>OOS Investigation</Button>
          </Stack>

          {trail.length > 0 && (
            <Box>
              <Divider sx={{ my: 1 }} />
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>Signature Trail</Typography>
              <Stack spacing={0.75}>
                {trail.map((s, i) => (
                  <Box key={i}>
                    <Typography sx={{ fontSize: 13 }}>
                      <strong>{s.printedName}</strong> ({s.role}) - {s.meaning} - {new Date(s.signedAt).toLocaleString()}
                    </Typography>
                    {s.comment && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>"{s.comment}"</Typography>}
                  </Box>
                ))}
              </Stack>
            </Box>
          )}
        </Stack>
      </FloatingDialog>

      {pendingDecision && (
        <SignatureDialog
          open
          meaningStatement={DECISION_LABELS[pendingDecision]}
          onCancel={() => setPendingDecision(null)}
          onConfirm={confirm}
        />
      )}
    </>
  );
}
