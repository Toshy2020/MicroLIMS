import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlined";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { PageHeader } from "../../components/PageHeader";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { tableHeadSx } from "../../theme";
import { useTestDefinitions } from "../../hooks/useTestDefinitions";
import { useAuth } from "../../contexts/AuthContext";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { MaterialService } from "../inventory/materials/services/MaterialService";
import { masterDataOptions, TestAnalyteDto } from "../../services/masterDataOptions";
import {
  CalibrationRunService,
  CalibrationRunView,
  CalibrationRunStatus,
  CalibrationCheckType,
  CorrelationType,
  CalibrationRunPreviewResult,
  CreateCalibrationRunRequest,
  CreateCalibrationRunAnalyteRequest
} from "./services/CalibrationRunService";

interface IcpInstrument {
  id: number;
  code: string;
  name: string;
  sectionId: number;
  type: string;
  cdsSoftware?: string;
}

interface ReferenceStandard {
  id: number;
  materialName: string;
  batchNumber: string;
  purity?: number | null;
  sectionId: number;
  expiryDate?: string | null;
}

type StatusFilter = "all" | "passed" | "failed";
type RunStatusFilter = "all" | "Active" | "Withdrawn";

interface CheckFormItem {
  id: string;
  checkType: CalibrationCheckType;
  sequencePosition: number;
  nominalMgPerL: string;
  measuredMgPerL: string;
}

interface AnalyteFormItem {
  testAnalyteId: number;
  element: string;
  wavelengthNm: number;
  view: "Axial" | "Radial";
  loqMgPerL?: number;
  correlationValue: string;
  correlationType: CorrelationType;
  numberOfStandards: string;
  lowestStandardMgPerL: string;
  highestStandardMgPerL: string;
  checks: CheckFormItem[];
}

const errorMessage = (e: unknown, fallback: string) => {
  const err = e as { response?: { data?: { message?: string } }; message?: string };
  return err.response?.data?.message ?? err.message ?? fallback;
};

