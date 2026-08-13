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

type StepMediaRow = { materialId: number | ""; tempMin: string; tempMax: string; isRequired: boolean; displayOrder: number };

interface StepFormState {
  stepName?: string;
  mediaTypeId?: number | "";
  incubationMinHours?: string | number;
  incubationMaxHours?: string | number;
  temperatureMin?: string | number;
  temperatureMax?: string | number;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId: number | null;
  stepMedia: StepMediaRow[];
}

// A function rather than a shared constant object, so every reset gets its
// own stepMedia array instead of every WorkflowStepsSection instance (one
// per expanded Test Master row) sharing a single mutable reference.
const defaultStepForm = (): StepFormState => ({ isFinalStep: false, stepType: "PlateCount", targetOrganismId: null, stepMedia: [] });

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
  for (const m of form.stepMedia) {
    if (m.materialId === "") return "Every medium row needs a selected material.";
    if (Number(m.tempMin) >= Number(m.tempMax)) return "Every medium's minimum temperature must be below its maximum.";
  }
  const materialIds = form.stepMedia.map((m) => m.materialId);
  if (new Set(materialIds).size !== materialIds.length) return "The same medium cannot be assigned to this step more than once.";
  return null;
}

// True for rows the pathogen-workflow migration could not backfill
// (TargetOrganismId/StepMedia are per-step and the migration had no source
// data for them) - flags templates that will fail validation the first time
// an analyst tries to run them, so an admin can find them here instead.
function stepNeedsConfiguration(s: any): boolean {
  if (STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (!["BiochemicalTest", "PlateCount"].includes(s.stepType) && (s.stepMedia?.length ?? 0) === 0) return true;
  return false;
}

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
function WorkflowStepsSection({ test, onWorkflowTypeChanged }: { test: TestDefinitionOption; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<any[]>([]);
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [organisms, setOrganisms] = useState<any[]>([]);
  const [materials, setMaterials] = useState<any[]>([]);
  const [form, setForm] = useState<StepFormState>(defaultStepForm);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadSteps = () => masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps);
  useEffect(() => {
    masterDataOptions.getMediaTypes().then(setMediaTypes);
    masterDataOptions.getOrganisms().then(setOrganisms);
    masterDataOptions.getMaterials("DehydratedMedia").then(setMaterials);
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
    setForm({
      stepName: s.stepName, mediaTypeId: s.mediaTypeId, incubationMinHours: s.incubationMinHours, incubationMaxHours: s.incubationMaxHours,
      temperatureMin: s.temperatureMin, temperatureMax: s.temperatureMax, isFinalStep: s.isFinalStep, stepType: s.stepType,
      targetOrganismId: s.targetOrganismId ?? null,
      stepMedia: (s.stepMedia ?? []).map((m: any) => ({
        materialId: m.materialId, tempMin: String(m.tempMin), tempMax: String(m.tempMax), isRequired: m.isRequired, displayOrder: m.displayOrder
      }))
    });
    setError(null);
  };

  const cancelEditStep = () => { setEditingStepId(null); setForm(defaultStepForm()); };

  const needsOrganism = STEP_TYPES_REQUIRING_ORGANISM.includes(form.stepType);
  const hasNoMedia = STEP_TYPES_WITH_NO_MEDIA.includes(form.stepType);
  const isSingleMedia = STEP_TYPES_SINGLE_MEDIA.includes(form.stepType);
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";

  // Switching StepType clears media/organism rather than carrying over a
  // combination that likely no longer satisfies that type's rules (e.g. a
  // Broth's single required medium isn't valid as-is for ConfirmatoryPlating,
  // which forbids IsRequired on every row) - forces a deliberate re-pick
  // instead of silently submitting a stale, mismatched configuration.
  const changeStepType = (stepType: string) => setForm({ ...form, stepType, targetOrganismId: null, stepMedia: [] });

  const addMediaRow = () => setForm({
    ...form,
    stepMedia: [...form.stepMedia, { materialId: "", tempMin: "", tempMax: "", isRequired: false, displayOrder: form.stepMedia.length }]
  });
  const updateMediaRow = (index: number, patch: Partial<StepMediaRow>) => setForm({
    ...form,
    stepMedia: form.stepMedia.map((m, i) => (i === index ? { ...m, ...patch } : m))
  });
  const removeMediaRow = (index: number) => setForm({ ...form, stepMedia: form.stepMedia.filter((_, i) => i !== index) });

  const saveStep = async () => {
    setError(null);
    if (!form.stepName || !form.mediaTypeId) {
      setError("Step Name and Media Type are required.");
      return;
    }
    const validationError = validateStepForm(form);
    if (validationError) {
      setError(validationError);
      return;
    }
    const payload = {
      stepName: form.stepName, mediaTypeId: Number(form.mediaTypeId),
      incubationMinHours: Number(form.incubationMinHours) || 0, incubationMaxHours: Number(form.incubationMaxHours) || 0,
      temperatureMin: Number(form.temperatureMin) || 0, temperatureMax: Number(form.temperatureMax) || 0,
      isFinalStep: !!form.isFinalStep, stepType: form.stepType, targetOrganismId: form.targetOrganismId,
      stepMedia: form.stepMedia.map((m, i) => ({
        materialId: Number(m.materialId), tempMin: Number(m.tempMin), tempMax: Number(m.tempMax),
        isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired, displayOrder: i
      }))
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
    <Box sx={{ p: 2, bgcolor: "#f5f3fa", borderTop: "1px solid #e5e7eb" }}>
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
              <TableCell>#</TableCell><TableCell>Step</TableCell><TableCell>Media Class</TableCell><TableCell>Incubation</TableCell>
              <TableCell>Temp °C</TableCell><TableCell>Step Type</TableCell><TableCell>Media</TableCell><TableCell>Organism</TableCell>
              <TableCell>Status</TableCell><TableCell>Final</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {steps.map((s, i) => (
              <TableRow key={s.id}>
                <TableCell>{s.stepOrder}</TableCell>
                <TableCell>{s.stepName}</TableCell>
                <TableCell>{mediaClassLabel(s.mediaType?.class)}</TableCell>
                <TableCell>{s.incubationMinHours}-{s.incubationMaxHours}h</TableCell>
                <TableCell>{s.temperatureMin}-{s.temperatureMax}</TableCell>
                <TableCell>{s.stepType}</TableCell>
                <TableCell>{s.stepMedia?.length > 0 ? s.stepMedia.map((m: any) => m.materialName).join(", ") : <em>—</em>}</TableCell>
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
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No workflow steps configured yet.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? "Edit Step" : "Add Step"}</Typography>
      <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
        <TextField size="small" label="Step Name" placeholder="e.g. TSB" value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {/* Still required for every StepType, including the pathogen ones -
            TestWorkflowStep.MediaTypeId is non-nullable server-side, so this
            selector stays even though it's semantically vestigial for
            anything but PlateCount. Do not remove or conditionally hide it. */}
        <Select size="small" displayEmpty value={form.mediaTypeId ?? ""} onChange={(e) => setForm({ ...form, mediaTypeId: e.target.value as number })} sx={{ minWidth: 160 }}>
          <MenuItem value=""><em>Media Type</em></MenuItem>
          {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
        </Select>
        <TextField size="small" type="number" label="Min Hours" value={form.incubationMinHours ?? ""} onChange={(e) => setForm({ ...form, incubationMinHours: e.target.value })} sx={{ width: 100 }} />
        <TextField size="small" type="number" label="Max Hours" value={form.incubationMaxHours ?? ""} onChange={(e) => setForm({ ...form, incubationMaxHours: e.target.value })} sx={{ width: 100 }} />
        <TextField size="small" type="number" label="Temp Min" value={form.temperatureMin ?? ""} onChange={(e) => setForm({ ...form, temperatureMin: e.target.value })} sx={{ width: 90 }} />
        <TextField size="small" type="number" label="Temp Max" value={form.temperatureMax ?? ""} onChange={(e) => setForm({ ...form, temperatureMax: e.target.value })} sx={{ width: 90 }} />
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }}>
          {STEP_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </Select>
        <FormControlLabel
          control={<Checkbox checked={!!form.isFinalStep} onChange={(e) => setForm({ ...form, isFinalStep: e.target.checked })} />}
          label="Final Step"
        />
        {editingStepId && <Button onClick={cancelEditStep}>Cancel</Button>}
        <Button variant="contained" onClick={saveStep}>{editingStepId ? "Save Changes" : "Add Step"}</Button>
      </Stack>

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
          <Stack spacing={1}>
            {form.stepMedia.map((row, idx) => (
              <Stack key={idx} direction="row" spacing={1.5} alignItems="center">
                <Select size="small" displayEmpty value={row.materialId} onChange={(e) => updateMediaRow(idx, { materialId: e.target.value === "" ? "" : Number(e.target.value) })} sx={{ minWidth: 220 }}>
                  <MenuItem value=""><em>Material</em></MenuItem>
                  {materials.map((m) => <MenuItem key={m.id} value={m.id}>{m.materialName}</MenuItem>)}
                </Select>
                <TextField size="small" type="number" label="Temp Min" value={row.tempMin} onChange={(e) => updateMediaRow(idx, { tempMin: e.target.value })} sx={{ width: 90 }} />
                <TextField size="small" type="number" label="Temp Max" value={row.tempMax} onChange={(e) => updateMediaRow(idx, { tempMax: e.target.value })} sx={{ width: 90 }} />
                {!isConfirmatory && (
                  <FormControlLabel
                    control={<Checkbox checked={row.isRequired} onChange={(e) => updateMediaRow(idx, { isRequired: e.target.checked })} />}
                    label="Required"
                  />
                )}
                <Typography variant="caption" color="text.secondary">Order {idx + 1}</Typography>
                <IconButton size="small" color="error" onClick={() => removeMediaRow(idx)} title="Remove medium"><DeleteIcon fontSize="small" /></IconButton>
              </Stack>
            ))}
          </Stack>
          <Button size="small" sx={{ mt: 1 }} disabled={isSingleMedia && form.stepMedia.length >= 1} onClick={addMediaRow}>Add Medium</Button>
        </Box>
      )}
    </Box>
  );
}

// Shown when a Test Master row is expanded - which MediaType(s) are
// approved to run this test, optionally scoped to one step of a
// multi-step chain (Pathogen). See backend TestDefinitionMedia.cs.
function ApprovedMediaSection({ testDefinitionId }: { testDefinitionId: number }) {
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [approved, setApproved] = useState<any[]>([]);
  const [mediaTypeId, setMediaTypeId] = useState<number | "">("");
  const [stepName, setStepName] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reload = () => masterDataOptions.getTestDefinitionMedia(testDefinitionId).then(setApproved);

  useEffect(() => {
    masterDataOptions.getMediaTypes().then(setMediaTypes);
    reload();
  }, [testDefinitionId]);

  const startEdit = (a: any) => {
    setEditingId(a.id);
    setMediaTypeId(a.mediaTypeId);
    setStepName(a.stepName ?? "");
    setError(null);
  };

  const cancelEdit = () => { setEditingId(null); setMediaTypeId(""); setStepName(""); };

  const save = async () => {
    setError(null);
    if (!mediaTypeId) {
      setError("Select a media type.");
      return;
    }
    try {
      if (editingId) {
        await masterDataOptions.updateTestDefinitionMedia(editingId, Number(mediaTypeId), stepName.trim() || undefined);
      } else {
        await masterDataOptions.createTestDefinitionMedia(testDefinitionId, Number(mediaTypeId), stepName.trim() || undefined);
      }
      cancelEdit();
      await reload();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this approved media.`);
    }
  };

  const remove = async (id: number) => {
    setError(null);
    try {
      await masterDataOptions.deleteTestDefinitionMedia(id);
      await reload();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not delete this approved media.");
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "#faf9fc" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>Approved Media</Typography>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {approved.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5, maxWidth: 480 }}>
          <TableHead><TableRow><TableCell>Media Type</TableCell><TableCell>Step Name</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>
            {approved.map((a) => (
              <TableRow key={a.id}>
                <TableCell>{mediaClassLabel(a.mediaType?.class ?? mediaTypes.find((m) => m.id === a.mediaTypeId)?.class)}</TableCell>
                <TableCell>{a.stepName ?? <em>—</em>}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(a)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => remove(a.id)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No media approved yet.</Typography>
      )}
      <Stack direction="row" spacing={1.5} alignItems="center">
        <Select size="small" displayEmpty value={mediaTypeId} onChange={(e) => setMediaTypeId(Number(e.target.value))} sx={{ minWidth: 200 }}>
          <MenuItem value=""><em>Media Type</em></MenuItem>
          {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
        </Select>
        <TextField size="small" label="Step Name (optional)" placeholder="e.g. TSB" value={stepName} onChange={(e) => setStepName(e.target.value)} sx={{ minWidth: 180 }} />
        {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
        <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add"}</Button>
      </Stack>
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
                      <ApprovedMediaSection testDefinitionId={t.id} />
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
