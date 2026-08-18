import { Fragment, useEffect, useState } from "react";
import {
  Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box,
  Table, TableHead, TableRow, TableCell, TableBody, IconButton, Collapse
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { TestCodePickerMulti } from "../../../components/TestCodePickerMulti";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { WaterConfigService } from "./services/WaterConfigService";

interface SamplingPoint { id: number; code: string; location: string; testingFrequency: string; assignedTestCodes: string[]; waterDepartmentId: number | null }
interface WaterDept { id: number; name: string; samplingPoints: SamplingPoint[] }

// Per-sample-location limit rows. Only CountTest-typed assigned tests
// (TAMC-Water/TYMC) get Alert/Action/Spec - pathogens are presence/
// absence. Mirrors EMConfigPage's RoomTestConfigSection.
function SamplingPointTestConfigSection({ point }: { point: SamplingPoint }) {
  const { options } = useTestDefinitions();
  const [configs, setConfigs] = useState<any[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<Record<string, any>>({});
  const [pendingDelete, setPendingDelete] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);

  const countTestCodes = point.assignedTestCodes.filter(
    (code) => options.find((o) => o.code === code)?.workflowType === "CountTest"
  );

  const load = () => WaterConfigService.getSamplingConfigurations(point.id).then(setConfigs);
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [point.id]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const startEdit = (c: any) => {
    setEditingId(c.id);
    setForm({ testCode: c.testCode, alertLimit: c.alertLimit, actionLimit: c.actionLimit, specLimit: c.specLimit });
    setError(null);
  };
  const cancelEdit = () => { setEditingId(null); setForm({}); };

  const save = async () => {
    setError(null);
    if (!form.testCode) { setError("Select a count test."); return; }
    try {
      if (editingId) {
        await WaterConfigService.updateSamplingConfiguration(editingId, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "");
      } else {
        await WaterConfigService.createSamplingConfiguration(point.id, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "");
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this configuration.");
    }
  };

  const remove = async (id: number) => {
    await WaterConfigService.deleteSamplingConfiguration(id);
    setPendingDelete(null);
    load();
  };

  return (
    <Box sx={{ p: 2, bgcolor: "#faf9fc" }}>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {configs.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow><TableCell>Test Code</TableCell><TableCell>Alert</TableCell><TableCell>Action</TableCell><TableCell>Specification</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {configs.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.testCode}</TableCell>
                <TableCell>{c.alertLimit || "—"}</TableCell>
                <TableCell>{c.actionLimit || "—"}</TableCell>
                <TableCell>{c.specLimit || "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(c)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(c)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No limits configured yet for this location.</Typography>
      )}

      {countTestCodes.length === 0 ? (
        <Typography variant="body2" color="text.secondary">Assign a count test (e.g. TAMC-Water) to this location to set Alert/Action/Specification limits.</Typography>
      ) : (
        <>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>{editingId ? "Edit Limits" : "Add Limits"}</Typography>
          <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
            <Select size="small" displayEmpty value={form.testCode ?? ""} onChange={(e) => setField("testCode", e.target.value)} sx={{ minWidth: 180 }}>
              <MenuItem value=""><em>Count Test</em></MenuItem>
              {countTestCodes.map((code) => <MenuItem key={code} value={code}>{code}</MenuItem>)}
            </Select>
            <TextField size="small" placeholder="Alert" value={form.alertLimit ?? ""} onChange={(e) => setField("alertLimit", e.target.value)} sx={{ width: 100 }} />
            <TextField size="small" placeholder="Action" value={form.actionLimit ?? ""} onChange={(e) => setField("actionLimit", e.target.value)} sx={{ width: 100 }} />
            <TextField size="small" placeholder="Specification" value={form.specLimit ?? ""} onChange={(e) => setField("specLimit", e.target.value)} sx={{ width: 120 }} />
            {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
            <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add"}</Button>
          </Stack>
        </>
      )}

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testCode} limits for this location?` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete.id)}
      />
    </Box>
  );
}

// Sampling Points + their assigned tests + per-count-test limits - read
// by WaterWorkflowEngine on every water sample receipt (assigned tests)
// and calculation (limits). Mirrors EMConfigPage's Department -> Room ->
// per-test-limits hierarchy.
export function WaterConfigPage() {
  const [departments, setDepartments] = useState<WaterDept[]>([]);
  const [deptForm, setDeptForm] = useState<Record<string, any>>({});
  const [editingDeptId, setEditingDeptId] = useState<number | null>(null);
  const [pendingDeleteDept, setPendingDeleteDept] = useState<WaterDept | null>(null);

  const [pointForm, setPointForm] = useState<Record<string, any>>({ testCodes: [] });
  const [editingPointId, setEditingPointId] = useState<number | null>(null);
  const [pendingDeletePoint, setPendingDeletePoint] = useState<SamplingPoint | null>(null);

  const [expandedDeptId, setExpandedDeptId] = useState<number | null>(null);
  const [expandedPointId, setExpandedPointId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => WaterConfigService.getWaterDepartments().then(setDepartments).catch(() => setDepartments([]));
  useEffect(() => { load(); }, []);

  const cancelDeptEdit = () => { setEditingDeptId(null); setDeptForm({}); };
  const startDeptEdit = (d: WaterDept) => { setEditingDeptId(d.id); setDeptForm({ name: d.name }); setMessage(null); };
  const saveDept = async () => {
    setMessage(null);
    try {
      if (editingDeptId) { await WaterConfigService.updateWaterDepartment(editingDeptId, deptForm.name); setMessage({ text: "Department updated.", ok: true }); }
      else { await WaterConfigService.createWaterDepartment(deptForm.name); setMessage({ text: "Department created.", ok: true }); }
      cancelDeptEdit(); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Could not save this department.", ok: false }); }
  };
  const deleteDept = async (d: WaterDept) => {
    setMessage(null);
    try { await WaterConfigService.deleteWaterDepartment(d.id); setPendingDeleteDept(null); load(); }
    catch (e: any) { setPendingDeleteDept(null); setMessage({ text: e?.response?.data?.message ?? "Could not delete this department.", ok: false }); }
  };

  const cancelPointEdit = () => { setEditingPointId(null); setPointForm({ testCodes: [] }); };
  const startPointEdit = (p: SamplingPoint) => { setEditingPointId(p.id); setPointForm({ code: p.code, location: p.location, frequency: p.testingFrequency, departmentId: p.waterDepartmentId, testCodes: p.assignedTestCodes }); setMessage(null); };
  const savePoint = async () => {
    setMessage(null);
    if (!pointForm.code || !pointForm.departmentId) { setMessage({ text: "Point Code and Department are required.", ok: false }); return; }
    try {
      if (editingPointId) { await WaterConfigService.updateSamplingPoint(editingPointId, pointForm.code, pointForm.location ?? "", pointForm.frequency ?? "", pointForm.testCodes ?? [], Number(pointForm.departmentId)); setMessage({ text: "Sample location updated.", ok: true }); }
      else { await WaterConfigService.createSamplingPoint(pointForm.code, pointForm.location ?? "", pointForm.frequency ?? "", pointForm.testCodes ?? [], Number(pointForm.departmentId)); setMessage({ text: "Sample location created.", ok: true }); }
      cancelPointEdit(); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Could not save this sample location.", ok: false }); }
  };
  const deletePoint = async (p: SamplingPoint) => {
    setMessage(null);
    try { await WaterConfigService.deleteSamplingPoint(p.id); setPendingDeletePoint(null); load(); }
    catch (e: any) { setPendingDeletePoint(null); setMessage({ text: e?.response?.data?.message ?? "Could not delete this sample location.", ok: false }); }
  };

  return (
    <>
      <PageHeader title="Water" subtitle="Departments, sample locations, assigned tests, and per-location limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingDeptId ? "Edit Department" : "New Department"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Name" value={deptForm.name ?? ""} onChange={(e) => setDeptForm({ ...deptForm, name: e.target.value })} />
          {editingDeptId && <Button onClick={cancelDeptEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={saveDept}>{editingDeptId ? "Save Changes" : "Add Department"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>{editingPointId ? "Edit Sample Location" : "New Sample Location"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Point Code" value={pointForm.code ?? ""} onChange={(e) => setPointForm({ ...pointForm, code: e.target.value })} />
          <TextField size="small" label="Point Name" value={pointForm.location ?? ""} onChange={(e) => setPointForm({ ...pointForm, location: e.target.value })} />
          <TextField size="small" label="Testing Frequency" value={pointForm.frequency ?? ""} onChange={(e) => setPointForm({ ...pointForm, frequency: e.target.value })} placeholder="e.g. Weekly" />
          <Select size="small" displayEmpty value={pointForm.departmentId ?? ""} onChange={(e) => setPointForm({ ...pointForm, departmentId: e.target.value })} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Department</em></MenuItem>
            {departments.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
          </Select>
          <TestCodePickerMulti value={pointForm.testCodes ?? []} onChange={(codes) => setPointForm({ ...pointForm, testCodes: codes })} label="Assigned Tests" sx={{ minWidth: 280 }} />
          {editingPointId && <Button onClick={cancelPointEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={savePoint}>{editingPointId ? "Save Changes" : "Add Sample Location"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>Departments</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Department</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>
            {departments.map((d) => (
              <Fragment key={d.id}>
                <TableRow>
                  <TableCell>
                    <IconButton size="small" onClick={() => setExpandedDeptId(expandedDeptId === d.id ? null : d.id)} title="Sample Locations">
                      {expandedDeptId === d.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{d.name}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startDeptEdit(d)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDeleteDept(d)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={3}>
                    <Collapse in={expandedDeptId === d.id} unmountOnExit>
                      <Box sx={{ p: 2, bgcolor: "#f5f3fa" }}>
                        {(d.samplingPoints ?? []).length === 0 ? (
                          <Typography variant="body2" color="text.secondary">No sample locations yet.</Typography>
                        ) : (
                          <Table size="small">
                            <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Location Code</TableCell><TableCell>Point Name</TableCell><TableCell>Testing Frequency</TableCell><TableCell>Assigned Tests</TableCell><TableCell /></TableRow></TableHead>
                            <TableBody>
                              {(d.samplingPoints ?? []).map((p) => (
                                <Fragment key={p.id}>
                                  <TableRow>
                                    <TableCell>
                                      <IconButton size="small" onClick={() => setExpandedPointId(expandedPointId === p.id ? null : p.id)} title="Limits">
                                        {expandedPointId === p.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                                      </IconButton>
                                    </TableCell>
                                    <TableCell>{p.code}</TableCell>
                                    <TableCell>{p.location || "—"}</TableCell>
                                    <TableCell>{p.testingFrequency || "—"}</TableCell>
                                    <TableCell>{(p.assignedTestCodes ?? []).join(", ") || "—"}</TableCell>
                                    <TableCell align="right">
                                      <IconButton size="small" onClick={() => startPointEdit(p)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                                      <IconButton size="small" color="error" onClick={() => setPendingDeletePoint(p)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                                    </TableCell>
                                  </TableRow>
                                  <TableRow>
                                    <TableCell sx={{ p: 0, border: 0 }} colSpan={6}>
                                      <Collapse in={expandedPointId === p.id} unmountOnExit>
                                        <SamplingPointTestConfigSection point={p} />
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
        open={pendingDeleteDept != null}
        message={pendingDeleteDept ? `Delete department "${pendingDeleteDept.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeleteDept(null)}
        onConfirm={() => pendingDeleteDept && deleteDept(pendingDeleteDept)}
      />
      <ConfirmationDialog
        open={pendingDeletePoint != null}
        message={pendingDeletePoint ? `Delete sample location "${pendingDeletePoint.code}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeletePoint(null)}
        onConfirm={() => pendingDeletePoint && deletePoint(pendingDeletePoint)}
      />
    </>
  );
}
