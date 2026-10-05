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
  CreateSpecificationPayload,
  UpdateSpecificationPayload,
  DosageForm,
  ProductionStageRole,
  PRODUCTION_STAGE_ROLE_OPTIONS
} from "../../specifications/services/SpecificationService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { TitrationSpecBasisFields, titrationBasisNeedsLabelClaim, validateTitrationSpecBasis } from "./TitrationSpecBasisFields";
import { ResidualSolventSpecFields } from "./ResidualSolventSpecFields";
import { IcpSpecFields, icpParameterName } from "./IcpSpecFields";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";
import { HplcMethodService, HplcMethodAnalyteResponse, HplcResultMode } from "../../masterDataSimple/services/HplcMethodService";
import { IcpMethodService, IcpMethodResponse } from "../../masterDataSimple/services/IcpMethodService";
import { PhyschemArea, areaIncludes } from "../../masterDataSimple/testMasterArea";

export interface TestDefinitionSummary {
  id: number;
  code: string;
  displayName: string;
  workflowType: string;
  equationType?: string;
  sectionId?: number | null;
  hplcMethodId?: number | null;
  icpMethodId?: number | null;
  physchemArea?: PhyschemArea | null;
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
  { value: "DissolutionQ", label: "Dissolution Q" },
  { value: "DisintegrationTime", label: "Disintegration Time" },
  { value: "WeightVariation", label: "Weight Variation (USP \u003C2091\u003E)" }
];

export const getDefaultLimitType = (workflowType?: string): LimitType => {
  if (workflowType === "CountTest") return "CountTiered";
  if (workflowType === "Observation") return "PresenceAbsence";
  if (workflowType === "Dissolution") return "DissolutionQ";
  if (workflowType === "Disintegration") return "DisintegrationTime";
  if (workflowType === "WeightVariation") return "WeightVariation";
  return "Range";
};

const hplcParameterName = (analyteName: string, basis: ResultBasis | ""): string =>
  basis === "MgPerUnit" ? `${analyteName} (amount per unit)` : analyteName;

