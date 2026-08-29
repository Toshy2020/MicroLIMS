import { Fragment, useEffect, useState } from "react";
import { Paper, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Collapse, Box, Typography, Checkbox, FormControlLabel, Chip, Tooltip } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteIcon from "@mui/icons-material/Delete";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";

const WORKFLOW_TYPES = ["CountTest", "Observation"];
const STEP_TYPES = ["PlateCount", "BrothEnrichment", "SelectiveBroth", "SelectivePlating", "ConfirmatoryPlating", "BiochemicalTest"];
const STEP_TYPES_REQUIRING_ORGANISM = ["SelectivePlating", "ConfirmatoryPlating"];
const STEP_TYPES_WITH_NO_MEDIA = ["BiochemicalTest"];
// Not server-enforced for PlateCount, but Broth/SelectiveBroth/SelectivePlating
// each require exactly one medium (WorkflowTemplateValidator rules 1-2) - this
// caps the editor at one row so the analyst gets that feedback immediately
// instead of only on server rejection.
const STEP_TYPES_SINGLE_MEDIA = ["BrothEnrichment", "SelectiveBroth", "SelectivePlating"];

const PHENOTYPIC_TEST_TYPES = ["Gram", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "IdentificationKit"];
const PHENOTYPIC_TEST_TYPE_LABELS: Record<string, string> = {
  Gram: "Gram Stain", Catalase: "Catalase", Oxidase: "Oxidase", Coagulase: "Coagulase",
  Antibiogram: "Antibiogram", IdentificationKit: "Identification Kit"
};

// TempMin/TempMax/incubationMinHours/incubationMaxHours are derived from
// mediaConfigurationId (read-only once one is picked) - see the Media
// Configuration Migration plan's Test Master reversal. A row with no
// mediaConfigurationId falls back to the free-typed fields, for the rare
// product with no matching MediaConfiguration profile yet.
type StepMediaRow = {
  materialId: number | ""; mediaConfigurationId: number | ""; tempMin: string; tempMax: string;
  incubationMinHours: string; incubationMaxHours: string; isRequired: boolean; displayOrder: number
};

interface StepFormState {
  stepName?: string;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId: number | null;
  stepMedia: StepMediaRow[];
  // PlateCount only (backend TestWorkflowStep.RequiresIncubationTransfer /
  // WorkflowTemplateValidator rule 7). Stage 2's own window is a separate
  // TestWorkflowStepIncubationStage row (StageNumber 2), not more columns
  // on this step - see stage2* fields below.
  requiresIncubationTransfer: boolean;
  stage2TempMin?: string | number;
  stage2TempMax?: string | number;
  stage2IncubationMinHours?: string | number;
  stage2IncubationMaxHours?: string | number;
  // Older single-value field, kept for backward compatibility with
  // existing chained-step templates - the Add Step form below now uses
  // phenotypicTestTypes (a bundle of one or more) instead, letting one
  // step cover e.g. Gram Stain + Oxidase + Identification Kit together.
  phenotypicTestType?: string | null;
  phenotypicTestTypes: string[];
}

// A function rather than a shared constant object, so every reset gets its
// own stepMedia array instead of every WorkflowStepsSection instance (one
// per expanded Test Master row) sharing a single mutable reference.
const defaultStepForm = (): StepFormState => ({
  isFinalStep: false, stepType: "PlateCount", targetOrganismId: null, stepMedia: [], requiresIncubationTransfer: false,
  phenotypicTestType: null, phenotypicTestTypes: []
});

