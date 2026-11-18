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
import { i18n, useTranslation } from "../../../../i18n";
import {
  NotApplicableTestService,
  NotApplicableTestDto,
  NotApplicableStatus,
} from "../services/NotApplicableTestService";
import { useHasPermission } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";

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
  const { t } = useTranslation("labConfig");
  const enT = i18n.getFixedT("en", "labConfig");
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
      setError(t("items.notApplicable.loadError"));
    } finally {
      setLoading(false);
    }
  }, [itemId, t]);

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
      setFormError(t("items.notApplicable.errors.testRequired"));
      return;
    }
    if (!justification.trim()) {
      setFormError(t("items.notApplicable.errors.justificationRequired"));
      return;
    }
    if (!criticality.trim()) {
      setFormError(t("items.notApplicable.errors.criticalityRequired"));
      return;
    }
    if (!immediateAction.trim()) {
      setFormError(t("items.notApplicable.errors.immediateActionRequired"));
      return;
    }
    if (!longTermAction.trim()) {
      setFormError(t("items.notApplicable.errors.longTermActionRequired"));
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
      setFormError(err?.response?.data?.message ?? t("items.notApplicable.errors.createFailed"));
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
        const msg = t("items.notApplicable.signature.reasonRequiredError");
        const customErr: any = new Error(msg);
        customErr.response = { data: { message: msg } };
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
    return formatDateTimeWithTimeZone(dateStr);
  };

  const getStatusChip = (status: NotApplicableStatus) => {
    switch (status) {
      case "Approved":
        return <Chip label={t("items.notApplicable.statuses.Approved")} size="small" color="success" sx={{ fontWeight: 600 }} />;
      case "HeadApproved":
        return <Chip label={t("items.notApplicable.statuses.HeadApproved")} size="small" color="warning" sx={{ fontWeight: 600 }} />;
      case "Withdrawn":
        return <Chip label={t("items.notApplicable.statuses.Withdrawn")} size="small" color="default" sx={{ fontWeight: 600 }} />;
      case "Draft":
      default:
        return <Chip label={t("items.notApplicable.statuses.Draft")} size="small" color="info" sx={{ fontWeight: 600 }} />;
    }
  };

  const getMeaningStatement = (translate: typeof t = enT) => {
    if (!signingRow) return "";
    const test = signingRow.testDisplayName || signingRow.testCode;
    switch (signingAction) {
      case "headApprove":
        return translate("items.notApplicable.signature.headApproveMeaning", { test });
      case "finalApprove":
        return translate("items.notApplicable.signature.finalApproveMeaning", { test });
      case "withdraw":
        return translate("items.notApplicable.signature.withdrawMeaning", { test });
      default:
        return "";
    }
  };

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            {t("items.notApplicable.title")}
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
            {t("items.notApplicable.subtitle")}
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
            {t("items.notApplicable.declareButton")}
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
          <Typography variant="body2">{t("items.notApplicable.empty")}</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: "action.hover" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.test")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.grade")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.criticality")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.status")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.headSigner")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.qcSigner")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("items.notApplicable.columns.reference")}</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t("items.notApplicable.columns.actions")}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {records.map((r) => (
                <TableRow key={r.id}>
                  <TableCell sx={{ fontWeight: 600 }}>{r.testDisplayName || r.testCode}</TableCell>
                  <TableCell>{r.grade || t("items.notApplicable.allGrades")}</TableCell>
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
                          {t("items.notApplicable.actions.headApprove")}
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
                          {t("items.notApplicable.actions.finalApprove")}
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
                          {t("items.notApplicable.actions.withdraw")}
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
          <DialogTitle sx={{ fontWeight: 700 }}>{t("items.notApplicable.dialog.title")}</DialogTitle>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ mt: 0.5 }}>
              {formError && <Alert severity="error">{formError}</Alert>}

              <TextField
                select
                slotProps={{ select: { native: true } }}
                label={t("items.notApplicable.dialog.testLabel")}
                value={testCode}
                onChange={(e) => setTestCode(e.target.value)}
                required
                fullWidth
                size="small"
              >
                <option value="" disabled>
                  {t("items.notApplicable.dialog.selectTest")}
                </option>
                {assignedTests.map((tOpt) => (
                  <option key={tOpt.testCode} value={tOpt.testCode}>
                    {tOpt.displayName ? `${tOpt.displayName} (${tOpt.testCode})` : tOpt.testCode}
                  </option>
                ))}
              </TextField>

              <TextField
                select
                slotProps={{ select: { native: true } }}
                label={t("items.notApplicable.dialog.gradeLabel")}
                value={grade}
                onChange={(e) => setGrade(e.target.value)}
                fullWidth
                size="small"
              >
                <option value="">{t("items.notApplicable.allGrades")}</option>
                {grades.map((g) => (
                  <option key={g} value={g}>
                    {g}
                  </option>
                ))}
              </TextField>

              <TextField
                label={t("items.notApplicable.dialog.justificationLabel")}
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label={t("items.notApplicable.dialog.criticalityLabel")}
                value={criticality}
                onChange={(e) => setCriticality(e.target.value)}
                required
                fullWidth
                size="small"
                placeholder={t("items.notApplicable.dialog.criticalityPlaceholder")}
              />

              <TextField
                label={t("items.notApplicable.dialog.immediateActionLabel")}
                value={immediateAction}
                onChange={(e) => setImmediateAction(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label={t("items.notApplicable.dialog.longTermActionLabel")}
                value={longTermAction}
                onChange={(e) => setLongTermAction(e.target.value)}
                required
                multiline
                rows={2}
                fullWidth
                size="small"
              />

              <TextField
                label={t("items.notApplicable.dialog.referenceLabel")}
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                fullWidth
                size="small"
                placeholder={t("items.notApplicable.dialog.referencePlaceholder")}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseDeclare} disabled={submitting}>
              {t("items.notApplicable.dialog.cancel")}
            </Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              {submitting ? t("items.notApplicable.dialog.submitting") : t("items.notApplicable.dialog.submit")}
            </Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Signature Dialog */}
      <SignatureDialog
        open={signingOpen}
        meaningStatement={getMeaningStatement(enT)}
        meaningStatementTranslation={getMeaningStatement(t)}
        showComment={true}
        commentLabel={signingAction === "withdraw" ? t("items.notApplicable.signature.reasonRequiredLabel") : undefined}
        commentRequired={signingAction === "withdraw"}
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
