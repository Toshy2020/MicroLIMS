import { useEffect, useRef, useState } from "react";
import { TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Box, Typography, Checkbox, FormControlLabel, Chip, Tooltip, TableContainer } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ContentCopyOutlinedIcon from "@mui/icons-material/ContentCopyOutlined";
import DeleteIcon from "@mui/icons-material/Delete";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useTranslation } from "react-i18next";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import { MediaProductPicker } from "../../../../components/MediaProductPicker";
import { useMediaProducts } from "../../../../hooks/useMediaProducts";
import { masterDataOptions, incubationConditionLabel, MediaIncubationConditionOption } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import { EQUATION_TYPE_LABELS, PHENOTYPIC_TEST_TYPES, PHENOTYPIC_TEST_TYPE_LABELS, STEP_TYPES, STEP_TYPES_REQUIRING_ORGANISM, STEP_TYPES_SINGLE_MEDIA, STEP_TYPES_WITH_NO_MEDIA, StepFormState, StepMediaRow, WORKFLOW_TYPE_LABELS, WorkflowStepMediaItem, defaultStepForm, stage1Ranges, stepNeedsConfiguration, validateStepForm } from "./testMasterConstants";
import type { DraftWorkflowStep } from "../testWizard/testFormModel";

export interface WorkflowStepIncubationStage {
  stageNumber: number;
  tempMin?: number;
  tempMax?: number;
  incubationMinHours?: number;
  incubationMaxHours?: number;
}

export interface WorkflowStepItem {
  id: number;
  stepName: string;
  stepOrder: number;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId?: number | null;
  targetOrganism?: { name: string } | null;
  requiresIncubationTransfer?: boolean;
  version?: number;
  stepMedia?: WorkflowStepMediaItem[];
  incubationStages?: WorkflowStepIncubationStage[];
  phenotypicTestType?: string | null;
  phenotypicTestTypes?: string[];
}

