import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useTheme
} from "@mui/material";
import { SignatureDialog } from "../../components/SignatureDialog";
import { CriteriaCard, ResultSection, VerdictBanner } from "../../components/lab";
import type { CriteriaRow, Verdict } from "../../components/lab";
import { tableHeadSx } from "../../theme";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import type { ParameterResultDetail } from "./types/sampleSummaryTypes";
import type {
  RecordUvParameterRequest,
  RecordUvResultRequest,
  SampleMoistureResult
} from "./types/testWorkflowTypes";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { MaterialService } from "../inventory/materials/services/MaterialService";
import { formatMoistureLabel, getMissingMoistureMessage } from "./utils/moistureRetentionHelpers";
import { UvReviewPanel } from "./uv/UvReviewPanel";

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

export interface StandardMaterialOption {
  id: number;
  materialName: string;
  batchNumber: string;
  lotLabel?: string;
  purity?: number | null;
  moisturePercent?: number | null;
  materialType?: string | null;
  code?: string | null;
}

interface SpecParamState {
  wavelengthNm: string;
  referenceAbsorbance: string;
  replicates: { weightG: string; absorbance: string }[];
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

export function UvPanel({
  testOrderId,
  displayName,
  testCode,
  sampleId,
  current,
  onRecorded
}: Props) {
  const theme = useTheme();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [testDef, setTestDef] = useState<TestDefinitionOption | null>(null);
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [equipmentList, setEquipmentList] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);

  // Standard comparison fields
  const [standardLots, setStandardLots] = useState<StandardMaterialOption[]>([]);
  const [standardMaterialId, setStandardMaterialId] = useState<number | "">("");
  const [standardWeightG, setStandardWeightG] = useState("");
  const [standardAbsorbance, setStandardAbsorbance] = useState("");

  // Moisture state
  const [moisture, setMoisture] = useState<SampleMoistureResult | null>(null);
  const [moistureStatus, setMoistureStatus] = useState<"idle" | "loading" | "loaded" | "error">("idle");
  const [moistureError, setMoistureError] = useState("");

  // Parameter states and touched replicate weights for row-0 prefilling
  const [paramStates, setParamStates] = useState<Record<number, SpecParamState>>({});
  const [touchedWeights, setTouchedWeights] = useState<Record<string, boolean>>({});

