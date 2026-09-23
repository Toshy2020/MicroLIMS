import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { StatusBadge } from "../../components/StatusBadge";
import { SignatureDialog } from "../../components/SignatureDialog";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CalibrationRunService, CalibrationRunView, CalibrationRunAnalyteView } from "../calibrationRuns/services/CalibrationRunService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { ElementalAssayElementDetail } from "./types/sampleSummaryTypes";

interface Props {
  testOrderId: number;
  displayName: string;
  testCode?: string;
  itemId?: number | null;
  sampleId?: number | null;
  current: {
    workflowType?: string;
    testName?: string;
    sampleContext?: {
      sampleName?: string;
      [key: string]: unknown;
    };
    allStepsComplete?: boolean;
    finalResult?: string | null;
    returnInfo?: { reason?: string | null } | null;
  };
  onRecorded: () => Promise<void> | void;
  onClose?: () => void;
}

interface UsableAnalyteOption {
  runId: number;
  runCode: string;
  analyte: CalibrationRunAnalyteView;
}

interface ElementRowState {
  specificationId: number;
  parameterName: string;
  element: string;
  testAnalyteId: number | null;
  calibrationRunAnalyteId: number | "";
  reportedPpm: string;
  overRange: boolean;
  belowLoq: boolean;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

export function ElementalAssayPanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  sampleId,
  current,
  onRecorded,
  onClose
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [sampleMatrix, setSampleMatrix] = useState<"Solid" | "Liquid">("Solid");
  const [unitAmount, setUnitAmount] = useState("");
  const [analysisTime, setAnalysisTime] = useState(() => getLocalIsoString());
  const [rows, setRows] = useState<ElementRowState[]>([]);
  const [optionsByAnalyteId, setOptionsByAnalyteId] = useState<Record<number, UsableAnalyteOption[]>>({});
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);

  // Outcome / completion view
  const [recordedElements, setRecordedElements] = useState<ElementalAssayElementDetail[] | null>(null);
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  useEffect(() => {
    let active = true;

    const loadData = async () => {
      setLoading(true);
      setError(null);

      try {
        // 1. Resolve itemId if not provided
        let resolvedItemId = itemId;
        if (resolvedItemId == null) {
          try {
            const allItems = await ItemService.getAll();
            const matched = allItems.find(
              (i) =>
                i.name === current.sampleContext?.sampleName ||
                i.assignedTests?.some((t) => t.testCode === effectiveTestCode)
            );
            resolvedItemId = matched?.id ?? null;
          } catch {
            // fallback
          }
        }

        // 2. Fetch specifications for this item and test
        let specs: SpecificationDto[] = [];
        if (resolvedItemId != null) {
          const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
          specs = itemSpecs.filter(
            (s) => s.testCode === effectiveTestCode && s.testAnalyteId != null
          );
        }

        if (specs.length === 0) {
          setError(`No elemental specifications configured for test ${effectiveTestCode}.`);
          setLoading(false);
          return;
        }

        const matrix = (specs[0].sampleMatrix as "Solid" | "Liquid") || "Solid";
        if (active) {
          setSampleMatrix(matrix);
        }

        // 3. Fetch active calibration runs for this test
        const runs: CalibrationRunView[] = await CalibrationRunService.getAll({
          testCode: effectiveTestCode,
          runStatus: "Active"
        });

        // 4. Map usable analytes per testAnalyteId
        const map: Record<number, UsableAnalyteOption[]> = {};
        for (const r of runs) {
          if (r.status !== "Active") continue;
          for (const a of r.analytes || []) {
            if (a.isUsable) {
              if (!map[a.testAnalyteId]) {
                map[a.testAnalyteId] = [];
              }
              map[a.testAnalyteId].push({
                runId: r.id,
                runCode: r.code,
                analyte: a
              });
            }
          }
        }

        if (active) {
          setOptionsByAnalyteId(map);

          // Build element rows
          const initialRows: ElementRowState[] = specs.map((spec) => {
            const analyteOpts = spec.testAnalyteId != null ? map[spec.testAnalyteId] || [] : [];
            const defaultChoice = analyteOpts.length > 0 ? analyteOpts[0].analyte.id : "";
            const elemName =
              analyteOpts[0]?.analyte.element || spec.parameterName || "Element";

            return {
              specificationId: spec.id!,
              parameterName: spec.parameterName || elemName,
              element: elemName,
              testAnalyteId: spec.testAnalyteId ?? null,
              calibrationRunAnalyteId: defaultChoice,
              reportedPpm: "",
              overRange: false,
              belowLoq: false
            };
          });

          setRows(initialRows);

          // If already complete, try to load summary
          if (current.allStepsComplete && sampleId != null) {
            try {
              const summary = await SampleSummaryService.getSummary(sampleId);
              const order = summary.testOrders.find((t) => t.testOrderId === testOrderId);
              if (order?.elementalAssay?.elements) {
                setRecordedElements(order.elementalAssay.elements);
              }
            } catch {
              // ignore
            }
          }
        }
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load elemental assay specifications or calibration runs.";
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    };

    loadData();

    return () => {
      active = false;
    };
  }, [testOrderId, effectiveTestCode, itemId, sampleId, current.sampleContext?.sampleName, current.allStepsComplete]);

  const handleRowChange = (index: number, patch: Partial<ElementRowState>) => {
    setRows((prev) =>
      prev.map((r, i) => {
        if (i !== index) return r;
        const updated = { ...r, ...patch };
        // Enforce mutual exclusivity of Over range and < LOQ
        if (patch.overRange === true) {
          updated.belowLoq = false;
        } else if (patch.belowLoq === true) {
          updated.overRange = false;
        }
        return updated;
      })
    );
  };

  const isRowValid = (r: ElementRowState) => {
    if (!r.calibrationRunAnalyteId) return false;
    const typed = r.reportedPpm.trim();
    if (typed !== "" && (isNaN(Number(typed)) || Number(typed) < 0)) return false;
    return r.overRange || r.belowLoq || typed !== "";
  };

  const readyToSign =
    Number(unitAmount) > 0 &&
    analysisTime.trim() !== "" &&
    rows.length > 0 &&
    rows.every(isRowValid);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload = {
        unitAmount: Number(unitAmount),
        analysedAt: new Date(analysisTime).toISOString(),
        elements: rows.map((r) => ({
          specificationId: r.specificationId,
          calibrationRunAnalyteId: Number(r.calibrationRunAnalyteId),
          // Record what the report shows; with a flag the reading is optional.
          reportedPpm: r.reportedPpm.trim() === "" ? 0 : Number(r.reportedPpm),
          overRange: r.overRange,
          belowLoq: r.belowLoq
        })),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordElementalResult(testOrderId, payload);
      setSigning(false);
      setOutcome({
        outcomeSummary: res?.outcomeSummary ?? res?.finalResult,
        status: res?.status
      });

      // Fetch per-element calculated display from server summary
      if (sampleId != null) {
        try {
          const summary = await SampleSummaryService.getSummary(sampleId);
          const order = summary.testOrders.find((t) => t.testOrderId === testOrderId);
          if (order?.elementalAssay?.elements) {
            setRecordedElements(order.elementalAssay.elements);
          }
        } catch {
          // ignore
        }
      }

      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to record elemental assay result.";
      setError(msg);
    }
  };

  // Completion view
  if (current.allStepsComplete || outcome) {
    const isOos = (outcome?.status ?? "").includes("OutOfSpecification");
    const isReview = (outcome?.status ?? "").includes("RequiresReview");
    const alertSeverity = isOos ? "error" : isReview ? "warning" : "success";

    return (
      <Box>
        <Alert severity={alertSeverity} sx={{ mb: 2 }}>
          {displayName}: <strong>{outcome?.outcomeSummary ?? current.finalResult ?? "Results Recorded"}</strong>
          {outcome?.status && ` (${outcome.status})`}
        </Alert>

        {recordedElements && recordedElements.length > 0 ? (
          <Box sx={{ mb: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5, p: 2 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
              Reported Elemental Results (Calculated by Server)
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow sx={{ bgcolor: "action.hover" }}>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Element</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Run Code</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }} align="right">Reported (ppm)</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported Display</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Specification</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12 }} align="center">Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {recordedElements.map((elem) => (
                  <TableRow key={elem.parameterName || elem.element}>
                    <TableCell sx={{ fontSize: 13, fontWeight: 600 }}>
                      {elem.element} {elem.parameterName !== elem.element && `(${elem.parameterName})`}
                    </TableCell>
                    <TableCell sx={{ fontSize: 13 }}>
                      {elem.runCode}
                      <Chip
                        size="small"
                        color={elem.runAnalytePassed ? "success" : "default"}
                        label={elem.runAnalytePassed ? "Passed" : "Failed"}
                        sx={{ height: 18, fontSize: 10, ml: 1 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 13 }} align="right">
                      {elem.overRange ? "Over range" : elem.belowLoq ? "< LOQ" : elem.reportedPpm}
                    </TableCell>
                    <TableCell sx={{ fontSize: 13, fontWeight: 700, color: "primary.main" }}>
                      {elem.reportedDisplay}
                    </TableCell>
                    <TableCell sx={{ fontSize: 13 }}>{elem.specLimit ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 13 }} align="center">
                      <StatusBadge status={elem.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        ) : null}

        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded — Pending Review" />
          {onClose && (
            <Button variant="contained" onClick={onClose}>
              Done / Close
            </Button>
          )}
        </Box>
      </Box>
    );
  }

  if (loading) {
    return (
      <Box sx={{ py: 4, display: "flex", justifyContent: "center", alignItems: "center" }}>
        <CircularProgress size={28} />
      </Box>
    );
  }

  const unitAmountLabel = sampleMatrix === "Liquid" ? "Dose volume (mL) *" : "Average unit weight (g) *";

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? `Returned for revision: ${current.returnInfo.reason}`
            : "Returned by reviewer for revision"}
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2.5}>
        {/* Header fields: Unit Amount & Analysis Time */}
        <Box sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            1. Sample & Run Configuration &middot; Matrix: {sampleMatrix}
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="number"
              label={unitAmountLabel}
              value={unitAmount}
              onChange={(e) => setUnitAmount(e.target.value)}
              placeholder={sampleMatrix === "Liquid" ? "e.g. 5" : "e.g. 1.2500"}
              slotProps={{ htmlInput: { step: "any", min: "0.000001" } }}
              required
              fullWidth
            />

            <TextField
              size="small"
              type="datetime-local"
              label="Analysis time *"
              value={analysisTime}
              onChange={(e) => setAnalysisTime(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              required
              fullWidth
            />
          </Box>
        </Box>

        {/* Element parameters rows */}
        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            2. Elemental Results ({rows.length} parameters)
          </Typography>

          <Stack spacing={2}>
            {rows.map((row, idx) => {
              const options = (row.testAnalyteId != null ? optionsByAnalyteId[row.testAnalyteId] : []) || [];

              return (
                <Box
                  key={row.specificationId}
                  sx={{
                    p: 1.5,
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 1,
                    bgcolor: "action.hover"
                  }}
                >
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
                      {row.element}
                      {row.parameterName && row.parameterName !== row.element && (
                        <Typography component="span" sx={{ fontWeight: 400, color: "text.secondary", ml: 1, fontSize: 12 }}>
                          ({row.parameterName})
                        </Typography>
                      )}
                    </Typography>
                    {options.length === 0 && (
                      <Chip
                        size="small"
                        color="warning"
                        label="No usable active calibration run for this element"
                        sx={{ height: 20, fontSize: 11 }}
                      />
                    )}
                  </Box>

                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: { xs: "1fr", sm: "2fr 2fr 1fr 1fr" },
                      gap: 1.5,
                      alignItems: "center"
                    }}
                  >
                    {/* Calibration run select */}
                    <FormControl size="small" fullWidth required error={options.length === 0}>
                      <InputLabel id={`cal-run-label-${idx}`}>Calibration Run *</InputLabel>
                      <Select
                        labelId={`cal-run-label-${idx}`}
                        label="Calibration Run *"
                        value={row.calibrationRunAnalyteId}
                        onChange={(e) =>
                          handleRowChange(idx, {
                            calibrationRunAnalyteId: Number(e.target.value)
                          })
                        }
                      >
                        {options.length === 0 ? (
                          <MenuItem value="" disabled>
                            <em>No usable runs found</em>
                          </MenuItem>
                        ) : (
                          options.map((opt) => (
                            <MenuItem key={opt.analyte.id} value={opt.analyte.id}>
                              {opt.runCode} &middot; {opt.analyte.element} &middot; passed
                            </MenuItem>
                          ))
                        )}
                      </Select>
                    </FormControl>

                    {/* Instrument-reported ppm (Syngistix for ICP-OES, the AAS software report for AAS) */}
                    <TextField
                      size="small"
                      type="number"
                      label="Concentration in the sample as reported by the instrument software (ppm)"
                      value={row.reportedPpm}
                      onChange={(e) => handleRowChange(idx, { reportedPpm: e.target.value })}
                      placeholder={row.overRange ? "Over range" : row.belowLoq ? "< LOQ" : "e.g. 8500"}
                      slotProps={{ htmlInput: { step: "any", min: "0" } }}
                      fullWidth
                    />

                    {/* Over range checkbox */}
                    <FormControlLabel
                      control={
                        <Checkbox
                          size="small"
                          checked={row.overRange}
                          onChange={(e) => handleRowChange(idx, { overRange: e.target.checked })}
                        />
                      }
                      label={<Typography variant="body2" sx={{ fontSize: 12 }}>Over range</Typography>}
                    />

                    {/* < LOQ checkbox */}
                    <FormControlLabel
                      control={
                        <Checkbox
                          size="small"
                          checked={row.belowLoq}
                          onChange={(e) => handleRowChange(idx, { belowLoq: e.target.checked })}
                        />
                      }
                      label={<Typography variant="body2" sx={{ fontSize: 12 }}>&lt; LOQ</Typography>}
                    />
                  </Box>
                </Box>
              );
            })}
          </Stack>
        </Box>

        {/* Comment */}
        <TextField
          size="small"
          label="Comment (optional)"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          rows={2}
          fullWidth
        />

        {/* Action button */}
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            disabled={!readyToSign}
            onClick={() => setSigning(true)}
            sx={{ minWidth: 140, fontWeight: 700, textTransform: "none" }}
          >
            Sign and calculate
          </Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these sample values from the instrument software report and am recording this elemental assay result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
