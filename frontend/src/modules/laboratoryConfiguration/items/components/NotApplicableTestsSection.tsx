import { useState, useEffect, useCallback } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  CircularProgress,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import {
  NotApplicableTestService,
  NotApplicableTestDto,
  NotApplicableStatus,
} from "../services/NotApplicableTestService";
import { useHasPermission } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { SignatureDialog } from "../../../../components/SignatureDialog";

export interface NotApplicableTestsSectionProps {
  itemId: number;
  assignedTests: { testCode: string; displayName: string }[];
  grades: string[];
}

type ActionType = "headApprove" | "finalApprove" | "withdraw";

export function NotApplicableTestsSection({
  itemId,
  assignedTests,
  grades,
}: NotApplicableTestsSectionProps) {
  const [records, setRecords] = useState<NotApplicableTestDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canManage = useHasPermission(PERMISSIONS.NOT_APPLICABLE_TESTS_MANAGE);
  const canFinalApprove = useHasPermission(PERMISSIONS.NOT_APPLICABLE_TESTS_FINAL_APPROVE);

  // Declare dialog state
  const [declareOpen, setDeclareOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [testCode, setTestCode] = useState("");
  const [grade, setGrade] = useState("");
  const [justification, setJustification] = useState("");
  const [criticality, setCriticality] = useState("");
  const [immediateAction, setImmediateAction] = useState("");
  const [longTermAction, setLongTermAction] = useState("");
  const [reference, setReference] = useState("");

  // Signature dialog state
  const [signingOpen, setSigningOpen] = useState(false);
  const [signingAction, setSigningAction] = useState<ActionType | null>(null);
  const [signingRow, setSigningRow] = useState<NotApplicableTestDto | null>(null);
  const [signingComment, setSigningComment] = useState("");

  const loadRecords = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await NotApplicableTestService.listForItem(itemId);
      setRecords(data);
    } catch {
      setError("Failed to load not-applicable test declarations.");
    } finally {
      setLoading(false);
    }
  }, [itemId]);

  useEffect(() => {
    loadRecords();
  }, [loadRecords]);

  const handleOpenDeclare = () => {
    setTestCode(assignedTests[0]?.testCode ?? "");
    setGrade("");
    setJustification("");
    setCriticality("");
    setImmediateAction("");
    setLongTermAction("");
    setReference("");
    setFormError(null);
    setDeclareOpen(true);
  };

  const handleCloseDeclare = () => {
    if (submitting) return;
    setDeclareOpen(false);
  };

  const handleSubmitDeclare = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!testCode) {
      setFormError("Test is required.");
      return;
    }
    if (!justification.trim()) {
      setFormError("Justification is required.");
      return;
    }
    if (!criticality.trim()) {
      setFormError("Criticality is required.");
      return;
    }
    if (!immediateAction.trim()) {
      setFormError("Immediate action is required.");
      return;
    }
    if (!longTermAction.trim()) {
      setFormError("Long-term action is required.");
      return;
    }

    setSubmitting(true);
    setFormError(null);
    try {
      await NotApplicableTestService.create(itemId, {
        testCode,
        grade: grade.trim() ? grade.trim() : null,
        justification: justification.trim(),
        criticality: criticality.trim(),
        immediateAction: immediateAction.trim(),
        longTermAction: longTermAction.trim(),
        reference: reference.trim() ? reference.trim() : null,
      });
      setDeclareOpen(false);
      await loadRecords();
    } catch (err: any) {
      setFormError(err?.response?.data?.message ?? "Failed to declare test as not applicable.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleOpenSignature = (row: NotApplicableTestDto, action: ActionType) => {
    setSigningRow(row);
    setSigningAction(action);
    setSigningComment("");
    setSigningOpen(true);
  };

  const handleSignatureConfirm = async (password: string) => {
    if (!signingRow || !signingAction) return;

    if (signingAction === "headApprove") {
      await NotApplicableTestService.headApprove(signingRow.id, password, signingComment);
    } else if (signingAction === "finalApprove") {
      await NotApplicableTestService.finalApprove(signingRow.id, password, signingComment);
    } else if (signingAction === "withdraw") {
      if (!signingComment.trim()) {
        const customErr: any = new Error("Reason is required.");
        customErr.response = { data: { message: "Reason is required." } };
        throw customErr;
      }
      await NotApplicableTestService.withdraw(signingRow.id, password, signingComment.trim());
    }

    setSigningOpen(false);
    setSigningRow(null);
    setSigningAction(null);
    setSigningComment("");
    await loadRecords();
  };

  const formatDateTime = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const getStatusChip = (status: NotApplicableStatus) => {
    switch (status) {
      case "Approved":
        return <Chip label="Approved" size="small" color="success" sx={{ fontWeight: 600 }} />;
      case "HeadApproved":
        return <Chip label="Head Approved" size="small" color="warning" sx={{ fontWeight: 600 }} />;
      case "Withdrawn":
        return <Chip label="Withdrawn" size="small" color="default" sx={{ fontWeight: 600 }} />;
      case "Draft":
      default:
        return <Chip label="Draft" size="small" color="info" sx={{ fontWeight: 600 }} />;
    }
  };

  const getMeaningStatement = () => {
    if (!signingRow) return "";
    switch (signingAction) {
      case "headApprove":
        return `I approve the test '${signingRow.testDisplayName || signingRow.testCode}' as not applicable (Section Head).`;
      case "finalApprove":
        return `I grant final approval for the test '${signingRow.testDisplayName || signingRow.testCode}' as not applicable (QC Manager).`;
      case "withdraw":
        return `I am withdrawing the not-applicable test declaration for '${signingRow.testDisplayName || signingRow.testCode}'.`;
      default:
        return "";
    }
  };

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Not-Applicable Tests
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
            Exempt assigned tests for specific grades or all grades with Section Head and QC Manager approval.
          </Typography>
        </Box>
        {canManage && (
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={handleOpenDeclare}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Declare not applicable
          </Button>
        )}
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={24} />
        </Box>
      ) : records.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{ p: 3, textAlign: "center", color: "text.secondary", bgcolor: "action.hover" }}
        >
          <Typography variant="body2">No not-applicable test declarations on file.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: "action.hover" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Test</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Grade</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Criticality</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Head Signer &amp; Time</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>QC Manager Signer &amp; Time</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Reference</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  Actions
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {records.map((r) => (
                <TableRow key={r.id}>
                  <TableCell sx={{ fontWeight: 600 }}>{r.testDisplayName || r.testCode}</TableCell>
                  <TableCell>{r.grade || "All grades"}</TableCell>
                  <TableCell>{r.criticality}</TableCell>
                  <TableCell>{getStatusChip(r.status)}</TableCell>
                  <TableCell>
                    {r.headSignedBy ? `${r.headSignedBy} (${formatDateTime(r.headSignedAt)})` : "—"}
                  </TableCell>
                  <TableCell>
                    {r.finalSignedBy ? `${r.finalSignedBy} (${formatDateTime(r.finalSignedAt)})` : "—"}
                  </TableCell>
                  <TableCell>{r.reference || "—"}</TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                      {r.status === "Draft" && canManage && (
                        <Button
                          size="small"
                          variant="outlined"
                          color="primary"
                          onClick={() => handleOpenSignature(r, "headApprove")}
                          sx={{ textTransform: "none", fontSize: 12, py: 0.2 }}
                        >
                          Approve (Section Head)
                        </Button>
                      )}
                      {r.status === "HeadApproved" && canFinalApprove && (
                        <Button
                          size="small"
                          variant="contained"
                          color="primary"
                          onClick={() => handleOpenSignature(r, "finalApprove")}
                          sx={{ textTransform: "none", fontSize: 12, py: 0.2 }}
                        >
                          Approve (QC Manager)
                        </Button>
                      )}
                      {r.status !== "Withdrawn" && canManage && (
                        <Button
                          size="small"
                          variant="outlined"
                          color="error"
                          onClick={() => handleOpenSignature(r, "withdraw")}
                          sx={{ textTransform: "none", fontSize: 12, py: 0.2 }}
                        >
                          Withdraw
                        </Button>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Declare Dialog */}
      <Dialog open={declareOpen} onClose={handleCloseDeclare} maxWidth="sm" fullWidth>
        <form onSubmit={handleSubmitDeclare}>
          <DialogTitle sx={{ fontWeight: 700 }}>Declare not applicable</DialogTitle>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ mt: 0.5 }}>
              {formError && <Alert severity="error">{formError}</Alert>}

              <TextField
                select
                slotProps={{ select: { native: true } }}
                label="Test"
                value={testCode}
                onChange={(e) => setTestCode(e.target.value)}
                required
                fullWidth
                size="small"
              >
                <option value="" disabled>
                  Select a test
                </option>
                {assignedTests.map((t) => (
                  <option key={t.testCode} value={t.testCode}>
                    {t.displayName ? `${t.displayName} (${t.testCode})` : t.testCode}
                  </option>
                ))}
              </TextField>

              <TextField
                select
                slotProps={{ select: { native: true } }}
                label="Grade"
                value={grade}
                onChange={(e) => setGrade(e.target.value)}
                fullWidth
                size="small"
              >
                <option value="">All grades</option>
                {grades.map((g) => (
                  <option key={g} value={g}>
                    {g}
                  </option>
                ))}
              </TextField>

              <TextField
                label="Justification"
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label="Criticality"
                value={criticality}
                onChange={(e) => setCriticality(e.target.value)}
                required
                fullWidth
                size="small"
                placeholder="e.g. Non-critical or Critical"
              />

              <TextField
                label="Immediate Action"
                value={immediateAction}
                onChange={(e) => setImmediateAction(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label="Long-term Action"
                value={longTermAction}
                onChange={(e) => setLongTermAction(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label="Reference"
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                fullWidth
                size="small"
                placeholder="e.g. Monograph or SOP reference"
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseDeclare} disabled={submitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              {submitting ? "Submitting..." : "Submit"}
            </Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Signature Dialog */}
      <SignatureDialog
        open={signingOpen}
        meaningStatement={getMeaningStatement()}
        showComment={true}
        comment={signingComment}
        onCommentChange={setSigningComment}
        onCancel={() => {
          setSigningOpen(false);
          setSigningRow(null);
          setSigningAction(null);
          setSigningComment("");
        }}
        onConfirm={handleSignatureConfirm}
      />
    </Box>
  );
}
