import React, { useState, useEffect, useMemo } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Button,
  IconButton,
  Alert,
  CircularProgress,
  Stack,
  FormControlLabel,
  Checkbox
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { Item } from "../services/ItemService";
import {
  SpecificationService,
  SpecificationDto,
  LimitType,
  ToleranceMode,
  ExpectedPresence,
  ResultBasis,
  SampleMatrix,
  CreateSpecificationPayload,
  UpdateSpecificationPayload
} from "../../specifications/services/SpecificationService";
import { masterDataOptions, TestAnalyteDto } from "../../../../services/masterDataOptions";

export interface TestDefinitionSummary {
  id: number;
  code: string;
  displayName: string;
  workflowType: string;
  equationType?: string;
}

interface SpecificationParameterDialogProps {
  open: boolean;
  item: Item;
  editingSpec?: SpecificationDto | null;
  preselectedTestCode?: string | null;
  workflowTypeByCode: Record<string, string>;
  testDefinitionByCode?: Record<string, TestDefinitionSummary>;
  existingSpecs: SpecificationDto[];
  onClose: () => void;
  onSuccess: () => void;
}

const LIMIT_TYPE_OPTIONS: { value: LimitType; label: string }[] = [
  { value: "Range", label: "Range (NLT \u2014 NMT)" },
  { value: "NotMoreThan", label: "NMT (Not More Than)" },
  { value: "NotLessThan", label: "NLT (Not Less Than)" },
  { value: "TargetWithTolerance", label: "Target \u00B1 Tolerance" },
  { value: "CountTiered", label: "Count-Tiered (Alert / Action / Spec)" },
  { value: "Qualitative", label: "Qualitative (descriptive text)" },
  { value: "PresenceAbsence", label: "Presence / Absence" },
  { value: "MultiStage", label: "Multi-Stage Criteria" },
  { value: "DissolutionQ", label: "Dissolution Q" }
];

export const getDefaultLimitType = (workflowType?: string): LimitType => {
  if (workflowType === "CountTest") return "CountTiered";
  if (workflowType === "Observation") return "PresenceAbsence";
  if (workflowType === "Dissolution") return "DissolutionQ";
  return "Range";
};

export const formatTrimmedDecimal = (val: number | string | null | undefined): string => {
  if (val == null || val === "") return "";
  const n = typeof val === "number" ? val : Number(val);
  if (Number.isNaN(n)) return String(val);
  return n.toString();
};