export const formatTrimmedDecimal =(val: number | string | null | undefined): string => {
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

  // Assigned Test picker for a brand-new specification only offers the
  // caller's own lab's tests (design.md §6 - a specification row is owned
  // by its TestCode's Test Master section). Editing or adding a parameter
  // to an already-picked test keeps the field disabled/preselected below,
  // so this filter only ever narrows the open "Add Specification
  // Parameter" flow.
  const { codes: myLabCodes } = useMyLabs();
  const { sections } = useLaboratorySections();
  const myLabSectionIds = useMemo(
    () => new Set(sections.filter((s) => myLabCodes.includes(s.sectionCode)).map((s) => s.sectionId)),
    [sections, myLabCodes]
  );
  const isTestDisabled = isEditing || Boolean(preselectedTestCode);

  // Form states
  const [testCode, setTestCode] = useState("");
  const [parameterName, setParameterName] = useState("");
  const [limitType, setLimitType] = useState<LimitType>("Range");
  const [referenceStandard, setReferenceStandard] = useState("");
  const [unit, setUnit] = useState("");
  const [dilutionFactor, setDilutionFactor] = useState("");
  const [dosageForm, setDosageForm] = useState<DosageForm | "">("");
  // "" = all stages. Production stages exist only on Finished Product
  // samples, so the field is offered only for Finished Product items.
  const [productionStageRole, setProductionStageRole] = useState<ProductionStageRole | "">("");
  const supportsProductionStage = item.category === "FinishedProduct";

  const [testDefs, setTestDefs] = useState<Record<string, TestDefinitionSummary>>(testDefinitionByCode || {});

  // Narrows the open "Add Specification Parameter" flow to the caller's own
  // lab; editing or adding a parameter to an already-picked test leaves the
  // field disabled above, so it keeps showing that test regardless. A test
  // whose section isn't loaded yet, or whose lab list hasn't resolved yet,
  // is left visible rather than hidden - the server is still the real gate.
  // Memoized: the form-reset effect below depends on this list, so a fresh
  // array on every render would reset the form on every render (an endless
  // render loop that wipes the "Add Specification Parameter" form).
  const selectableAssignedTests = useMemo(
    () =>
      isTestDisabled
        ? assignedTests
        : assignedTests.filter((t) => {
            const def = testDefs[t.testCode];
            const sectionId = def?.sectionId;
            // Physicochemical tests are tagged with an area; FP items use "fp", RM/PM items "rmpm".
            return (
              areaIncludes(def?.physchemArea, item.category === "FinishedProduct" ? "fp" : "rmpm") &&
              (sectionId == null || myLabSectionIds.size === 0 || myLabSectionIds.has(sectionId))
            );
          }),
    [isTestDisabled, assignedTests, testDefs, myLabSectionIds, item.category]
  );
  const [resultBasis, setResultBasis] = useState<ResultBasis | "">("MgPerKg");
  const [labelClaim, setLabelClaim] = useState("");
  const [labelClaimUnit, setLabelClaimUnit] = useState("");
  const [hplcMethodAnalyteId, setHplcMethodAnalyteId] = useState<number | "">("");
  const [methodAnalytes, setMethodAnalytes] = useState<HplcMethodAnalyteResponse[]>([]);
  const [loadingMethodAnalytes, setLoadingMethodAnalytes] = useState(false);
  const [hplcMethodResultMode, setHplcMethodResultMode] = useState<HplcResultMode | undefined>(undefined);
  const [icpMethodElementId, setIcpMethodElementId] = useState<number | "">("");
  const [icpMethod, setIcpMethod] = useState<IcpMethodResponse | null>(null);
  const [loadingIcpMethod, setLoadingIcpMethod] = useState(false);

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
  // Without the definitions the dialog cannot tell which fields a test needs.
  const [testDefsFailed, setTestDefsFailed] = useState(false);

  useEffect(() => {
    if (testDefinitionByCode && Object.keys(testDefinitionByCode).length > 0) {
      setTestDefs(testDefinitionByCode);
    } else {
      masterDataOptions
        .getTestDefinitions()
        .then((defs: TestDefinitionSummary[]) => {
          setTestDefs(Object.fromEntries(defs.map((d) => [d.code, d])));
          setTestDefsFailed(false);
        })
        .catch(() => setTestDefsFailed(true));
    }
  }, [testDefinitionByCode]);

  const currentTestDef = testDefs[testCode];
  const isDissolution =
    workflowTypeByCode[testCode] === "Dissolution" ||
    currentTestDef?.workflowType === "Dissolution" ||
    limitType === "DissolutionQ";
  const isDisintegration =
    workflowTypeByCode[testCode] === "Disintegration" ||
    currentTestDef?.workflowType === "Disintegration" ||
    limitType === "DisintegrationTime";
  const isTitration =
    workflowTypeByCode[testCode] === "Titration" || currentTestDef?.workflowType === "Titration";
  const isWeightVariation =
    workflowTypeByCode[testCode] === "WeightVariation" ||
    currentTestDef?.workflowType === "WeightVariation" ||
    limitType === "WeightVariation";

  const isHplcMethodAssay =
    currentTestDef?.equationType === "HplcMethodAssay" ||
    currentTestDef?.workflowType === "HplcMethodAssay" ||
    workflowTypeByCode[testCode] === "HplcMethodAssay";

  const isResidualSolvents =
    isHplcMethodAssay && (hplcMethodResultMode === "ResidualSolvents" || editingSpec?.resultBasis === "Ppm");

  useEffect(() => {
    if (!isHplcMethodAssay || !currentTestDef?.hplcMethodId) {
      setMethodAnalytes([]);
      setHplcMethodResultMode(undefined);
      return;
    }
    setLoadingMethodAnalytes(true);
    HplcMethodService.getById(currentTestDef.hplcMethodId)
      .then((res) => {
        setMethodAnalytes(res.analytes ?? []);
        setHplcMethodResultMode(res.resultMode);
        if (res.resultMode === "ResidualSolvents") {
          setResultBasis("Ppm");
          setLimitType("NotMoreThan");
          setUnit("ppm");
        }
      })
      .catch(() => {
        setMethodAnalytes([]);
        setHplcMethodResultMode(undefined);
      })
      .finally(() => {
        setLoadingMethodAnalytes(false);
      });
  }, [isHplcMethodAssay, currentTestDef?.hplcMethodId]);

  const isIcpMethodAssay =
    currentTestDef?.equationType === "IcpMethodAssay" ||
    currentTestDef?.workflowType === "IcpMethodAssay" ||
    workflowTypeByCode[testCode] === "IcpMethodAssay";

  const isIcpElementalImpurities =
    isIcpMethodAssay && (icpMethod?.mode === "ElementalImpurities" || editingSpec?.resultBasis === "MgPerKg");

  useEffect(() => {
    if (!isIcpMethodAssay || !currentTestDef?.icpMethodId) {
      setIcpMethod(null);
      return;
    }
    setLoadingIcpMethod(true);
    IcpMethodService.getById(currentTestDef.icpMethodId)
      .then((res) => {
        setIcpMethod(res);
        if (res.mode === "ElementalImpurities") {
          setResultBasis("MgPerKg");
          setLimitType("NotMoreThan");
          setUnit("µg/g");
        } else if (res.mode === "MineralAssay") {
          if (!editingSpec?.resultBasis) {
            setResultBasis("PercentLabelClaim");
            setUnit("%");
          }
        }
      })
      .catch(() => {
        setIcpMethod(null);
      })
      .finally(() => {
        setLoadingIcpMethod(false);
      });
  }, [isIcpMethodAssay, currentTestDef?.icpMethodId, editingSpec?.resultBasis]);

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
      setDosageForm((editingSpec.dosageForm as DosageForm) || "");
      setProductionStageRole((editingSpec.productionStageRole as ProductionStageRole) || "");
      if (editingSpec.limitType === "WeightVariation") {
        setUnit("mg");
      }

      setHplcMethodAnalyteId(editingSpec.hplcMethodAnalyteId ?? "");
      setIcpMethodElementId(editingSpec.icpMethodElementId ?? "");
      const editDef = testDefs[editingSpec.testCode];
      const isEditHplcAssay =
        editDef?.equationType === "HplcMethodAssay" ||
        editDef?.workflowType === "HplcMethodAssay" ||
        workflowTypeByCode[editingSpec.testCode] === "HplcMethodAssay";
      const isEditIcpAssay =
        editDef?.equationType === "IcpMethodAssay" ||
        editDef?.workflowType === "IcpMethodAssay" ||
        workflowTypeByCode[editingSpec.testCode] === "IcpMethodAssay";
      setResultBasis((editingSpec.resultBasis as ResultBasis) || (isEditHplcAssay || isEditIcpAssay ? "PercentLabelClaim" : "MgPerKg"));
      setLabelClaim(formatTrimmedDecimal(editingSpec.labelClaim));
      setLabelClaimUnit(editingSpec.labelClaimUnit ?? "");

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
      const initialCode = preselectedTestCode || selectableAssignedTests[0]?.testCode || "";
      setTestCode(initialCode);

      const match = assignedTests.find((t) => t.testCode === initialCode);
      const initialParam = match?.displayName || initialCode;
      setParameterName(initialParam);

      const def = testDefs[initialCode];
      const isInitHplc = def?.equationType === "HplcMethodAssay" || def?.workflowType === "HplcMethodAssay" || workflowTypeByCode[initialCode] === "HplcMethodAssay";
      const isInitIcp = def?.equationType === "IcpMethodAssay" || def?.workflowType === "IcpMethodAssay" || workflowTypeByCode[initialCode] === "IcpMethodAssay";
      const defaultType = isInitHplc || isInitIcp ? "Range" : getDefaultLimitType(workflowTypeByCode[initialCode]);
      setLimitType(defaultType);

      setReferenceStandard("");
      setUnit(isInitHplc || isInitIcp ? "%" : defaultType === "WeightVariation" ? "mg" : defaultType === "DisintegrationTime" ? "min" : "");
      setDilutionFactor("");
      setDosageForm("");
      setProductionStageRole("");

      setHplcMethodAnalyteId("");
      setIcpMethodElementId("");
      setResultBasis(isInitHplc || isInitIcp ? "PercentLabelClaim" : "MgPerKg");
      setLabelClaim("");
      setLabelClaimUnit("");

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
  }, [open, editingSpec, preselectedTestCode, item, workflowTypeByCode, assignedTests, testDefs, existingSpecs, selectableAssignedTests]);

  const handleTestChange = (newCode: string) => {
    setTestCode(newCode);
    const match = assignedTests.find((t) => t.testCode === newCode);
    setParameterName(match?.displayName || newCode);

    const def = testDefs[newCode];
    const isHplcAssay = def?.equationType === "HplcMethodAssay" || def?.workflowType === "HplcMethodAssay" || workflowTypeByCode[newCode] === "HplcMethodAssay";
    const isIcpAssay = def?.equationType === "IcpMethodAssay" || def?.workflowType === "IcpMethodAssay" || workflowTypeByCode[newCode] === "IcpMethodAssay";
    const isDis = workflowTypeByCode[newCode] === "Dissolution" || def?.workflowType === "Dissolution";
    const isDisint = workflowTypeByCode[newCode] === "Disintegration" || def?.workflowType === "Disintegration";
    const isWv = workflowTypeByCode[newCode] === "WeightVariation" || def?.workflowType === "WeightVariation";
    const isTit = workflowTypeByCode[newCode] === "Titration" || def?.workflowType === "Titration";

    if (isTit) {
      setLimitType("Range");
      setDilutionFactor("");
      setResultBasis("PercentAsIs");
      setUnit("%");
      setLabelClaim("");
      setLabelClaimUnit("");
    } else if (isHplcAssay) {
      setLimitType("Range");
      setHplcMethodAnalyteId("");
      setDilutionFactor("");
      setResultBasis("PercentLabelClaim");
      setUnit("%");
      setLabelClaim("");
      setLabelClaimUnit("");
    } else if (isIcpAssay) {
      setLimitType("Range");
      setIcpMethodElementId("");
      setHplcMethodAnalyteId("");
      setDilutionFactor("");
      setResultBasis("PercentLabelClaim");
      setUnit("%");
      setLabelClaim("");
      setLabelClaimUnit("");
    } else if (isDis) {
      setLimitType("DissolutionQ");
      setDilutionFactor("");
    } else if (isDisint) {
      setLimitType("DisintegrationTime");
      setDilutionFactor("");
      setUnit("min");
    } else if (isWv) {
      setLimitType("WeightVariation");
      setDilutionFactor("");
      setUnit("mg");
      setDosageForm("");
    } else {
      const defType = getDefaultLimitType(workflowTypeByCode[newCode]);
      setLimitType(defType);
      if (defType !== "CountTiered") {
        setDilutionFactor("");
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

    if (isHplcMethodAssay) {
      if (!hplcMethodAnalyteId) {
        setError(isResidualSolvents ? "Please select a solvent from the GC method." : "Please select an analyte from the HPLC method.");
        return;
      }
      if (isResidualSolvents) {
        if (resultBasis !== "Ppm") {
          setError("Basis must be ppm for residual solvents.");
          return;
        }
      } else {
        if (!resultBasis || (resultBasis !== "PercentLabelClaim" && resultBasis !== "MgPerUnit")) {
          setError("Please select a basis (Assay % or Amount per unit).");
          return;
        }
        if (resultBasis === "MgPerUnit") {
          const lcNum = Number(labelClaim);
          if (!labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
            setError("Label claim must be greater than 0 for Amount per unit.");
            return;
          }
          if (!labelClaimUnit.trim()) {
            setError("Label claim unit is required for Amount per unit.");
            return;
          }
        }
      }
    }

    if (isIcpMethodAssay) {
      if (!icpMethodElementId) {
        setError("Please select an element from the ICP method.");
        return;
      }
      if (icpMethod?.mode === "ElementalImpurities" || resultBasis === "MgPerKg") {
        if (resultBasis !== "MgPerKg") {
          setError("Basis must be µg/g for elemental impurities.");
          return;
        }
      } else {
        if (!resultBasis || (resultBasis !== "PercentLabelClaim" && resultBasis !== "MgPerUnit")) {
          setError("Please select a basis (% of label claim or mg per unit).");
          return;
        }
        const lcNum = Number(labelClaim);
        if (!labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
          setError("Label claim must be greater than 0.");
          return;
        }
        if (!labelClaimUnit.trim()) {
          setError("Label claim unit is required.");
          return;
        }
      }
    }

    if (isTitration) {
      const basisError = validateTitrationSpecBasis(resultBasis, labelClaim);
      if (basisError) {
        setError(basisError);
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

    if (limitType === "DisintegrationTime") {
      const tNum = Number(upperLimit);
      if (!upperLimit.trim() || isNaN(tNum) || tNum <= 0) {
        setError("Time limit (min) must be greater than zero for Disintegration Time specifications.");
        return;
      }
      const otherDisintegrationSpec = existingSpecs.find(
        (s) => s.testCode === testCode && s.id !== editingSpec?.id && s.limitType === "DisintegrationTime"
      );
      if (otherDisintegrationSpec) {
        setError("Only one DisintegrationTime specification is allowed per test.");
        return;
      }
    }

    if (limitType === "WeightVariation") {
      if (!dosageForm) {
        setError("Dosage form is required for Weight Variation specifications.");
        return;
      }
      const otherWvSpec = existingSpecs.find(
        (s) => s.testCode === testCode && s.id !== editingSpec?.id && s.limitType === "WeightVariation"
      );
      if (otherWvSpec) {
        setError("Only one WeightVariation specification is allowed per test.");
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
      unit:
        isResidualSolvents
          ? "ppm"
          : limitType === "WeightVariation"
          ? "mg"
          : limitType === "DisintegrationTime"
            ? "min"
            : (unit.trim() || null),
      dosageForm: limitType === "WeightVariation" ? (dosageForm || null) : null,
      dilutionFactor:
        limitType === "CountTiered" && dilutionFactor.trim() !== ""
          ? Number(dilutionFactor)
          : null,
      lowerLimit:
        limitType === "DissolutionQ"
          ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
          : (limitType === "Range" || limitType === "NotLessThan"
            ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
            : null),
      upperLimit:
        limitType === "DisintegrationTime"
          ? (upperLimit.trim() !== "" ? Number(upperLimit) : null)
          : (limitType === "Range" || limitType === "NotMoreThan"
            ? (upperLimit.trim() !== "" ? Number(upperLimit) : null)
            : null),
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
        limitType === "WeightVariation"
          ? (dosageForm === "Tablet"
            ? "USP <2091>: tablets, limit by average weight"
            : "USP <2091>: net content 90-110 % of average")
          : (limitType === "DisintegrationTime"
            ? (upperLimit.trim() !== "" ? `NMT ${upperLimit.trim()} min` : null)
            : (limitType === "DissolutionQ"
              ? (lowerLimit.trim() !== "" ? `Q = ${lowerLimit.trim()} %` : null)
              : (limitType === "CountTiered" ? specLimit.trim() : null))),
      stages:
        limitType === "MultiStage"
          ? stages.map((s, idx) => ({
              stageNumber: s.stageNumber || idx + 1,
              stageLabel: s.stageLabel.trim(),
              acceptanceCriteriaText: s.acceptanceCriteriaText.trim()
            }))
          : undefined,
      hplcMethodAnalyteId: isHplcMethodAssay && hplcMethodAnalyteId !== "" ? Number(hplcMethodAnalyteId) : null,
      icpMethodElementId: isIcpMethodAssay && icpMethodElementId !== "" ? Number(icpMethodElementId) : null,
      resultBasis: isIcpMethodAssay
        ? (resultBasis as ResultBasis)
        : (isHplcMethodAssay || isTitration) && resultBasis
        ? (resultBasis as ResultBasis)
        : null,
      labelClaim:
        limitType === "WeightVariation" || limitType === "DisintegrationTime"
          ? null
          : (limitType === "DissolutionQ"
            ? (labelClaim.trim() !== "" ? Number(labelClaim) : null)
            : isIcpMethodAssay
            ? (icpMethod?.mode === "MineralAssay" && labelClaim.trim() !== "" ? Number(labelClaim) : null)
            : (((isHplcMethodAssay && resultBasis === "MgPerUnit") || (isTitration && titrationBasisNeedsLabelClaim(resultBasis))) && labelClaim.trim() !== ""
              ? Number(labelClaim)
              : null)),
      labelClaimUnit:
        limitType === "WeightVariation" || limitType === "DisintegrationTime"
          ? null
          : (limitType === "DissolutionQ"
            ? "mg"
            : isIcpMethodAssay
            ? (icpMethod?.mode === "MineralAssay" ? labelClaimUnit.trim() || null : null)
            : (isTitration
              ? (titrationBasisNeedsLabelClaim(resultBasis) ? "mg" : null)
              : isHplcMethodAssay && resultBasis === "MgPerUnit"
              ? labelClaimUnit.trim() || null
              : null)),
      productionStageRole: supportsProductionStage && productionStageRole ? productionStageRole : null
    };

    try {
      if (isEditing && editingSpec?.id != null) {
        const updatePayload: UpdateSpecificationPayload = basePayload;
        await SpecificationService.update(editingSpec.id, updatePayload, editingSpec.version);
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
        {testDefsFailed && (
          <Alert severity="error" sx={{ mb: 2 }}>
            The test definitions could not be loaded, so test-specific fields may be missing. Close and reopen this dialog to try again.
          </Alert>
        )}
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {!isTestDisabled && selectableAssignedTests.length === 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            None of this item&rsquo;s assigned tests belong to your laboratory. Ask that test&rsquo;s Section Head to add
            the specification.
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
                {selectableAssignedTests.map((t) => (
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

          {supportsProductionStage && (
            <FormControl size="small" fullWidth>
              <InputLabel id="production-stage-label">Production Stage</InputLabel>
              <Select
                labelId="production-stage-label"
                label="Production Stage"
                value={productionStageRole}
                onChange={(e) => setProductionStageRole(e.target.value as ProductionStageRole | "")}
              >
                <MenuItem value="">All stages</MenuItem>
                {PRODUCTION_STAGE_ROLE_OPTIONS.map((o) => (
                  <MenuItem key={o.value} value={o.value}>
                    {o.label}
                  </MenuItem>
                ))}
              </Select>
              <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                A sample at a stage that has its own specification for this test uses it; every other sample uses the
                &quot;All stages&quot; specification.
              </Typography>
            </FormControl>
          )}

          {/* HPLC Method Assay Parameters Block */}
          {isHplcMethodAssay &&
            (isResidualSolvents ? (
              <ResidualSolventSpecFields
                methodAnalytes={methodAnalytes}
                loadingMethodAnalytes={loadingMethodAnalytes}
                selectedAnalyteId={hplcMethodAnalyteId}
                onAnalyteChange={(id, chosen) => {
                  setHplcMethodAnalyteId(id);
                  if (chosen) {
                    setParameterName(chosen.name);
                  }
                }}
              />
            ) : (
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
                  HPLC Method Assay Specification
                </Typography>

                <Stack spacing={2}>
                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: {
                        xs: "1fr",
                        sm: "1fr 1fr"
                      },
                      gap: 2
                    }}
                  >
                    <FormControl size="small" fullWidth required>
                      <InputLabel id="hplc-method-analyte-label">Analyte *</InputLabel>
                      <Select
                        labelId="hplc-method-analyte-label"
                        label="Analyte *"
                        value={hplcMethodAnalyteId}
                        onChange={(e) => {
                          const id = Number(e.target.value);
                          setHplcMethodAnalyteId(id);
                          const chosen = methodAnalytes.find((a) => a.id === id);
                          if (chosen) {
                            const prevMatchesAnalyte = methodAnalytes.some(
                              (a) => a.name === parameterName || hplcParameterName(a.name, "MgPerUnit") === parameterName
                            );
                            const prevMatchesTest = assignedTests.some(
                              (t) => t.displayName === parameterName || t.testCode === parameterName
                            );
                            if (!parameterName.trim() || prevMatchesAnalyte || prevMatchesTest) {
                              setParameterName(hplcParameterName(chosen.name, resultBasis));
                            }
                          }
                        }}
                        disabled={loadingMethodAnalytes}
                      >
                        {loadingMethodAnalytes ? (
                          <MenuItem disabled value="">
                            <em>Loading method analytes...</em>
                          </MenuItem>
                        ) : methodAnalytes.length === 0 ? (
                          <MenuItem disabled value="">
                            <em>No analytes found in HPLC method</em>
                          </MenuItem>
                        ) : (
                          methodAnalytes.map((a) => (
                            <MenuItem key={a.id} value={a.id}>
                              {a.name}
                              {a.wavelengthNm != null ? ` (${a.wavelengthNm} nm)` : ""}
                            </MenuItem>
                          ))
                        )}
                      </Select>
                    </FormControl>

                    <FormControl size="small" fullWidth required>
                      <InputLabel id="hplc-result-basis-label">Basis *</InputLabel>
                      <Select
                        labelId="hplc-result-basis-label"
                        label="Basis *"
                        value={resultBasis}
                        onChange={(e) => {
                          const val = e.target.value as ResultBasis;
                          setResultBasis(val);
                          // Both basis rows of one analyte share the parameter name
                          // unique key, so the mg/unit row carries a suffix.
                          const analyte = methodAnalytes.find((a) => a.id === hplcMethodAnalyteId);
                          if (analyte && (parameterName === analyte.name || parameterName === hplcParameterName(analyte.name, "MgPerUnit"))) {
                            setParameterName(hplcParameterName(analyte.name, val));
                          }
                          if (val === "PercentLabelClaim") {
                            setUnit("%");
                          } else if (val === "MgPerUnit") {
                            if (labelClaimUnit) setUnit(labelClaimUnit);
                          }
                        }}
                      >
                        <MenuItem value="PercentLabelClaim">Assay %</MenuItem>
                        <MenuItem value="MgPerUnit">Amount per unit</MenuItem>
                      </Select>
                    </FormControl>
                  </Box>

                  {resultBasis === "MgPerUnit" && (
                    <Box
                      sx={{
                        display: "grid",
                        gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                        gap: 2
                      }}
                    >
                      <TextField
                        size="small"
                        label="Label Claim *"
                        type="number"
                        value={labelClaim}
                        onChange={(e) => setLabelClaim(e.target.value)}
                        required
                        helperText="Target amount per unit"
                        slotProps={{ htmlInput: { step: "any", min: "0" } }}
                        fullWidth
                      />

                      <TextField
                        size="small"
                        label="Label Claim Unit *"
                        value={labelClaimUnit}
                        onChange={(e) => {
                          setLabelClaimUnit(e.target.value);
                          setUnit(e.target.value);
                        }}
                        required
                        placeholder="e.g. mg, g, mcg"
                        helperText="Unit for claim and result"
                        fullWidth
                      />
                    </Box>
                  )}
                </Stack>
              </Box>
            ))}

          {isTitration && (
            <TitrationSpecBasisFields
              finishedProduct={item.category === "FinishedProduct"}
              resultBasis={resultBasis}
              labelClaim={labelClaim}
              onBasisChange={setResultBasis}
              onLabelClaimChange={setLabelClaim}
            />
          )}

          {/* ICP Method Assay Parameters Block */}
          {isIcpMethodAssay && (
            <IcpSpecFields
              method={icpMethod}
              loadingMethod={loadingIcpMethod}
              selectedElementId={icpMethodElementId}
              onElementChange={(id, chosen) => {
                setIcpMethodElementId(id);
                if (chosen) {
                  const prevMatchesElement = (icpMethod?.elements ?? []).some(
                    (el) => el.symbol === parameterName || icpParameterName(el.symbol, "MgPerUnit") === parameterName
                  );
                  const prevMatchesTest = assignedTests.some(
                    (t) => t.displayName === parameterName || t.testCode === parameterName
                  );
                  if (!parameterName.trim() || prevMatchesElement || prevMatchesTest) {
                    setParameterName(icpParameterName(chosen.symbol, resultBasis));
                  }
                }
              }}
              resultBasis={resultBasis}
              onBasisChange={(val) => {
                setResultBasis(val);
                const el = icpMethod?.elements?.find((e) => e.id === icpMethodElementId);
                if (el && (parameterName === el.symbol || parameterName === icpParameterName(el.symbol, "MgPerUnit"))) {
                  setParameterName(icpParameterName(el.symbol, val));
                }
                if (val === "PercentLabelClaim") {
                  setUnit("%");
                } else if (val === "MgPerUnit") {
                  if (labelClaimUnit) setUnit(labelClaimUnit);
                }
              }}
              labelClaim={labelClaim}
              onLabelClaimChange={setLabelClaim}
              labelClaimUnit={labelClaimUnit}
              onLabelClaimUnitChange={(u) => {
                setLabelClaimUnit(u);
                if (resultBasis === "MgPerUnit") {
                  setUnit(u);
                }
              }}
            />
          )}

          {/* Row 2: Limit Type */}
          {isResidualSolvents ? (
            <TextField
              size="small"
              label="Limit Type *"
              value="NMT (Not More Than)"
              slotProps={{ input: { readOnly: true } }}
              helperText="Fixed limit type for residual solvents"
              fullWidth
            />
          ) : isIcpElementalImpurities ? null : (
            <FormControl size="small" fullWidth>
              <InputLabel id="limit-type-label">Limit Type *</InputLabel>
              <Select
                labelId="limit-type-label"
                label="Limit Type *"
                value={limitType}
                onChange={(e) => handleLimitTypeChange(e.target.value as LimitType)}
              >
                {(isWeightVariation
                  ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "WeightVariation")
                  : isDisintegration
                  ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "DisintegrationTime")
                  : isDissolution
                  ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "DissolutionQ")
                  : (isHplcMethodAssay || isIcpMethodAssay || isTitration)
                  ? LIMIT_TYPE_OPTIONS.filter((opt) =>
                      ["Range", "NotMoreThan", "NotLessThan", "TargetWithTolerance"].includes(opt.value)
                    )
                  : LIMIT_TYPE_OPTIONS.filter(
                      (opt) =>
                        opt.value !== "DissolutionQ" &&
                        opt.value !== "DisintegrationTime" &&
                        opt.value !== "WeightVariation"
                    )
                ).map((opt) => (
                  <MenuItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

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
                  label={isResidualSolvents ? "NMT (ppm) *" : isIcpElementalImpurities ? "NMT (µg/g) *" : "Upper Limit (NMT) *"}
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
                    <IconButton aria-label={`Remove stage ${idx + 1}`}
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

            {/* Limit Type: DisintegrationTime */}
            {limitType === "DisintegrationTime" && (
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
                  Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Time limit (min) *"
                    placeholder="e.g. 15"
                    value={upperLimit}
                    onChange={(e) => setUpperLimit(e.target.value)}
                    slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    helperText="Stored in upper limit (NMT time in minutes)"
                    required
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    label="Unit"
                    value="min"
                    disabled
                    helperText="Fixed unit for disintegration time"
                    sx={{ width: 120 }}
                  />
                </Stack>
                {upperLimit.trim() !== "" && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Specification limit display: <strong>NMT {upperLimit.trim()} min</strong>
                  </Typography>
                )}
              </Box>
            )}

            {/* Limit Type: WeightVariation */}
            {limitType === "WeightVariation" && (
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
                  Weight Variation Acceptance (USP &lt;2091&gt;)
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
                  <FormControl size="small" sx={{ flex: 1 }}>
                    <InputLabel id="wv-dosage-form-label">Dosage Form *</InputLabel>
                    <Select
                      labelId="wv-dosage-form-label"
                      label="Dosage Form *"
                      value={dosageForm}
                      onChange={(e) => setDosageForm(e.target.value as DosageForm)}
                    >
                      <MenuItem value="Tablet">Tablet</MenuItem>
                      <MenuItem value="HardCapsule">Hard Capsule</MenuItem>
                      <MenuItem value="SoftCapsule">Soft Capsule</MenuItem>
                    </Select>
                  </FormControl>
                  <TextField
                    size="small"
                    label="Unit"
                    value="mg"
                    disabled
                    helperText="Fixed unit for weight variation"
                    sx={{ width: 120 }}
                  />
                </Stack>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Specification limit display:{" "}
                  <strong>
                    {dosageForm === "Tablet"
                      ? "USP <2091>: tablets, limit by average weight"
                      : "USP <2091>: net content 90-110 % of average"}
                  </strong>
                </Typography>
              </Box>
            )}
          </Box>

          {/* Row 4: Unit & Reference Standard */}
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              label="Unit"
              value={
                isResidualSolvents
                  ? "ppm"
                  : isIcpElementalImpurities
                  ? "µg/g"
                  : limitType === "WeightVariation"
                  ? "mg"
                  : limitType === "DisintegrationTime"
                  ? "min"
                  : unit
              }
              onChange={(e) => setUnit(e.target.value)}
              disabled={isResidualSolvents || isIcpElementalImpurities || limitType === "DisintegrationTime" || limitType === "WeightVariation"}
              helperText={
                isResidualSolvents
                  ? "Fixed unit for residual solvents"
                  : isIcpElementalImpurities
                  ? "Fixed unit for elemental impurities"
                  : limitType === "WeightVariation"
                  ? "Fixed unit for weight variation"
                  : limitType === "DisintegrationTime"
                  ? "Fixed unit for disintegration time"
                  : undefined
              }
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
          {!isHplcMethodAssay && !isIcpMethodAssay && !isTitration && limitType !== "WeightVariation" && (
            <TextField
              size="small"
              label="Dilution Factor"
              type="number"
              value={dilutionFactor}
              onChange={(e) => setDilutionFactor(e.target.value)}
              disabled={limitType !== "CountTiered"}
              helperText="DILUTION FACTOR: editable only for Count-Tiered parameters"
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
