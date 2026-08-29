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
import { TestCodePicker } from "../../../components/TestCodePicker";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { EMConfigService } from "./services/EMConfigService";

const TEST_TYPES = ["PassiveAirSample", "SurfaceAirSample"];

// Room-scoped test configurations (Alert/Action/Spec limits per Room x
// TestType) - previously captured via a form but never shown anywhere,
// so there was no way to see, edit, or delete what had been configured.
// Expanded from a Room row, mirroring AfterCleaningConfigPage's
// PartConfigSection.
function RoomTestConfigSection({ roomId }: { roomId: number }) {
  const [configs, setConfigs] = useState<any[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<Record<string, any>>({ testType: "PassiveAirSample" });
  const [pendingDelete, setPendingDelete] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = () => EMConfigService.getRoomTestConfigurations(roomId).then(setConfigs);
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [roomId]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const startEdit = (c: any) => {
    setEditingId(c.id);
    setForm({ testType: c.testType, testCode: c.testCode, alertLimit: c.alertLimit, actionLimit: c.actionLimit, specLimit: c.specLimit, unit: c.unit });
    setError(null);
  };
  const cancelEdit = () => { setEditingId(null); setForm({ testType: "PassiveAirSample" }); };

  const save = async () => {
    setError(null);
    if (!form.testCode) { setError("Test Code is required."); return; }
    try {
      if (editingId) {
        await EMConfigService.updateRoomTestConfiguration(editingId, form.testType, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "", form.unit ?? "");
      } else {
        await EMConfigService.createRoomTestConfiguration(roomId, form.testType, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "", form.unit ?? "");
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this configuration.");
    }
  };

  const remove = async (id: number) => {
    await EMConfigService.deleteRoomTestConfiguration(id);
    setPendingDelete(null);
    load();
  };

  return (
    <Box sx={{ p: 2, bgcolor: "background.default" }}>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {configs.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow><TableCell>Test Type</TableCell><TableCell>Test Code</TableCell><TableCell>Alert</TableCell><TableCell>Action</TableCell><TableCell>Spec</TableCell><TableCell>Unit</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {configs.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.testType}</TableCell>
                <TableCell>{c.testCode}</TableCell>
                <TableCell>{c.alertLimit || "—"}</TableCell>
                <TableCell>{c.actionLimit || "—"}</TableCell>
                <TableCell>{c.specLimit || "—"}</TableCell>
                <TableCell>{c.unit || "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(c)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(c)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No test configurations yet for this room.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>{editingId ? "Edit Configuration" : "Add Configuration"}</Typography>
      <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
        <Select size="small" value={form.testType} onChange={(e) => setField("testType", e.target.value)} sx={{ minWidth: 160 }}>
          {TEST_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </Select>
        <TestCodePicker value={form.testCode ?? ""} onChange={(code) => setField("testCode", code)} label="Test Code" sx={{ minWidth: 200 }} />
        <TextField size="small" placeholder="Alert" value={form.alertLimit ?? ""} onChange={(e) => setField("alertLimit", e.target.value)} sx={{ width: 90 }} />
        <TextField size="small" placeholder="Action" value={form.actionLimit ?? ""} onChange={(e) => setField("actionLimit", e.target.value)} sx={{ width: 90 }} />
        <TextField size="small" placeholder="Spec" value={form.specLimit ?? ""} onChange={(e) => setField("specLimit", e.target.value)} sx={{ width: 90 }} />
        <TextField size="small" placeholder="Unit (e.g. plate/4h)" value={form.unit ?? ""} onChange={(e) => setField("unit", e.target.value)} sx={{ width: 120 }} />
        {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
        <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add"}</Button>
      </Stack>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testType} / ${pendingDelete.testCode} configuration for this room?` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete.id)}
      />
    </Box>
  );
}

export function EMConfigPage() {
  const [departments, setDepartments] = useState<any[]>([]);
  const [deptForm, setDeptForm] = useState<Record<string, any>>({});
  const [editingDeptId, setEditingDeptId] = useState<number | null>(null);
  const [pendingDeleteDept, setPendingDeleteDept] = useState<any | null>(null);

  const [roomForm, setRoomForm] = useState<Record<string, any>>({ grade: "A" });
  const [editingRoomId, setEditingRoomId] = useState<number | null>(null);
  const [pendingDeleteRoom, setPendingDeleteRoom] = useState<any | null>(null);

  const [expandedDeptId, setExpandedDeptId] = useState<number | null>(null);
  const [expandedRoomId, setExpandedRoomId] = useState<number | null>(null);

  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => EMConfigService.getDepartments().then(setDepartments);
  useEffect(() => { load(); }, []);

  const cancelDeptEdit = () => { setEditingDeptId(null); setDeptForm({}); };
  const startDeptEdit = (d: any) => {
    setEditingDeptId(d.id);
    setDeptForm({ name: d.name, class: d.class, frequency: d.testingFrequency });
    setMessage(null);
  };
  const saveDept = async () => {
    setMessage(null);
    try {
      if (editingDeptId) {
        await EMConfigService.updateDepartment(editingDeptId, deptForm.name, deptForm.class ?? "", deptForm.frequency ?? "");
        setMessage({ text: "Department updated.", ok: true });
      } else {
        await EMConfigService.createDepartment(deptForm.name, deptForm.class ?? "", deptForm.frequency ?? "");
        setMessage({ text: "Department created.", ok: true });
      }
      cancelDeptEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this department.", ok: false });
    }
  };
  const deleteDept = async (d: any) => {
    setMessage(null);
    try {
      await EMConfigService.deleteDepartment(d.id);
      setPendingDeleteDept(null);
      load();
    } catch (e: any) {
      setPendingDeleteDept(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this department.", ok: false });
    }
  };

  const cancelRoomEdit = () => { setEditingRoomId(null); setRoomForm({ grade: "A" }); };
  const startRoomEdit = (r: any) => {
    setEditingRoomId(r.id);
    setRoomForm({ name: r.name, departmentId: r.departmentId, grade: r.gradeClassification });
    setMessage(null);
  };
  const saveRoom = async () => {
    setMessage(null);
    try {
      if (editingRoomId) {
        await EMConfigService.updateRoom(editingRoomId, roomForm.name, Number(roomForm.departmentId), roomForm.grade);
        setMessage({ text: "Room updated.", ok: true });
      } else {
        await EMConfigService.createRoom(roomForm.name, Number(roomForm.departmentId), roomForm.grade);
        setMessage({ text: "Room created.", ok: true });
      }
      cancelRoomEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this room.", ok: false });
    }
  };
  const deleteRoom = async (r: any) => {
    setMessage(null);
    try {
      await EMConfigService.deleteRoom(r.id);
      setPendingDeleteRoom(null);
      load();
    } catch (e: any) {
      setPendingDeleteRoom(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this room.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Environmental Monitoring" subtitle="Departments, rooms, grade, and per-room test limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingDeptId ? "Edit Department" : "New Department"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Name" value={deptForm.name ?? ""} onChange={(e) => setDeptForm({ ...deptForm, name: e.target.value })} />
          <TextField size="small" label="Class" value={deptForm.class ?? ""} onChange={(e) => setDeptForm({ ...deptForm, class: e.target.value })} placeholder="e.g. Grade C" />
          <TextField size="small" label="Testing Frequency" value={deptForm.frequency ?? ""} onChange={(e) => setDeptForm({ ...deptForm, frequency: e.target.value })} placeholder="e.g. Monthly" />
          {editingDeptId && <Button onClick={cancelDeptEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={saveDept}>{editingDeptId ? "Save Changes" : "Add Department"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>{editingRoomId ? "Edit Room" : "New Room"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Room Name" value={roomForm.name ?? ""} onChange={(e) => setRoomForm({ ...roomForm, name: e.target.value })} />
          <Select size="small" displayEmpty value={roomForm.departmentId ?? ""} onChange={(e) => setRoomForm({ ...roomForm, departmentId: e.target.value })} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Department</em></MenuItem>
            {departments.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
          </Select>
          <Select size="small" value={roomForm.grade} onChange={(e) => setRoomForm({ ...roomForm, grade: e.target.value })}>
            {["A", "B", "C", "D"].map((g) => <MenuItem key={g} value={g}>Grade {g}</MenuItem>)}
          </Select>
          {editingRoomId && <Button onClick={cancelRoomEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={saveRoom}>{editingRoomId ? "Save Changes" : "Add Room"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>Departments</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Department</TableCell><TableCell>Class</TableCell><TableCell>Testing Frequency</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>
            {departments.map((d) => (
              <Fragment key={d.id}>
                <TableRow>
                  <TableCell>
                    <IconButton size="small" onClick={() => setExpandedDeptId(expandedDeptId === d.id ? null : d.id)} title="Rooms">
                      {expandedDeptId === d.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{d.name}</TableCell>
                  <TableCell>{d.class || "—"}</TableCell>
                  <TableCell>{d.testingFrequency || "—"}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startDeptEdit(d)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDeleteDept(d)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={5}>
                    <Collapse in={expandedDeptId === d.id} unmountOnExit>
                      <Box sx={{ p: 2, bgcolor: "background.default" }}>
                        {(d.rooms ?? []).length === 0 ? (
                          <Typography variant="body2" color="text.secondary">No rooms configured yet.</Typography>
                        ) : (
                          <Table size="small">
                            <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Room</TableCell><TableCell>Grade</TableCell><TableCell /></TableRow></TableHead>
                            <TableBody>
                              {(d.rooms ?? []).map((r: any) => (
                                <Fragment key={r.id}>
                                  <TableRow>
                                    <TableCell>
                                      <IconButton size="small" onClick={() => setExpandedRoomId(expandedRoomId === r.id ? null : r.id)} title="Test Configurations">
                                        {expandedRoomId === r.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                                      </IconButton>
                                    </TableCell>
                                    <TableCell>{r.name}</TableCell>
                                    <TableCell>{r.gradeClassification}</TableCell>
                                    <TableCell align="right">
                                      <IconButton size="small" onClick={() => startRoomEdit({ ...r, departmentId: d.id })} title="Edit"><EditIcon fontSize="small" /></IconButton>
                                      <IconButton size="small" color="error" onClick={() => setPendingDeleteRoom(r)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                                    </TableCell>
                                  </TableRow>
                                  <TableRow>
                                    <TableCell sx={{ p: 0, border: 0 }} colSpan={4}>
                                      <Collapse in={expandedRoomId === r.id} unmountOnExit>
                                        <RoomTestConfigSection roomId={r.id} />
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
        open={pendingDeleteRoom != null}
        message={pendingDeleteRoom ? `Delete room "${pendingDeleteRoom.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeleteRoom(null)}
        onConfirm={() => pendingDeleteRoom && deleteRoom(pendingDeleteRoom)}
      />
    </>
  );
}