  // Outcome / completion view
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);
  const [recorded, setRecorded] = useState<ParameterResultDetail[]>([]);
  const [summaryError, setSummaryError] = useState<string | null>(null);

  const effectiveTestCode = current.testName || testCode || "";
  const showCompleted = Boolean(current.allStepsComplete || outcome);

  // Fetch recorded results on completion
  useEffect(() => {
    if (!showCompleted || sampleId == null) return;
    let active = true;
    setSummaryError(null);
    SampleSummaryService.getSummary(sampleId)
      .then((summary) => {
        const ord = summary.testOrders?.find((t) => t.testOrderId === testOrderId);
        if (active) setRecorded(ord?.analysis?.parameterResults ?? []);
      })
      .catch((err: unknown) => {
        if (active) {
          setSummaryError(
            (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
              "Could not load test analysis summary."
          );
        }
      });
    return () => {
      active = false;
    };
  }, [showCompleted, sampleId, testOrderId]);

  // Initial load
  useEffect(() => {
    let active = true;

    const loadData = async () => {
      setLoading(true);
      setError(null);

      try {
        // 1. Fetch test definitions
        const defs: TestDefinitionOption[] = await masterDataOptions.getTestDefinitions();
        const matchedDef = defs.find((d) => d.code === effectiveTestCode) ?? null;

        // 2. Fetch specifications for this item and test
        const itemSpecs = await SpecificationService.getForTestOrder(testOrderId);
        const loadedSpecs: SpecificationDto[] = (itemSpecs ?? [])
          .filter((s) => s.testCode === effectiveTestCode)
          .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));

        if (loadedSpecs.length === 0) {
          if (active) {
            setError(`No specifications configured for test ${effectiveTestCode}.`);
            setLoading(false);
          }
          return;
        }

        // 3. Load FP section equipment (UvVis or Other only)
        let fpInstruments: ConfiguredEquipmentSummary[] = [];
        try {
          const [sections, allEquip] = await Promise.all([
            getSections(),
            EquipmentConfigurationService.getConfiguredSummary()
          ]);
          const fpSec = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === "FP");
          const isUvType = (t: string | number | undefined) => t === "UvVis" || t === "Other";
          fpInstruments = (allEquip ?? []).filter(
            (e) =>
              isUvType(e.type) &&
              ((fpSec && e.sectionId === fpSec.sectionId) ||
                e.section?.code === "FP" ||
                e.section?.sectionCode === "FP")
          );
        } catch (err: unknown) {
          if (active) {
            setError(
              (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
                "Could not load laboratory equipment."
            );
            setLoading(false);
          }
          return;
        }

        // 4. Load usable standard lots if standard comparison
        const mode = matchedDef?.uvCalculationMode;
        if (mode === "StandardComparison") {
          try {
            const lots = await MaterialService.getUsableReferenceStandards();
            if (active && Array.isArray(lots)) {
              setStandardLots(lots as StandardMaterialOption[]);
              if (lots.length === 1) {
                setStandardMaterialId(lots[0].id);
              }
            }
          } catch (err: unknown) {
            if (active) {
              setError(
                (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
                  "Could not load reference standard lots."
              );
              setLoading(false);
            }
            return;
          }
        }

        // 5. Check moisture requirement
        const hasLod = loadedSpecs.some(
          (s) => s.resultBasis === "PercentDriedBasis" || s.resultBasis === "PercentAnhydrousBasis"
        );
        if (hasLod) {
          setMoistureStatus("loading");
          try {
            const m = await TestWorkflowService.getMoisture(testOrderId);
            if (active) {
              setMoisture(m);
              setMoistureStatus("loaded");
            }
          } catch (err: unknown) {
            if (active) {
              setMoisture(null);
              setMoistureError(
                (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
                  "Could not load the sample moisture result."
              );
              setMoistureStatus("error");
            }
          }
        } else {
          setMoisture(null);
          setMoistureStatus("idle");
        }

        // 6. Initialize parameters and replicates (wavelength starts empty)
        const repCount =
          matchedDef?.replicateCount && matchedDef.replicateCount >= 1 && matchedDef.replicateCount <= 30
            ? matchedDef.replicateCount
            : 1;

        const initParams: Record<number, SpecParamState> = {};
        for (const s of loadedSpecs) {
          initParams[s.id!] = {
            wavelengthNm: "",
            referenceAbsorbance: "",
            replicates: Array.from({ length: repCount }, () => ({
              weightG: "",
              absorbance: ""
            }))
          };
        }

        if (active) {
          setTestDef(matchedDef);
          setSpecs(loadedSpecs);
          setEquipmentList(fpInstruments);
          if (fpInstruments.length === 1) {
            setSelectedEquipmentId(fpInstruments[0].id);
          }
          setParamStates(initParams);
        }
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load UV spectrophotometry configuration or specifications.";
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    };

    loadData();

    return () => {
      active = false;
    };
  }, [effectiveTestCode, testOrderId]);

  const replicateCount = useMemo(() => {
    return testDef?.replicateCount && testDef.replicateCount >= 1 && testDef.replicateCount <= 30
      ? testDef.replicateCount
      : 1;
  }, [testDef?.replicateCount]);

  const mode = testDef?.uvCalculationMode ?? "SpecificAbsorbanceAssay";
  const isLimitMode = mode === "AbsorbanceLimit";
  const isStandardMode = mode === "StandardComparison";
  const isAssayMode = mode === "SpecificAbsorbanceAssay";
  const isValueMode = mode === "SpecificAbsorbanceValue";

  const hasLodRequirement = useMemo(() => {
    return specs.some((s) => s.resultBasis === "PercentDriedBasis" || s.resultBasis === "PercentAnhydrousBasis");
  }, [specs]);

  const handleRetryMoisture = useCallback(async () => {
    setMoistureStatus("loading");
    try {
      const m = await TestWorkflowService.getMoisture(testOrderId);
      setMoisture(m);
      setMoistureStatus("loaded");
    } catch (err: unknown) {
      setMoisture(null);
      setMoistureError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load the sample moisture result."
      );
      setMoistureStatus("error");
    }
  }, [testOrderId]);

  // Handle replicate weight change with prefill logic from row 0
  const handleReplicateWeightChange = (specId: number, repIdx: number, val: string) => {
    setParamStates((prev) => {
      const next = { ...prev };
      const currentParam = next[specId];
      if (!currentParam) return prev;

      const nextReps = [...currentParam.replicates];
      nextReps[repIdx] = { ...nextReps[repIdx], weightG: val };
      next[specId] = { ...currentParam, replicates: nextReps };

      // If editing row 0 (the first spec), prefill other specs that have not been manually touched
      const firstSpecId = specs[0]?.id;
      if (specId === firstSpecId) {
        for (let i = 1; i < specs.length; i++) {
          const otherSpecId = specs[i].id!;
          const touchKey = `${otherSpecId}-${repIdx}`;
          if (!touchedWeights[touchKey]) {
            const otherParam = next[otherSpecId];
            if (otherParam) {
              const otherReps = [...otherParam.replicates];
              otherReps[repIdx] = { ...otherReps[repIdx], weightG: val };
              next[otherSpecId] = { ...otherParam, replicates: otherReps };
            }
          }
        }
      }

      return next;
    });

    // If editing a non-first spec, mark this cell as touched so it won't be overwritten
    const firstSpecId = specs[0]?.id;
    if (specId !== firstSpecId) {
      setTouchedWeights((prev) => ({
        ...prev,
        [`${specId}-${repIdx}`]: true
      }));
    }
  };

  const handleReplicateAbsorbanceChange = (specId: number, repIdx: number, val: string) => {
    setParamStates((prev) => {
      const next = { ...prev };
      const currentParam = next[specId];
      if (!currentParam) return prev;

      const nextReps = [...currentParam.replicates];
      nextReps[repIdx] = { ...nextReps[repIdx], absorbance: val };
      next[specId] = { ...currentParam, replicates: nextReps };
      return next;
    });
  };

  const handleWavelengthChange = (specId: number, val: string) => {
    setParamStates((prev) => ({
      ...prev,
      [specId]: { ...prev[specId], wavelengthNm: val }
    }));
  };

  const handleReferenceAbsorbanceChange = (specId: number, val: string) => {
    setParamStates((prev) => ({
      ...prev,
      [specId]: { ...prev[specId], referenceAbsorbance: val }
    }));
  };

  // Preview computation per replicate
  const computeReplicatePreview = (spec: SpecificationDto, rep: { weightG: string; absorbance: string }): string => {
    const a = Number(rep.absorbance);
    if (isNaN(a) || rep.absorbance.trim() === "") return "—";

    if (isLimitMode) {
      return `${a.toFixed(3)} AU`;
    }

    const w = Number(rep.weightG);
    if (isNaN(w) || rep.weightG.trim() === "" || w <= 0) return "—";
    if (a <= 0) return "—";

    let asIs = 0;
    const v = testDef?.uvTotalVolumeMl ?? 0;

    if (isAssayMode) {
      const a1 = testDef?.uvSpecificAbsorbance ?? 0;
      if (a1 <= 0 || v <= 0) return "—";
      asIs = (a * v) / (a1 * w);
    } else if (isValueMode) {
      if (v <= 0) return "—";
      asIs = (a * v) / (w * 100);
    } else if (isStandardMode) {
      const stdA = Number(standardAbsorbance);
      const stdW = Number(standardWeightG);
      const stdV = testDef?.uvStandardVolumeMl ?? 0;
      if (stdA <= 0 || stdW <= 0 || stdV <= 0 || v <= 0) return "—";
      const lot = standardLots.find((l) => l.id === standardMaterialId);
      const purity = lot?.purity ?? 100;
      const lotMc = lot?.moisturePercent ?? 0;
      asIs = (a / stdA) * (stdW / w) * (v / stdV) * purity * ((100 - lotMc) / 100);
    }

    // A specific absorbance has no unit of its own; the others are percentages.
    const suffix = isValueMode ? (spec.unit?.trim() === "%" ? " %" : "") : " %";

    // Basis conversion
    if (spec.resultBasis === "PercentDriedBasis" || spec.resultBasis === "PercentAnhydrousBasis") {
      if (!moisture) return `${asIs.toFixed(2)}${suffix} (as-is)`;
      const dried = asIs / ((100 - moisture.percent) / 100);
      return `${dried.toFixed(2)}${suffix}`;
    }

    return `${asIs.toFixed(2)}${suffix}`;
  };

  // Validation
  const isFormValid = useMemo(() => {
    if (specs.length === 0) return false;
    if (selectedEquipmentId === "") return false;
    if (!analysedAt.trim()) return false;

    // Check analysis time is not in the future
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }

    // Check moisture requirement
    if (hasLodRequirement && !moisture) {
      return false;
    }

    // Standard comparison fields
    if (isStandardMode) {
      if (standardMaterialId === "") return false;
      const sW = Number(standardWeightG);
      const sA = Number(standardAbsorbance);
      if (isNaN(sW) || sW <= 0) return false;
      if (isNaN(sA) || sA <= 0 || sA > 5) return false;
    }

    // Validate parameters
    for (const s of specs) {
      const p = paramStates[s.id!];
      if (!p) return false;

      const wl = Number(p.wavelengthNm);
      if (isNaN(wl) || wl < 190 || wl > 1100) return false;

      if (isLimitMode) {
        const refA = Number(p.referenceAbsorbance);
        if (isNaN(refA) || refA <= 0 || refA > 5) return false;
      }

      if (p.replicates.length !== replicateCount) return false;

      for (const r of p.replicates) {
        if (r.absorbance.trim() === "") return false;
        const a = Number(r.absorbance);
        if (isLimitMode) {
          if (isNaN(a) || a < 0 || a > 5) return false;
        } else {
          if (isNaN(a) || a <= 0 || a > 5) return false;
          const w = Number(r.weightG);
          if (isNaN(w) || w <= 0) return false;
        }
      }
    }

    return true;
  }, [
    specs,
    selectedEquipmentId,
    analysedAt,
    hasLodRequirement,
    moisture,
    isStandardMode,
    standardMaterialId,
    standardWeightG,
    standardAbsorbance,
    paramStates,
    replicateCount,
    isLimitMode
  ]);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload: RecordUvResultRequest = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: Number(selectedEquipmentId),
        standardMaterialId: isStandardMode && standardMaterialId !== "" ? Number(standardMaterialId) : null,
        standardWeightG: isStandardMode && standardWeightG !== "" ? Number(standardWeightG) : null,
        standardAbsorbance: isStandardMode && standardAbsorbance !== "" ? Number(standardAbsorbance) : null,
        parameters: specs.map((s): RecordUvParameterRequest => {
          const p = paramStates[s.id!];
          return {
            specificationId: s.id!,
            wavelengthNm: Number(p.wavelengthNm),
            referenceAbsorbance: isLimitMode && p.referenceAbsorbance !== "" ? Number(p.referenceAbsorbance) : null,
            replicates: (p?.replicates || []).map((r) => ({
              weightG: isLimitMode ? null : Number(r.weightG),
              absorbance: Number(r.absorbance)
            }))
          };
        }),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordUvResult(testOrderId, payload);
      setSigning(false);
      setOutcome({
        outcomeSummary: res?.outcomeSummary ?? res?.finalResult ?? undefined,
        status: res?.status ?? undefined
      });

      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to record UV spectrophotometry result.";
      setError(msg);
    }
  };

  const criteriaRows: CriteriaRow[] = specs.map((s) => ({
    parameter: s.parameterName || s.testCode,
    criterion:
      s.specLimit ||
      s.expectedResultText ||
      (s.lowerLimit != null || s.upperLimit != null ? `${s.lowerLimit ?? ""} \u2013 ${s.upperLimit ?? ""}` : "\u2014"),
    unit: s.unit || (isValueMode ? "" : "%"),
    source: "Specification"
  }));

  // Completion view
  if (showCompleted) {
    const serverStatus = outcome?.status ?? "";
    const headline = `${displayName}: ${outcome?.outcomeSummary ?? current.finalResult ?? "Results Recorded"}`;
    const verdict: Verdict | null = !serverStatus
      ? null
      : serverStatus.includes("OutOfSpecification")
      ? "Fail"
      : serverStatus.includes("RequiresReview")
      ? "Pending"
      : serverStatus.includes("WithinLimits")
      ? "Pass"
      : "Pending";

    return (
      <Stack spacing={2}>
        {verdict ? (
          <VerdictBanner verdict={verdict} detail={`${headline} (${serverStatus})`} />
        ) : (
          <Typography sx={{ fontWeight: 600 }}>{headline}</Typography>
        )}

        {summaryError && (
          <Alert severity="error">{summaryError}</Alert>
        )}

        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        {recorded.map((p) => (
          <UvReviewPanel key={p.id} parameter={p} testOrderId={testOrderId} />
        ))}
      </Stack>
    );
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress size={32} />
      </Box>
    );
  }

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
        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        {/* Section 1: Analysis Configuration */}
        <ResultSection
          step={1}
          title="Analysis configuration"
          actions={
            <Stack direction="row" spacing={1}>
              <Chip size="small" variant="outlined" label={testDef?.uvCalculationMode ?? "UV"} />
              <Chip size="small" variant="outlined" label={`Replicates: ${replicateCount}`} />
              {testDef?.uvTotalVolumeMl != null && (
                <Chip size="small" variant="outlined" label={`Volume: ${testDef.uvTotalVolumeMl} mL`} />
              )}
            </Stack>
          }
        >
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="datetime-local"
              label="Analysis time"
              value={analysedAt}
              onChange={(e) => setAnalysedAt(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              required
              fullWidth
            />

            <FormControl size="small" fullWidth required>
              <InputLabel id="uv-equipment-select-label">Equipment</InputLabel>
              <Select
                labelId="uv-equipment-select-label"
                label="Equipment"
                value={selectedEquipmentId}
                onChange={(e) => setSelectedEquipmentId(String(e.target.value) === "" ? "" : Number(e.target.value))}
              >
                <MenuItem value="">
                  <em>Select spectrophotometer</em>
                </MenuItem>
                {equipmentList.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    {eq.name} ({eq.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Box>

          {/* Standard Comparison Block */}
          {isStandardMode && (
            <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                Reference Standard Preparation
              </Typography>
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
                <FormControl size="small" fullWidth required>
                  <InputLabel id="standard-lot-label">Standard lot</InputLabel>
                  <Select
                    labelId="standard-lot-label"
                    label="Standard lot"
                    value={standardMaterialId}
                    onChange={(e) => setStandardMaterialId(String(e.target.value) === "" ? "" : Number(e.target.value))}
                  >
                    <MenuItem value="">
                      <em>Select standard lot</em>
                    </MenuItem>
                    {standardLots.map((lot) => (
                      <MenuItem key={lot.id} value={lot.id}>
                        {lot.materialName} ({lot.lotLabel ?? lot.batchNumber})
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <TextField
                  size="small"
                  label="Standard weight (g) *"
                  type="number"
                  value={standardWeightG}
                  onChange={(e) => setStandardWeightG(e.target.value)}
                  slotProps={{ htmlInput: { step: "any", min: "0" } }}
                  required
                  fullWidth
                />

                <TextField
                  size="small"
                  label="Standard absorbance *"
                  type="number"
                  value={standardAbsorbance}
                  onChange={(e) => setStandardAbsorbance(e.target.value)}
                  slotProps={{ htmlInput: { step: "any", min: "0", max: "5" } }}
                  required
                  fullWidth
                />
              </Box>
            </Box>
          )}

          {/* Moisture result / missing message */}
          {hasLodRequirement && (
            <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
              {moistureStatus === "loading" ? (
                <Typography sx={{ fontSize: 13, color: "text.secondary", py: 0.5 }}>
                  Loading moisture result...
                </Typography>
              ) : moistureStatus === "error" ? (
                <Alert
                  severity="error"
                  action={
                    <Button color="inherit" size="small" onClick={handleRetryMoisture}>
                      Retry
                    </Button>
                  }
                  sx={{ width: "100%", fontSize: 13, py: 0.5 }}
                >
                  {moistureError || "Could not load moisture result."}
                </Alert>
              ) : moisture ? (
                <Typography sx={{ fontSize: 13, color: "text.primary", py: 0.5, fontWeight: 500 }}>
                  {formatMoistureLabel(moisture)}
                </Typography>
              ) : (
                <Alert severity="warning" sx={{ width: "100%", fontSize: 13, py: 0.5 }}>
                  {getMissingMoistureMessage(specs)}
                </Alert>
              )}
            </Box>
          )}
        </ResultSection>

        {/* Section 2+: Parameters and Replicates */}
        {specs.map((s, specIdx) => {
          const p = paramStates[s.id!] ?? {
            wavelengthNm: "",
            referenceAbsorbance: "",
            replicates: []
          };

          return (
            <ResultSection
              key={s.id}
              step={specIdx + 2}
              title={`Readings · ${s.parameterName || s.testCode}`}
            >
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: isLimitMode ? "1fr 1fr" : "1fr" }, gap: 2, mb: 2 }}>
                <TextField
                  size="small"
                  label="Wavelength (nm) *"
                  type="number"
                  value={p.wavelengthNm}
                  onChange={(e) => handleWavelengthChange(s.id!, e.target.value)}
                  helperText="Reading wavelength (190 - 1100 nm)"
                  slotProps={{ htmlInput: { min: "190", max: "1100", step: "1" } }}
                  required
                  fullWidth
                />

                {isLimitMode && (
                  <TextField
                    size="small"
                    label="Reference absorbance *"
                    type="number"
                    value={p.referenceAbsorbance}
                    onChange={(e) => handleReferenceAbsorbanceChange(s.id!, e.target.value)}
                    helperText="Absorbance of reference solution (max 5)"
                    slotProps={{ htmlInput: { min: "0", max: "5", step: "any" } }}
                    required
                    fullWidth
                  />
                )}
              </Box>

              <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
                <Table size="small">
                  <TableHead sx={tableHeadSx(theme)}>
                    <TableRow>
                      <TableCell sx={{ fontSize: 12 }}>Replicate</TableCell>
                      {!isLimitMode && (
                        <TableCell sx={{ fontSize: 12 }}>Sample weight (g)</TableCell>
                      )}
                      <TableCell sx={{ fontSize: 12 }}>Absorbance</TableCell>
                      <TableCell sx={{ fontSize: 12 }}>Preview</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {p.replicates.map((r, rIdx) => {
                      const previewStr = computeReplicatePreview(s, r);
                      return (
                        <TableRow key={rIdx} hover>
                          <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                            Rep {rIdx + 1}
                          </TableCell>
                          {!isLimitMode && (
                            <TableCell sx={{ py: 0.5 }}>
                              <TextField
                                size="small"
                                label={`Sample weight ${rIdx + 1}`}
                                type="number"
                                value={r.weightG}
                                onChange={(e) => handleReplicateWeightChange(s.id!, rIdx, e.target.value)}
                                slotProps={{ htmlInput: { min: "0", step: "any" } }}
                                required
                                sx={{ maxWidth: 180 }}
                              />
                            </TableCell>
                          )}
                          <TableCell sx={{ py: 0.5 }}>
                            <TextField
                              size="small"
                              label={`Absorbance ${rIdx + 1}`}
                              type="number"
                              value={r.absorbance}
                              onChange={(e) => handleReplicateAbsorbanceChange(s.id!, rIdx, e.target.value)}
                              slotProps={{ htmlInput: { min: isLimitMode ? "0" : "0.001", max: "5", step: "any" } }}
                              required
                              sx={{ maxWidth: 180 }}
                            />
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                            {previewStr}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            </ResultSection>
          );
        })}

        {/* Section: Comment */}
        <TextField
          size="small"
          label="Comment (optional)"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          rows={2}
          fullWidth
        />

        {/* Section: Action button */}
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            disabled={!isFormValid}
            onClick={() => setSigning(true)}
            sx={{ minWidth: 140, fontWeight: 700, textTransform: "none" }}
          >
            Sign and record result
          </Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these spectrophotometric measurements and am recording this test result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