// datetime-local wants the browser's local wall-clock time, not UTC.
function localNowForInput(): string {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

export function CalibrationRunsPage() {
  const { role } = useAuth();
  const { activeOptions: tests } = useTestDefinitions();
  const [runs, setRuns] = useState<CalibrationRunView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [methodFilter, setMethodFilter] = useState<string>("all");
  const [runStatusFilter, setRunStatusFilter] = useState<RunStatusFilter>("all");

  // New Run Wizard Dialog
  const [dialogOpen, setDialogOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState<1 | 2 | 3>(1);
  const [wizardError, setWizardError] = useState<string | null>(null);

  // Step 1 Form Data
  const [selectedTestId, setSelectedTestId] = useState<string>("");
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<string>("");
  const [selectedStandardId, setSelectedStandardId] = useState<string>("");
  const [selectedIcvStandardId, setSelectedIcvStandardId] = useState<string>("");
  // datetime-local wants the browser's local wall-clock time, not UTC.
  const [calibrationAt, setCalibrationAt] = useState<string>(localNowForInput);
  const [comment, setComment] = useState<string>("");
  const [reportFile, setReportFile] = useState<File | null>(null);

  // Step 2 Analyte Form Data
  const [analyteForms, setAnalyteForms] = useState<AnalyteFormItem[]>([]);
  const [analytesLoading, setAnalytesLoading] = useState<boolean>(false);

  // Step 3 Preview Results
  const [previewing, setPreviewing] = useState<boolean>(false);
  const [previewResult, setPreviewResult] = useState<CalibrationRunPreviewResult | null>(null);

  // Electronic Signature
  const [signing, setSigning] = useState<boolean>(false);
  const [createdRun, setCreatedRun] = useState<CalibrationRunView | null>(null);

  // Withdraw Flow
  const [withdrawDialogOpen, setWithdrawDialogOpen] = useState<boolean>(false);
  const [runToWithdraw, setRunToWithdraw] = useState<CalibrationRunView | null>(null);
  const [withdrawReason, setWithdrawReason] = useState<string>("");
  const [withdrawSigning, setWithdrawSigning] = useState<boolean>(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  // Master Data Resources
  const [instruments, setInstruments] = useState<IcpInstrument[]>([]);
  const [standards, setStandards] = useState<ReferenceStandard[]>([]);

  // Eligible methods requiring calibration
  const calMethods = useMemo(
    () =>
      tests.filter(
        (t) =>
          t.equationType === "CalibrationCurve" ||
          t.workflowType === "ElementalAssay"
      ),
    [tests]
  );

  const selectedMethod = useMemo(
    () => calMethods.find((t) => String(t.id) === selectedTestId),
    [calMethods, selectedTestId]
  );

  const methodSectionId = selectedMethod?.sectionId;

  // Load runs list
  const loadRuns = useCallback(() => {
    setLoading(true);
    setError(null);
    CalibrationRunService.getAll({
      passed: statusFilter === "all" ? undefined : statusFilter === "passed",
      testDefinitionId: methodFilter === "all" ? undefined : Number(methodFilter),
      runStatus: runStatusFilter === "all" ? undefined : (runStatusFilter as CalibrationRunStatus)
    })
      .then(setRuns)
      .catch((e) => setError(errorMessage(e, "Could not load calibration runs.")))
      .finally(() => setLoading(false));
  }, [statusFilter, methodFilter, runStatusFilter]);

  useEffect(() => {
    loadRuns();
  }, [loadRuns]);

  // Open New Run wizard
  const openNewRunDialog = async () => {
    setSelectedTestId("");
    setSelectedEquipmentId("");
    setSelectedStandardId("");
    setSelectedIcvStandardId("");
    setCalibrationAt(localNowForInput());
    setComment("");
    setReportFile(null);
    setAnalyteForms([]);
    setPreviewResult(null);
    setWizardError(null);
    setCreatedRun(null);
    setWizardStep(1);
    setDialogOpen(true);

    try {
      const [eq, stds] = await Promise.all([
        EquipmentConfigurationService.getEquipmentList("IcpOes"),
        MaterialService.getUsableReferenceStandards()
      ]);
      setInstruments(eq as IcpInstrument[]);
      setStandards(stds as ReferenceStandard[]);
    } catch (e) {
      setWizardError(errorMessage(e, "Could not load instruments or reference standards."));
    }
  };

  // Section-scoped options
  const sectionInstruments = useMemo(
    () =>
      instruments.filter(
        (i) => methodSectionId === undefined || i.sectionId === methodSectionId
      ),
    [instruments, methodSectionId]
  );

  const sectionStandards = useMemo(
    () =>
      standards.filter(
        (s) => methodSectionId === undefined || s.sectionId === methodSectionId
      ),
    [standards, methodSectionId]
  );

  // When selected test changes, prepare default analyte inputs
  const handleTestSelection = async (testIdStr: string) => {
    setSelectedTestId(testIdStr);
    setAnalyteForms([]);
    setWizardError(null);
    if (!testIdStr) return;

    const testId = Number(testIdStr);
    const method = calMethods.find((t) => t.id === testId);
    if (!method) return;

    setAnalytesLoading(true);
    try {
      const analytes: TestAnalyteDto[] = await masterDataOptions.getTestAnalytes(testId);
      const activeAnalytes = analytes.filter((a) => a.isActive);

      const defaultCorrelationType: CorrelationType =
        (method.calCorrelationType as CorrelationType) || "R";
      const defaultMinStandards = method.calMinStandards != null ? String(method.calMinStandards) : "5";

      // Pre-populate default checks according to method criteria
      const initialForms: AnalyteFormItem[] = activeAnalytes.map((a) => {
        const defaultChecks: CheckFormItem[] = [];
        let seq = 1;

        if (method.calRequireBlank) {
          defaultChecks.push({
            id: `${a.id}-blank-${seq}`,
            checkType: "Blank",
            sequencePosition: seq++,
            nominalMgPerL: "",
            measuredMgPerL: ""
          });
        }
        if (method.calRequireIcv) {
          defaultChecks.push({
            id: `${a.id}-icv-${seq}`,
            checkType: "Icv",
            sequencePosition: seq++,
            nominalMgPerL: "",
            measuredMgPerL: ""
          });
        }
        if (method.calRequireCcv) {
          defaultChecks.push({
            id: `${a.id}-ccv-${seq}`,
            checkType: "Ccv",
            sequencePosition: seq++,
            nominalMgPerL: "",
            measuredMgPerL: ""
          });
        }
        if (method.calRequireInternalStandard) {
          defaultChecks.push({
            id: `${a.id}-is-${seq}`,
            checkType: "InternalStandard",
            sequencePosition: seq,
            nominalMgPerL: "",
            measuredMgPerL: ""
          });
        }

        return {
          testAnalyteId: a.id,
          element: a.element,
          wavelengthNm: a.wavelengthNm,
          view: a.view,
          loqMgPerL: a.loqMgPerL,
          correlationValue: "",
          correlationType: defaultCorrelationType,
          numberOfStandards: defaultMinStandards,
          lowestStandardMgPerL: "",
          highestStandardMgPerL: "",
          checks: defaultChecks
        };
      });

      setAnalyteForms(initialForms);
    } catch (e) {
      setWizardError(errorMessage(e, "Could not load test analytes for the selected method."));
    } finally {
      setAnalytesLoading(false);
    }
  };

  // Step 1 Validation
  const canProceedToStep2 = useMemo(() => {
    return (
      Boolean(selectedTestId) &&
      Boolean(selectedEquipmentId) &&
      Boolean(selectedStandardId) &&
      Boolean(calibrationAt) &&
      Boolean(reportFile) &&
      analyteForms.length > 0
    );
  }, [selectedTestId, selectedEquipmentId, selectedStandardId, calibrationAt, reportFile, analyteForms]);

  // Analyte form field mutators
  const updateAnalyteField = (
    index: number,
    field: keyof Omit<AnalyteFormItem, "checks" | "testAnalyteId" | "element" | "wavelengthNm" | "view">,
    value: string
  ) => {
    setAnalyteForms((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: value };
      return copy;
    });
  };

  // Check mutators for a specific analyte
  const addCheckToAnalyte = (analyteIndex: number) => {
    setAnalyteForms((prev) => {
      const copy = [...prev];
      const target = { ...copy[analyteIndex] };
      const nextSeq = target.checks.length > 0 ? Math.max(...target.checks.map((c) => c.sequencePosition)) + 1 : 1;
      target.checks = [
        ...target.checks,
        {
          id: `${target.testAnalyteId}-custom-${Date.now()}-${nextSeq}`,
          checkType: "Ccv",
          sequencePosition: nextSeq,
          nominalMgPerL: "",
          measuredMgPerL: ""
        }
      ];
      copy[analyteIndex] = target;
      return copy;
    });
  };

  const removeCheckFromAnalyte = (analyteIndex: number, checkId: string) => {
    setAnalyteForms((prev) => {
      const copy = [...prev];
      const target = { ...copy[analyteIndex] };
      target.checks = target.checks.filter((c) => c.id !== checkId);
      copy[analyteIndex] = target;
      return copy;
    });
  };

  const updateCheckField = (
    analyteIndex: number,
    checkId: string,
    field: keyof CheckFormItem,
    value: string | number | CalibrationCheckType
  ) => {
    setAnalyteForms((prev) => {
      const copy = [...prev];
      const target = { ...copy[analyteIndex] };
      target.checks = target.checks.map((c) => (c.id === checkId ? { ...c, [field]: value } : c));
      copy[analyteIndex] = target;
      return copy;
    });
  };

  // Build DTO payload for preview / create
  const buildPayload = (): { valid: boolean; error?: string; analytes?: CreateCalibrationRunAnalyteRequest[] } => {
    for (const a of analyteForms) {
      if (!a.correlationValue || isNaN(Number(a.correlationValue))) {
        return { valid: false, error: `Valid correlation coefficient is required for analyte ${a.element}.` };
      }
      if (!a.numberOfStandards || isNaN(Number(a.numberOfStandards)) || Number(a.numberOfStandards) < 1) {
        return { valid: false, error: `Number of standards must be >= 1 for analyte ${a.element}.` };
      }
      if (!a.lowestStandardMgPerL || isNaN(Number(a.lowestStandardMgPerL))) {
        return { valid: false, error: `Lowest standard mg/L is required for analyte ${a.element}.` };
      }
      if (!a.highestStandardMgPerL || isNaN(Number(a.highestStandardMgPerL))) {
        return { valid: false, error: `Highest standard mg/L is required for analyte ${a.element}.` };
      }

      for (const chk of a.checks) {
        if (!chk.measuredMgPerL || isNaN(Number(chk.measuredMgPerL))) {
          return { valid: false, error: `Measured mg/L is required for all checks on analyte ${a.element}.` };
        }
        if (chk.checkType !== "Blank" && (chk.nominalMgPerL === "" || isNaN(Number(chk.nominalMgPerL)))) {
          return { valid: false, error: `Nominal mg/L is required for ${chk.checkType} checks on analyte ${a.element}.` };
        }
      }
    }

    const analytesDto: CreateCalibrationRunAnalyteRequest[] = analyteForms.map((a) => ({
      testAnalyteId: a.testAnalyteId,
      correlationValue: Number(a.correlationValue),
      correlationType: a.correlationType,
      numberOfStandards: Number(a.numberOfStandards),
      lowestStandardMgPerL: Number(a.lowestStandardMgPerL),
      highestStandardMgPerL: Number(a.highestStandardMgPerL),
      checks: a.checks.map((c) => ({
        checkType: c.checkType,
        sequencePosition: Number(c.sequencePosition),
        nominalMgPerL: c.nominalMgPerL.trim() !== "" ? Number(c.nominalMgPerL) : null,
        measuredMgPerL: Number(c.measuredMgPerL)
      }))
    }));

    return { valid: true, analytes: analytesDto };
  };

  // Preview action
  const handlePreview = async () => {
    setWizardError(null);
    const { valid, error: valErr, analytes } = buildPayload();
    if (!valid || !analytes) {
      setWizardError(valErr ?? "Please fill in all required analyte fields.");
      return;
    }

    setPreviewing(true);
    try {
      const res = await CalibrationRunService.preview({
        testDefinitionId: Number(selectedTestId),
        equipmentId: Number(selectedEquipmentId),
        calibrationStandardMaterialId: Number(selectedStandardId),
        icvStandardMaterialId: selectedIcvStandardId ? Number(selectedIcvStandardId) : null,
        calibrationAt: new Date(calibrationAt).toISOString(),
        comment: comment.trim() || null,
        analytes
      });
      setPreviewResult(res);
      setWizardStep(3);
    } catch (e) {
      setWizardError(errorMessage(e, "Preview failed. Please review your input."));
    } finally {
      setPreviewing(false);
    }
  };

  // Final Submit with signature
  const handleSignatureConfirm = async (password: string) => {
    if (!reportFile) {
      throw new Error("A calibration report document is required.");
    }
    const { valid, error: valErr, analytes } = buildPayload();
    if (!valid || !analytes) {
      throw new Error(valErr ?? "Invalid analyte parameters.");
    }

    const payload: CreateCalibrationRunRequest = {
      testDefinitionId: Number(selectedTestId),
      equipmentId: Number(selectedEquipmentId),
      calibrationStandardMaterialId: Number(selectedStandardId),
      icvStandardMaterialId: selectedIcvStandardId ? Number(selectedIcvStandardId) : null,
      calibrationAt: new Date(calibrationAt).toISOString(),
      password,
      comment: comment.trim() || null,
      analytes
    };

    const run = await CalibrationRunService.create(payload, reportFile);
    setSigning(false);
    setCreatedRun(run);
    loadRuns();
  };

  // Withdraw Action Handlers
  const canWithdraw = role === "SectionHead" || role === "SystemAdministrator";

  const openWithdrawDialog = (run: CalibrationRunView) => {
    setRunToWithdraw(run);
    setWithdrawReason("");
    setWithdrawError(null);
    setWithdrawDialogOpen(true);
  };

  const handleWithdrawSignatureConfirm = async (password: string) => {
    if (!runToWithdraw) return;
    if (withdrawReason.trim().length < 10) {
      throw new Error("Withdrawal reason must contain at least 10 characters.");
    }

    await CalibrationRunService.withdraw(runToWithdraw.id, {
      reason: withdrawReason.trim(),
      password
    });

    setWithdrawSigning(false);
    setWithdrawDialogOpen(false);
    setRunToWithdraw(null);
    loadRuns();
  };

  return (
    <>
      <PageHeader
        title="Calibration Runs (ICP-OES)"
        subtitle="Elemental assay calibration curves with multi-point linear regression and QC checks."
      >
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={loadRuns} disabled={loading}>
            Refresh
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openNewRunDialog}>
            New Run
          </Button>
        </Stack>
      </PageHeader>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Paper sx={{ p: 2.5 }}>
        {/* Filter bar */}
        <Stack direction="row" spacing={2} sx={{ mb: 2, flexWrap: "wrap", alignItems: "center" }}>
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Result</InputLabel>
            <Select
              label="Result"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
            >
              <MenuItem value="all">All Results</MenuItem>
              <MenuItem value="passed">Passed</MenuItem>
              <MenuItem value="failed">Failed</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 220 }}>
            <InputLabel>Method</InputLabel>
            <Select
              label="Method"
              value={methodFilter}
              onChange={(e) => setMethodFilter(e.target.value)}
            >
              <MenuItem value="all">All methods</MenuItem>
              {calMethods.map((t) => (
                <MenuItem key={t.id} value={String(t.id)}>
                  {t.displayName} ({t.methodAbbreviation || t.code})
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Run Status</InputLabel>
            <Select
              label="Run Status"
              value={runStatusFilter}
              onChange={(e) => setRunStatusFilter(e.target.value as RunStatusFilter)}
            >
              <MenuItem value="all">All Runs</MenuItem>
              <MenuItem value="Active">Active Only</MenuItem>
              <MenuItem value="Withdrawn">Withdrawn Only</MenuItem>
            </Select>
          </FormControl>
        </Stack>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>Run Code</TableCell>
                <TableCell>Method</TableCell>
                <TableCell>Instrument</TableCell>
                <TableCell>Calibration Date</TableCell>
                <TableCell>Analytes Summary</TableCell>
                <TableCell>Result</TableCell>
                <TableCell>Performed By</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {runs.map((r) => (
                <TableRow key={r.id}>
                  <TableCell sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{r.code}</TableCell>
                  <TableCell>
                    {r.testDisplayName || r.testCode}
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {r.methodAbbreviation ? `${r.methodAbbreviation} · ` : ""}
                      {r.sectionName}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {r.equipmentCode}
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.equipmentName}</Typography>
                  </TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {new Date(r.calibrationAt).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                      {r.analytes.map((a) => (
                        <Tooltip
                          key={a.id}
                          title={`${a.element} (${a.wavelengthNm} nm ${a.view}): ${
                            a.passed ? "Passed" : a.failureReasons || "Failed"
                          }`}
                        >
                          <Chip
                            size="small"
                            label={a.element}
                            color={a.passed ? "success" : "error"}
                            variant="outlined"
                            sx={{ fontWeight: 600, fontSize: "0.75rem" }}
                          />
                        </Tooltip>
                      ))}
                      <Typography sx={{ fontSize: 11, color: "text.secondary", ml: 0.5 }}>
                        ({r.analytesPassed}/{r.analytesTotal})
                      </Typography>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                      {r.passed ? (
                        <Chip size="small" color="success" label="Passed" />
                      ) : (
                        <Tooltip
                          title={
                            r.analytes
                              .filter((a) => !a.passed)
                              .map((a) => `${a.element}: ${a.failureReasons}`)
                              .join("; ") || "Calibration criteria not met"
                          }
                        >
                          <Chip size="small" color="error" label="Failed" />
                        </Tooltip>
                      )}
                      {r.status === "Withdrawn" && (
                        <Chip
                          size="small"
                          color="warning"
                          label="Withdrawn"
                          variant="filled"
                          sx={{ fontWeight: 600 }}
                        />
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {r.performedByName}
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {new Date(r.performedAt).toLocaleString()}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                      <Button
                        size="small"
                        startIcon={<DescriptionOutlinedIcon />}
                        href={`/laboratory/calibration-runs/${r.id}/report`}
                        target="_blank"
                        rel="noopener"
                      >
                        Report
                      </Button>
                      {canWithdraw && r.status === "Active" && (
                        <Button
                          size="small"
                          color="warning"
                          startIcon={<RemoveCircleOutlineIcon />}
                          onClick={() => openWithdrawDialog(r)}
                        >
                          Withdraw
                        </Button>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {runs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={8} align="center" sx={{ py: 4, color: "text.secondary" }}>
                    No calibration runs found.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        )}
      </Paper>

      {/* New Run Wizard Modal */}
      <FloatingDialog
        open={dialogOpen}
        title={
          createdRun
            ? `Run ${createdRun.code}`
            : `New Calibration Run — Step ${wizardStep} of 3: ${
                wizardStep === 1
                  ? "Header & Standards"
                  : wizardStep === 2
                  ? "Analyte Measurements"
                  : "Review Computed Results"
              }`
        }
        onClose={() => setDialogOpen(false)}
        maxWidth={wizardStep === 1 ? "md" : "lg"}
        actions={
          createdRun ? (
            <Button variant="contained" onClick={() => setDialogOpen(false)}>
              Close
            </Button>
          ) : (
            <>
              <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
              {wizardStep === 2 && (
                <Button startIcon={<ArrowBackIcon />} onClick={() => setWizardStep(1)}>
                  Back: Header
                </Button>
              )}
              {wizardStep === 3 && (
                <Button startIcon={<ArrowBackIcon />} onClick={() => setWizardStep(2)}>
                  Back: Edit Analytes
                </Button>
              )}
              {wizardStep === 1 && (
                <Button
                  variant="contained"
                  endIcon={<ArrowForwardIcon />}
                  disabled={!canProceedToStep2}
                  onClick={() => setWizardStep(2)}
                >
                  Next: Analytes ({analyteForms.length})
                </Button>
              )}
              {wizardStep === 2 && (
                <Button
                  variant="contained"
                  endIcon={<CheckCircleOutlineIcon />}
                  disabled={previewing || analyteForms.length === 0}
                  onClick={handlePreview}
                >
                  {previewing ? "Validating..." : "Review Computed Results"}
                </Button>
              )}
              {wizardStep === 3 && (
                <Button
                  variant="contained"
                  color="primary"
                  onClick={() => setSigning(true)}
                >
                  Confirm &amp; Sign
                </Button>
              )}
            </>
          )
        }
      >
        {createdRun ? (
          <Stack spacing={2}>
            <Alert severity={createdRun.passed ? "success" : "warning"}>
              {createdRun.passed
                ? `Run ${createdRun.code} passed all criteria. It is active and ready for elemental assay test orders.`
                : `Run ${createdRun.code} was recorded as FAILED (${createdRun.analytesPassed}/${createdRun.analytesTotal} passed). It is preserved for audit trail and compliance.`}
            </Alert>
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1 }}>
              <Typography variant="subtitle2">Run Details</Typography>
              <Typography variant="body2">Method: {createdRun.testDisplayName}</Typography>
              <Typography variant="body2">Instrument: {createdRun.equipmentCode}</Typography>
              <Typography variant="body2">Performed By: {createdRun.performedByName}</Typography>
            </Box>
          </Stack>
        ) : (
          <Stack spacing={2.5}>
            {wizardError && <Alert severity="error">{wizardError}</Alert>}

            {/* STEP 1: Header + Standards */}
            {wizardStep === 1 && (
              <Stack spacing={2}>
                <FormControl size="small" fullWidth>
                  <InputLabel>Elemental Method</InputLabel>
                  <Select
                    label="Elemental Method"
                    value={selectedTestId}
                    onChange={(e) => handleTestSelection(e.target.value)}
                  >
                    {calMethods.map((t) => (
                      <MenuItem key={t.id} value={String(t.id)}>
                        {t.displayName} ({t.methodAbbreviation || t.code})
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                {calMethods.length === 0 && (
                  <Alert severity="info">
                    No active elemental test method requires a calibration run. Configure one in FP Test Master.
                  </Alert>
                )}

                {selectedMethod && (
                  <Box
                    sx={{
                      p: 1.5,
                      bgcolor: "action.hover",
                      borderRadius: 1,
                      border: "1px solid",
                      borderColor: "divider",
                      fontSize: "0.85rem"
                    }}
                  >
                    <Typography variant="caption" sx={{ fontWeight: 600, display: "block" }}>
                      ACCEPTANCE CRITERIA FOR {selectedMethod.methodAbbreviation || selectedMethod.code}
                    </Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
                      Min Correlation: {selectedMethod.calMinCorrelation ?? "0.9995"} (
                      {selectedMethod.calCorrelationType === "RSquared" ? "r²" : "r"}) · Min Standards:{" "}
                      {selectedMethod.calMinStandards ?? 5} · ICV/CCV:{" "}
                      {selectedMethod.calCheckRecoveryLowPercent ?? 90}%–
                      {selectedMethod.calCheckRecoveryHighPercent ?? 110}% · Max Age:{" "}
                      {selectedMethod.calMaxRunAgeHours ?? 24}h
                    </Typography>
                  </Box>
                )}

                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <FormControl size="small" fullWidth disabled={!selectedMethod}>
                    <InputLabel>ICP-OES Instrument</InputLabel>
                    <Select
                      label="ICP-OES Instrument"
                      value={selectedEquipmentId}
                      onChange={(e) => setSelectedEquipmentId(e.target.value)}
                    >
                      {sectionInstruments.map((i) => (
                        <MenuItem key={i.id} value={String(i.id)}>
                          {i.code} — {i.name} ({i.cdsSoftware || "PerkinElmerSyngistix"})
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>

                  <TextField
                    size="small"
                    fullWidth
                    type="datetime-local"
                    label="Calibration Date/Time"
                    value={calibrationAt}
                    onChange={(e) => setCalibrationAt(e.target.value)}
                    slotProps={{
                      inputLabel: { shrink: true },
                      htmlInput: { max: new Date().toISOString().slice(0, 16) }
                    }}
                  />
                </Stack>

                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <FormControl size="small" fullWidth disabled={!selectedMethod}>
                    <InputLabel>Calibration Standard (Required)</InputLabel>
                    <Select
                      label="Calibration Standard (Required)"
                      value={selectedStandardId}
                      onChange={(e) => setSelectedStandardId(e.target.value)}
                    >
                      {sectionStandards.map((s) => (
                        <MenuItem key={s.id} value={String(s.id)}>
                          {s.materialName} — Lot {s.batchNumber} (Exp:{" "}
                          {s.expiryDate ? new Date(s.expiryDate).toLocaleDateString() : "N/A"})
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>

                  <FormControl size="small" fullWidth disabled={!selectedMethod}>
                    <InputLabel>ICV Standard (Second Source)</InputLabel>
                    <Select
                      label="ICV Standard (Second Source)"
                      value={selectedIcvStandardId}
                      onChange={(e) => setSelectedIcvStandardId(e.target.value)}
                    >
                      <MenuItem value="">
                        <em>None</em>
                      </MenuItem>
                      {sectionStandards.map((s) => (
                        <MenuItem key={s.id} value={String(s.id)}>
                          {s.materialName} — Lot {s.batchNumber} (Exp:{" "}
                          {s.expiryDate ? new Date(s.expiryDate).toLocaleDateString() : "N/A"})
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Stack>

                {selectedMethod?.calRequireIcv && !selectedIcvStandardId && (
                  <Alert severity="warning">
                    This method requires an Initial Calibration Verification (ICV) check. A second-source ICV standard is strongly recommended.
                  </Alert>
                )}

                {/* Syngistix Report Attachment */}
                <Box sx={{ p: 2, border: "1px dashed", borderColor: "divider", borderRadius: 1 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                    Syngistix Calibration Report Attachment *
                  </Typography>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
                    Upload the original instrument report (.pdf, .txt, .csv, .rep, max 30 MB).
                  </Typography>
                  <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
                    <Button
                      variant="outlined"
                      component="label"
                      startIcon={<UploadFileIcon />}
                      size="small"
                    >
                      Select File
                      <input
                        type="file"
                        hidden
                        accept=".pdf,.txt,.csv,.xlsx,.rep"
                        onChange={(e) => {
                          const file = e.target.files?.[0] || null;
                          setReportFile(file);
                        }}
                      />
                    </Button>
                    {reportFile ? (
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {reportFile.name} ({(reportFile.size / 1024).toFixed(1)} KB)
                      </Typography>
                    ) : (
                      <Typography variant="body2" sx={{ color: "error.main" }}>
                        No file chosen (required)
                      </Typography>
                    )}
                  </Stack>
                </Box>

                <TextField
                  size="small"
                  fullWidth
                  multiline
                  rows={2}
                  label="Comment (optional)"
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                />
              </Stack>
            )}

            {/* STEP 2: Analyte Grid */}
            {wizardStep === 2 && (
              <Stack spacing={2.5}>
                <Alert severity="info">
                  Zero client-side computation: Enter the correlation values and measured concentrations from the Syngistix report exactly as reported. Proceed to &quot;Review Computed Results&quot; to evaluate pass/fail.
                </Alert>

                {analytesLoading ? (
                  <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
                    <CircularProgress size={28} />
                  </Box>
                ) : analyteForms.length === 0 ? (
                  <Alert severity="warning">
                    No analytes configured for this test method. Add analytes to the method in FP Test Master before running calibration.
                  </Alert>
                ) : (
                  analyteForms.map((analyte, aIdx) => (
                    <Card
                      key={analyte.testAnalyteId}
                      variant="outlined"
                      sx={{ borderColor: "divider", borderRadius: 1.5 }}
                    >
                      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
                        <Stack
                          direction="row"
                          spacing={1.5}
                          sx={{ alignItems: "center", mb: 1.5, flexWrap: "wrap" }}
                        >
                          <Chip
                            label={analyte.element}
                            color="primary"
                            size="small"
                            sx={{ fontWeight: 700, fontSize: "0.85rem" }}
                          />
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                            {analyte.element} · {analyte.wavelengthNm} nm ({analyte.view})
                          </Typography>
                          {analyte.loqMgPerL != null && (
                            <Typography variant="caption" sx={{ color: "text.secondary" }}>
                              (LOQ: {analyte.loqMgPerL} mg/L)
                            </Typography>
                          )}
                        </Stack>

                        {/* Analyte Curve Inputs */}
                        <Stack
                          direction="row"
                          spacing={1.5}
                          sx={{ flexWrap: "wrap", alignItems: "center", mb: 2 }}
                        >
                          <TextField
                            size="small"
                            type="number"
                            label="Correlation Value"
                            placeholder="e.g. 0.9998"
                            value={analyte.correlationValue}
                            onChange={(e) => updateAnalyteField(aIdx, "correlationValue", e.target.value)}
                            slotProps={{ htmlInput: { min: 0, max: 1, step: "any" } }}
                            sx={{ flex: "1 1 140px", minWidth: 120 }}
                          />
                          <FormControl size="small" sx={{ flex: "1 1 120px", minWidth: 110 }}>
                            <InputLabel>Corr Type</InputLabel>
                            <Select
                              label="Corr Type"
                              value={analyte.correlationType}
                              onChange={(e) =>
                                updateAnalyteField(aIdx, "correlationType", e.target.value as CorrelationType)
                              }
                            >
                              <MenuItem value="R">r</MenuItem>
                              <MenuItem value="RSquared">r²</MenuItem>
                            </Select>
                          </FormControl>
                          <TextField
                            size="small"
                            type="number"
                            label="Standards Count"
                            value={analyte.numberOfStandards}
                            onChange={(e) => updateAnalyteField(aIdx, "numberOfStandards", e.target.value)}
                            slotProps={{ htmlInput: { min: 1, step: 1 } }}
                            sx={{ flex: "1 1 120px", minWidth: 100 }}
                          />
                          <TextField
                            size="small"
                            type="number"
                            label="Lowest Std (mg/L)"
                            placeholder="0.005"
                            value={analyte.lowestStandardMgPerL}
                            onChange={(e) =>
                              updateAnalyteField(aIdx, "lowestStandardMgPerL", e.target.value)
                            }
                            slotProps={{ htmlInput: { min: 0, step: "any" } }}
                            sx={{ flex: "1 1 140px", minWidth: 120 }}
                          />
                          <TextField
                            size="small"
                            type="number"
                            label="Highest Std (mg/L)"
                            placeholder="10.0"
                            value={analyte.highestStandardMgPerL}
                            onChange={(e) =>
                              updateAnalyteField(aIdx, "highestStandardMgPerL", e.target.value)
                            }
                            slotProps={{ htmlInput: { min: 0, step: "any" } }}
                            sx={{ flex: "1 1 140px", minWidth: 120 }}
                          />
                        </Stack>

                        <Divider sx={{ my: 1.5 }} />

                        {/* Analyte Checks Grid */}
                        <Stack
                          direction="row"
                          sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}
                        >
                          <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                            CALIBRATION CHECKS FOR {analyte.element}
                          </Typography>
                          <Button
                            size="small"
                            startIcon={<AddIcon />}
                            onClick={() => addCheckToAnalyte(aIdx)}
                          >
                            Add Check
                          </Button>
                        </Stack>

                        {analyte.checks.length === 0 ? (
                          <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                            No checks added. Use &quot;Add Check&quot; to define Blank, ICV, or CCV checks.
                          </Typography>
                        ) : (
                          <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
                            <TableHead>
                              <TableRow sx={tableHeadSx}>
                                <TableCell sx={{ width: 150 }}>Check Type</TableCell>
                                <TableCell sx={{ width: 80 }}>Seq</TableCell>
                                <TableCell sx={{ minWidth: 130 }}>Nominal (mg/L)</TableCell>
                                <TableCell sx={{ minWidth: 130 }}>Measured (mg/L)</TableCell>
                                <TableCell align="right" sx={{ width: 60 }} />
                              </TableRow>
                            </TableHead>
                            <TableBody>
                              {analyte.checks.map((chk) => (
                                <TableRow key={chk.id}>
                                  <TableCell>
                                    <Select
                                      size="small"
                                      fullWidth
                                      value={chk.checkType}
                                      onChange={(e) =>
                                        updateCheckField(
                                          aIdx,
                                          chk.id,
                                          "checkType",
                                          e.target.value as CalibrationCheckType
                                        )
                                      }
                                    >
                                      <MenuItem value="Blank">Blank</MenuItem>
                                      <MenuItem value="Icv">ICV</MenuItem>
                                      <MenuItem value="Ccv">CCV</MenuItem>
                                      <MenuItem value="InternalStandard">Internal Standard</MenuItem>
                                    </Select>
                                  </TableCell>
                                  <TableCell>
                                    <TextField
                                      size="small"
                                      type="number"
                                      value={chk.sequencePosition}
                                      onChange={(e) =>
                                        updateCheckField(aIdx, chk.id, "sequencePosition", Number(e.target.value))
                                      }
                                      slotProps={{ htmlInput: { min: 1, step: 1 } }}
                                    />
                                  </TableCell>
                                  <TableCell>
                                    <TextField
                                      size="small"
                                      type="number"
                                      placeholder={chk.checkType === "Blank" ? "N/A" : "e.g. 1.0"}
                                      disabled={chk.checkType === "Blank"}
                                      value={chk.nominalMgPerL}
                                      onChange={(e) =>
                                        updateCheckField(aIdx, chk.id, "nominalMgPerL", e.target.value)
                                      }
                                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                                    />
                                  </TableCell>
                                  <TableCell>
                                    <TextField
                                      size="small"
                                      type="number"
                                      placeholder="e.g. 0.998"
                                      value={chk.measuredMgPerL}
                                      onChange={(e) =>
                                        updateCheckField(aIdx, chk.id, "measuredMgPerL", e.target.value)
                                      }
                                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                                    />
                                  </TableCell>
                                  <TableCell align="right">
                                    <IconButton
                                      size="small"
                                      color="error"
                                      onClick={() => removeCheckFromAnalyte(aIdx, chk.id)}
                                    >
                                      <DeleteOutlineIcon fontSize="small" />
                                    </IconButton>
                                  </TableCell>
                                </TableRow>
                              ))}
                            </TableBody>
                          </Table>
                        )}
                      </CardContent>
                    </Card>
                  ))
                )}
              </Stack>
            )}

            {/* STEP 3: Preview Computed Results */}
            {wizardStep === 3 && previewResult && (
              <Stack spacing={2}>
                {previewResult.standardsExpiredOrMissing ? (
                  <Alert severity="error">
                    <strong>Standard Expiry Warning:</strong> {previewResult.standardsExpiryFailureReason}
                  </Alert>
                ) : (
                  <Alert severity="success">
                    Reference standard lot and expiry date verified valid.
                  </Alert>
                )}

                <Alert severity={previewResult.passed ? "success" : "warning"}>
                  <strong>
                    Overall Calibration Run Result: {previewResult.passed ? "PASSED" : "FAILED"}
                  </strong>{" "}
                  ({previewResult.analytesPassed} of {previewResult.analytesTotal} analytes passed)
                </Alert>

                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Analyte Evaluation Breakdown (Server Computed)
                </Typography>

                {previewResult.analytes.map((a) => (
                  <Card
                    key={a.testAnalyteId}
                    variant="outlined"
                    sx={{
                      borderColor: a.passed ? "success.light" : "error.light",
                      bgcolor: a.passed ? "rgba(46, 125, 50, 0.02)" : "rgba(211, 47, 47, 0.02)"
                    }}
                  >
                    <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
                      <Stack
                        direction="row"
                        spacing={1.5}
                        sx={{ alignItems: "center", justifyContent: "space-between", mb: 1 }}
                      >
                        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                          <Chip
                            label={a.element}
                            color={a.passed ? "success" : "error"}
                            size="small"
                            sx={{ fontWeight: 700 }}
                          />
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                            {a.element} · {a.wavelengthNm} nm ({a.view})
                          </Typography>
                        </Stack>
                        <Chip
                          label={a.passed ? "PASSED" : "FAILED"}
                          color={a.passed ? "success" : "error"}
                          size="small"
                          sx={{ fontWeight: 700 }}
                        />
                      </Stack>

                      {!a.passed && a.failureReasons && (
                        <Alert severity="error" sx={{ my: 1, py: 0.5 }}>
                          {a.failureReasons}
                        </Alert>
                      )}

                      <Typography variant="body2" sx={{ color: "text.secondary", fontSize: "0.85rem", mb: 1 }}>
                        Correlation: {a.correlationValue} ({a.correlationType === "RSquared" ? "r²" : "r"}) ·
                        Standards: {a.numberOfStandards} · Range: {a.lowestStandardMgPerL} – {a.highestStandardMgPerL} mg/L
                      </Typography>

                      <Table size="small">
                        <TableHead>
                          <TableRow sx={tableHeadSx}>
                            <TableCell>Check</TableCell>
                            <TableCell>Seq</TableCell>
                            <TableCell align="right">Nominal (mg/L)</TableCell>
                            <TableCell align="right">Measured (mg/L)</TableCell>
                            <TableCell align="right">Computed Recovery</TableCell>
                            <TableCell align="center">Result</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {a.checks.map((c, cIdx) => (
                            <TableRow key={cIdx}>
                              <TableCell>{c.checkType}</TableCell>
                              <TableCell>{c.sequencePosition}</TableCell>
                              <TableCell align="right">{c.nominalMgPerL ?? "—"}</TableCell>
                              <TableCell align="right">{c.measuredMgPerL}</TableCell>
                              <TableCell align="right" sx={{ fontWeight: 600 }}>
                                {c.recoveryPercent != null ? `${c.recoveryPercent.toFixed(2)}%` : "—"}
                              </TableCell>
                              <TableCell align="center">
                                <Chip
                                  size="small"
                                  label={c.passed ? "Pass" : "Fail"}
                                  color={c.passed ? "success" : "error"}
                                  variant="outlined"
                                />
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </CardContent>
                  </Card>
                ))}
              </Stack>
            )}
          </Stack>
        )}
      </FloatingDialog>

      {/* New Run Electronic Signature Dialog */}
      <SignatureDialog
        open={signing}
        meaningStatement="I have performed this calibration run and verified that the entered values match the instrument report."
        onCancel={() => setSigning(false)}
        onConfirm={handleSignatureConfirm}
      />

      {/* Withdraw Dialog */}
      <FloatingDialog
        open={withdrawDialogOpen}
        title={`Withdraw Calibration Run ${runToWithdraw?.code}`}
        onClose={() => setWithdrawDialogOpen(false)}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={() => setWithdrawDialogOpen(false)}>Cancel</Button>
            <Button
              variant="contained"
              color="warning"
              disabled={withdrawReason.trim().length < 10}
              onClick={() => setWithdrawSigning(true)}
            >
              Sign &amp; Withdraw
            </Button>
          </>
        }
      >
        <Stack spacing={2}>
          {withdrawError && <Alert severity="error">{withdrawError}</Alert>}
          <Alert severity="warning">
            Withdrawing this calibration run will prevent it from being used for any new test orders. Existing test orders linked to this run will be flagged. This action cannot be undone.
          </Alert>
          <TextField
            size="small"
            fullWidth
            required
            multiline
            rows={3}
            label="Withdrawal Reason"
            placeholder="Explain why this calibration run is being withdrawn (minimum 10 characters)..."
            value={withdrawReason}
            onChange={(e) => setWithdrawReason(e.target.value)}
            helperText={`${withdrawReason.trim().length}/10 characters minimum`}
            error={withdrawReason.length > 0 && withdrawReason.trim().length < 10}
          />
        </Stack>
      </FloatingDialog>

      {/* Withdraw Electronic Signature Dialog */}
      <SignatureDialog
        open={withdrawSigning}
        meaningStatement="I am withdrawing this calibration run as Section Head / Administrator."
        onCancel={() => setWithdrawSigning(false)}
        onConfirm={handleWithdrawSignatureConfirm}
      />
    </>
  );
}