// Mirrors WorkflowTemplateValidator's six structural rules (backend
// MicroLIMS.Application/Services/WorkflowTemplateValidator.cs) so the admin
// gets immediate feedback - the server is still authoritative and any
// rejection it returns is surfaced as-is.
function validateStepForm(form: StepFormState): string | null {
  const isBroth = form.stepType === "BrothEnrichment" || form.stepType === "SelectiveBroth";
  const isSelectivePlating = form.stepType === "SelectivePlating";
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  if (isBroth && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A broth step must have exactly one assigned medium, marked as required.";
  if (isBroth && form.targetOrganismId)
    return "A broth step must not target an organism.";
  if (isSelectivePlating && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A selective plating step must have exactly one assigned medium, marked as required.";
  if (isSelectivePlating && !form.targetOrganismId)
    return "A selective plating step must target an organism.";
  if (isConfirmatory && form.stepMedia.length === 0)
    return "A confirmatory plating step must have at least one permitted medium.";
  if (isConfirmatory && !form.targetOrganismId)
    return "A confirmatory plating step must target an organism.";
  if (isBiochemical && form.stepMedia.length > 0)
    return "A biochemical test step must have no assigned media.";
  if (isBiochemical && form.targetOrganismId)
    return "A biochemical test step must not target an organism.";
  if (isBiochemical && !form.phenotypicTestType && form.phenotypicTestTypes.length === 0)
    return "A biochemical test step must specify at least one phenotypic test type.";
  if (isBiochemical && new Set(form.phenotypicTestTypes).size !== form.phenotypicTestTypes.length)
    return "The same phenotypic test type cannot be assigned to this step more than once.";
  if (!isBiochemical && (form.phenotypicTestType || form.phenotypicTestTypes.length > 0))
    return "Only a biochemical test step may specify a phenotypic test type.";
  // Mirrors WorkflowTemplateValidator rule 8 - every non-biochemical step
  // needs at least one medium. Rules above already cover Broth/Selective/
  // Confirmatory; this is PlateCount's only media check.
  if (form.stepType === "PlateCount" && form.stepMedia.length === 0)
    return "At least one medium is required for this step type.";
  for (const m of form.stepMedia) {
    if (m.materialId === "") return "Every medium row needs a selected material.";
    if (m.mediaConfigurationId === "" && (m.tempMin === "" || m.tempMax === "" || m.incubationMinHours === "" || m.incubationMaxHours === ""))
      return "Every medium row needs either a media configuration or its own temperature and incubation range.";
    if (Number(m.tempMin) >= Number(m.tempMax)) return "Every medium's minimum temperature must be below its maximum.";
    if (Number(m.incubationMinHours) <= 0 || Number(m.incubationMaxHours) < Number(m.incubationMinHours))
      return "Every medium's incubation range must have a positive minimum and a maximum no less than the minimum.";
  }
  const materialIds = form.stepMedia.map((m) => m.materialId);
  if (new Set(materialIds).size !== materialIds.length) return "The same medium cannot be assigned to this step more than once.";

  // Mirrors WorkflowTemplateValidator rule 7 - the server is still
  // authoritative, this only spares a round trip for the common case.
  if (form.stepType === "PlateCount" && form.requiresIncubationTransfer) {
    const { stage2TempMin, stage2TempMax, stage2IncubationMinHours, stage2IncubationMaxHours } = form;
    if (stage2TempMin === undefined || stage2TempMin === "" || stage2TempMax === undefined || stage2TempMax === "" ||
        stage2IncubationMinHours === undefined || stage2IncubationMinHours === "" ||
        stage2IncubationMaxHours === undefined || stage2IncubationMaxHours === "")
      return "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range.";
    if (Number(stage2TempMin) >= Number(stage2TempMax))
      return "Stage 2's minimum temperature must be below its maximum.";
    if (Number(stage2IncubationMinHours) <= 0 || Number(stage2IncubationMaxHours) < Number(stage2IncubationMinHours))
      return "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum.";
  }
  return null;
}

// True for rows the pathogen-workflow migration could not backfill
// (TargetOrganismId/StepMedia are per-step and the migration had no source
// data for them) - flags templates that will fail validation the first time
// an analyst tries to run them, so an admin can find them here instead.
// The step's own incubationMinHours/MaxHours/temperatureMin/Max are no
// longer authoritative (see TestWorkflowEngine.cs) - a step with more than
// one permitted medium can have genuinely different windows per medium
// (e.g. Confirmatory Plating's XLD vs TSI), so stage 1's display is built
// from the picked media's own ranges instead, joined when they differ.
function stage1Ranges(stepMedia: any[], min: string, max: string): string {
  if (!stepMedia?.length) return "—";
  const distinct = Array.from(new Set(stepMedia.map((m) => `${m[min]}-${m[max]}`)));
  return distinct.join("; ");
}

function stepNeedsConfiguration(s: any): boolean {
  if (STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (s.stepType !== "BiochemicalTest" && (s.stepMedia?.length ?? 0) === 0) return true;
  if (s.stepType === "BiochemicalTest" && !s.phenotypicTestType && (s.phenotypicTestTypes?.length ?? 0) === 0) return true;
  return false;
}

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
function WorkflowStepsSection({ test, onWorkflowTypeChanged }: { test: TestDefinitionOption; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<any[]>([]);
  const [organisms, setOrganisms] = useState<any[]>([]);
  const [materials, setMaterials] = useState<any[]>([]);
  const [mediaConfigurations, setMediaConfigurations] = useState<any[]>([]);
  const [form, setForm] = useState<StepFormState>(defaultStepForm);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadSteps = () => masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps);
  useEffect(() => {
    masterDataOptions.getOrganisms().then(setOrganisms);
    masterDataOptions.getMaterials("DehydratedMedia").then(setMaterials);
    masterDataOptions.getMediaConfigurations().then(setMediaConfigurations);
    loadSteps();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [test.id]);

  const changeWorkflowType = async (workflowType: string) => {
    setError(null);
    try {
      await masterDataOptions.updateWorkflowType(test.id, workflowType);
      onWorkflowTypeChanged();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not update the workflow type.");
    }
  };

  const startEditStep = (s: any) => {
    setEditingStepId(s.id);
    const stage2 = (s.incubationStages ?? []).find((x: any) => x.stageNumber === 2);
    setForm({
      stepName: s.stepName, isFinalStep: s.isFinalStep, stepType: s.stepType,
      targetOrganismId: s.targetOrganismId ?? null,
      stepMedia: (s.stepMedia ?? []).map((m: any) => ({
        materialId: m.materialId, mediaConfigurationId: m.mediaConfigurationId ?? "",
        tempMin: String(m.tempMin), tempMax: String(m.tempMax),
        incubationMinHours: String(m.incubationMinHours ?? ""), incubationMaxHours: String(m.incubationMaxHours ?? ""),
        isRequired: m.isRequired, displayOrder: m.displayOrder
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
      materialId: "", mediaConfigurationId: "", tempMin: "", tempMax: "",
      incubationMinHours: "", incubationMaxHours: "", isRequired: false, displayOrder: form.stepMedia.length
    }]
  });
  // Picking a MediaConfiguration derives its temp/incubation range into the
  // row for display; the server re-derives from the FK at save time
  // regardless (see MasterDataController.BuildStepMediaAsync), so this is
  // just keeping the admin's on-screen preview honest, not the source of
  // truth actually saved.
  const updateMediaRow = (index: number, patch: Partial<StepMediaRow>) => setForm({
    ...form,
    stepMedia: form.stepMedia.map((m, i) => {
      if (i !== index) return m;
      const next = { ...m, ...patch };
      if (patch.mediaConfigurationId !== undefined) {
        const config = mediaConfigurations.find((c) => c.id === patch.mediaConfigurationId);
        if (config) {
          next.tempMin = String(config.temperatureMin);
          next.tempMax = String(config.temperatureMax);
          next.incubationMinHours = String(config.incubationMinHours);
          next.incubationMaxHours = String(config.incubationMaxHours);
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
        materialId: Number(m.materialId),
        mediaConfigurationId: m.mediaConfigurationId === "" ? null : Number(m.mediaConfigurationId),
        tempMin: Number(m.tempMin) || 0, tempMax: Number(m.tempMax) || 0,
        incubationMinHours: Number(m.incubationMinHours) || 0, incubationMaxHours: Number(m.incubationMaxHours) || 0,
        isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired, displayOrder: i
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
        await masterDataOptions.updateTestWorkflowStep(editingStepId, payload);
      } else {
        await masterDataOptions.createTestWorkflowStep(test.id, payload);
      }
      cancelEditStep();
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? `Could not ${editingStepId ? "update" : "add"} this step.`);
    }
  };

  const move = async (stepId: number, direction: "up" | "down") => {
    setError(null);
    try {
      await masterDataOptions.moveTestWorkflowStep(stepId, direction);
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not reorder this step.");
    }
  };

  const remove = async (stepId: number) => {
    setError(null);
    try {
      await masterDataOptions.deleteTestWorkflowStep(stepId);
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not delete this step.");
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "background.default", borderTop: "1px solid", borderTopColor: "divider" }}>
      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1.5 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>Workflow Steps</Typography>
        <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)}>
          {WORKFLOW_TYPES.map((w) => <MenuItem key={w} value={w}>{w}</MenuItem>)}
        </Select>
      </Stack>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {steps.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow>
              <TableCell>#</TableCell><TableCell>Step</TableCell><TableCell>Incubation</TableCell>
              <TableCell>Temp °C</TableCell><TableCell>Step Type</TableCell><TableCell>Media</TableCell><TableCell>Organism</TableCell>
              <TableCell>Status</TableCell><TableCell>Final</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {steps.map((s, i) => {
              const stage2 = (s.incubationStages ?? []).find((x: any) => x.stageNumber === 2);
              const isTwoStage = s.stepType === "PlateCount" && s.requiresIncubationTransfer;
              return (
                <TableRow key={s.id}>
                  <TableCell>{s.stepOrder}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1} alignItems="center">
                      <span>{s.stepName}</span>
                      {isTwoStage && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="2-Stage"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
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
                    <Stack direction="row" spacing={0.5} alignItems="center">
                      <span>{s.stepType}</span>
                      {isTwoStage && (
                        <Chip
                          size="small"
                          color="secondary"
                          label="Transfer"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    {s.stepType === "BiochemicalTest"
                      ? (s.phenotypicTestTypes?.length > 0
                          ? s.phenotypicTestTypes.map((t: string) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")
                          : s.phenotypicTestType ? PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType : <em>—</em>)
                      : (s.stepMedia?.length > 0 ? s.stepMedia.map((m: any) => m.materialName).join(", ") : <em>—</em>)}
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
                    <IconButton size="small" disabled={i === 0} onClick={() => move(s.id, "up")} title="Move up"><ArrowUpwardIcon fontSize="small" /></IconButton>
                    <IconButton size="small" disabled={i === steps.length - 1} onClick={() => move(s.id, "down")} title="Move down"><ArrowDownwardIcon fontSize="small" /></IconButton>
                    <IconButton size="small" onClick={() => startEditStep(s)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => remove(s.id)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No workflow steps configured yet.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? "Edit Step" : "Add Step"}</Typography>
      <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
        <TextField size="small" label="Step Name" placeholder="e.g. TSB" value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {isBiochemical && (
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
            <Select size="small" displayEmpty value={pendingPhenotypicTest} onChange={(e) => setPendingPhenotypicTest(e.target.value as string)} sx={{ minWidth: 180 }}>
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
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }}>
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
          <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
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
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mt: 1.5 }}>
          <Select size="small" displayEmpty value={form.targetOrganismId ?? ""} onChange={(e) => setForm({ ...form, targetOrganismId: e.target.value === "" ? null : Number(e.target.value) })} sx={{ minWidth: 220 }}>
            <MenuItem value=""><em>Target Organism (required)</em></MenuItem>
            {organisms.map((o) => <MenuItem key={o.id} value={o.id}>{o.scientificName}</MenuItem>)}
          </Select>
        </Stack>
      )}

      {!hasNoMedia && (
        <Box sx={{ mt: 1.5 }}>
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 0.5 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 12 }}>Step Media</Typography>
            {isSingleMedia && <Typography variant="caption" color="text.secondary">This step type allows exactly one medium.</Typography>}
          </Stack>
          {/* Update replaces the whole StepMedia set server-side (no merge) -
              flagged here so an admin editing an existing step isn't
              surprised that rows not shown in this list get removed. */}
          {editingStepId && (
            <Alert severity="info" sx={{ mb: 1, maxWidth: 520 }}>Saving replaces this step's entire medium list with what's shown below.</Alert>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
            Pick a Media Configuration to derive this medium's temperature and incubation range (recommended - keeps
            this in sync with the profile approved on the Media Configurations page). Material stays a separate pick:
            it identifies the specific product for release/traceability, while the configuration governs its window.
          </Typography>
          <Stack spacing={1}>
            {form.stepMedia.map((row, idx) => {
              const hasConfig = row.mediaConfigurationId !== "";
              return (
                <Stack key={idx} direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
                  <Select size="small" displayEmpty value={row.materialId} onChange={(e) => updateMediaRow(idx, { materialId: e.target.value === "" ? "" : Number(e.target.value) })} sx={{ minWidth: 200 }}>
                    <MenuItem value=""><em>Material</em></MenuItem>
                    {materials.map((m) => <MenuItem key={m.id} value={m.id}>{m.materialName}</MenuItem>)}
                  </Select>
                  <Select size="small" displayEmpty value={row.mediaConfigurationId} onChange={(e) => updateMediaRow(idx, { mediaConfigurationId: e.target.value === "" ? "" : Number(e.target.value) })} sx={{ minWidth: 260 }}>
                    <MenuItem value=""><em>Media Configuration (optional)</em></MenuItem>
                    {mediaConfigurations.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {c.name} — {c.incubationMinHours}–{c.incubationMaxHours}h @ {c.temperatureMin}–{c.temperatureMax}°C
                      </MenuItem>
                    ))}
                  </Select>
                  <TextField
                    size="small" type="number" label="Temp Min" value={row.tempMin} disabled={hasConfig}
                    onChange={(e) => updateMediaRow(idx, { tempMin: e.target.value })} sx={{ width: 90 }}
                  />
                  <TextField
                    size="small" type="number" label="Temp Max" value={row.tempMax} disabled={hasConfig}
                    onChange={(e) => updateMediaRow(idx, { tempMax: e.target.value })} sx={{ width: 90 }}
                  />
                  <TextField
                    size="small" type="number" label="Min Hours" value={row.incubationMinHours} disabled={hasConfig}
                    onChange={(e) => updateMediaRow(idx, { incubationMinHours: e.target.value })} sx={{ width: 100 }}
                  />
                  <TextField
                    size="small" type="number" label="Max Hours" value={row.incubationMaxHours} disabled={hasConfig}
                    onChange={(e) => updateMediaRow(idx, { incubationMaxHours: e.target.value })} sx={{ width: 100 }}
                  />
                  {!isConfirmatory && (
                    <FormControlLabel
                      control={<Checkbox checked={row.isRequired} onChange={(e) => updateMediaRow(idx, { isRequired: e.target.checked })} />}
                      label="Required"
                    />
                  )}
                  <Typography variant="caption" color="text.secondary">Order {idx + 1}</Typography>
                  <IconButton size="small" color="error" onClick={() => removeMediaRow(idx)} title="Remove medium"><DeleteIcon fontSize="small" /></IconButton>
                </Stack>
              );
            })}
          </Stack>
          <Button size="small" sx={{ mt: 1 }} disabled={isSingleMedia && form.stepMedia.length >= 1} onClick={addMediaRow}>Add Medium</Button>
        </Box>
      )}
    </Box>
  );
}



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
export function TestMasterPage() {
  const { options, addNew, update, setActive, reload } = useTestDefinitions();
  const [code, setCode] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const startEdit = (t: TestDefinitionOption) => {
    setEditingId(t.id);
    setCode(t.code);
    setDisplayName(t.displayName);
    setMessage(null);
  };

  const cancelEdit = () => { setEditingId(null); setCode(""); setDisplayName(""); };

  const save = async () => {
    setMessage(null);
    if (!code || !displayName) {
      setMessage({ text: "Both Code and Display Name are required.", ok: false });
      return;
    }
    try {
      if (editingId) {
        await update(editingId, code, displayName);
        setMessage({ text: `Test "${code}" updated.`, ok: true });
      } else {
        await addNew(code, displayName);
        setMessage({ text: `Test "${code}" added to the Test Master.`, ok: true });
      }
      cancelEdit();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this test.`, ok: false });
    }
  };

  const toggleFreeze = async (t: TestDefinitionOption) => {
    setMessage(null);
    try {
      await setActive(t.id, !t.isActive);
      setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update this test's status.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Test Master" subtitle="The canonical list of tests available to assign to Items, Sampling Points, Rooms, and Machine Parts." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingId ? "Edit Test" : "Add Test"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Code" placeholder="e.g. PATHOGEN_SALMONELLA" value={code} onChange={(e) => setCode(e.target.value)} sx={{ minWidth: 220 }} />
          <TextField size="small" label="Display Name" placeholder="e.g. Pathogen - Salmonella" value={displayName} onChange={(e) => setDisplayName(e.target.value)} sx={{ minWidth: 260 }} />
          {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
          <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add Test"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>All Tests</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table size="small">
          <TableHead><TableRow><TableCell /><TableCell>Code</TableCell><TableCell>Display Name</TableCell><TableCell>Status</TableCell><TableCell></TableCell></TableRow></TableHead>
          <TableBody>
            {options.map((t) => (
              <Fragment key={t.id}>
                <TableRow sx={{ opacity: t.isActive ? 1 : 0.6 }}>
                  <TableCell sx={{ width: 40 }}>
                    <IconButton size="small" onClick={() => setExpandedId(expandedId === t.id ? null : t.id)} title="Approved Media">
                      {expandedId === t.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{t.code}</TableCell>
                  <TableCell>{t.displayName}</TableCell>
                  <TableCell><StatusBadge status={t.isActive ? "Active" : "Frozen"} /></TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startEdit(t)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" onClick={() => toggleFreeze(t)} title={t.isActive ? "Freeze" : "Unfreeze"}>
                      {t.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={5}>
                    <Collapse in={expandedId === t.id} unmountOnExit>
                      <WorkflowStepsSection test={t} onWorkflowTypeChanged={reload} />
                    </Collapse>
                  </TableCell>
                </TableRow>
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
