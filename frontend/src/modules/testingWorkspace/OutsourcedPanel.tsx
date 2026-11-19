import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle,
  Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { Trans } from "react-i18next";
import { SignatureDialog } from "../../components/SignatureDialog";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { VerdictBanner } from "../../components/lab";
import type { Verdict } from "../../components/lab";
import { tableHeadSx } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ReceiveService } from "../receiving/services/ReceiveService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { ExternalLabSubtitle, externalReportId } from "./TestResultCards";
import type { AnalysisDetail } from "./types/sampleSummaryTypes";
import type { ExternalReportDocument, RecordOutsourcedParameterRequest } from "./types/testWorkflowTypes";

const errorText = (err: unknown, fallback: string) =>
  (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

// The report opens in a new tab; the blob is freed after a minute.
async function viewReport(documentId: number) {
  const url = URL.createObjectURL(await ReceiveService.getSampleDocumentContent(documentId));
  window.open(url, "_blank", "noopener,noreferrer");
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

// "Outsource test": external lab and reason, then the signature. Shown only to holders of
// TestsOutsource while the order is still open; the server re-checks everything.
export function OutsourceTestAction({ testOrderId, status, onOutsourced }: {
  testOrderId: number;
  status?: string | null;
  onOutsourced: (externalLab: string) => void;
}) {
  const { t } = useTranslation("testingWorkspace");
  const { permissions } = useAuth();
  const [open, setOpen] = useState(false);
  const [signing, setSigning] = useState(false);
  const [lab, setLab] = useState("");
  const [reason, setReason] = useState("");

  if (!permissions.includes(PERMISSIONS.TESTS_OUTSOURCE) || (status !== "Pending" && status !== "InProgress")) return null;

  const confirm = async (password: string) => {
    // a refusal propagates to the signature dialog, which shows the server's message
    await TestWorkflowService.outsourceTest(testOrderId, { externalLab: lab.trim(), reason: reason.trim(), password });
    setSigning(false);
    setOpen(false);
    onOutsourced(lab.trim());
  };

  return (
    <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}>
      <Button size="small" variant="outlined" onClick={() => setOpen(true)} sx={{ textTransform: "none" }}>
        {t("outsourced.outsourceTestBtn")}
      </Button>
      <Dialog open={open && !signing} onClose={() => setOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("outsourced.dialogTitle")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Alert severity="info">
              {t("outsourced.outsourceAlert")}
            </Alert>
            <TextField label={t("outsourced.externalLabLabel")} size="small" value={lab} onChange={(e) => setLab(e.target.value)} fullWidth />
            <TextField label={t("outsourced.outsourceReasonLabel")} size="small" value={reason} onChange={(e) => setReason(e.target.value)} multiline minRows={2} fullWidth />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>{t("outsourced.cancel")}</Button>
          <Button variant="contained" disabled={!lab.trim() || !reason.trim()} onClick={() => setSigning(true)}>
            {t("outsourced.continueToSignature")}
          </Button>
        </DialogActions>
      </Dialog>
      <SignatureDialog
        open={signing}
        meaningStatement={`I am sending this test to ${lab.trim()} and take responsibility for this decision.`}
        meaningStatementTranslation={t("outsourced.outsourceSignatureMeaning", { lab: lab.trim() })}
        onCancel={() => setSigning(false)}
        onConfirm={confirm}
      />
    </Box>
  );
}

interface RowState { value: string; conforms: boolean | null; observation: string }
const emptyRow: RowState = { value: "", conforms: null, observation: "" };
const isQualitative = (s: SpecificationDto) => s.limitType === "Qualitative" || s.limitType === "PresenceAbsence";

const rowComplete = (s: SpecificationDto, r: RowState) =>
  isQualitative(s)
    ? r.conforms !== null && (r.conforms || r.observation.trim() !== "")
    : r.value.trim() !== "" && Number.isFinite(Number(r.value));

interface Props {
  testOrderId: number;
  displayName: string;
  testCode: string;
  outsourcedToLab: string;
  sampleId?: number | null;
  // The order's status: once it is past Pending/InProgress the result is already recorded.
  status?: string | null;
  onRecorded?: () => Promise<void> | void;
}

// Result entry for a test an outside laboratory ran: upload its report, copy the reported values
// per specification, sign. Conformity is the server's call and is shown after signing.
export function OutsourcedPanel({ testOrderId, displayName, testCode, outsourcedToLab, sampleId, status, onRecorded }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const recorded = !!status && status !== "Pending" && status !== "InProgress";
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [rows, setRows] = useState<Record<number, RowState>>({});
  const [report, setReport] = useState<ExternalReportDocument | null>(null);
  const [uploading, setUploading] = useState(false);
  const [reportNumber, setReportNumber] = useState("");
  const [reportDate, setReportDate] = useState("");
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string | null } | null>(null);

  useEffect(() => {
    if (recorded) return;
    let active = true;
    SpecificationService.getForTestOrder(testOrderId)
      .then((all) => {
        if (!active) return;
        const mine = (all ?? []).filter((s) => s.testCode === testCode).sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
        setSpecs(mine);
        if (mine.length === 0) setError(t("outsourced.noSpecsConfigured", { testCode }));
      })
      .catch((e) => active && setError(errorText(e, t("outsourced.loadSpecsFailed"))))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [testOrderId, testCode, recorded, t]);

  // Recorded already: show what the server holds instead of a form.
  const [analysis, setAnalysis] = useState<AnalysisDetail | null>(null);
  useEffect(() => {
    if (!recorded || sampleId == null) return;
    let active = true;
    SampleSummaryService.getSummary(sampleId)
      .then((sum) => active && setAnalysis(sum.testOrders?.find((t) => t.testOrderId === testOrderId)?.analysis ?? null))
      .catch(() => undefined); // the pointer text below still stands
    return () => { active = false; };
  }, [recorded, sampleId, testOrderId]);

  const rowOf = (id: number) => rows[id] ?? emptyRow;
  const setRow = (id: number, patch: Partial<RowState>) => setRows((r) => ({ ...r, [id]: { ...(r[id] ?? emptyRow), ...patch } }));

  const upload = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      setReport(await TestWorkflowService.uploadExternalReport(testOrderId, file));
    } catch (e) {
      setError(errorText(e, t("outsourced.uploadFailed")));
    } finally {
      setUploading(false);
    }
  };

  const canSign = !!report && reportNumber.trim() !== "" && reportDate !== "" && specs.length > 0 &&
    specs.every((s) => rowComplete(s, rowOf(s.id!)));

  // A refusal propagates to the signature dialog, which stays open and shows the server's message.
  const submit = async (password: string) => {
    setError(null);
    const parameters = specs.map((s): RecordOutsourcedParameterRequest => {
      const r = rowOf(s.id!);
      return isQualitative(s)
        ? { specificationId: s.id!, conforms: r.conforms, observation: r.observation.trim() || null }
        : { specificationId: s.id!, value: Number(r.value) };
    });
    const res = await TestWorkflowService.recordOutsourcedResult(testOrderId, {
      reportedAt: new Date(`${reportDate}T00:00:00`).toISOString(),
      externalReportNumber: reportNumber.trim(),
      reportDocumentId: report!.id,
      parameters,
      password,
      comment: comment.trim() || null
    });
    setSigning(false);
    setOutcome({ outcomeSummary: res?.outcomeSummary, status: res?.status });
    await onRecorded?.();
  };

  if (recorded && !outcome) {
    return (
      <Stack spacing={2}>
        <Alert severity="info">
          <Trans
            i18nKey="outsourced.recordedInfo"
            ns="testingWorkspace"
            values={{ lab: outsourcedToLab, displayName }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
        {analysis && (
          <Box>
            <ExternalLabSubtitle lab={outsourcedToLab} reportId={externalReportId(analysis)} />
            {analysis.parameterResults.map((r) => (
              <Typography key={r.id} variant="body2">{r.parameterName}: {r.reportedDisplay} ({r.comparisonStatus})</Typography>
            ))}
          </Box>
        )}
      </Stack>
    );
  }

  if (outcome) {
    const status = outcome.status ?? "";
    const verdict: Verdict = status.includes("OutOfSpecification") ? "Fail" : status.includes("WithinLimits") ? "Pass" : "Pending";
    return (
      <VerdictBanner verdict={verdict} detail={`${displayName}: ${outcome.outcomeSummary ?? t("outsourced.resultRecordedFallback")}${status ? ` (${status})` : ""}`} />
    );
  }

  if (loading) return <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={32} /></Box>;

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        <Trans
          i18nKey="outsourced.uploadPrompt"
          ns="testingWorkspace"
          values={{ lab: outsourcedToLab }}
          components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
        />
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" } }}>
        <Button component="label" variant="outlined" disabled={uploading} sx={{ textTransform: "none" }}>
          {report ? t("outsourced.replaceReport") : t("outsourced.uploadReport")}
          <input
            hidden
            type="file"
            accept="application/pdf,image/*"
            aria-label={t("outsourced.reportFileAria")}
            onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = ""; }}
          />
        </Button>
        {report && (
          <Chip
            label={report.originalFileName}
            onClick={() => void viewReport(report.id).catch(() => setError(t("outsourced.openReportFailed")))}
          />
        )}
      </Stack>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
        <TextField
          label={t("outsourced.externalReportNumber")}
          size="small"
          value={reportNumber}
          onChange={(e) => setReportNumber(e.target.value)}
          slotProps={{ htmlInput: { dir: "ltr" } }}
        />
        <TextField
          label={t("outsourced.reportDate")} size="small" type="date" value={reportDate}
          onChange={(e) => setReportDate(e.target.value)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { dir: "ltr" } }}
        />
      </Stack>

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={tableHeadSx}>{t("outsourced.parameterCol")}</TableCell>
              <TableCell sx={tableHeadSx}>{t("outsourced.specificationCol")}</TableCell>
              <TableCell sx={tableHeadSx}>{t("outsourced.reportedExternalCol")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {specs.map((s) => {
              const r = rowOf(s.id!);
              const name = s.parameterName || s.testCode;
              return (
                <TableRow key={s.id}>
                  <TableCell>{name}</TableCell>
                  <TableCell><Ltr>{s.specLimit || s.expectedResultText || "—"}{s.unit ? ` ${s.unit}` : ""}</Ltr></TableCell>
                  <TableCell>
                    {isQualitative(s) ? (
                      <Stack direction="row" spacing={1} sx={{ alignItems: "flex-start" }}>
                        <Button size="small" variant={r.conforms === true ? "contained" : "outlined"} onClick={() => setRow(s.id!, { conforms: true })}>
                          {t("outsourced.conforms")}
                        </Button>
                        <Button size="small" color="error" variant={r.conforms === false ? "contained" : "outlined"} onClick={() => setRow(s.id!, { conforms: false })}>
                          {t("outsourced.doesNotConform")}
                        </Button>
                        <TextField
                          label={t("outsourced.observationLabel")} size="small" value={r.observation}
                          onChange={(e) => setRow(s.id!, { observation: e.target.value })}
                          helperText={r.conforms === false ? t("outsourced.requiredWhenNotConforming") : " "}
                        />
                      </Stack>
                    ) : (
                      <TextField
                        size="small" type="number" value={r.value}
                        onChange={(e) => setRow(s.id!, { value: e.target.value })}
                        slotProps={{ htmlInput: { "aria-label": t("outsourced.valueForAria", { name }), step: "any", dir: "ltr" } }}
                      />
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <TextField label={t("outsourced.commentOptional")} size="small" value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} fullWidth />
      <Box sx={{ display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 2 }}>
        {!canSign && <Typography variant="caption" color="text.secondary">{t("outsourced.uploadAndFillHint")}</Typography>}
        <Button variant="contained" disabled={!canSign} onClick={() => setSigning(true)} sx={{ fontWeight: 700, textTransform: "none" }}>
          {t("outsourced.signAndRecord")}
        </Button>
      </Box>

      <SignatureDialog
        open={signing}
        meaningStatement="I transcribed these results from the external laboratory's report and am recording this test result."
        meaningStatementTranslation={t("outsourced.recordSignatureMeaning")}
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Stack>
  );
}
