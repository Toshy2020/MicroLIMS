import { Fragment, useEffect, useState } from "react";
import {
  Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box, Checkbox, FormControlLabel,
  Table, TableHead, TableRow, TableCell, TableBody, IconButton, Collapse
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { TestCodePicker } from "../../../components/TestCodePicker";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { AfterCleaningConfigService } from "./services/AfterCleaningConfigService";

const TEST_TYPES = ["Swab", "Rinse", "Pathogen"];

// Test configurations for one machine part - previously captured via a
// form but never shown anywhere, so there was no way to see, edit, or
// delete what had been configured. Mirrors EMConfigPage's
// RoomTestConfigSection.
function PartConfigSection({ machinePartId }: { machinePartId: number }) {
  const [configs, setConfigs] = useState<any[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<Record<string, any>>({ testType: "Swab", isPathogenTest: false });
  const [pendingDelete, setPendingDelete] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = () => AfterCleaningConfigService.getPartConfigurations(machinePartId).then(setConfigs);
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [machinePartId]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const startEdit = (c: any) => {
    setEditingId(c.id);
    setForm({ testType: c.testType, testCode: c.testCode, alertLimit: c.alertLimit, actionLimit: c.actionLimit, specLimit: c.specLimit, isPathogenTest: c.isPathogenTest });
    setError(null);
  };
  const cancelEdit = () => { setEditingId(null); setForm({ testType: "Swab", isPathogenTest: false }); };

  const save = async () => {
    setError(null);
    if (!form.testCode) { setError("Test Code is required."); return; }
    try {
      if (editingId) {
        await AfterCleaningConfigService.updatePartConfiguration(editingId, form.testType, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "", !!form.isPathogenTest);
      } else {
        await AfterCleaningConfigService.createPartConfiguration(machinePartId, form.testType, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "", !!form.isPathogenTest);
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this configuration.");
    }
  };

  const remove = async (id: number) => {
    await AfterCleaningConfigService.deletePartConfiguration(id);
    setPendingDelete(null);
    load();
  };

  return (
    <Box sx={{ p: 2, bgcolor: "#faf9fc" }}>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {configs.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow><TableCell>Test Type</TableCell><TableCell>Test Code</TableCell><TableCell>Alert</TableCell><TableCell>Action</TableCell><TableCell>Spec</TableCell><TableCell>Pathogen</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {configs.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.testType}</TableCell>
                <TableCell>{c.testCode}</TableCell>
                <TableCell>{c.alertLimit || "—"}</TableCell>
                <TableCell>{c.actionLimit || "—"}</TableCell>
                <TableCell>{c.specLimit || "—"}</TableCell>
                <TableCell>{c.isPathogenTest ? "Yes" : "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(c)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(c)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No test configurations yet for this part.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>{editingId ? "Edit Configuration" : "Add Configuration"}</Typography>
      <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
        <Select size="small" value={form.testType} onChange={(e) => setField("testType", e.target.value)} sx={{ minWidth: 120 }}>
          {TEST_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </Select>
        <TestCodePicker value={form.testCode ?? ""} onChange={(code) => setField("testCode", code)} label="Test Code" sx={{ minWidth: 200 }} />
        <TextField size="small" placeholder="Alert" value={form.alertLimit ?? ""} onChange={(e) => setField("alertLimit", e.target.value)} sx={{ width: 90 }} />
        <TextField size="small" placeholder="Action" value={form.actionLimit ?? ""} onChange={(e) => setField("actionLimit", e.target.value)} sx={{ width: 90 }} />
        <TextField size="small" placeholder="Spec" value={form.specLimit ?? ""} onChange={(e) => setField("specLimit", e.target.value)} sx={{ width: 90 }} />
        <FormControlLabel
          control={<Checkbox checked={!!form.isPathogenTest} onChange={(e) => setField("isPathogenTest", e.target.checked)} />}
          label="Pathogen test"
        />
        {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
        <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add"}</Button>
      </Stack>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testType} / ${pendingDelete.testCode} configuration for this part?` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete.id)}
      />
    </Box>
  );
}

export function AfterCleaningConfigPage() {
  const [machines, setMachines] = useState<any[]>([]);
  const [machineName, setMachineName] = useState("");
  const [editingMachineId, setEditingMachineId] = useState<number | null>(null);
  const [pendingDeleteMachine, setPendingDeleteMachine] = useState<any | null>(null);

  const [partName, setPartName] = useState("");
  const [machineId, setMachineId] = useState("");
  const [editingPartId, setEditingPartId] = useState<number | null>(null);
  const [pendingDeletePart, setPendingDeletePart] = useState<any | null>(null);

  const [expandedMachineId, setExpandedMachineId] = useState<number | null>(null);
  const [expandedPartId, setExpandedPartId] = useState<number | null>(null);

  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => AfterCleaningConfigService.getMachines().then(setMachines);
  useEffect(() => { load(); }, []);

  const cancelMachineEdit = () => { setEditingMachineId(null); setMachineName(""); };
  const startMachineEdit = (m: any) => { setEditingMachineId(m.id); setMachineName(m.name); setMessage(null); };
  const saveMachine = async () => {
    setMessage(null);
    try {
      if (editingMachineId) {
        await AfterCleaningConfigService.updateMachine(editingMachineId, machineName);
        setMessage({ text: "Machine updated.", ok: true });
      } else {
        await AfterCleaningConfigService.createMachine(machineName);
        setMessage({ text: "Machine created.", ok: true });
      }
      cancelMachineEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this machine.", ok: false });
    }
  };
  const deleteMachine = async (m: any) => {
    setMessage(null);
    try {
      await AfterCleaningConfigService.deleteMachine(m.id);
      setPendingDeleteMachine(null);
      load();
    } catch (e: any) {
      setPendingDeleteMachine(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this machine.", ok: false });
    }
  };

  const cancelPartEdit = () => { setEditingPartId(null); setPartName(""); setMachineId(""); };
  const startPartEdit = (p: any) => { setEditingPartId(p.id); setPartName(p.name); setMachineId(String(p.machineId)); setMessage(null); };
  const savePart = async () => {
    setMessage(null);
    try {
      if (editingPartId) {
        await AfterCleaningConfigService.updateMachinePart(editingPartId, partName, Number(machineId));
        setMessage({ text: "Part updated.", ok: true });
      } else {
        await AfterCleaningConfigService.createMachinePart(partName, Number(machineId));
        setMessage({ text: "Part added.", ok: true });
      }
      cancelPartEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this part.", ok: false });
    }
  };
  const deletePart = async (p: any) => {
    setMessage(null);
    try {
      await AfterCleaningConfigService.deleteMachinePart(p.id);
      setPendingDeletePart(null);
      load();
    } catch (e: any) {
      setPendingDeletePart(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this part.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="After Cleaning" subtitle="Machines, parts, and per-part test limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingMachineId ? "Edit Machine" : "New Machine"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <TextField size="small" label="Machine Name" value={machineName} onChange={(e) => setMachineName(e.target.value)} />
          {editingMachineId && <Button onClick={cancelMachineEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={saveMachine}>{editingMachineId ? "Save Changes" : "Add Machine"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>{editingPartId ? "Edit Part" : "New Part"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <Select size="small" displayEmpty value={machineId} onChange={(e) => setMachineId(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Machine</em></MenuItem>
            {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
          </Select>
          <TextField size="small" label="Part Name" value={partName} onChange={(e) => setPartName(e.target.value)} />
          {editingPartId && <Button onClick={cancelPartEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={savePart}>{editingPartId ? "Save Changes" : "Add Part"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>Machines</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Machine</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>
            {machines.map((m) => (
              <Fragment key={m.id}>
                <TableRow>
                  <TableCell>
                    <IconButton size="small" onClick={() => setExpandedMachineId(expandedMachineId === m.id ? null : m.id)} title="Parts">
                      {expandedMachineId === m.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{m.name}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startMachineEdit(m)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDeleteMachine(m)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={3}>
                    <Collapse in={expandedMachineId === m.id} unmountOnExit>
                      <Box sx={{ p: 2, bgcolor: "#f5f3fa" }}>
                        {(m.parts ?? []).length === 0 ? (
                          <Typography variant="body2" color="text.secondary">No parts configured yet.</Typography>
                        ) : (
                          <Table size="small">
                            <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Part</TableCell><TableCell /></TableRow></TableHead>
                            <TableBody>
                              {(m.parts ?? []).map((p: any) => (
                                <Fragment key={p.id}>
                                  <TableRow>
                                    <TableCell>
                                      <IconButton size="small" onClick={() => setExpandedPartId(expandedPartId === p.id ? null : p.id)} title="Test Configurations">
                                        {expandedPartId === p.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                                      </IconButton>
                                    </TableCell>
                                    <TableCell>{p.name}</TableCell>
                                    <TableCell align="right">
                                      <IconButton size="small" onClick={() => startPartEdit({ ...p, machineId: m.id })} title="Edit"><EditIcon fontSize="small" /></IconButton>
                                      <IconButton size="small" color="error" onClick={() => setPendingDeletePart(p)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                                    </TableCell>
                                  </TableRow>
                                  <TableRow>
                                    <TableCell sx={{ p: 0, border: 0 }} colSpan={3}>
                                      <Collapse in={expandedPartId === p.id} unmountOnExit>
                                        <PartConfigSection machinePartId={p.id} />
                                      </Collapse>
                                    </TableCell>
                                  </TableRow>
                                </Fragment>
                              ))}
                            </TableBody>
                          </Table>
                        )}
                      </Box>
                    </Collapse>
                  </TableCell>
                </TableRow>
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <ConfirmationDialog
        open={pendingDeleteMachine != null}
        message={pendingDeleteMachine ? `Delete machine "${pendingDeleteMachine.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeleteMachine(null)}
        onConfirm={() => pendingDeleteMachine && deleteMachine(pendingDeleteMachine)}
      />
      <ConfirmationDialog
        open={pendingDeletePart != null}
        message={pendingDeletePart ? `Delete part "${pendingDeletePart.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeletePart(null)}
        onConfirm={() => pendingDeletePart && deletePart(pendingDeletePart)}
      />
    </>
  );
}
