import { useEffect, useState } from "react";
import { TextField, Button, Stack, Alert, Select, MenuItem, Box, Typography, FormControlLabel, Chip, Tooltip, FormControl, InputLabel, FormHelperText, Switch } from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { LoadErrorAlert, LoadFailuresAlert } from "../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../hooks/useLoadFailures";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { CreateTestDefinitionPayload, UpdateTestDefinitionPayload } from "../../../services/masterDataOptions";
import { HplcMethodService, HplcMethodListItem } from "./services/HplcMethodService";
import { IcpMethodService, IcpMethodListItem } from "./services/IcpMethodService";
import { TitrationConfigSection } from "./TitrationConfigSection";
import { TitrationFormState, createInitialTitrationForm, titrationFormFromDefinition, titrationPayloadFields, validateTitrationForm } from "./titrationConfig";
import { PageArea, PhyschemArea, areaIncludes, defaultAreaFor } from "./testMasterArea";
import { TestAreaField } from "./TestAreaField";
import { TitrationChangeReasonField, hasTitrationSettingsChanged, titrationChangeReasonCheck } from "./TitrationChangeReasonField";
import { EQUATION_TYPES, EQUATION_TYPE_LABELS, FP_SECTION_CODE, TestMasterLab, WORKFLOW_TYPES_BY_LAB, WORKFLOW_TYPE_LABELS } from "./testMaster/testMasterConstants";
import { TestStageReplicatesSection } from "./testMaster/TestStageReplicatesSection";
import { WorkflowStepsSection } from "./testMaster/WorkflowStepsSection";

