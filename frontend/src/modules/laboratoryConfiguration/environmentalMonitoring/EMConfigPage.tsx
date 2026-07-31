import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { EMConfigService } from "./services/EMConfigService";

const TEST_TYPES = ["PassiveAirSample", "SurfaceAirSample"];

export function EMConfigPage() {
  const [departments, setDepartments] = useState<any[]>([]);
  const [rooms, setRooms] = useState<any[]>([]);
  const [deptForm, setDeptForm] = useState<Record<string, any>>({});
  const [roomForm, setRoomForm] = useState<Record<string, any>>({ grade: "A" });
  const [configForm, setConfigForm] = useState<Record<string, any>>({ testType: "PassiveAirSample" });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => {
    EMConfigService.getDepartments().then(setDepartments);
    EMConfigService.getRooms().then(setRooms);
  };
  useEffect(() => { load(); }, []);

  const createDept = async () => {
    await EMConfigService.createDepartment(deptForm.name, deptForm.class ?? "", deptForm.frequency ?? "");
    setDeptForm({});
    setMessage({ text: "Department created.", ok: true }); load();
  };

  const createRoom = async () => {
    await EMConfigService.createRoom(roomForm.name, Number(roomForm.departmentId), roomForm.grade);
    setRoomForm({ grade: "A" });
    setMessage({ text: "Room created.", ok: true }); load();
  };

  const createConfig = async () => {
    await EMConfigService.createRoomTestConfiguration(
      Number(configForm.roomId), configForm.testType, configForm.testCode,
      configForm.alertLimit ?? "", configForm.actionLimit ?? "", configForm.specLimit ?? ""
    );
    setMessage({ text: "Test configuration added.", ok: true }); load();
  };

  return (
    <>
      <PageHeader title="Environmental Monitoring" subtitle="Departments, rooms, grade, and per-room test limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Department</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          <TextField size="small" label="Name" value={deptForm.name ?? ""} onChange={(e) => setDeptForm({ ...deptForm, name: e.target.value })} />
          <TextField size="small" label="Class" value={deptForm.class ?? ""} onChange={(e) => setDeptForm({ ...deptForm, class: e.target.value })} placeholder="e.g. Grade C" />
          <TextField size="small" label="Testing Frequency" value={deptForm.frequency ?? ""} onChange={(e) => setDeptForm({ ...deptForm, frequency: e.target.value })} placeholder="e.g. Monthly" />
          <Button variant="outlined" onClick={createDept}>Add Department</Button>
        </Stack>
      </Paper>

      <SectionTitle>New Room</SectionTitle>
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
          <Button variant="outlined" onClick={createRoom}>Add Room</Button>
        </Stack>
      </Paper>

      <SectionTitle>Room Test Configuration</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 2 }}>
          <Select displayEmpty value={configForm.roomId ?? ""} onChange={(e) => setConfigForm({ ...configForm, roomId: e.target.value })}>
            <MenuItem value=""><em>Room</em></MenuItem>
            {rooms.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <Select value={configForm.testType} onChange={(e) => setConfigForm({ ...configForm, testType: e.target.value })}>
            {TEST_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </Select>
          <TextField placeholder="Test Code" value={configForm.testCode ?? ""} onChange={(e) => setConfigForm({ ...configForm, testCode: e.target.value })} />
          <TextField placeholder="Alert" value={configForm.alertLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, alertLimit: e.target.value })} />
          <TextField placeholder="Action" value={configForm.actionLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, actionLimit: e.target.value })} />
          <TextField placeholder="Spec" value={configForm.specLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, specLimit: e.target.value })} />
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}><Button variant="outlined" onClick={createConfig}>Add Configuration</Button></Box>
      </Paper>

      <SectionTitle>Rooms</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Room</TableCell><TableCell>Department</TableCell><TableCell>Grade</TableCell></TableRow></TableHead>
          <TableBody>{rooms.map((r) => (
            <TableRow key={r.id}><TableCell>{r.name}</TableCell><TableCell>{r.department?.name}</TableCell><TableCell>{r.gradeClassification}</TableCell></TableRow>
          ))}</TableBody>
        </Table>
      </Paper>
    </>
  );
}
