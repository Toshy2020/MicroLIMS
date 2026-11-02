import { useEffect, useState } from "react";
import { TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Box, Typography, Checkbox, FormControlLabel, Chip, Tooltip, TableContainer } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteIcon from "@mui/icons-material/Delete";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import { MediaProductPicker } from "../../../../components/MediaProductPicker";
import { useMediaProducts } from "../../../../hooks/useMediaProducts";
import { masterDataOptions, incubationConditionLabel, MediaIncubationConditionOption } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import { EQUATION_TYPE_LABELS, PHENOTYPIC_TEST_TYPES, PHENOTYPIC_TEST_TYPE_LABELS, STEP_TYPES, STEP_TYPES_REQUIRING_ORGANISM, STEP_TYPES_SINGLE_MEDIA, STEP_TYPES_WITH_NO_MEDIA, StepFormState, StepMediaRow, WORKFLOW_TYPE_LABELS, WorkflowStepMediaItem, defaultStepForm, stage1Ranges, stepNeedsConfiguration, validateStepForm } from "./testMasterConstants";

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

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
export function WorkflowStepsSection({ test, workflowTypes, onWorkflowTypeChanged }: { test: TestDefinitionOption; workflowTypes: string[]; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<WorkflowStepItem[]>([]);
  const [organisms, setOrganisms] = useState<Array<{ id: number; scientificName: string; commonName?: string }>>([]);
  // Step media name a Culture Media product; the picker hides inactive ones except a product already on the row.
  const { options: mediaProducts } = useMediaProducts();
  const [conditions, setConditions] = useState<MediaIncubationConditionOption[]>([]);
  const [form, setForm] = useState<StepFormState>(defaultStepForm);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [stepToDelete, setStepToDelete] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadSteps = () => masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps);
  useEffect(() => {
    masterDataOptions.getOrganisms().then(setOrganisms);
    masterDataOptions.getMediaIncubationConditions().then(setConditions);
    loadSteps();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [test.id]);

  const changeWorkflowType = async (workflowType: string) => {
    setError(null);
    try {
      await masterDataOptions.updateWorkflowType(test.id, workflowType);
      onWorkflowTypeChanged();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not update the workflow type.");
    }
  };

  const startEditStep = (s: WorkflowStepItem) => {
    setEditingStepId(s.id);
    const stage2 = (s.incubationStages ?? []).find((x) => x.stageNumber === 2);
    setForm({
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
    });
    setError(null);
  };

  const cancelEditStep = () => { setEditingStepId(null); setForm(defaultStepForm()); };

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
      setError(isBiochemical ? "Step Name and at least one Phenotypic Test Type are required." : "Step Name is required.");
      return;
    }
    const validationError = validateStepForm(form);
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
      setError(err?.response?.data?.message ?? `Could not ${editingStepId ? "update" : "add"} this step.`);
    }
  };

  const move = async (stepId: number, direction: "up" | "down") => {
    setError(null);
    try {
      await masterDataOptions.moveTestWorkflowStep(stepId, direction);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not reorder this step.");
    }
  };

  const remove = async (stepId: number) => {
    setError(null);
    try {
      await masterDataOptions.deleteTestWorkflowStep(stepId);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not delete this step.");
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
          {["Measurement", "Gravimetric", "Qualitative", "HplcMethodAssay", "IcpMethodAssay", "Titration"].includes(test.workflowType) ? "Workflow Type" : "Workflow Steps"}
        </Typography>
        {/* Only step-driven tests can switch; every other workflow is set by the test type. */}
        {["CountTest", "Observation"].includes(test.workflowType) && (
          <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)} inputProps={{ "aria-label": "Workflow type" }}>
            {workflowTypes.filter((w) => w === "CountTest" || w === "Observation").map((w) => <MenuItem key={w} value={w}>{WORKFLOW_TYPE_LABELS[w] ?? w}</MenuItem>)}
          </Select>
        )}
      </Stack>
      {test.workflowType === "HplcMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>HPLC Method Assay Configuration</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "HplcMethodAssay"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Method Abbreviation</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>Required (from HPLC Method Master)</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {test.workflowType === "IcpMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>ICP Method Assay Configuration</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "IcpMethodAssay"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Method Abbreviation</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {test.workflowType === "HplcMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          HPLC Method Assay tests have no workflow steps or local test analytes: parameters, analytes, standard weights, and system suitability criteria are configured centrally in HPLC Methods Master.
        </Typography>
      ) : test.workflowType === "IcpMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          ICP Method Assay tests have no workflow steps or local test analytes: elements, calibration standards, and quality controls are configured centrally in ICP Methods Master.
        </Typography>
      ) : test.workflowType === "Measurement" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Measurement Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Measurement"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Evaluation Basis</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.evaluationBasis ?? "Mean"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicate Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Measurement tests have no workflow steps: readings are entered per specification parameter directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Gravimetric" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Gravimetric Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "GravimetricLoss"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicate Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Uses Tare</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.usesTare ? "Yes" : "No"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Gravimetric tests have no workflow steps: container/sample weights and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Titration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Titration Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            {[
              ["Type", test.titrationType],
              ["Mode", test.titrationMode],
              ["Calculation", test.titrationCalculation],
              ["Endpoint", test.titrationEndpoint],
              ["Replicates", test.replicateCount]
            ].map(([label, value]) => (
              <Box key={String(label)}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{label}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{value ?? "-"}</Typography>
              </Box>
            ))}
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Titration tests have no workflow steps: replicate weights and titrant volumes are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Qualitative" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Qualitative Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Qualitative"] ?? test.equationType}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Qualitative tests have no workflow steps: compliance observation is entered per specification parameter directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Dissolution" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Dissolution Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Dissolution"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.requiresSystemSuitability ? `Required (${test.methodAbbreviation ?? "No abbr"})` : "Required"}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S1 Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>≥ Q + {test.dissolutionS1Offset ?? 5} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S2 Min Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS2MinOffset ?? 15} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S3 Min Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS3MinOffset ?? 25} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S3 Max Below S2</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.dissolutionS3MaxBelowS2Min ?? 2} units</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Dissolution tests have no workflow steps: staged vessel peak areas (S1: 6, S2: +6, S3: +12) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Disintegration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Disintegration Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Disintegration"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Stage 1 Units</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage1Units ?? 6}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Stage 2 Units</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage2Units ?? 12}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max S1 Failures</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMaxStage1Failures ?? 2}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Min Pass Total</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMinPassTotal ?? 16}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Disintegration tests have no workflow steps: staged unit times (S1: {test.disintegrationStage1Units ?? 6}, S2: +{test.disintegrationStage2Units ?? 12}) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "WeightVariation" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Weight Variation Configuration (USP &lt;2091&gt;)
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "WeightVariation"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Unit Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.wvUnitCount ?? 20}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Tablets (Bands / Max Out)</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                &le;{test.wvTabletBand1MaxMg ?? 130}mg: {test.wvTabletBand1Percent ?? 10}% | &le;{test.wvTabletBand2MaxMg ?? 324}mg: {test.wvTabletBand2Percent ?? 7.5}% | &gt;324mg: {test.wvTabletBand3Percent ?? 5}% (max {test.wvTabletMaxOutside ?? 2} out)
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Capsules (Limits / Retest)</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                &plusmn;{test.wvCapsuleInnerPercent ?? 10}% / &plusmn;{test.wvCapsuleOuterPercent ?? 25}% (S1: &le;{test.wvCapsuleS1MaxOutside ?? 2} pass, &le;{test.wvCapsuleS1MaxForRetest ?? 6} retest +{test.wvCapsuleS2ExtraUnits ?? 40} units, S2: &le;{test.wvCapsuleS2MaxOutside ?? 6} out)
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Weight Variation tests have no workflow steps: unit weights (or gross and shell weights) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : (
      <>
      {steps.length > 0 ? (
        <TableContainer>
          <Table size="small" sx={{ mb: 1.5 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>#</TableCell><TableCell>Step</TableCell><TableCell>Incubation</TableCell>
                <TableCell>Temp °C</TableCell><TableCell>Step Type</TableCell><TableCell>Media</TableCell><TableCell>Organism</TableCell>
                <TableCell>Status</TableCell><TableCell>Final</TableCell><TableCell /></TableRow>
            </TableHead>
            <TableBody>
              {steps.map((s, i) => {
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
                            label="2-Stage"
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>Stage 1: {stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours")}h</div>
                          <div>Stage 2: {stage2.incubationMinHours}-{stage2.incubationMaxHours}h</div>
                        </Box>
                      ) : (
                        `${stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours")}h`
                      )}
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>Stage 1: {stage1Ranges(s.stepMedia, "tempMin", "tempMax")}</div>
                          <div>Stage 2: {stage2.tempMin}-{stage2.tempMax}</div>
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
                            label="Transfer"
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {s.stepType === "BiochemicalTest"
                        ? (s.phenotypicTestTypes && s.phenotypicTestTypes.length > 0
                            ? s.phenotypicTestTypes.map((t: string) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")
                            : s.phenotypicTestType ? PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType : <em>—</em>)
                        : (s.stepMedia && s.stepMedia.length > 0 ? s.stepMedia.map((m) => m.mediaProductName).join(", ") : <em>—</em>)}
                    </TableCell>
                    <TableCell>{s.targetOrganism?.name ?? <em>—</em>}</TableCell>
                    <TableCell>
                      {stepNeedsConfiguration(s) && (
                        <Tooltip title="This template is missing a required organism or medium (likely inherited from the pre-refactor migration) and will fail validation the first time an analyst runs it. Edit it to complete the configuration.">
                          <Chip size="small" color="warning" icon={<WarningAmberIcon fontSize="small" />} label="Needs configuration" />
                        </Tooltip>
                      )}
                    </TableCell>
                    <TableCell>{s.isFinalStep ? "Yes" : "—"}</TableCell>
                    <TableCell align="right">
                      <Tooltip title="Move up">
                        <span>
                          <IconButton size="small" disabled={i === 0} onClick={() => move(s.id, "up")} aria-label="Move up">
                            <ArrowUpwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title="Move down">
                        <span>
                          <IconButton size="small" disabled={i === steps.length - 1} onClick={() => move(s.id, "down")} aria-label="Move down">
                            <ArrowDownwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title="Edit step">
                        <IconButton size="small" onClick={() => startEditStep(s)} aria-label="Edit step">
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Delete step">
                        <IconButton size="small" color="error" onClick={() => setStepToDelete(s.id)} aria-label="Delete step">
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
          }}>No workflow steps configured yet.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? "Edit Step" : "Add Step"}</Typography>
      <Stack useFlexGap
        direction="row"
        spacing={1.5}
        sx={{
          flexWrap: "wrap",
          alignItems: "center"
        }}>
        <TextField size="small" label="Step Name" placeholder="e.g. TSB" value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {isBiochemical && (
          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <Select size="small" displayEmpty value={pendingPhenotypicTest} onChange={(e) => setPendingPhenotypicTest(e.target.value as string)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": "Phenotypic Test Type" }}>
              <MenuItem value=""><em>Phenotypic Test Type</em></MenuItem>
              {PHENOTYPIC_TEST_TYPES
                .filter((t) => !form.phenotypicTestTypes.includes(t))
                .map((t) => <MenuItem key={t} value={t}>{PHENOTYPIC_TEST_TYPE_LABELS[t]}</MenuItem>)}
            </Select>
            <Button size="small" variant="outlined" disabled={!pendingPhenotypicTest} onClick={addPhenotypicTest}>Add</Button>
            {form.phenotypicTestTypes.map((t) => (
              <Chip key={t} size="small" label={PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t} onDelete={() => removePhenotypicTest(t)} />
            ))}
          </Stack>
        )}
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": "Step type" }}>
          {STEP_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </Select>
        <FormControlLabel
          control={<Checkbox checked={!!form.isFinalStep} onChange={(e) => setForm({ ...form, isFinalStep: e.target.checked })} />}
          label="Final Step"
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
            label="Requires incubation transfer"
          />
        )}
        {editingStepId && <Button onClick={cancelEditStep}>Cancel</Button>}
        <Button variant="contained" onClick={saveStep}>{editingStepId ? "Save Changes" : "Add Step"}</Button>
      </Stack>

      {form.stepType === "PlateCount" && form.requiresIncubationTransfer && (
        <Box sx={{ mt: 1.5, p: 1.5, bgcolor: "background.paper", border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "text.primary" }}>
            Stage 2 Incubation (Transfer)
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
              label="Stage 2 Temp Min"
              value={form.stage2TempMin ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMin: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Temp Max"
              value={form.stage2TempMax ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMax: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Min Hours"
              value={form.stage2IncubationMinHours ?? ""}
              onChange={(e) => setForm({ ...form, stage2IncubationMinHours: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Max Hours"
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
          <Select<number | ""> size="small" displayEmpty value={form.targetOrganismId ?? ""} onChange={(e) => setForm({ ...form, targetOrganismId: e.target.value === "" ? null : Number(e.target.value) })} sx={{ minWidth: 220 }} inputProps={{ "aria-label": "Target Organism (required)" }}>
            <MenuItem value=""><em>Target Organism (required)</em></MenuItem>
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
            <Typography sx={{ fontWeight: 700, fontSize: 12 }}>Step Media</Typography>
            {isSingleMedia && <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>This step type allows exactly one medium.</Typography>}
          </Stack>
          {/* Update replaces the whole StepMedia set server-side (no merge) -
              flagged here so an admin editing an existing step isn't
              surprised that rows not shown in this list get removed. */}
          {editingStepId && (
            <Alert severity="info" sx={{ mb: 1, maxWidth: 520 }}>Saving replaces this step's entire medium list with what's shown below.</Alert>
          )}
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              mb: 1
            }}>
            Pick the culture media product, then one of its incubation conditions. The condition sets this medium's temperature and incubation hours. Conditions are added under Master Testing Materials, Culture Media tab.
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
                    label="Culture media product"
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
                    inputProps={{ "aria-label": "Incubation condition" }}>
                    <MenuItem value=""><em>Incubation condition</em></MenuItem>
                    {productConditions.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {incubationConditionLabel(c)}
                      </MenuItem>
                    ))}
                  </Select>
                  {row.mediaProductId !== "" && productConditions.length === 0 && (
                    <Typography variant="caption" sx={{ color: "text.secondary" }}>
                      This medium has no incubation conditions yet.
                    </Typography>
                  )}
                  {!isConfirmatory && (
                    <FormControlLabel
                      control={<Checkbox checked={row.isRequired} onChange={(e) => updateMediaRow(idx, { isRequired: e.target.checked })} />}
                      label="Required"
                    />
                  )}
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>Order {idx + 1}</Typography>
                  <Tooltip title="Remove medium">
                    <IconButton size="small" color="error" onClick={() => removeMediaRow(idx)} aria-label="Remove medium">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              );
            })}
          </Stack>
          <Button size="small" sx={{ mt: 1 }} disabled={isSingleMedia && form.stepMedia.length >= 1} onClick={addMediaRow}>Add Medium</Button>
        </Box>
      )}
      </>
      )}

      <ConfirmationDialog
        open={stepToDelete !== null}
        title="Delete Workflow Step"
        message="Are you sure you want to delete this workflow step? This action cannot be undone."
        confirmText="Delete Step"
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
