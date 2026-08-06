import { Fragment, useEffect, useState } from "react";
import { Paper, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Collapse, Box, Typography, Checkbox, FormControlLabel } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";

const WORKFLOW_TYPES = ["CountTest", "Observation", "DualPlate"];

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
function WorkflowStepsSection({ test, onWorkflowTypeChanged }: { test: TestDefinitionOption; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<any[]>([]);
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ isFinalStep: false, isDualPlate: false });
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadSteps = () => masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps);
  useEffect(() => {
    masterDataOptions.getMediaTypes().then(setMediaTypes);
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
      temperatureMin: s.temperatureMin, temperatureMax: s.temperatureMax, isFinalStep: s.isFinalStep, isDualPlate: s.isDualPlate
    });
    setError(null);
  };

  const cancelEditStep = () => { setEditingStepId(null); setForm({ isFinalStep: false, isDualPlate: false }); };

  const saveStep = async () => {
    setError(null);
    if (!form.stepName || !form.mediaTypeId) {
      setError("Step Name and Media Type are required.");
      return;
    }
    const payload = {
      stepName: form.stepName, mediaTypeId: Number(form.mediaTypeId),
      incubationMinHours: Number(form.incubationMinHours) || 0, incubationMaxHours: Number(form.incubationMaxHours) || 0,
      temperatureMin: Number(form.temperatureMin) || 0, temperatureMax: Number(form.temperatureMax) || 0,
      isFinalStep: !!form.isFinalStep, isDualPlate: !!form.isDualPlate
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
              <TableCell>Temp °C</TableCell><TableCell>Final</TableCell><TableCell>Dual Plate</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {steps.map((s, i) => (
              <TableRow key={s.id}>
                <TableCell>{s.stepOrder}</TableCell>
                <TableCell>{s.stepName}</TableCell>
                <TableCell>{mediaClassLabel(s.mediaType?.class)}</TableCell>
                <TableCell>{s.incubationMinHours}-{s.incubationMaxHours}h</TableCell>
                <TableCell>{s.temperatureMin}-{s.temperatureMax}</TableCell>
                <TableCell>{s.isFinalStep ? "Yes" : "—"}</TableCell>
                <TableCell>{s.isDualPlate ? "Yes" : "—"}</TableCell>
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
        <Select size="small" displayEmpty value={form.mediaTypeId ?? ""} onChange={(e) => setForm({ ...form, mediaTypeId: e.target.value })} sx={{ minWidth: 160 }}>
          <MenuItem value=""><em>Media Type</em></MenuItem>
          {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
        </Select>
        <TextField size="small" type="number" label="Min Hours" value={form.incubationMinHours ?? ""} onChange={(e) => setForm({ ...form, incubationMinHours: e.target.value })} sx={{ width: 100 }} />
        <TextField size="small" type="number" label="Max Hours" value={form.incubationMaxHours ?? ""} onChange={(e) => setForm({ ...form, incubationMaxHours: e.target.value })} sx={{ width: 100 }} />
        <TextField size="small" type="number" label="Temp Min" value={form.temperatureMin ?? ""} onChange={(e) => setForm({ ...form, temperatureMin: e.target.value })} sx={{ width: 90 }} />
        <TextField size="small" type="number" label="Temp Max" value={form.temperatureMax ?? ""} onChange={(e) => setForm({ ...form, temperatureMax: e.target.value })} sx={{ width: 90 }} />
        <FormControlLabel
          control={<Checkbox checked={!!form.isFinalStep} onChange={(e) => setForm({ ...form, isFinalStep: e.target.checked })} />}
          label="Final Step"
        />
        <FormControlLabel
          control={<Checkbox checked={!!form.isDualPlate} disabled={test.workflowType !== "DualPlate"} onChange={(e) => setForm({ ...form, isDualPlate: e.target.checked })} />}
          label="Dual Plate"
        />
        {editingStepId && <Button onClick={cancelEditStep}>Cancel</Button>}
        <Button variant="contained" onClick={saveStep}>{editingStepId ? "Save Changes" : "Add Step"}</Button>
      </Stack>
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