export const SpecificationParameterDialog: React.FC<SpecificationParameterDialogProps> = ({
  open,
  item,
  editingSpec,
  preselectedTestCode,
  workflowTypeByCode,
  testDefinitionByCode,
  existingSpecs,
  onClose,
  onSuccess
}) => {
  const isEditing = Boolean(editingSpec && editingSpec.id != null);
  const assignedTests = useMemo(() => item.assignedTests ?? [], [item.assignedTests]);

  // Form states
  const [testCode, setTestCode] = useState("");
  const [parameterName, setParameterName] = useState("");
  const [limitType, setLimitType] = useState<LimitType>("Range");
  const [referenceStandard, setReferenceStandard] = useState("");
  const [unit, setUnit] = useState("");
  const [dilutionFactor, setDilutionFactor] = useState("");

  // CalibrationCurve states
  const [testDefs, setTestDefs] = useState<Record<string, TestDefinitionSummary>>(testDefinitionByCode || {});
  const [testAnalyteId, setTestAnalyteId] = useState<number | "">("");
  const [resultBasis, setResultBasis] = useState<ResultBasis | "">("MgPerKg");
  const [sampleMatrix, setSampleMatrix] = useState<SampleMatrix | "">("Solid");
  const [labelClaim, setLabelClaim] = useState("");
  const [labelClaimUnit, setLabelClaimUnit] = useState("");
  const [conversionFactor, setConversionFactor] = useState("1");
  const [analytes, setAnalytes] = useState<TestAnalyteDto[]>([]);
  const [loadingAnalytes, setLoadingAnalytes] = useState(false);

  // Range
  const [lowerLimit, setLowerLimit] = useState("");
  const [upperLimit, setUpperLimit] = useState("");
  const [lowerInclusive, setLowerInclusive] = useState(true);
  const [upperInclusive, setUpperInclusive] = useState(true);

  // TargetWithTolerance
  const [target, setTarget] = useState("");
  const [tolerance, setTolerance] = useState("");
  const [toleranceMode, setToleranceMode] = useState<ToleranceMode>("Absolute");

  // CountTiered
  const [alertLimit, setAlertLimit] = useState("");
  const [actionLimit, setActionLimit] = useState("");
  const [specLimit, setSpecLimit] = useState("");

  // Qualitative
  const [expectedResultText, setExpectedResultText] = useState("");

  // PresenceAbsence
  const [expectedState, setExpectedState] = useState<ExpectedPresence>("Absence");
  const [sampleQuantity, setSampleQuantity] = useState("");
  const [sampleQuantityUnit, setSampleQuantityUnit] = useState("");

  // MultiStage
  const [stages, setStages] = useState<
    Array<{ stageNumber: number; stageLabel: string; acceptanceCriteriaText: string }>
  >([]);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (testDefinitionByCode && Object.keys(testDefinitionByCode).length > 0) {
      setTestDefs(testDefinitionByCode);
    } else {
      masterDataOptions
        .getTestDefinitions()
        .then((defs: any[]) => {
          setTestDefs(Object.fromEntries(defs.map((d) => [d.code, d])));
        })
        .catch(() => {});
    }
  }, [testDefinitionByCode]);

  const currentTestDef = testDefs[testCode];
  const isCalibrationCurve = currentTestDef?.equationType === "CalibrationCurve";
  const isDissolution =
    workflowTypeByCode[testCode] === "Dissolution" ||
    currentTestDef?.workflowType === "Dissolution" ||
    limitType === "DissolutionQ";

  useEffect(() => {
    if (!isCalibrationCurve || !currentTestDef?.id) {
      setAnalytes([]);
      return;
    }
    setLoadingAnalytes(true);
    masterDataOptions
      .getTestAnalytes(currentTestDef.id)
      .then((res) => {
        setAnalytes(res.filter((a) => a.isActive !== false));
      })
      .catch(() => {
        setAnalytes([]);
      })
      .finally(() => {
        setLoadingAnalytes(false);
      });
  }, [isCalibrationCurve, currentTestDef?.id]);

  const getTestDisplayName = (code: string) => {
    const match = assignedTests.find((t) => t.testCode === code);
    return match?.displayName && match.displayName !== code
      ? `${match.displayName} (${code})`
      : match?.displayName || code;
  };

  useEffect(() => {
    if (!open) return;

    setError(null);
    setSaving(false);

    if (editingSpec) {
      setTestCode(editingSpec.testCode);
      setParameterName(editingSpec.parameterName ?? "");
      setLimitType((editingSpec.limitType as LimitType) || "CountTiered");
      setReferenceStandard(editingSpec.referenceStandard ?? "");
      setUnit(editingSpec.unit ?? "");
      setDilutionFactor(
        editingSpec.dilutionFactor != null ? String(editingSpec.dilutionFactor) : ""
      );

      setTestAnalyteId(editingSpec.testAnalyteId ?? "");
      setResultBasis((editingSpec.resultBasis as ResultBasis) || "MgPerKg");
      setSampleMatrix((editingSpec.sampleMatrix as SampleMatrix) || "Solid");
      setLabelClaim(formatTrimmedDecimal(editingSpec.labelClaim));
      setLabelClaimUnit(editingSpec.labelClaimUnit ?? "");
      setConversionFactor(
        editingSpec.conversionFactor != null ? String(editingSpec.conversionFactor) : "1"
      );

      setLowerLimit(formatTrimmedDecimal(editingSpec.lowerLimit));
      setUpperLimit(formatTrimmedDecimal(editingSpec.upperLimit));
      setLowerInclusive(editingSpec.lowerInclusive ?? true);
      setUpperInclusive(editingSpec.upperInclusive ?? true);

      setTarget(formatTrimmedDecimal(editingSpec.target));
      setTolerance(formatTrimmedDecimal(editingSpec.tolerance));
      setToleranceMode((editingSpec.toleranceMode as ToleranceMode) || "Absolute");

      setAlertLimit(editingSpec.alertLimit ?? "");
      setActionLimit(editingSpec.actionLimit ?? "");
      setSpecLimit(editingSpec.specLimit ?? "");

      setExpectedResultText(editingSpec.expectedResultText ?? "");

      setExpectedState((editingSpec.expectedState as ExpectedPresence) || "Absence");
      setSampleQuantity(formatTrimmedDecimal(editingSpec.sampleQuantity));
      setSampleQuantityUnit(editingSpec.sampleQuantityUnit ?? "");

      if (editingSpec.stages && editingSpec.stages.length > 0) {
        setStages(
          editingSpec.stages.map((s) => ({
            stageNumber: s.stageNumber,
            stageLabel: s.stageLabel,
            acceptanceCriteriaText: s.acceptanceCriteriaText
          }))
        );
      } else {
        setStages([{ stageNumber: 1, stageLabel: "Stage 1", acceptanceCriteriaText: "" }]);
      }
    } else {
      const initialCode = preselectedTestCode || assignedTests[0]?.testCode || "";
      setTestCode(initialCode);

      const match = assignedTests.find((t) => t.testCode === initialCode);
      const initialParam = match?.displayName || initialCode;
      setParameterName(initialParam);

      const def = testDefs[initialCode];
      const isCal = def?.equationType === "CalibrationCurve";
      const defaultType = isCal ? "Range" : getDefaultLimitType(workflowTypeByCode[initialCode]);
      setLimitType(defaultType);

      setReferenceStandard("");
      setUnit("");
      setDilutionFactor("");

      setTestAnalyteId("");
      setResultBasis("MgPerKg");
      const existingMatrix = existingSpecs.find((s) => s.sampleMatrix)?.sampleMatrix as SampleMatrix | undefined;
      setSampleMatrix(existingMatrix || "Solid");
      setLabelClaim("");
      setLabelClaimUnit("");
      setConversionFactor("1");

      setLowerLimit("");
      setUpperLimit("");
      setLowerInclusive(true);
      setUpperInclusive(true);

      setTarget("");
      setTolerance("");
      setToleranceMode("Absolute");

      setAlertLimit("");
      setActionLimit("");
      setSpecLimit("");

      setExpectedResultText("");

      setExpectedState("Absence");
      setSampleQuantity("");
      setSampleQuantityUnit("");

      setStages([
        { stageNumber: 1, stageLabel: "Stage 1 (S1, n=6)", acceptanceCriteriaText: "" },
        { stageNumber: 2, stageLabel: "Stage 2 (S1+S2, n=12)", acceptanceCriteriaText: "" },
        { stageNumber: 3, stageLabel: "Stage 3 (S1+S2+S3, n=24)", acceptanceCriteriaText: "" }
      ]);
    }
  }, [open, editingSpec, preselectedTestCode, item, workflowTypeByCode, assignedTests, testDefs, existingSpecs]);

  const handleTestChange = (newCode: string) => {
    setTestCode(newCode);
    const match = assignedTests.find((t) => t.testCode === newCode);
    setParameterName(match?.displayName || newCode);

    const def = testDefs[newCode];
    const isCal = def?.equationType === "CalibrationCurve";
    const isDis = workflowTypeByCode[newCode] === "Dissolution" || def?.workflowType === "Dissolution";

    if (isCal) {
      setLimitType("Range");
      setTestAnalyteId("");
      setDilutionFactor("");
    } else if (isDis) {
      setLimitType("DissolutionQ");
      setDilutionFactor("");
    } else {
      const defType = getDefaultLimitType(workflowTypeByCode[newCode]);
      setLimitType(defType);
      if (defType !== "CountTiered") {
        setDilutionFactor("");
      }
    }
  };

  const handleAnalyteChange = (analyteId: number) => {
    setTestAnalyteId(analyteId);
    const chosen = analytes.find((a) => a.id === analyteId);
    if (chosen) {
      const prevMatchesAnalyte = analytes.some((a) => a.element === parameterName);
      const prevMatchesTest = assignedTests.some((t) => t.displayName === parameterName || t.testCode === parameterName);
      if (!parameterName.trim() || prevMatchesAnalyte || prevMatchesTest) {
        setParameterName(chosen.element);
      }
    }
  };

  const handleLimitTypeChange = (newType: LimitType) => {
    setLimitType(newType);
    if (newType !== "CountTiered") {
      setDilutionFactor("");
    }
    if (newType === "MultiStage" && stages.length === 0) {
      setStages([{ stageNumber: 1, stageLabel: "Stage 1", acceptanceCriteriaText: "" }]);
    }
  };

  const handleAddStage = () => {
    setStages((prev) => [
      ...prev,
      {
        stageNumber: prev.length + 1,
        stageLabel: `Stage ${prev.length + 1}`,
        acceptanceCriteriaText: ""
      }
    ]);
  };

  const handleRemoveStage = (index: number) => {
    setStages((prev) => {
      const next = prev.filter((_, idx) => idx !== index);
      return next.map((s, idx) => ({ ...s, stageNumber: idx + 1 }));
    });
  };

  const handleStageChange = (
    index: number,
    field: "stageLabel" | "acceptanceCriteriaText",
    val: string
  ) => {
    setStages((prev) =>
      prev.map((s, idx) => (idx === index ? { ...s, [field]: val } : s))
    );
  };

  const handleSave = async () => {
    if (!testCode) {
      setError("Assigned Test is required.");
      return;
    }

    if (isCalibrationCurve) {
      if (!testAnalyteId) {
        setError("Please select an element analyte.");
        return;
      }
      if (!resultBasis) {
        setError("Please select a result basis.");
        return;
      }
      if (!sampleMatrix) {
        setError("Please select a sample matrix.");
        return;
      }
      if (resultBasis === "PercentLabelClaim") {
        const lcNum = Number(labelClaim);
        if (!labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
          setError("Label claim must be greater than 0 when result basis is % label claim.");
          return;
        }
      }
      const cfNum = conversionFactor.trim() !== "" ? Number(conversionFactor) : 1;
      if (isNaN(cfNum) || cfNum <= 0) {
        setError("Conversion factor must be greater than 0.");
        return;
      }
    }

    if (limitType === "DissolutionQ") {
      const qNum = Number(lowerLimit);
      if (!lowerLimit.trim() || isNaN(qNum) || qNum <= 0 || qNum > 100) {
        setError("Lower limit Q (%) must be between 0 and 100 (exclusive of 0, inclusive of 100).");
        return;
      }
      const lcNum = Number(labelClaim);
      if (!labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
        setError("Label claim must be greater than zero for Dissolution Q specifications.");
        return;
      }
      const otherDissolutionSpec = existingSpecs.find(
        (s) => s.testCode === testCode && s.id !== editingSpec?.id && s.limitType === "DissolutionQ"
      );
      if (otherDissolutionSpec) {
        setError("Only one DissolutionQ specification is allowed per test.");
        return;
      }
    }

    setSaving(true);
    setError(null);

    const testSpecs = existingSpecs.filter((s) => s.testCode === testCode);
    const maxOrder = testSpecs.reduce((max, s) => Math.max(max, s.displayOrder ?? 0), -1);
    const displayOrder = editingSpec?.displayOrder ?? maxOrder + 1;

    const basePayload = {
      testCode,
      parameterName: parameterName.trim() || undefined,
      displayOrder,
      limitType,
      referenceStandard: referenceStandard.trim() || null,
      unit: unit.trim() || null,
      dilutionFactor:
        !isCalibrationCurve && limitType === "CountTiered" && dilutionFactor.trim() !== ""
          ? Number(dilutionFactor)
          : null,
      lowerLimit:
        limitType === "DissolutionQ"
          ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
          : (limitType === "Range" || limitType === "NotLessThan"
            ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
            : null),
      upperLimit:
        limitType === "Range" || limitType === "NotMoreThan"
          ? upperLimit.trim() !== ""
            ? Number(upperLimit)
            : null
          : null,
      lowerInclusive: limitType === "Range" ? lowerInclusive : undefined,
      upperInclusive: limitType === "Range" ? upperInclusive : undefined,
      target:
        limitType === "TargetWithTolerance" && target.trim() !== ""
          ? Number(target)
          : null,
      tolerance:
        limitType === "TargetWithTolerance" && tolerance.trim() !== ""
          ? Number(tolerance)
          : null,
      toleranceMode: limitType === "TargetWithTolerance" ? toleranceMode : null,
      expectedResultText:
        limitType === "Qualitative" ? expectedResultText.trim() : null,
      expectedState: limitType === "PresenceAbsence" ? expectedState : null,
      sampleQuantity:
        limitType === "PresenceAbsence" && sampleQuantity.trim() !== ""
          ? Number(sampleQuantity)
          : null,
      sampleQuantityUnit:
        limitType === "PresenceAbsence" ? sampleQuantityUnit.trim() || null : null,
      alertLimit: limitType === "CountTiered" ? alertLimit.trim() || "" : null,
      actionLimit: limitType === "CountTiered" ? actionLimit.trim() || "" : null,
      specLimit:
        limitType === "DissolutionQ"
          ? (lowerLimit.trim() !== "" ? `Q = ${lowerLimit.trim()} %` : null)
          : (limitType === "CountTiered" ? specLimit.trim() : null),
      stages:
        limitType === "MultiStage"
          ? stages.map((s, idx) => ({
              stageNumber: s.stageNumber || idx + 1,
              stageLabel: s.stageLabel.trim(),
              acceptanceCriteriaText: s.acceptanceCriteriaText.trim()
            }))
          : undefined,
      testAnalyteId: isCalibrationCurve && testAnalyteId !== "" ? Number(testAnalyteId) : null,
      resultBasis: isCalibrationCurve && resultBasis ? (resultBasis as ResultBasis) : null,
      sampleMatrix: isCalibrationCurve && sampleMatrix ? (sampleMatrix as SampleMatrix) : null,
      labelClaim:
        limitType === "DissolutionQ"
          ? (labelClaim.trim() !== "" ? Number(labelClaim) : null)
          : (isCalibrationCurve && labelClaim.trim() !== "" ? Number(labelClaim) : null),
      labelClaimUnit:
        limitType === "DissolutionQ"
          ? "mg"
          : (isCalibrationCurve ? labelClaimUnit.trim() || null : null),
      conversionFactor: isCalibrationCurve ? (conversionFactor.trim() !== "" ? Number(conversionFactor) : 1) : 1
    };

    try {
      if (isEditing && editingSpec?.id != null) {
        const updatePayload: UpdateSpecificationPayload = basePayload;
        await SpecificationService.update(editingSpec.id, updatePayload);
      } else {
        const createPayload: CreateSpecificationPayload = {
          itemId: item.id,
          ...basePayload
        };
        await SpecificationService.create(createPayload);
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const axiosError = err as {
        response?: { data?: { message?: string } };
        message?: string;
      };
      const msg =
        axiosError?.response?.data?.message ||
        axiosError?.message ||
        "Failed to save specification.";
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  const isTestDisabled = isEditing || Boolean(preselectedTestCode);

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between" }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
              {isEditing ? "Edit Specification Parameter" : "Add Specification Parameter"}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
              {item.name} &middot; {item.code}
            </Typography>
          </Box>
          <IconButton size="small" onClick={onClose} disabled={saving} aria-label="close">
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Stack spacing={2.5}>
          {/* Row 1: Assigned Test & Parameter Name */}
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <FormControl size="small" fullWidth disabled={isTestDisabled}>
              <InputLabel id="assigned-test-label">Assigned Test *</InputLabel>
              <Select
                labelId="assigned-test-label"
                label="Assigned Test *"
                value={testCode}
                onChange={(e) => handleTestChange(e.target.value)}
              >
                {assignedTests.map((t) => (
                  <MenuItem key={t.testCode} value={t.testCode}>
                    {getTestDisplayName(t.testCode)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              size="small"
              label="Parameter Name *"
              value={parameterName}
              onChange={(e) => setParameterName(e.target.value)}
              fullWidth
              placeholder="e.g. Assay (HPLC) or Impurity A"
            />
          </Box>

          {/* Calibration Curve Parameters Block */}
          {isCalibrationCurve && (
            <Box
              sx={{
                border: "1px solid",
                borderColor: "primary.main",
                borderRadius: 1,
                p: 2,
                bgcolor: "action.hover"
              }}
            >
              <Typography
                variant="caption"
                sx={{
                  fontWeight: 700,
                  letterSpacing: "0.5px",
                  color: "primary.main",
                  textTransform: "uppercase",
                  display: "block",
                  mb: 1.5
                }}
              >
                Calibration Curve Specifications (ICP-OES)
              </Typography>

              <Stack spacing={2}>
                <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
                  <FormControl size="small" fullWidth required>
                    <InputLabel id="element-analyte-label">Element *</InputLabel>
                    <Select
                      labelId="element-analyte-label"
                      label="Element *"
                      value={testAnalyteId}
                      onChange={(e) => handleAnalyteChange(Number(e.target.value))}
                      disabled={loadingAnalytes}
                    >
                      {loadingAnalytes ? (
                        <MenuItem disabled value=""><em>Loading analytes...</em></MenuItem>
                      ) : analytes.length === 0 ? (
                        <MenuItem disabled value=""><em>No active analytes configured for this test</em></MenuItem>
                      ) : (
                        analytes.map((a) => (
                          <MenuItem key={a.id} value={a.id}>
                            {a.element} ({a.wavelengthNm} nm &middot; {a.view})
                          </MenuItem>
                        ))
                      )}
                    </Select>
                  </FormControl>

                  <FormControl size="small" fullWidth required>
                    <InputLabel id="result-basis-label">Result Basis *</InputLabel>
                    <Select
                      labelId="result-basis-label"
                      label="Result Basis *"
                      value={resultBasis}
                      onChange={(e) => setResultBasis(e.target.value as ResultBasis)}
                    >
                      <MenuItem value="MgPerKg">mg/kg or mg/L per sample</MenuItem>
                      <MenuItem value="MgPerUnit">mg per unit</MenuItem>
                      <MenuItem value="PercentLabelClaim">% label claim</MenuItem>
                    </Select>
                  </FormControl>

                  <FormControl size="small" fullWidth required>
                    <InputLabel id="sample-matrix-label">Sample Matrix *</InputLabel>
                    <Select
                      labelId="sample-matrix-label"
                      label="Sample Matrix *"
                      value={sampleMatrix}
                      onChange={(e) => setSampleMatrix(e.target.value as SampleMatrix)}
                    >
                      <MenuItem value="Solid">Solid (ppm is mg/kg)</MenuItem>
                      <MenuItem value="Liquid">Liquid (ppm is mg/L)</MenuItem>
                    </Select>
                  </FormControl>
                </Box>

                <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
                  <TextField
                    size="small"
                    label={resultBasis === "PercentLabelClaim" ? "Label Claim *" : "Label Claim"}
                    type="number"
                    value={labelClaim}
                    onChange={(e) => setLabelClaim(e.target.value)}
                    required={resultBasis === "PercentLabelClaim"}
                    helperText={resultBasis === "PercentLabelClaim" ? "Required for % label claim" : "Optional"}
                    slotProps={{ htmlInput: { step: "any", min: "0" } }}
                    fullWidth
                  />

                  <TextField
                    size="small"
                    label="Label Claim Unit"
                    value={labelClaimUnit}
                    onChange={(e) => setLabelClaimUnit(e.target.value)}
                    placeholder="e.g. mg"
                    fullWidth
                  />

                  <TextField
                    size="small"
                    label="Conversion Factor *"
                    type="number"
                    value={conversionFactor}
                    onChange={(e) => setConversionFactor(e.target.value)}
                    placeholder="1"
                    helperText="Multiplier to claim (default 1)"
                    slotProps={{ htmlInput: { step: "any", min: "0.000001" } }}
                    fullWidth
                  />
                </Box>
              </Stack>
            </Box>
          )}

          {/* Row 2: Limit Type */}
          <FormControl size="small" fullWidth>
            <InputLabel id="limit-type-label">Limit Type *</InputLabel>
            <Select
              labelId="limit-type-label"
              label="Limit Type *"
              value={limitType}
              onChange={(e) => handleLimitTypeChange(e.target.value as LimitType)}
            >
              {(isDissolution
                ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "DissolutionQ")
                : isCalibrationCurve
                ? LIMIT_TYPE_OPTIONS.filter((opt) =>
                    ["Range", "NotMoreThan", "NotLessThan", "TargetWithTolerance"].includes(opt.value)
                  )
                : LIMIT_TYPE_OPTIONS.filter((opt) => opt.value !== "DissolutionQ")
              ).map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {/* Row 3: Dashed Limit Definition Box */}
          <Box
            sx={{
              border: "1px dashed",
              borderColor: "divider",
              borderRadius: 1,
              p: 2,
              bgcolor: "action.hover"
            }}
          >
            <Typography
              variant="caption"
              sx={{
                fontWeight: 700,
                letterSpacing: "0.5px",
                color: "text.secondary",
                textTransform: "uppercase",
                display: "block",
                mb: 1.5
              }}
            >
              LIMIT DEFINITION &middot; CHANGES WITH TYPE ABOVE
            </Typography>

            {/* Limit Type: Range */}
            {limitType === "Range" && (
              <Stack spacing={1.5}>
                <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
                  <TextField
                    size="small"
                    label="Lower Limit (NLT) *"
                    type="number"
                    value={lowerLimit}
                    onChange={(e) => setLowerLimit(e.target.value)}
                    slotProps={{ htmlInput: { step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label="Upper Limit (NMT) *"
                    type="number"
                    value={upperLimit}
                    onChange={(e) => setUpperLimit(e.target.value)}
                    slotProps={{ htmlInput: { step: "any" } }}
                    fullWidth
                  />
                </Box>
                <Stack direction="row" spacing={3} sx={{ alignItems: "center", mt: 0.5 }}>
                  <FormControlLabel
                    control={
                      <Checkbox
                        size="small"
                        checked={lowerInclusive}
                        onChange={(e) => setLowerInclusive(e.target.checked)}
                      />
                    }
                    label={<Typography variant="body2">Lower inclusive (&ge;)</Typography>}
                  />
                  <FormControlLabel
                    control={
                      <Checkbox
                        size="small"
                        checked={upperInclusive}
                        onChange={(e) => setUpperInclusive(e.target.checked)}
                      />
                    }
                    label={<Typography variant="body2">Upper inclusive (&le;)</Typography>}
                  />
                </Stack>
              </Stack>
            )}

            {/* Limit Type: NotMoreThan */}
            {limitType === "NotMoreThan" && (
              <Box sx={{ maxWidth: 300 }}>
                <TextField
                  size="small"
                  label="Upper Limit (NMT) *"
                  type="number"
                  value={upperLimit}
                  onChange={(e) => setUpperLimit(e.target.value)}
                  slotProps={{ htmlInput: { step: "any" } }}
                  fullWidth
                />
              </Box>
            )}

            {/* Limit Type: NotLessThan */}
            {limitType === "NotLessThan" && (
              <Box sx={{ maxWidth: 300 }}>
                <TextField
                  size="small"
                  label="Lower Limit (NLT) *"
                  type="number"
                  value={lowerLimit}
                  onChange={(e) => setLowerLimit(e.target.value)}
                  slotProps={{ htmlInput: { step: "any" } }}
                  fullWidth
                />
              </Box>
            )}

            {/* Limit Type: TargetWithTolerance */}
            {limitType === "TargetWithTolerance" && (
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
                  gap: 2
                }}
              >
                <TextField
                  size="small"
                  label="Target *"
                  type="number"
                  value={target}
                  onChange={(e) => setTarget(e.target.value)}
                  slotProps={{ htmlInput: { step: "any" } }}
                  fullWidth
                />
                <TextField
                  size="small"
                  label="Tolerance *"
                  type="number"
                  value={tolerance}
                  onChange={(e) => setTolerance(e.target.value)}
                  slotProps={{ htmlInput: { step: "any", min: "0" } }}
                  fullWidth
                />
                <FormControl size="small" fullWidth>
                  <InputLabel id="tolerance-mode-label">Mode *</InputLabel>
                  <Select
                    labelId="tolerance-mode-label"
                    label="Mode *"
                    value={toleranceMode}
                    onChange={(e) => setToleranceMode(e.target.value as ToleranceMode)}
                  >
                    <MenuItem value="Absolute">Absolute (&plusmn; value)</MenuItem>
                    <MenuItem value="Percent">Percent (&plusmn; %)</MenuItem>
                  </Select>
                </FormControl>
              </Box>
            )}

            {/* Limit Type: CountTiered */}
            {limitType === "CountTiered" && (
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
                  gap: 2
                }}
              >
                <TextField
                  size="small"
                  label="Alert Limit"
                  placeholder="e.g. 100"
                  value={alertLimit}
                  onChange={(e) => setAlertLimit(e.target.value)}
                  fullWidth
                />
                <TextField
                  size="small"
                  label="Action Limit"
                  placeholder="e.g. 500"
                  value={actionLimit}
                  onChange={(e) => setActionLimit(e.target.value)}
                  fullWidth
                />
                <TextField
                  size="small"
                  label="Specification Limit *"
                  placeholder="e.g. 1000"
                  value={specLimit}
                  onChange={(e) => setSpecLimit(e.target.value)}
                  fullWidth
                  required
                />
              </Box>
            )}

            {/* Limit Type: Qualitative */}
            {limitType === "Qualitative" && (
              <TextField
                size="small"
                label="Expected Result Text *"
                multiline
                minRows={2}
                placeholder="e.g. White to off-white, round biconvex effervescent tablets, characteristic citrus odor"
                value={expectedResultText}
                onChange={(e) => setExpectedResultText(e.target.value)}
                fullWidth
              />
            )}

            {/* Limit Type: PresenceAbsence */}
            {limitType === "PresenceAbsence" && (
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
                  gap: 2
                }}
              >
                <FormControl size="small" fullWidth>
                  <InputLabel id="expected-state-label">Expected State *</InputLabel>
                  <Select
                    labelId="expected-state-label"
                    label="Expected State *"
                    value={expectedState}
                    onChange={(e) => setExpectedState(e.target.value as ExpectedPresence)}
                  >
                    <MenuItem value="Absence">Absence (Absent)</MenuItem>
                    <MenuItem value="Presence">Presence (Present)</MenuItem>
                  </Select>
                </FormControl>
                <TextField
                  size="small"
                  label="Sample Quantity"
                  type="number"
                  placeholder="e.g. 1"
                  value={sampleQuantity}
                  onChange={(e) => setSampleQuantity(e.target.value)}
                  slotProps={{ htmlInput: { step: "any" } }}
                  fullWidth
                />
                <TextField
                  size="small"
                  label="Quantity Unit"
                  placeholder="e.g. g or mL"
                  value={sampleQuantityUnit}
                  onChange={(e) => setSampleQuantityUnit(e.target.value)}
                  fullWidth
                />
              </Box>
            )}

            {/* Limit Type: MultiStage */}
            {limitType === "MultiStage" && (
              <Stack spacing={1.5}>
                {stages.map((stage, idx) => (
                  <Stack
                    key={idx}
                    direction="row"
                    spacing={1}
                    sx={{ alignItems: "flex-start" }}
                  >
                    <TextField
                      size="small"
                      label="Stage #"
                      type="number"
                      value={stage.stageNumber}
                      disabled
                      sx={{ width: 75 }}
                    />
                    <TextField
                      size="small"
                      label="Stage Label *"
                      value={stage.stageLabel}
                      placeholder="e.g. Stage 1 (S1, n=6)"
                      onChange={(e) => handleStageChange(idx, "stageLabel", e.target.value)}
                      sx={{ width: { xs: 140, sm: 200 } }}
                    />
                    <TextField
                      size="small"
                      label="Acceptance Criteria Text *"
                      multiline
                      minRows={1}
                      value={stage.acceptanceCriteriaText}
                      placeholder="e.g. Each unit ≥ Q + 5% (≥ 85%)"
                      onChange={(e) =>
                        handleStageChange(idx, "acceptanceCriteriaText", e.target.value)
                      }
                      fullWidth
                    />
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => handleRemoveStage(idx)}
                      disabled={stages.length <= 1}
                      title="Remove Stage"
                      sx={{ mt: 0.5 }}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                ))}
                <Box>
                  <Button
                    size="small"
                    startIcon={<AddIcon />}
                    onClick={handleAddStage}
                    sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                  >
                    + Add Stage
                  </Button>
                </Box>
              </Stack>
            )}

            {/* Limit Type: DissolutionQ */}
            {limitType === "DissolutionQ" && (
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
                  Dissolution Acceptance (USP &lt;711&gt; / EP 2.9.3)
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Q (% dissolved) *"
                    placeholder="e.g. 75"
                    value={lowerLimit}
                    onChange={(e) => setLowerLimit(e.target.value)}
                    slotProps={{ htmlInput: { min: 0.01, max: 100, step: "any" } }}
                    helperText="Stored in lower limit (0 < Q ≤ 100 %)"
                    required
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Label Claim *"
                    placeholder="e.g. 100"
                    value={labelClaim}
                    onChange={(e) => setLabelClaim(e.target.value)}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    helperText="Active substance per dosage unit"
                    required
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    label="Unit"
                    value="mg"
                    disabled
                    helperText="Fixed unit for dissolution claim"
                    sx={{ width: 120 }}
                  />
                </Stack>
                {lowerLimit.trim() !== "" && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Specification limit display: <strong>Q = {lowerLimit.trim()} %</strong>
                  </Typography>
                )}
              </Box>
            )}
          </Box>

          {/* Row 4: Unit & Reference Standard */}
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              label="Unit"
              value={unit}
              onChange={(e) => setUnit(e.target.value)}
              placeholder="e.g. % w/w, CFU/g, pH units"
              fullWidth
            />
            <TextField
              size="small"
              label="Reference Standard"
              value={referenceStandard}
              onChange={(e) => setReferenceStandard(e.target.value)}
              placeholder="e.g. USP <711>, EP 2.2.29, STP-VITC-01"
              fullWidth
            />
          </Box>

          {/* Row 5: Dilution Factor */}
          {!isCalibrationCurve && (
            <TextField
              size="small"
              label="Dilution Factor"
              type="number"
              value={dilutionFactor}
              onChange={(e) => setDilutionFactor(e.target.value)}
              disabled={limitType !== "CountTiered"}
              helperText="DILUTION FACTOR — editable only for Count-Tiered parameters"
              placeholder={limitType === "CountTiered" ? "e.g. 10" : "—"}
              slotProps={{ htmlInput: { step: "1", min: "1" } }}
              fullWidth
            />
          )}
        </Stack>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={saving} color="inherit">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={saving || !testCode}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
          sx={{ minWidth: 100, fontWeight: 700, textTransform: "none" }}
        >
          {isEditing ? "Save Changes" : "Add Parameter"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