// Maps a saved step to the step form's state; shared by edit and duplicate.
function stepToFormState(s: WorkflowStepItem): StepFormState {
  const stage2 = (s.incubationStages ?? []).find((x) => x.stageNumber === 2);
  return {
    stepName: s.stepName, isFinalStep: s.isFinalStep, stepType: s.stepType,
    targetOrganismId: s.targetOrganismId ?? null,
    stepMedia: (s.stepMedia ?? []).map((m) => ({
      mediaProductId: m.mediaProductId,
      mediaIncubationConditionId: typeof m.mediaIncubationConditionId === "number" ? m.mediaIncubationConditionId : "",
      isRequired: !!m.isRequired,
      displayOrder: typeof m.displayOrder === "number" ? m.displayOrder : 0
    })),
    requiresIncubationTransfer: !!s.requiresIncubationTransfer,
    stage2TempMin: stage2 ? String(stage2.tempMin) : undefined,
    stage2TempMax: stage2 ? String(stage2.tempMax) : undefined,
    stage2IncubationMinHours: stage2 ? stage2.incubationMinHours : undefined,
    stage2IncubationMaxHours: stage2 ? stage2.incubationMaxHours : undefined,
    phenotypicTestType: s.phenotypicTestType ?? null,
    phenotypicTestTypes: s.phenotypicTestTypes ?? []
  };
}

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
export function WorkflowStepsSection({
  test,
  workflowTypes,
  onWorkflowTypeChanged,
  draftSteps,
  onDraftStepsChange,
  onDraftWorkflowTypeChange
}: {
  test: TestDefinitionOption;
  workflowTypes: string[];
  onWorkflowTypeChanged: () => void;
  draftSteps?: DraftWorkflowStep[];
  onDraftStepsChange?: (steps: DraftWorkflowStep[]) => void;
  onDraftWorkflowTypeChange?: (workflowType: string) => void;
}) {
  const { t } = useTranslation("masterData");
  const isDraftMode = draftSteps !== undefined && onDraftStepsChange !== undefined;
  const [steps, setSteps] = useState<WorkflowStepItem[]>([]);
  const [organisms, setOrganisms] = useState<Array<{ id: number; scientificName: string; commonName?: string }>>([]);
  // Step media name a Culture Media product; the picker hides inactive ones except a product already on the row.
  const { options: mediaProducts } = useMediaProducts();
  const [conditions, setConditions] = useState<MediaIncubationConditionOption[]>([]);
  const [form, setForm] = useState<StepFormState>(defaultStepForm);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [editingDraftIndex, setEditingDraftIndex] = useState<number | null>(null);
  const [stepToDelete, setStepToDelete] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const stepNameInputRef = useRef<HTMLInputElement>(null);

  const loadSteps = () => (isDraftMode ? Promise.resolve() : masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps));
  useEffect(() => {
    masterDataOptions.getOrganisms().then(setOrganisms);
    masterDataOptions.getMediaIncubationConditions().then(setConditions);
    if (!isDraftMode) {
      loadSteps();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [test.id, isDraftMode]);

  const sortedDraftSteps = (draftSteps ?? []).slice().sort((a, b) => a.stepOrder - b.stepOrder);
  const effectiveSteps: WorkflowStepItem[] = isDraftMode
    ? sortedDraftSteps.map((s, idx) => {
        const org = s.targetOrganismId != null ? organisms.find((o) => o.id === s.targetOrganismId) : null;
        const orgName = org ? (org.commonName ? `${org.scientificName} (${org.commonName})` : org.scientificName) : s.targetOrganismName ?? undefined;
        return {
          id: s.id ?? -(idx + 1),
          stepName: s.stepName,
          stepOrder: s.stepOrder,
          isFinalStep: s.isFinalStep,
          stepType: s.stepType,
          targetOrganismId: s.targetOrganismId,
          targetOrganism: orgName ? { name: orgName } : null,
          requiresIncubationTransfer: s.requiresIncubationTransfer,
          version: s.version,
          stepMedia: s.stepMedia.map((m) => {
            const cond = m.mediaIncubationConditionId !== "" ? conditions.find((c) => c.id === m.mediaIncubationConditionId) : undefined;
            const prod = m.mediaProductId !== "" ? mediaProducts.find((p) => p.id === m.mediaProductId) : undefined;
            return {
              mediaProductId: Number(m.mediaProductId || 0),
              mediaProductName: m.mediaProductName ?? prod?.name,
              mediaIncubationConditionId: m.mediaIncubationConditionId,
              isRequired: m.isRequired,
              displayOrder: m.displayOrder,
              incubationMinHours: cond?.incubationMinHours ?? m.incubationMinHours,
              incubationMaxHours: cond?.incubationMaxHours ?? m.incubationMaxHours,
              tempMin: cond?.temperatureMin ?? m.tempMin,
              tempMax: cond?.temperatureMax ?? m.tempMax
            };
          }),
          incubationStages:
            s.requiresIncubationTransfer && s.stepType === "PlateCount"
              ? [
                  {
                    stageNumber: 2,
                    tempMin: Number(s.stage2TempMin),
                    tempMax: Number(s.stage2TempMax),
                    incubationMinHours: Number(s.stage2IncubationMinHours),
                    incubationMaxHours: Number(s.stage2IncubationMaxHours)
                  }
                ]
              : [],
          phenotypicTestType: s.phenotypicTestType ?? null,
          phenotypicTestTypes: s.phenotypicTestTypes ?? []
        };
      })
    : steps;

  const changeWorkflowType = async (workflowType: string) => {
    setError(null);
    if (isDraftMode && onDraftWorkflowTypeChange) {
      onDraftWorkflowTypeChange(workflowType);
      return;
    }
    try {
      await masterDataOptions.updateWorkflowType(test.id, workflowType, test.version);
      onWorkflowTypeChanged();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("testMaster.workflowSteps.errors.updateWorkflowTypeFailed"));
    }
  };

  const startEditStep = (s: WorkflowStepItem, index: number) => {
    setEditingStepId(s.id);
    setEditingDraftIndex(index);
    setForm(stepToFormState(s));
    setError(null);
  };

  // Prefills the add form from an existing step. Nothing is saved until
  // "Add Step"; a workflow may have only one final step, so the copy is
  // never final (TestWorkflowStepMasterDataService rejects a second one).
  const startDuplicateStep = (s: WorkflowStepItem) => {
    setEditingStepId(null);
    setEditingDraftIndex(null);
    setForm({ ...stepToFormState(s), stepName: t("testMaster.workflowSteps.copySuffix", { name: s.stepName }), isFinalStep: false });
    setError(null);
    stepNameInputRef.current?.focus();
  };

  const cancelEditStep = () => {
    setEditingStepId(null);
    setEditingDraftIndex(null);
    setForm(defaultStepForm());
  };

  const needsOrganism = STEP_TYPES_REQUIRING_ORGANISM.includes(form.stepType);
  const hasNoMedia = STEP_TYPES_WITH_NO_MEDIA.includes(form.stepType);
  const isSingleMedia = STEP_TYPES_SINGLE_MEDIA.includes(form.stepType);
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  // Switching StepType clears media/organism rather than carrying over a
  // combination that likely no longer satisfies that type's rules (e.g. a
  // Broth's single required medium isn't valid as-is for ConfirmatoryPlating,
  // which forbids IsRequired on every row) - forces a deliberate re-pick
  // instead of silently submitting a stale, mismatched configuration.
  const changeStepType = (stepType: string) => setForm({
    ...form, stepType, targetOrganismId: null, stepMedia: [], phenotypicTestType: null, phenotypicTestTypes: [],
    requiresIncubationTransfer: false, stage2TempMin: undefined, stage2TempMax: undefined,
    stage2IncubationMinHours: undefined, stage2IncubationMaxHours: undefined
  });

  const [pendingPhenotypicTest, setPendingPhenotypicTest] = useState("");
  const addPhenotypicTest = () => {
    if (!pendingPhenotypicTest || form.phenotypicTestTypes.includes(pendingPhenotypicTest)) return;
    setForm({ ...form, phenotypicTestTypes: [...form.phenotypicTestTypes, pendingPhenotypicTest] });
    setPendingPhenotypicTest("");
  };
  const removePhenotypicTest = (type: string) =>
    setForm({ ...form, phenotypicTestTypes: form.phenotypicTestTypes.filter((t) => t !== type) });

  const addMediaRow = () => setForm({
    ...form,
    stepMedia: [...form.stepMedia, {
      mediaProductId: "", mediaIncubationConditionId: "", isRequired: false, displayOrder: form.stepMedia.length
    }]
  });
  // A condition belongs to one media product, so changing the product
  // clears a condition that no longer fits.
  const updateMediaRow = (index: number, patch: Partial<StepMediaRow>) => setForm({
    ...form,
    stepMedia: form.stepMedia.map((m, i) => {
      if (i !== index) return m;
      const next = { ...m, ...patch };
      if (patch.mediaProductId !== undefined) {
        const matchesProduct = conditions.some(
          (c) => c.id === next.mediaIncubationConditionId && c.mediaProductId === next.mediaProductId
        );
        if (!matchesProduct) {
          next.mediaIncubationConditionId = "";
        }
      }
      return next;
    })
  });
  const removeMediaRow = (index: number) => setForm({ ...form, stepMedia: form.stepMedia.filter((_, i) => i !== index) });

  const saveStep = async () => {
    setError(null);
    if (!form.stepName || (isBiochemical && !form.phenotypicTestType && form.phenotypicTestTypes.length === 0)) {
      setError(isBiochemical ? t("testMaster.workflowSteps.errors.stepNameAndPhenotypicRequired") : t("testMaster.workflowSteps.errors.stepNameRequired"));
      return;
    }
    const validationError = validateStepForm(form, t);
    if (validationError) {
      setError(validationError);
      return;
    }
    const payload = {
      stepName: form.stepName,
      phenotypicTestType: isBiochemical ? form.phenotypicTestType ?? null : null,
      phenotypicTestTypes: isBiochemical ? form.phenotypicTestTypes : [],
      // No longer read at execution time - the picked medium's own window
      // is authoritative (see TestWorkflowEngine.cs). Kept on the request
      // shape only because the column itself isn't dropped yet.
      incubationMinHours: 0, incubationMaxHours: 0, temperatureMin: 0, temperatureMax: 0,
      isFinalStep: !!form.isFinalStep, stepType: form.stepType, targetOrganismId: form.targetOrganismId,
      stepMedia: form.stepMedia.map((m, i) => ({
        mediaProductId: Number(m.mediaProductId),
        mediaIncubationConditionId: m.mediaIncubationConditionId === "" ? null : Number(m.mediaIncubationConditionId),
        isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired,
        displayOrder: i
      })),
      requiresIncubationTransfer: form.stepType === "PlateCount" && !!form.requiresIncubationTransfer,
      incubationStages: (form.stepType === "PlateCount" && form.requiresIncubationTransfer) ? [
        {
          stageNumber: 2,
          tempMin: Number(form.stage2TempMin),
          tempMax: Number(form.stage2TempMax),
          incubationMinHours: Number(form.stage2IncubationMinHours),
          incubationMaxHours: Number(form.stage2IncubationMaxHours)
        }
      ] : []
    };
    if (isDraftMode && onDraftStepsChange) {
      if (form.isFinalStep && sortedDraftSteps.some((s, idx) => s.isFinalStep && idx !== editingDraftIndex)) {
        setError(t("testMaster.workflowSteps.errors.onlyOneFinalStep"));
        return;
      }
      const org = form.targetOrganismId != null ? organisms.find((o) => o.id === form.targetOrganismId) : null;
      const targetOrganismName = org ? (org.commonName ? `${org.scientificName} (${org.commonName})` : org.scientificName) : null;
      const existing = editingDraftIndex !== null ? sortedDraftSteps[editingDraftIndex] : undefined;
      const draftEntry: DraftWorkflowStep = {
        id: existing?.id,
        version: existing?.version,
        stepOrder: existing ? existing.stepOrder : sortedDraftSteps.length + 1,
        stepName: form.stepName.trim(),
        isFinalStep: !!form.isFinalStep,
        stepType: form.stepType,
        targetOrganismId: form.targetOrganismId,
        targetOrganismName,
        stepMedia: form.stepMedia.map((m, i) => {
          const cond = m.mediaIncubationConditionId !== "" ? conditions.find((c) => c.id === m.mediaIncubationConditionId) : undefined;
          const prod = m.mediaProductId !== "" ? mediaProducts.find((p) => p.id === m.mediaProductId) : undefined;
          return {
            mediaProductId: m.mediaProductId,
            mediaIncubationConditionId: m.mediaIncubationConditionId,
            isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired,
            displayOrder: i,
            mediaProductName: prod?.name,
            tempMin: cond?.temperatureMin,
            tempMax: cond?.temperatureMax,
            incubationMinHours: cond?.incubationMinHours,
            incubationMaxHours: cond?.incubationMaxHours
          };
        }),
        requiresIncubationTransfer: form.stepType === "PlateCount" && !!form.requiresIncubationTransfer,
        stage2TempMin: form.stage2TempMin,
        stage2TempMax: form.stage2TempMax,
        stage2IncubationMinHours: form.stage2IncubationMinHours,
        stage2IncubationMaxHours: form.stage2IncubationMaxHours,
        phenotypicTestType: isBiochemical ? form.phenotypicTestType ?? null : null,
        phenotypicTestTypes: isBiochemical ? form.phenotypicTestTypes : []
      };
      const next = editingDraftIndex !== null
        ? sortedDraftSteps.map((item, idx) => (idx === editingDraftIndex ? draftEntry : item))
        : [...sortedDraftSteps, draftEntry];
      cancelEditStep();
      onDraftStepsChange(next);
      return;
    }
    try {
      if (editingStepId) {
        await masterDataOptions.updateTestWorkflowStep(editingStepId, payload, steps.find((s) => s.id === editingStepId)?.version);
      } else {
        await masterDataOptions.createTestWorkflowStep(test.id, payload);
      }
      cancelEditStep();
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? (editingStepId ? t("testMaster.workflowSteps.errors.updateStepFailed") : t("testMaster.workflowSteps.errors.addStepFailed")));
    }
  };

  const move = async (stepId: number, direction: "up" | "down", index: number) => {
    setError(null);
    if (isDraftMode && onDraftStepsChange) {
      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= sortedDraftSteps.length) return;
      const reordered = sortedDraftSteps.slice();
      const [moved] = reordered.splice(index, 1);
      reordered.splice(targetIndex, 0, moved);
      onDraftStepsChange(reordered.map((s, idx) => ({ ...s, stepOrder: idx + 1 })));
      return;
    }
    try {
      // Move returns no version, so the reload is what gives the next action the server's current version.
      await masterDataOptions.moveTestWorkflowStep(stepId, direction, steps.find((s) => s.id === stepId)?.version);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("testMaster.workflowSteps.errors.reorderStepFailed"));
    }
  };

  const remove = async (stepId: number) => {
    setError(null);
    if (isDraftMode && onDraftStepsChange) {
      const deleteIdx = effectiveSteps.findIndex((s) => s.id === stepId);
      if (deleteIdx >= 0) {
        const remaining = sortedDraftSteps
          .filter((_, idx) => idx !== deleteIdx)
          .map((s, idx) => ({ ...s, stepOrder: idx + 1 }));
        onDraftStepsChange(remaining);
      }
      return;
    }
    try {
      await masterDataOptions.deleteTestWorkflowStep(stepId, steps.find((s) => s.id === stepId)?.version);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("testMaster.workflowSteps.errors.deleteStepFailed"));
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "background.default", borderTop: "1px solid", borderTopColor: "divider" }}>
      <Stack
        direction="row"
        spacing={1.5}
        sx={{
          alignItems: "center",
          mb: 1.5
        }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          {["Measurement", "Gravimetric", "Qualitative", "HplcMethodAssay", "IcpMethodAssay", "Titration", "UvSpectrophotometry"].includes(test.workflowType) ? t("testMaster.workflowSteps.header.workflowType") : t("testMaster.workflowSteps.header.workflowSteps")}
        </Typography>
        {/* Only step-driven tests can switch; every other workflow is set by the test type. */}
        {["CountTest", "Observation"].includes(test.workflowType) && (
          <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)} inputProps={{ "aria-label": t("testMaster.workflowSteps.header.workflowTypeAria") }}>
            {workflowTypes.filter((w) => w === "CountTest" || w === "Observation").map((w) => <MenuItem key={w} value={w}>{t(`testMaster.constants.workflowTypes.${w}`, { defaultValue: WORKFLOW_TYPE_LABELS[w] ?? w })}</MenuItem>)}
          </Select>
        )}
      </Stack>
      {test.workflowType === "HplcMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>{t("testMaster.workflowSteps.hplc.title")}</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "HplcMethodAssay"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "HplcMethodAssay"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.methodAbbreviation")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.systemSuitability")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("testMaster.workflowSteps.hplc.systemSuitabilityValue")}</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {test.workflowType === "IcpMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>{t("testMaster.workflowSteps.icp.title")}</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.icp.testType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.icpMode === "Impurity" ? t("testMaster.workflowSteps.icp.impurity") : test.icpMode === "Assay" ? t("testMaster.workflowSteps.icp.assay") : t("testMaster.workflowSteps.icp.notConfigured")}
              </Typography>
            </Box>
            {test.icpMode === "Assay" && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.icp.sampleBasis")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {test.icpSampleForm === "Liquid"
                    ? t("testMaster.workflowSteps.icp.thVol", { amount: test.icpTheoreticalAmount ?? "—", sg: test.icpSpecificGravity ?? "—" })
                    : t("testMaster.workflowSteps.icp.thWt", { amount: test.icpTheoreticalAmount ?? "—" })}
                </Typography>
              </Box>
            )}
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.icp.elements")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {(test.icpElements ?? []).length === 0
                  ? t("testMaster.workflowSteps.icp.noElements")
                  : (test.icpElements ?? [])
                      .map((e) => (test.icpMode === "Assay" && e.targetPpm != null ? `${e.symbol} (${e.targetPpm} ppm)` : e.symbol))
                      .join(", ")}
              </Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {test.workflowType === "HplcMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          {t("testMaster.workflowSteps.hplc.noStepsNote")}
        </Typography>
      ) : test.workflowType === "IcpMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          {t("testMaster.workflowSteps.icp.noStepsNote")}
        </Typography>
      ) : test.workflowType === "Measurement" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.measurement.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "Measurement"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "Measurement"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.measurement.evaluationBasis")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.evaluationBasis ?? "Mean"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.measurement.replicateCount")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.measurement.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "Gravimetric" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.gravimetric.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "GravimetricLoss"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "GravimetricLoss"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.measurement.replicateCount")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.gravimetric.usesTare")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.usesTare ? t("testMaster.workflowSteps.gravimetric.yes") : t("testMaster.workflowSteps.gravimetric.no")}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.gravimetric.conditionFields")}</Typography>
              <Typography variant="body2">{test.conditionFields || t("testMaster.workflowSteps.gravimetric.noneConfigured")}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.gravimetric.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "Titration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.titration.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            {[
              [t("testMaster.workflowSteps.titration.type"), test.titrationType],
              [t("testMaster.workflowSteps.titration.mode"), test.titrationMode],
              [t("testMaster.workflowSteps.titration.calculation"), test.titrationCalculation],
              [t("testMaster.workflowSteps.titration.endpoint"), test.titrationEndpoint],
              [t("testMaster.workflowSteps.titration.replicates"), test.replicateCount]
            ].map(([label, value]) => (
              <Box key={String(label)}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{label}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{value ?? "-"}</Typography>
              </Box>
            ))}
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.titration.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "UvSpectrophotometry" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.uv.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            {[
              [t("testMaster.workflowSteps.uv.calculationMode"), test.uvCalculationMode],
              [t("testMaster.workflowSteps.uv.specificAbsorbance"), test.uvSpecificAbsorbance],
              [t("testMaster.workflowSteps.uv.totalVolume"), test.uvTotalVolumeMl],
              [t("testMaster.workflowSteps.uv.standardVolume"), test.uvStandardVolumeMl],
              [t("testMaster.workflowSteps.uv.standardMaterial"), test.uvStandardEntryName],
              [t("testMaster.workflowSteps.titration.replicates"), test.replicateCount]
            ].filter(([, value]) => value !== undefined && value !== null && value !== "").map(([label, value]) => (
              <Box key={String(label)}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{label}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{String(value)}</Typography>
              </Box>
            ))}
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.uv.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "Qualitative" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.qualitative.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "Qualitative"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "Qualitative"] ?? test.equationType ?? "" })}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.qualitative.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "Dissolution" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.dissolution.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "Dissolution"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "Dissolution"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.systemSuitability")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.requiresSystemSuitability ? t("testMaster.workflowSteps.dissolution.requiredWithAbbr", { abbr: test.methodAbbreviation ?? t("testMaster.workflowSteps.dissolution.noAbbr") }) : t("testMaster.workflowSteps.dissolution.required")}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.dissolution.s1Offset")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>≥ Q + {test.dissolutionS1Offset ?? 5} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.dissolution.s2MinOffset")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS2MinOffset ?? 15} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.dissolution.s3MinOffset")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS3MinOffset ?? 25} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.dissolution.s3MaxBelowS2")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("testMaster.workflowSteps.dissolution.unitsCount", { count: test.dissolutionS3MaxBelowS2Min ?? 2 })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.gravimetric.conditionFields")}</Typography>
              <Typography variant="body2">{test.conditionFields || t("testMaster.workflowSteps.gravimetric.noneConfigured")}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.dissolution.noStepsNote")}
          </Typography>
        </Box>
      ) : test.workflowType === "Disintegration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.disintegration.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "Disintegration"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "Disintegration"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.disintegration.stage1Units")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage1Units ?? 6}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.disintegration.stage2Units")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage2Units ?? 12}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.disintegration.maxS1Failures")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMaxStage1Failures ?? 2}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.disintegration.minPassTotal")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMinPassTotal ?? 16}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.gravimetric.conditionFields")}</Typography>
              <Typography variant="body2">{test.conditionFields || t("testMaster.workflowSteps.gravimetric.noneConfigured")}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.disintegration.noStepsNote", { s1: test.disintegrationStage1Units ?? 6, s2: test.disintegrationStage2Units ?? 12 })}
          </Typography>
        </Box>
      ) : test.workflowType === "WeightVariation" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            {t("testMaster.workflowSteps.weightVariation.title")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.hplc.equationType")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{t(`testMaster.constants.equationTypes.${test.equationType ?? "WeightVariation"}`, { defaultValue: EQUATION_TYPE_LABELS[test.equationType ?? "WeightVariation"] ?? test.equationType ?? "" })}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.weightVariation.unitCount")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.wvUnitCount ?? 20}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.weightVariation.tabletsLabel")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {t("testMaster.workflowSteps.weightVariation.tabletsSummary", {
                  b1Mg: test.wvTabletBand1MaxMg ?? 130,
                  b1Pct: test.wvTabletBand1Percent ?? 10,
                  b2Mg: test.wvTabletBand2MaxMg ?? 324,
                  b2Pct: test.wvTabletBand2Percent ?? 7.5,
                  b3Pct: test.wvTabletBand3Percent ?? 5,
                  maxOut: test.wvTabletMaxOutside ?? 2
                })}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.weightVariation.capsulesLabel")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {t("testMaster.workflowSteps.weightVariation.capsulesSummary", {
                  inner: test.wvCapsuleInnerPercent ?? 10,
                  outer: test.wvCapsuleOuterPercent ?? 25,
                  s1Out: test.wvCapsuleS1MaxOutside ?? 2,
                  s1Retest: test.wvCapsuleS1MaxForRetest ?? 6,
                  s2Extra: test.wvCapsuleS2ExtraUnits ?? 40,
                  s2Out: test.wvCapsuleS2MaxOutside ?? 6
                })}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("testMaster.workflowSteps.gravimetric.conditionFields")}</Typography>
              <Typography variant="body2">{test.conditionFields || t("testMaster.workflowSteps.gravimetric.noneConfigured")}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("testMaster.workflowSteps.weightVariation.noStepsNote")}
          </Typography>
        </Box>
      ) : (
      <>
      {effectiveSteps.length > 0 ? (
        <TableContainer>
          <Table size="small" sx={{ mb: 1.5 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>#</TableCell><TableCell>{t("testMaster.workflowSteps.table.step")}</TableCell><TableCell>{t("testMaster.workflowSteps.table.incubation")}</TableCell>
                <TableCell>{t("testMaster.workflowSteps.table.tempC")}</TableCell><TableCell>{t("testMaster.workflowSteps.table.stepType")}</TableCell><TableCell>{t("testMaster.workflowSteps.table.media")}</TableCell><TableCell>{t("testMaster.workflowSteps.table.organism")}</TableCell>
                <TableCell>{t("testMaster.workflowSteps.table.status")}</TableCell><TableCell>{t("testMaster.workflowSteps.table.final")}</TableCell><TableCell /></TableRow>
            </TableHead>
            <TableBody>
              {effectiveSteps.map((s, i) => {
                const stage2 = (s.incubationStages ?? []).find((x) => x.stageNumber === 2);
                const isTwoStage = s.stepType === "PlateCount" && s.requiresIncubationTransfer;
                return (
                  <TableRow key={s.id}>
                    <TableCell>{s.stepOrder}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{
                        alignItems: "center"
                      }}>
                        <span>{s.stepName}</span>
                        {isTwoStage && (
                          <Chip
                            size="small"
                            color="primary"
                            variant="outlined"
                            label={t("testMaster.workflowSteps.table.twoStage")}
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>{t("testMaster.workflowSteps.table.stage1Hours", { range: stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours") })}</div>
                          <div>{t("testMaster.workflowSteps.table.stage2Hours", { min: stage2.incubationMinHours, max: stage2.incubationMaxHours })}</div>
                        </Box>
                      ) : (
                        t("testMaster.workflowSteps.table.hoursSuffix", { range: stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours") })
                      )}
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>{t("testMaster.workflowSteps.table.stage1Temp", { range: stage1Ranges(s.stepMedia, "tempMin", "tempMax") })}</div>
                          <div>{t("testMaster.workflowSteps.table.stage2Temp", { min: stage2.tempMin, max: stage2.tempMax })}</div>
                        </Box>
                      ) : (
                        stage1Ranges(s.stepMedia, "tempMin", "tempMax")
                      )}
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} sx={{
                        alignItems: "center"
                      }}>
                        <span>{s.stepType}</span>
                        {isTwoStage && (
                          <Chip
                            size="small"
                            color="secondary"
                            label={t("testMaster.workflowSteps.table.transfer")}
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {s.stepType === "BiochemicalTest"
                        ? (s.phenotypicTestTypes && s.phenotypicTestTypes.length > 0
                            ? s.phenotypicTestTypes.map((pt: string) => t(`testMaster.constants.phenotypicTypes.${pt}`, { defaultValue: PHENOTYPIC_TEST_TYPE_LABELS[pt] ?? pt })).join(", ")
                            : s.phenotypicTestType ? t(`testMaster.constants.phenotypicTypes.${s.phenotypicTestType}`, { defaultValue: PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType }) : <em>—</em>)
                        : (s.stepMedia && s.stepMedia.length > 0 ? s.stepMedia.map((m) => m.mediaProductName).join(", ") : <em>—</em>)}
                    </TableCell>
                    <TableCell>{s.targetOrganism?.name ?? <em>—</em>}</TableCell>
                    <TableCell>
                      {stepNeedsConfiguration(s) && (
                        <Tooltip title={t("testMaster.workflowSteps.table.needsConfigurationTooltip")}>
                          <Chip size="small" color="warning" icon={<WarningAmberIcon fontSize="small" />} label={t("testMaster.workflowSteps.table.needsConfiguration")} />
                        </Tooltip>
                      )}
                    </TableCell>
                    <TableCell>{s.isFinalStep ? t("testMaster.workflowSteps.gravimetric.yes") : "—"}</TableCell>
                    <TableCell align="right">
                      <Tooltip title={t("testMaster.workflowSteps.table.moveUp")}>
                        <span>
                          <IconButton size="small" disabled={i === 0} onClick={() => move(s.id, "up", i)} aria-label={t("testMaster.workflowSteps.table.moveUp")}>
                            <ArrowUpwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title={t("testMaster.workflowSteps.table.moveDown")}>
                        <span>
                          <IconButton size="small" disabled={i === effectiveSteps.length - 1} onClick={() => move(s.id, "down", i)} aria-label={t("testMaster.workflowSteps.table.moveDown")}>
                            <ArrowDownwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title={t("testMaster.workflowSteps.table.editStep")}>
                        <IconButton size="small" onClick={() => startEditStep(s, i)} aria-label={t("testMaster.workflowSteps.table.editStep")}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t("testMaster.workflowSteps.table.duplicateStep")}>
                        <IconButton size="small" onClick={() => startDuplicateStep(s)} aria-label={t("testMaster.workflowSteps.table.duplicateStep")}>
                          <ContentCopyOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t("testMaster.workflowSteps.table.deleteStep")}>
                        <IconButton size="small" color="error" onClick={() => setStepToDelete(s.id)} aria-label={t("testMaster.workflowSteps.table.deleteStep")}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      ) : (
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            mb: 1.5
          }}>{t("testMaster.workflowSteps.table.empty")}</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? t("testMaster.workflowSteps.form.editStep") : t("testMaster.workflowSteps.form.addStep")}</Typography>
      <Stack useFlexGap
        direction="row"
        spacing={1.5}
        sx={{
          flexWrap: "wrap",
          alignItems: "center"
        }}>
        <TextField size="small" label={t("testMaster.workflowSteps.form.stepName")} inputRef={stepNameInputRef} placeholder={t("testMaster.workflowSteps.form.stepNamePlaceholder")} value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {isBiochemical && (
          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <Select size="small" displayEmpty value={pendingPhenotypicTest} onChange={(e) => setPendingPhenotypicTest(e.target.value as string)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": t("testMaster.workflowSteps.form.phenotypicAria") }}>
              <MenuItem value=""><em>{t("testMaster.workflowSteps.form.phenotypicPlaceholder")}</em></MenuItem>
              {PHENOTYPIC_TEST_TYPES
                .filter((pt) => !form.phenotypicTestTypes.includes(pt))
                .map((pt) => <MenuItem key={pt} value={pt}>{t(`testMaster.constants.phenotypicTypes.${pt}`, { defaultValue: PHENOTYPIC_TEST_TYPE_LABELS[pt] ?? pt })}</MenuItem>)}
            </Select>
            <Button size="small" variant="outlined" disabled={!pendingPhenotypicTest} onClick={addPhenotypicTest}>{t("testMaster.workflowSteps.form.add")}</Button>
            {form.phenotypicTestTypes.map((pt) => (
              <Chip key={pt} size="small" label={t(`testMaster.constants.phenotypicTypes.${pt}`, { defaultValue: PHENOTYPIC_TEST_TYPE_LABELS[pt] ?? pt })} onDelete={() => removePhenotypicTest(pt)} />
            ))}
          </Stack>
        )}
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": t("testMaster.workflowSteps.form.stepTypeAria") }}>
          {STEP_TYPES.map((st) => <MenuItem key={st} value={st}>{st}</MenuItem>)}
        </Select>
        <FormControlLabel
          control={<Checkbox checked={!!form.isFinalStep} onChange={(e) => setForm({ ...form, isFinalStep: e.target.checked })} />}
          label={t("testMaster.workflowSteps.form.finalStep")}
        />
        {form.stepType === "PlateCount" && (
          <FormControlLabel
            control={
              <Checkbox
                checked={!!form.requiresIncubationTransfer}
                onChange={(e) =>
                  setForm({
                    ...form,
                    requiresIncubationTransfer: e.target.checked,
                    ...(e.target.checked
                      ? {}
                      : {
                          stage2TempMin: undefined,
                          stage2TempMax: undefined,
                          stage2IncubationMinHours: undefined,
                          stage2IncubationMaxHours: undefined
                        })
                  })
                }
              />
            }
            label={t("testMaster.workflowSteps.form.requiresTransfer")}
          />
        )}
        {editingStepId && <Button onClick={cancelEditStep}>{t("testMaster.workflowSteps.form.cancel")}</Button>}
        <Button variant="contained" onClick={saveStep}>{editingStepId ? t("testMaster.workflowSteps.form.saveChanges") : t("testMaster.workflowSteps.form.addStep")}</Button>
      </Stack>

      {form.stepType === "PlateCount" && form.requiresIncubationTransfer && (
        <Box sx={{ mt: 1.5, p: 1.5, bgcolor: "background.paper", border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "text.primary" }}>
            {t("testMaster.workflowSteps.form.stage2Title")}
          </Typography>
          <Stack useFlexGap
            direction="row"
            spacing={1.5}
            sx={{
              flexWrap: "wrap",
              alignItems: "center"
            }}>
            <TextField
              size="small"
              type="number"
              label={t("testMaster.workflowSteps.form.stage2TempMin")}
              value={form.stage2TempMin ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMin: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label={t("testMaster.workflowSteps.form.stage2TempMax")}
              value={form.stage2TempMax ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMax: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label={t("testMaster.workflowSteps.form.stage2MinHours")}
              value={form.stage2IncubationMinHours ?? ""}
              onChange={(e) => setForm({ ...form, stage2IncubationMinHours: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label={t("testMaster.workflowSteps.form.stage2MaxHours")}
              value={form.stage2IncubationMaxHours ?? ""}
              onChange={(e) => setForm({ ...form, stage2IncubationMaxHours: e.target.value })}
              sx={{ width: 140 }}
            />
          </Stack>
        </Box>
      )}

      {needsOrganism && (
        <Stack
          direction="row"
          spacing={1.5}
          sx={{
            alignItems: "center",
            mt: 1.5
          }}>
          <Select<number | ""> size="small" displayEmpty value={form.targetOrganismId ?? ""} onChange={(e) => setForm({ ...form, targetOrganismId: e.target.value === "" ? null : Number(e.target.value) })} sx={{ minWidth: 220 }} inputProps={{ "aria-label": t("testMaster.workflowSteps.form.targetOrganismAria") }}>
            <MenuItem value=""><em>{t("testMaster.workflowSteps.form.targetOrganismPlaceholder")}</em></MenuItem>
            {organisms.map((o) => <MenuItem key={o.id} value={o.id}>{o.scientificName}</MenuItem>)}
          </Select>
        </Stack>
      )}

      {!hasNoMedia && (
        <Box sx={{ mt: 1.5 }}>
          <Stack
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: "center",
              mb: 0.5
            }}>
            <Typography sx={{ fontWeight: 700, fontSize: 12 }}>{t("testMaster.workflowSteps.form.stepMedia")}</Typography>
            {isSingleMedia && <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>{t("testMaster.workflowSteps.form.singleMediaHint")}</Typography>}
          </Stack>
          {/* Update replaces the whole StepMedia set server-side (no merge) -
              flagged here so an admin editing an existing step isn't
              surprised that rows not shown in this list get removed. */}
          {!isDraftMode && editingStepId && (
            <Alert severity="info" sx={{ mb: 1, maxWidth: 520 }}>{t("testMaster.workflowSteps.form.replaceMediaInfo")}</Alert>
          )}
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              mb: 1
            }}>
            {t("testMaster.workflowSteps.form.mediaHelp")}
          </Typography>
          <Stack spacing={1}>
            {form.stepMedia.map((row, idx) => {
              const productConditions = row.mediaProductId !== "" ? conditions.filter((c) => c.mediaProductId === row.mediaProductId) : [];
              return (
                <Stack useFlexGap
                  key={idx}
                  direction="row"
                  spacing={1.5}
                  sx={{
                    alignItems: "center",
                    flexWrap: "wrap"
                  }}>
                  <MediaProductPicker
                    label={t("testMaster.workflowSteps.form.cultureMediaProduct")}
                    value={row.mediaProductId === "" ? null : row.mediaProductId}
                    options={mediaProducts}
                    onChange={(id) => updateMediaRow(idx, { mediaProductId: id ?? "" })}
                    sx={{ minWidth: 240 }}
                  />
                  <Select<number | "">
                    size="small"
                    displayEmpty
                    value={row.mediaIncubationConditionId}
                    disabled={productConditions.length === 0}
                    onChange={(e) => updateMediaRow(idx, { mediaIncubationConditionId: e.target.value === "" ? "" : Number(e.target.value) })}
                    sx={{ minWidth: 240 }}
                    inputProps={{ "aria-label": t("testMaster.workflowSteps.form.incubationConditionAria") }}>
                    <MenuItem value=""><em>{t("testMaster.workflowSteps.form.incubationConditionPlaceholder")}</em></MenuItem>
                    {productConditions.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {incubationConditionLabel(c)}
                      </MenuItem>
                    ))}
                  </Select>
                  {row.mediaProductId !== "" && productConditions.length === 0 && (
                    <Typography variant="caption" sx={{ color: "text.secondary" }}>
                      {t("testMaster.workflowSteps.form.noConditions")}
                    </Typography>
                  )}
                  {!isConfirmatory && (
                    <FormControlLabel
                      control={<Checkbox checked={row.isRequired} onChange={(e) => updateMediaRow(idx, { isRequired: e.target.checked })} />}
                      label={t("testMaster.workflowSteps.form.required")}
                    />
                  )}
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>{t("testMaster.workflowSteps.form.order", { order: idx + 1 })}</Typography>
                  <Tooltip title={t("testMaster.workflowSteps.form.removeMedium")}>
                    <IconButton size="small" color="error" onClick={() => removeMediaRow(idx)} aria-label={t("testMaster.workflowSteps.form.removeMedium")}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              );
            })}
          </Stack>
          <Button size="small" sx={{ mt: 1 }} disabled={isSingleMedia && form.stepMedia.length >= 1} onClick={addMediaRow}>{t("testMaster.workflowSteps.form.addMedium")}</Button>
        </Box>
      )}
      </>
      )}

      <ConfirmationDialog
        open={stepToDelete !== null}
        title={t("testMaster.workflowSteps.deleteDialog.title")}
        message={t("testMaster.workflowSteps.deleteDialog.message")}
        confirmText={t("testMaster.workflowSteps.deleteDialog.confirm")}
        destructive
        onConfirm={async () => {
          if (stepToDelete !== null) {
            const id = stepToDelete;
            setStepToDelete(null);
            await remove(id);
          }
        }}
        onCancel={() => setStepToDelete(null)}
      />
    </Box>
  );
}