// The Test Master - one canonical Code/DisplayName per test, referenced
// everywhere a TestCode is assigned (Items, Water Sampling Points, Room
// Test Configurations, Machine Part Configurations) via TestCodePicker/
// TestCodePickerMulti. Those pickers can also add a new test inline,
// but this page is the place to see the whole list and add one
// deliberately (e.g. before configuring several items that will all
// need it).
//
// Freezing a test hides it from those pickers' dropdown for *new*
// selections without touching anything that already references its
// Code - see useTestDefinitions.activeOptions.
export function TestMasterPage({ lab = "micro", area }: { lab?: TestMasterLab; area?: PageArea }) {
  const { options: allOptions, addNew, update, setActive, reload } = useTestDefinitions();
  const isFp = lab === "fp";
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[lab];
  const defaultWorkflowType: string = isFp ? "HplcMethodAssay" : "Observation";
  const [fpSectionId, setFpSectionId] = useState<number | null>(null);
  // Separate from fpSectionId: null meant "still loading", "failed" and "no
  // FP section exists" alike, so a failure (or a site without an FP section)
  // left the register showing skeleton rows forever.
  const [sectionsState, setSectionsState] = useState<"loading" | "loaded" | "failed">("loading");
  const [sectionsReloadKey, setSectionsReloadKey] = useState(0);
  useEffect(() => {
    setSectionsState("loading");
    getSections()
      .then((secs) => {
        setFpSectionId(secs.find((s) => s.sectionCode === FP_SECTION_CODE)?.sectionId ?? null);
        setSectionsState("loaded");
      })
      .catch(() => setSectionsState("failed"));
  }, [sectionsReloadKey]);
  const inLab = (sid?: number | null) =>
    isFp ? fpSectionId !== null && sid === fpSectionId : sid !== fpSectionId;
  const options = allOptions.filter(
    (t) =>
      sectionsState === "loaded" &&
      inLab(t.sectionId) &&
      (!isFp || !area || areaIncludes(t.physchemArea, area))
  );
  const [physchemArea, setPhyschemArea] = useState<PhyschemArea | null>(null);
  const [changeReason, setChangeReason] = useState("");
  const [changeReasonError, setChangeReasonError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
  const [workflowType, setWorkflowType] = useState<string>(defaultWorkflowType);
  const [equationType, setEquationType] = useState<string>(isFp ? "HplcMethodAssay" : "None");
  const [requiresSystemSuitability, setRequiresSystemSuitability] = useState<boolean>(false);
  const [methodAbbreviation, setMethodAbbreviation] = useState<string>("");
  const [hplcMethodId, setHplcMethodId] = useState<number | "">("");
  const [hplcMethods, setHplcMethods] = useState<HplcMethodListItem[]>([]);
  const [icpMethodId, setIcpMethodId] = useState<number | "">("");
  const [icpMethods, setIcpMethods] = useState<IcpMethodListItem[]>([]);

  // Lists the Add/Edit Test dialog picks from; a failure is named in the
  // dialog instead of leaving an empty picker.
  const { failed: dialogListFailures, fail: failDialogList } = useLoadFailures();

  useEffect(() => {
    HplcMethodService.getAll(false)
      .then((data) => setHplcMethods(data))
      .catch(failDialogList("HPLC methods"));
    IcpMethodService.getAll(false)
      .then((data) => setIcpMethods(data))
      .catch(failDialogList("ICP methods"));
  }, [failDialogList]);

  const [replicateCount, setReplicateCount] = useState<string>("1");
  const [evaluationBasis, setEvaluationBasis] = useState<"Mean" | "EachValue" | "Min" | "Max">("Mean");
  const [conditionFields, setConditionFields] = useState<string>("");
  const [usesTare, setUsesTare] = useState<boolean>(false);
  const [titration, setTitration] = useState<TitrationFormState>(createInitialTitrationForm);

  const [dissolutionS1Offset, setDissolutionS1Offset] = useState<string>("5");
  const [dissolutionS2MinOffset, setDissolutionS2MinOffset] = useState<string>("15");
  const [dissolutionS3MinOffset, setDissolutionS3MinOffset] = useState<string>("25");
  const [dissolutionS3MaxBelowS2Min, setDissolutionS3MaxBelowS2Min] = useState<string>("2");

  const [disintegrationStage1Units, setDisintegrationStage1Units] = useState<string>("6");
  const [disintegrationStage2Units, setDisintegrationStage2Units] = useState<string>("12");
  const [disintegrationMaxStage1Failures, setDisintegrationMaxStage1Failures] = useState<string>("2");
  const [disintegrationMinPassTotal, setDisintegrationMinPassTotal] = useState<string>("16");

  const [wvUnitCount, setWvUnitCount] = useState<string>("20");
  const [wvTabletBand1MaxMg, setWvTabletBand1MaxMg] = useState<string>("130");
  const [wvTabletBand1Percent, setWvTabletBand1Percent] = useState<string>("10");
  const [wvTabletBand2MaxMg, setWvTabletBand2MaxMg] = useState<string>("324");
  const [wvTabletBand2Percent, setWvTabletBand2Percent] = useState<string>("7.5");
  const [wvTabletBand3Percent, setWvTabletBand3Percent] = useState<string>("5");
  const [wvTabletMaxOutside, setWvTabletMaxOutside] = useState<string>("2");
  const [wvCapsuleInnerPercent, setWvCapsuleInnerPercent] = useState<string>("10");
  const [wvCapsuleOuterPercent, setWvCapsuleOuterPercent] = useState<string>("25");
  const [wvCapsuleS1MaxOutside, setWvCapsuleS1MaxOutside] = useState<string>("2");
  const [wvCapsuleS1MaxForRetest, setWvCapsuleS1MaxForRetest] = useState<string>("6");
  const [wvCapsuleS2ExtraUnits, setWvCapsuleS2ExtraUnits] = useState<string>("40");
  const [wvCapsuleS2MaxOutside, setWvCapsuleS2MaxOutside] = useState<string>("6");

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editingSectionId, setEditingSectionId] = useState<number | null>(null);
  const [editingTest, setEditingTest] = useState<TestDefinitionOption | null>(null);
  const [allMySections, setMySections] = useState<LaboratorySection[]>([]);
  const mySections = allMySections.filter((s) => sectionsState === "loaded" && inLab(s.sectionId));
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [search, setSearch] = useState("");

  useEffect(() => {
    getMySections()
      .then((secs) => setMySections(secs))
      .catch(failDialogList("your laboratory sections"));
  }, [failDialogList]);

  const openCreateDialog = () => {
    setEditingId(null);
    setEditingTest(null);
    setCode("");
    setDisplayName("");
    setSectionId(mySections.length === 1 ? mySections[0].sectionId : "");
    setEditingSectionId(null);
    setWorkflowType(defaultWorkflowType);
    setEquationType(isFp ? (defaultWorkflowType === "HplcMethodAssay" ? "HplcMethodAssay" : "None") : "None");
    setRequiresSystemSuitability(false);
    setMethodAbbreviation("");
    setHplcMethodId("");
    setIcpMethodId("");
    setReplicateCount("1");
    setEvaluationBasis("Mean");
    setConditionFields("");
    setUsesTare(false);
    setTitration(createInitialTitrationForm());
    setDissolutionS1Offset("5");
    setDissolutionS2MinOffset("15");
    setDissolutionS3MinOffset("25");
    setDissolutionS3MaxBelowS2Min("2");
    setDisintegrationStage1Units("6");
    setDisintegrationStage2Units("12");
    setDisintegrationMaxStage1Failures("2");
    setDisintegrationMinPassTotal("16");
    setWvUnitCount("20");
    setWvTabletBand1MaxMg("130");
    setWvTabletBand1Percent("10");
    setWvTabletBand2MaxMg("324");
    setWvTabletBand2Percent("7.5");
    setWvTabletBand3Percent("5");
    setWvTabletMaxOutside("2");
    setWvCapsuleInnerPercent("10");
    setWvCapsuleOuterPercent("25");
    setWvCapsuleS1MaxOutside("2");
    setWvCapsuleS1MaxForRetest("6");
    setWvCapsuleS2ExtraUnits("40");
    setWvCapsuleS2MaxOutside("6");
    setPhyschemArea(area ? defaultAreaFor(area) : null);
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const startEdit = (t: TestDefinitionOption) => {
    setEditingId(t.id);
    setEditingTest(t);
    setCode(t.code);
    setDisplayName(t.displayName);
    setSectionId(t.sectionId ?? (mySections.length === 1 ? mySections[0].sectionId : ""));
    setEditingSectionId(t.sectionId ?? null);
    setWorkflowType(t.workflowType || defaultWorkflowType);
    setEquationType(t.equationType || (t.workflowType === "HplcMethodAssay" ? "HplcMethodAssay" : t.workflowType === "IcpMethodAssay" ? "IcpMethodAssay" : t.workflowType === "Measurement" ? "Measurement" : t.workflowType === "Gravimetric" ? "GravimetricLoss" : t.workflowType === "Qualitative" ? "Qualitative" : t.workflowType === "Dissolution" ? "Dissolution" : t.workflowType === "Disintegration" ? "Disintegration" : t.workflowType === "WeightVariation" ? "WeightVariation" : t.workflowType === "Titration" ? "Titration" : "None"));
    setRequiresSystemSuitability((t.workflowType === "Dissolution" || t.workflowType === "HplcMethodAssay") ? true : (t.workflowType === "Disintegration" || t.workflowType === "WeightVariation" || t.workflowType === "IcpMethodAssay") ? false : !!t.requiresSystemSuitability);
    setMethodAbbreviation(t.methodAbbreviation ?? "");
    setHplcMethodId(t.hplcMethodId ?? "");
    setIcpMethodId(t.icpMethodId ?? "");
    setReplicateCount(t.replicateCount != null ? String(t.replicateCount) : "1");
    setEvaluationBasis(t.evaluationBasis || "Mean");
    setConditionFields(t.conditionFields || "");
    setUsesTare(!!t.usesTare);
    setTitration(titrationFormFromDefinition(t));
    setDissolutionS1Offset(t.dissolutionS1Offset != null ? String(t.dissolutionS1Offset) : "5");
    setDissolutionS2MinOffset(t.dissolutionS2MinOffset != null ? String(t.dissolutionS2MinOffset) : "15");
    setDissolutionS3MinOffset(t.dissolutionS3MinOffset != null ? String(t.dissolutionS3MinOffset) : "25");
    setDissolutionS3MaxBelowS2Min(t.dissolutionS3MaxBelowS2Min != null ? String(t.dissolutionS3MaxBelowS2Min) : "2");
    setDisintegrationStage1Units(t.disintegrationStage1Units != null ? String(t.disintegrationStage1Units) : "6");
    setDisintegrationStage2Units(t.disintegrationStage2Units != null ? String(t.disintegrationStage2Units) : "12");
    setDisintegrationMaxStage1Failures(t.disintegrationMaxStage1Failures != null ? String(t.disintegrationMaxStage1Failures) : "2");
    setDisintegrationMinPassTotal(t.disintegrationMinPassTotal != null ? String(t.disintegrationMinPassTotal) : "16");
    setWvUnitCount(t.wvUnitCount != null ? String(t.wvUnitCount) : "20");
    setWvTabletBand1MaxMg(t.wvTabletBand1MaxMg != null ? String(t.wvTabletBand1MaxMg) : "130");
    setWvTabletBand1Percent(t.wvTabletBand1Percent != null ? String(t.wvTabletBand1Percent) : "10");
    setWvTabletBand2MaxMg(t.wvTabletBand2MaxMg != null ? String(t.wvTabletBand2MaxMg) : "324");
    setWvTabletBand2Percent(t.wvTabletBand2Percent != null ? String(t.wvTabletBand2Percent) : "7.5");
    setWvTabletBand3Percent(t.wvTabletBand3Percent != null ? String(t.wvTabletBand3Percent) : "5");
    setWvTabletMaxOutside(t.wvTabletMaxOutside != null ? String(t.wvTabletMaxOutside) : "2");
    setWvCapsuleInnerPercent(t.wvCapsuleInnerPercent != null ? String(t.wvCapsuleInnerPercent) : "10");
    setWvCapsuleOuterPercent(t.wvCapsuleOuterPercent != null ? String(t.wvCapsuleOuterPercent) : "25");
    setWvCapsuleS1MaxOutside(t.wvCapsuleS1MaxOutside != null ? String(t.wvCapsuleS1MaxOutside) : "2");
    setWvCapsuleS1MaxForRetest(t.wvCapsuleS1MaxForRetest != null ? String(t.wvCapsuleS1MaxForRetest) : "6");
    setWvCapsuleS2ExtraUnits(t.wvCapsuleS2ExtraUnits != null ? String(t.wvCapsuleS2ExtraUnits) : "40");
    setWvCapsuleS2MaxOutside(t.wvCapsuleS2MaxOutside != null ? String(t.wvCapsuleS2MaxOutside) : "6");
    setPhyschemArea(t.physchemArea ?? (area ? defaultAreaFor(area) : null));
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setEditingTest(null);
    setPhyschemArea(null);
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
  };

  const save = async () => {
    setDialogError(null);
    setMessage(null);

    const trimmedCode = code.trim();
    const trimmedDisplayName = displayName.trim();

    if (!trimmedCode || !trimmedDisplayName) {
      setDialogError("Both Code and Display Name are required.");
      return;
    }
    if (sectionId === "") {
      setDialogError("Laboratory section is required.");
      return;
    }

    const chosenSectionId = Number(sectionId);
    const isDissolution = workflowType === "Dissolution";

    if (isDissolution) {
      const trimmedAbbr = methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        setDialogError("Method abbreviation is required when system suitability is enabled.");
        return;
      }
      if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        setDialogError("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        return;
      }
    }

    if (isDissolution) {
      const s1 = dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5;
      const s2 = dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15;
      const s3 = dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25;
      const maxBelow = dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2;

      if (isNaN(s1) || s1 < 0 || isNaN(s2) || s2 < 0 || isNaN(s3) || s3 < 0 || isNaN(maxBelow) || maxBelow < 0) {
        setDialogError("Dissolution stage offsets must be greater than or equal to zero.");
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    const isDisintegration = workflowType === "Disintegration";
    const isWeightVariation = workflowType === "WeightVariation";

    if (isDisintegration) {
      const s1 = disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6;
      const s2 = disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12;
      const maxFail = disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2;
      const minPass = disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16;

      if (isNaN(s1) || s1 < 1 || isNaN(s2) || s2 < 1) {
        setDialogError("Disintegration stage units must be greater than or equal to 1.");
        return;
      }
      if (isNaN(maxFail) || maxFail < 0 || maxFail >= s1) {
        setDialogError(`Disintegration maximum Stage 1 failures must be between 0 and ${s1 - 1}.`);
        return;
      }
      if (isNaN(minPass) || minPass < 1 || minPass > (s1 + s2)) {
        setDialogError(`Disintegration minimum pass total must be between 1 and ${s1 + s2}.`);
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    if (isWeightVariation) {
      const uCount = wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20;
      const b1Mg = wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130;
      const b1Pct = wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10;
      const b2Mg = wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324;
      const b2Pct = wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5;
      const b3Pct = wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5;
      const tabMaxOut = wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2;

      const capInner = wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10;
      const capOuter = wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25;
      const capS1Out = wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2;
      const capS1Retest = wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6;
      const capS2Extra = wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40;
      const capS2Out = wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6;

      if (isNaN(uCount) || uCount < 1 || isNaN(capS2Extra) || capS2Extra < 1) {
        setDialogError("Weight variation unit count and extra units must be greater than or equal to 1.");
        return;
      }
      if (isNaN(tabMaxOut) || tabMaxOut < 0 || isNaN(capS1Out) || capS1Out < 0 || isNaN(capS2Out) || capS2Out < 0) {
        setDialogError("Weight variation maximum outside counts must be greater than or equal to 0.");
        return;
      }
      if (isNaN(b1Mg) || b1Mg <= 0 || isNaN(b2Mg) || b2Mg <= 0) {
        setDialogError("Weight variation tablet band weight limits must be greater than zero.");
        return;
      }
      if (isNaN(b1Pct) || b1Pct <= 0 || isNaN(b2Pct) || b2Pct <= 0 || isNaN(b3Pct) || b3Pct <= 0 || isNaN(capInner) || capInner <= 0 || isNaN(capOuter) || capOuter <= 0) {
        setDialogError("Weight variation percentages must be greater than zero.");
        return;
      }
      if (b1Mg >= b2Mg) {
        setDialogError("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
        return;
      }
      if (capInner >= capOuter) {
        setDialogError("Weight variation capsule inner percentage must be less than outer percentage.");
        return;
      }
      if (capS1Out >= capS1Retest || capS1Retest > uCount) {
        setDialogError("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
        return;
      }
      if (capS2Out >= uCount + capS2Extra) {
        setDialogError(`Weight variation capsule Stage 2 max outside must be less than total units (${uCount + capS2Extra}).`);
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    const isMeasurement = workflowType === "Measurement";
    const isGravimetric = workflowType === "Gravimetric";
    const isQualitative = workflowType === "Qualitative";
    const isHplcMethodAssay = workflowType === "HplcMethodAssay";
    const isIcpMethodAssay = workflowType === "IcpMethodAssay";
    const isTitration = workflowType === "Titration";

    if (isTitration) {
      const hint = validateTitrationForm(titration);
      if (hint) {
        setDialogError(hint);
        return;
      }
    }

    const reasonCheck = editingId
      ? titrationChangeReasonCheck(editingTest, titration, workflowType, changeReason, isFp ? physchemArea : null)
      : { required: false, error: null, payload: null };
    setChangeReasonError(reasonCheck.error);
    if (reasonCheck.error) return;

    if (isHplcMethodAssay) {
      if (!hplcMethodId) {
        setDialogError("HPLC method is required for HPLC method assay tests.");
        return;
      }
    }

    if (isIcpMethodAssay) {
      if (!icpMethodId) {
        setDialogError("ICP method is required for ICP method assay tests.");
        return;
      }
    }

    if (isMeasurement) {
      const rep = Number(replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        setDialogError("Replicate count must be between 1 and 30 for Measurement tests.");
        return;
      }
      if (!evaluationBasis) {
        setDialogError("Evaluation basis is required for Measurement tests.");
        return;
      }
    }

    if (isGravimetric) {
      const rep = Number(replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        setDialogError("Replicate count must be between 1 and 30 for Gravimetric tests.");
        return;
      }
      if (equationType !== "GravimetricLoss" && equationType !== "GravimetricResidue") {
        setDialogError("Equation type must be GravimetricLoss or GravimetricResidue for Gravimetric tests.");
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    setSaving(true);
    try {
      const resolvedEquationType = isHplcMethodAssay
        ? "HplcMethodAssay"
        : isIcpMethodAssay
        ? "IcpMethodAssay"
        : isMeasurement
        ? "Measurement"
        : isGravimetric
        ? equationType
        : isQualitative
        ? "Qualitative"
        : isDissolution
        ? "Dissolution"
        : isDisintegration
        ? "Disintegration"
        : isWeightVariation
        ? "WeightVariation"
        : isTitration
        ? "Titration"
        : "None";

      if (editingId) {
        const payload: UpdateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: resolvedEquationType,
          requiresSystemSuitability: (isDissolution || isHplcMethodAssay) ? true : false,
          methodAbbreviation: isDissolution || isHplcMethodAssay || isIcpMethodAssay ? methodAbbreviation.trim().toUpperCase() : null,
          replicateCount: isTitration ? Number(titration.replicateCount) : (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
          evaluationBasis: isMeasurement ? evaluationBasis : null,
          conditionFields: (isGravimetric || isDissolution || isDisintegration || isWeightVariation) ? (conditionFields.trim() || null) : null,
          usesTare: isGravimetric ? usesTare : null,
          dissolutionS1Offset: isDissolution ? (dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5) : null,
          dissolutionS2MinOffset: isDissolution ? (dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15) : null,
          dissolutionS3MinOffset: isDissolution ? (dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25) : null,
          dissolutionS3MaxBelowS2Min: isDissolution ? (dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2) : null,
          disintegrationStage1Units: isDisintegration ? (disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6) : null,
          disintegrationStage2Units: isDisintegration ? (disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12) : null,
          disintegrationMaxStage1Failures: isDisintegration ? (disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2) : null,
          disintegrationMinPassTotal: isDisintegration ? (disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16) : null,
          wvUnitCount: isWeightVariation ? (wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20) : null,
          wvTabletBand1MaxMg: isWeightVariation ? (wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130) : null,
          wvTabletBand1Percent: isWeightVariation ? (wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10) : null,
          wvTabletBand2MaxMg: isWeightVariation ? (wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324) : null,
          wvTabletBand2Percent: isWeightVariation ? (wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5) : null,
          wvTabletBand3Percent: isWeightVariation ? (wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5) : null,
          wvTabletMaxOutside: isWeightVariation ? (wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2) : null,
          wvCapsuleInnerPercent: isWeightVariation ? (wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10) : null,
          wvCapsuleOuterPercent: isWeightVariation ? (wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25) : null,
          wvCapsuleS1MaxOutside: isWeightVariation ? (wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2) : null,
          wvCapsuleS1MaxForRetest: isWeightVariation ? (wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6) : null,
          wvCapsuleS2ExtraUnits: isWeightVariation ? (wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40) : null,
          wvCapsuleS2MaxOutside: isWeightVariation ? (wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6) : null,
          ...titrationPayloadFields(titration, isTitration),
          physchemArea: isFp ? physchemArea ?? defaultAreaFor(area ?? "fp") : null,
          changeReason: reasonCheck.payload,
          hplcMethodId: (isHplcMethodAssay || workflowType === "Dissolution") && hplcMethodId !== "" ? Number(hplcMethodId) : null,
          icpMethodId: isIcpMethodAssay && icpMethodId !== "" ? Number(icpMethodId) : null
        };
        await update(editingId, payload);
        setMessage({ text: `Test "${trimmedCode}" updated.`, ok: true });
      } else {
        const payload: CreateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: resolvedEquationType,
          requiresSystemSuitability: (isDissolution || isHplcMethodAssay) ? true : false,
          methodAbbreviation: isDissolution || isHplcMethodAssay || isIcpMethodAssay ? methodAbbreviation.trim().toUpperCase() : null,
          replicateCount: isTitration ? Number(titration.replicateCount) : (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
          evaluationBasis: isMeasurement ? evaluationBasis : null,
          conditionFields: (isGravimetric || isDissolution || isDisintegration || isWeightVariation) ? (conditionFields.trim() || null) : null,
          usesTare: isGravimetric ? usesTare : null,
          dissolutionS1Offset: isDissolution ? (dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5) : null,
          dissolutionS2MinOffset: isDissolution ? (dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15) : null,
          dissolutionS3MinOffset: isDissolution ? (dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25) : null,
          dissolutionS3MaxBelowS2Min: isDissolution ? (dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2) : null,
          disintegrationStage1Units: isDisintegration ? (disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6) : null,
          disintegrationStage2Units: isDisintegration ? (disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12) : null,
          disintegrationMaxStage1Failures: isDisintegration ? (disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2) : null,
          disintegrationMinPassTotal: isDisintegration ? (disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16) : null,
          wvUnitCount: isWeightVariation ? (wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20) : null,
          wvTabletBand1MaxMg: isWeightVariation ? (wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130) : null,
          wvTabletBand1Percent: isWeightVariation ? (wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10) : null,
          wvTabletBand2MaxMg: isWeightVariation ? (wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324) : null,
          wvTabletBand2Percent: isWeightVariation ? (wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5) : null,
          wvTabletBand3Percent: isWeightVariation ? (wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5) : null,
          wvTabletMaxOutside: isWeightVariation ? (wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2) : null,
          wvCapsuleInnerPercent: isWeightVariation ? (wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10) : null,
          wvCapsuleOuterPercent: isWeightVariation ? (wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25) : null,
          wvCapsuleS1MaxOutside: isWeightVariation ? (wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2) : null,
          wvCapsuleS1MaxForRetest: isWeightVariation ? (wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6) : null,
          wvCapsuleS2ExtraUnits: isWeightVariation ? (wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40) : null,
          wvCapsuleS2MaxOutside: isWeightVariation ? (wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6) : null,
          ...titrationPayloadFields(titration, isTitration),
          physchemArea: isFp ? physchemArea ?? defaultAreaFor(area ?? "fp") : null,
          hplcMethodId: (isHplcMethodAssay || workflowType === "Dissolution") && hplcMethodId !== "" ? Number(hplcMethodId) : null,
          icpMethodId: isIcpMethodAssay && icpMethodId !== "" ? Number(icpMethodId) : null
        };
        await addNew(payload);
        setMessage({ text: `Test "${trimmedCode}" added to the Test Master.`, ok: true });
      }
      closeDialog();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setDialogError(err?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this test.`);
    } finally {
      setSaving(false);
    }
  };

  const toggleFreeze = async (t: TestDefinitionOption) => {
    setMessage(null);
    try {
      await setActive(t.id, !t.isActive);
      setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setMessage({ text: err?.response?.data?.message ?? "Could not update this test's status.", ok: false });
    }
  };

  const columns: RegisterColumn<TestDefinitionOption>[] = [
    { key: "code", label: "Code", sortable: true, render: (t) => <span style={{ fontFamily: monospaceFontFamily }}>{t.code}</span> },
    {
      key: "displayName",
      label: "Display Name",
      sortable: true,
      render: (t) => (
                    <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                      <span>{t.displayName}</span>
                      {t.workflowType === "HplcMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="HPLC Assay"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "IcpMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="ICP Assay"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Dissolution" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Dissolution"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Disintegration" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Disintegration"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "WeightVariation" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Weight Variation"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.requiresSystemSuitability && (
                        <Tooltip
                          title={[
                            t.sstMaxRsdPercent != null ? `Max RSD: ${t.sstMaxRsdPercent}%` : null,
                            t.sstMinResolution != null ? `Min Res: ${t.sstMinResolution}` : null,
                            t.sstMaxTailingFactor != null ? `Max Tailing: ${t.sstMaxTailingFactor}` : null,
                            t.sstMinTheoreticalPlates != null ? `Min Plates: ${t.sstMinTheoreticalPlates}` : null
                          ].filter(Boolean).join(" | ") || "System suitability required"}
                        >
                          <Chip
                            size="small"
                            color="info"
                            variant="outlined"
                            label={`SST: ${t.methodAbbreviation ?? "Required"}`}
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                    </Stack>
      )
    },
    { key: "section", label: "Section", sortable: true, sortValue: (t) => t.section?.name ?? "", render: (t) => t.section?.name ?? "—" },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (t) => (t.isActive ? "Active" : "Frozen"),
      render: (t) => <StatusBadge status={t.isActive ? "Active" : "Frozen"} />
    }
  ];

  const q = search.trim().toLowerCase();
  const visibleOptions = q
    ? options.filter((t) => t.code.toLowerCase().includes(q) || t.displayName.toLowerCase().includes(q) || (t.section?.name ?? "").toLowerCase().includes(q))
    : options;
  const detailsTest = expandedId !== null ? options.find((t) => t.id === expandedId) ?? null : null;

  const isDissolutionMethod = workflowType === "Dissolution";

  return (
    <>
      <LabPage
        title={isFp ? (area === "rmpm" ? "RM & PM Test Master" : "FP Test Master") : "Microbiology Test Master"}
        subtitle={isFp
          ? "Physicochemical tests: HPLC methods, equation type and system suitability criteria."
          : "Microbiology tests available to assign to Items, Sampling Points, Rooms, and Machine Parts."}
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>}
        filters={
          <FilterBar
            search={search}
            onSearch={setSearch}
            placeholder="Search code, name, section"
            resultCount={visibleOptions.length}
            onRefresh={() => { void reload(); }}
          />
        }
      >
        {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
        {sectionsState === "failed" && (
          <LoadErrorAlert
            message="The laboratory sections could not be loaded, so the tests cannot be sorted into this laboratory and none are shown."
            onRetry={() => setSectionsReloadKey((k) => k + 1)}
          />
        )}
        <RegisterTable
          columns={columns}
          rows={visibleOptions}
          getRowId={(t) => t.id}
          loading={sectionsState === "loading"}
          onRowClick={(t) => setExpandedId(t.id)}
          rowActions={(t) => [
            { label: "Details & Workflow Steps", onClick: () => setExpandedId(t.id) },
            { label: "Edit", onClick: () => startEdit(t) },
            { label: t.isActive ? "Freeze" : "Unfreeze", onClick: () => { void toggleFreeze(t); }, danger: t.isActive }
          ]}
          empty={{
            title: "No tests found",
            description: q ? "No test matches your search." : "Add a test to get started.",
            action: q ? undefined : <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>
          }}
        />
      </LabPage>

      <FloatingDialog
        open={detailsTest !== null}
        title={detailsTest ? `Details & Workflow Steps: ${detailsTest.code}` : ""}
        onClose={() => setExpandedId(null)}
        maxWidth="lg"
      >
        {detailsTest && (
          <WorkflowStepsSection test={detailsTest} workflowTypes={workflowTypes} onWorkflowTypeChanged={reload} />
        )}
      </FloatingDialog>

      <FloatingDialog
        open={dialogOpen}
        title={editingId ? "Edit Test" : "Add Test"}
        onClose={closeDialog}
        maxWidth="md"
        actions={
          <>
            <Button onClick={closeDialog} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button variant="contained" onClick={save} disabled={saving}>
              {saving ? "Saving…" : editingId ? "Save Changes" : "Add Test"}
            </Button>
          </>
        }
      >
        <LoadFailuresAlert failed={dialogListFailures} retryHint="Reload the page to try again." sx={{ mb: 2 }} />
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}

        <Stack spacing={2} sx={{ pt: 1 }}>
          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <TextField
              size="small"
              label="Code"
              placeholder="e.g. HPLC_VITC"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              sx={{ flex: 1, minWidth: 200 }}
            />
            <TextField
              size="small"
              label="Display Name"
              placeholder="e.g. Vitamin C Assay"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              sx={{ flex: 1.5, minWidth: 240 }}
            />
          </Stack>

          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required>
              <InputLabel id="test-section-select-label">Section</InputLabel>
              <Select<number | "">
                labelId="test-section-select-label"
                label="Section"
                value={sectionId}
                onChange={(e) => setSectionId(e.target.value === "" ? "" : Number(e.target.value))}
                inputProps={{ "aria-label": "Section" }}
              >
                {mySections.length > 1 && <MenuItem value=""><em>Select Section</em></MenuItem>}
                {editingSectionId !== null && !mySections.some((s) => s.sectionId === editingSectionId) && (
                  <MenuItem value={editingSectionId}>
                    {editingTest?.section?.name ?? `Section #${editingSectionId}`} (Current)
                  </MenuItem>
                )}
                {mySections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName} ({s.departmentName})
                  </MenuItem>
                ))}
              </Select>
              {editingId && editingSectionId !== null && sectionId !== "" && sectionId !== editingSectionId && (
                <FormHelperText>Existing test orders keep their current section.</FormHelperText>
              )}
            </FormControl>

            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }}>
              <InputLabel id="dialog-workflow-type-label">Workflow Type</InputLabel>
              <Select
                labelId="dialog-workflow-type-label"
                label="Workflow Type"
                value={workflowType}
                onChange={(e) => {
                  const next = e.target.value;
                  setWorkflowType(next);
                  if (next === "HplcMethodAssay") {
                    setEquationType("HplcMethodAssay");
                    setRequiresSystemSuitability(true);
                  } else if (next === "IcpMethodAssay") {
                    setEquationType("IcpMethodAssay");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Measurement") {
                    setEquationType("Measurement");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Gravimetric") {
                    if (equationType !== "GravimetricLoss" && equationType !== "GravimetricResidue") {
                      setEquationType("GravimetricLoss");
                    }
                    setRequiresSystemSuitability(false);
                  } else if (next === "Qualitative") {
                    setEquationType("Qualitative");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Dissolution") {
                    setEquationType("Dissolution");
                    setRequiresSystemSuitability(true);
                  } else if (next === "Disintegration") {
                    setEquationType("Disintegration");
                    setRequiresSystemSuitability(false);
                  } else if (next === "WeightVariation") {
                    setEquationType("WeightVariation");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Titration") {
                    setEquationType("Titration");
                    setRequiresSystemSuitability(false);
                  } else {
                    setEquationType("None");
                    setRequiresSystemSuitability(false);
                  }
                }}
              >
                {workflowTypes.map((w) => (
                  <MenuItem key={w} value={w}>
                    {WORKFLOW_TYPE_LABELS[w] ?? w}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          {isFp && (
            <FormControl size="small" fullWidth>
              <InputLabel id="dialog-equation-type-label">Equation Type</InputLabel>
              <Select
                labelId="dialog-equation-type-label"
                label="Equation Type"
                value={equationType}
                onChange={(e) => {
                  const next = e.target.value;
                  setEquationType(next);
                  if (next === "HplcMethodAssay") {
                    setWorkflowType("HplcMethodAssay");
                    setRequiresSystemSuitability(true);
                  } else if (next === "IcpMethodAssay") {
                    setWorkflowType("IcpMethodAssay");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Disintegration") {
                    setWorkflowType("Disintegration");
                    setRequiresSystemSuitability(false);
                  } else if (next === "WeightVariation") {
                    setWorkflowType("WeightVariation");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Dissolution") {
                    setWorkflowType("Dissolution");
                    setRequiresSystemSuitability(true);
                  }
                }}
              >
                {EQUATION_TYPES.filter((eq) => {
                  if (workflowType === "HplcMethodAssay") {
                    return eq === "HplcMethodAssay";
                  }
                  if (workflowType === "IcpMethodAssay") {
                    return eq === "IcpMethodAssay";
                  }
                  if (workflowType === "Disintegration") {
                    return eq === "Disintegration";
                  }
                  if (workflowType === "WeightVariation") {
                    return eq === "WeightVariation";
                  }
                  if (workflowType === "Titration") {
                    return eq === "Titration";
                  }
                  if (workflowType === "Dissolution") {
                    return eq === "Dissolution";
                  }
                  if (workflowType === "Measurement") {
                    return eq === "Measurement";
                  }
                  if (workflowType === "Gravimetric") {
                    return eq === "GravimetricLoss" || eq === "GravimetricResidue";
                  }
                  if (workflowType === "Qualitative") {
                    return eq === "Qualitative";
                  }
                  return eq !== "Measurement" && eq !== "GravimetricLoss" && eq !== "GravimetricResidue" && eq !== "Qualitative" && eq !== "Dissolution" && eq !== "Disintegration" && eq !== "WeightVariation" && eq !== "Titration" && eq !== "HplcMethodAssay" && eq !== "IcpMethodAssay";
                }).map((eq) => (
                  <MenuItem key={eq} value={eq}>
                    {EQUATION_TYPE_LABELS[eq] ?? eq}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {(workflowType === "HplcMethodAssay" || workflowType === "Dissolution") && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                {isDissolutionMethod ? "HPLC Method (supplies the dissolution standard)" : "HPLC Method Assay Configuration"}
              </Typography>
              <Stack spacing={2}>
                <FormControl size="small" fullWidth required={!isDissolutionMethod}>
                  <InputLabel id="dialog-hplc-method-label">{isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}</InputLabel>
                  <Select<number | "">
                    labelId="dialog-hplc-method-label"
                    label={isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}
                    value={hplcMethodId}
                    onChange={(e) => {
                      const val = e.target.value === "" ? "" : Number(e.target.value);
                      setHplcMethodId(val);
                      const chosen = hplcMethods.find((m) => m.id === val);
                      if (chosen && workflowType === "HplcMethodAssay") {
                        setMethodAbbreviation(chosen.abbreviation);
                      }
                    }}
                    inputProps={{ "aria-label": "HPLC Method" }}
                  >
                    <MenuItem value=""><em>Select HPLC Method</em></MenuItem>
                    {hplcMethods
                      .filter((m) => m.isActive || m.id === hplcMethodId)
                      .map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.name} ({m.abbreviation}){!m.isActive ? " (Inactive)" : ""}
                        </MenuItem>
                      ))}
                  </Select>
                  <FormHelperText>Select the active master HPLC method that defines this test&apos;s chromatographic conditions and analytes.</FormHelperText>
                </FormControl>

                <TextField
                  size="small"
                  label="Method Abbreviation"
                  value={methodAbbreviation}
                  slotProps={{
                    input: {
                      readOnly: true,
                    },
                  }}
                  helperText={workflowType === "Dissolution" ? "Dissolution keeps its own abbreviation." : "Auto-populated from the selected HPLC method (read-only)."}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "IcpMethodAssay" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                ICP Method Assay Configuration
              </Typography>
              <Stack spacing={2}>
                <FormControl size="small" fullWidth required>
                  <InputLabel id="dialog-icp-method-label">ICP Method *</InputLabel>
                  <Select<number | "">
                    labelId="dialog-icp-method-label"
                    label="ICP Method *"
                    value={icpMethodId}
                    onChange={(e) => {
                      const val = e.target.value === "" ? "" : Number(e.target.value);
                      setIcpMethodId(val);
                      const chosen = icpMethods.find((m) => m.id === val);
                      if (chosen) {
                        setMethodAbbreviation(chosen.abbreviation);
                      }
                    }}
                    inputProps={{ "aria-label": "ICP Method" }}
                  >
                    <MenuItem value=""><em>Select ICP Method</em></MenuItem>
                    {icpMethods
                      .filter((m) => m.isActive || m.id === icpMethodId)
                      .map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.name} ({m.abbreviation}){!m.isActive ? " (Inactive)" : ""}
                        </MenuItem>
                      ))}
                  </Select>
                  <FormHelperText>Select the active master ICP method that defines this test&apos;s elements and calibration standards.</FormHelperText>
                </FormControl>

                <TextField
                  size="small"
                  label="Method Abbreviation"
                  value={methodAbbreviation}
                  slotProps={{
                    input: {
                      readOnly: true,
                    },
                  }}
                  helperText="Auto-populated from the selected ICP method (read-only)."
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Measurement" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Measurement Settings
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Replicate Count *"
                  value={replicateCount}
                  onChange={(e) => setReplicateCount(e.target.value)}
                  slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                  helperText="Replicates per parameter (1–30)"
                  required
                  sx={{ flex: 1 }}
                />
                <FormControl size="small" sx={{ flex: 1 }} required>
                  <InputLabel id="dialog-eval-basis-label">Evaluation Basis *</InputLabel>
                  <Select
                    labelId="dialog-eval-basis-label"
                    label="Evaluation Basis *"
                    value={evaluationBasis}
                    onChange={(e) => setEvaluationBasis(e.target.value as "Mean" | "EachValue" | "Min" | "Max")}
                  >
                    <MenuItem value="Mean">Mean</MenuItem>
                    <MenuItem value="EachValue">Each Value</MenuItem>
                    <MenuItem value="Min">Minimum</MenuItem>
                    <MenuItem value="Max">Maximum</MenuItem>
                  </Select>
                </FormControl>
              </Stack>
            </Box>
          )}

          {workflowType === "Gravimetric" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Gravimetric Settings
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Replicate Count *"
                    value={replicateCount}
                    onChange={(e) => setReplicateCount(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                    helperText="Replicates per parameter (1–30)"
                    required
                    sx={{ flex: 1 }}
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        checked={usesTare}
                        onChange={(e) => setUsesTare(e.target.checked)}
                      />
                    }
                    label="Uses tare (container weight)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="e.g. Temperature (°C),Time (h)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Temperature (°C),Time (h)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Titration" && (
            <TitrationConfigSection form={titration} onChange={setTitration} sectionId={sectionId} />
          )}

          {isFp && area && <TestAreaField pageArea={area} value={physchemArea} onChange={setPhyschemArea} />}

          {editingId && hasTitrationSettingsChanged(editingTest, titration, workflowType, isFp ? physchemArea : null) && (
            <TitrationChangeReasonField value={changeReason} onChange={setChangeReason} error={changeReasonError} />
          )}

          {workflowType === "Qualitative" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary" }}>
                Qualitative test: records compliance observation against specifications directly (no replicates or steps).
              </Typography>
            </Box>
          )}

          {workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Dissolution Acceptance Offsets (USP &lt;711&gt; / EP 2.9.3)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S1: each unit ≥ Q + "
                    value={dissolutionS1Offset}
                    onChange={(e) => setDissolutionS1Offset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 5 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S2: no unit < Q − "
                    value={dissolutionS2MinOffset}
                    onChange={(e) => setDissolutionS2MinOffset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 15 (%)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S3: no unit < Q − "
                    value={dissolutionS3MinOffset}
                    onChange={(e) => setDissolutionS3MinOffset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 25 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S3: max units < Q − S2 value"
                    value={dissolutionS3MaxBelowS2Min}
                    onChange={(e) => setDissolutionS3MaxBelowS2Min(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (units)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Disintegration" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 1 Units"
                    value={disintegrationStage1Units}
                    onChange={(e) => setDisintegrationStage1Units(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 6"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 2 Units"
                    value={disintegrationStage2Units}
                    onChange={(e) => setDisintegrationStage2Units(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 12"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Max S1 Failures"
                    value={disintegrationMaxStage1Failures}
                    onChange={(e) => setDisintegrationMaxStage1Failures(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (0..S1-1)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Min Pass Total"
                    value={disintegrationMinPassTotal}
                    onChange={(e) => setDisintegrationMinPassTotal(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 16 (1..S1+S2)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Medium,Temperature (°C),Discs"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Medium,Temperature (°C),Discs"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "WeightVariation" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Weight Variation Acceptance (USP &lt;2091&gt;)
              </Typography>
              <Stack spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Unit Count (Stage 1)"
                  value={wvUnitCount}
                  onChange={(e) => setWvUnitCount(e.target.value)}
                  slotProps={{ htmlInput: { min: 1, step: 1 } }}
                  helperText="Default: 20"
                  sx={{ maxWidth: 220 }}
                />

                {/* Group: Tablets */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Tablets
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Max (mg)"
                        value={wvTabletBand1MaxMg}
                        onChange={(e) => setWvTabletBand1MaxMg(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 130"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Deviation (%)"
                        value={wvTabletBand1Percent}
                        onChange={(e) => setWvTabletBand1Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Max (mg)"
                        value={wvTabletBand2MaxMg}
                        onChange={(e) => setWvTabletBand2MaxMg(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 324"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Deviation (%)"
                        value={wvTabletBand2Percent}
                        onChange={(e) => setWvTabletBand2Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 7.5"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 3 Deviation (%)"
                        value={wvTabletBand3Percent}
                        onChange={(e) => setWvTabletBand3Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 5 (> Band 2)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Max Outside Count"
                        value={wvTabletMaxOutside}
                        onChange={(e) => setWvTabletMaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                {/* Group: Capsules */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Capsules
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Inner Limit (%)"
                        value={wvCapsuleInnerPercent}
                        onChange={(e) => setWvCapsuleInnerPercent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Outer Limit (%)"
                        value={wvCapsuleOuterPercent}
                        onChange={(e) => setWvCapsuleOuterPercent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 25"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max Outside"
                        value={wvCapsuleS1MaxOutside}
                        onChange={(e) => setWvCapsuleS1MaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2 (pass without retest)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max For Retest"
                        value={wvCapsuleS1MaxForRetest}
                        onChange={(e) => setWvCapsuleS1MaxForRetest(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (qualifies for S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Extra Units"
                        value={wvCapsuleS2ExtraUnits}
                        onChange={(e) => setWvCapsuleS2ExtraUnits(e.target.value)}
                        slotProps={{ htmlInput: { min: 1, step: 1 } }}
                        helperText="Default: 40"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Max Outside"
                        value={wvCapsuleS2MaxOutside}
                        onChange={(e) => setWvCapsuleS2MaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (across S1+S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Balance ID,Temperature (°C)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Balance ID,Temperature (°C)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={<Switch checked={requiresSystemSuitability} disabled />}
                label="Requires system suitability (standard from the assigned HPLC run)"
              />

              <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
                <TextField
                  size="small"
                  label="Method Abbreviation"
                  placeholder="e.g. VIT-C"
                  value={methodAbbreviation}
                  onChange={(e) => setMethodAbbreviation(e.target.value.toUpperCase())}
                  required
                  fullWidth
                  slotProps={{
                    htmlInput: {
                      maxLength: 20
                    }
                  }}
                  helperText="1–20 uppercase alphanumeric characters or hyphens (auto-uppercased)"
                />
              </Box>
            </Box>
          )}

          {/* Titration uses its own replicate count, not replicates per stage. */}
          {workflowType !== "Titration" && (isFp || (fpSectionId !== null && (sectionId === fpSectionId || editingTest?.sectionId === fpSectionId))) && (
            editingId ? (
              <TestStageReplicatesSection testDefinitionId={editingId} />
            ) : (
              <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 0.5 }}>
                  Replicates per stage
                </Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Save this test first to configure replicates per stage.
                </Typography>
              </Box>
            )
          )}
        </Stack>
      </FloatingDialog>
    </>
  );
}
