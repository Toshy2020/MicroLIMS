import { useEffect, useState } from "react";
import { Box, Paper, Typography, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Select, MenuItem, Alert } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { EMPreparationService } from "./services/EMPreparationService";

const TEST_TYPES = [
  { value: "PassiveAirSample", label: "Passive Air Sample (Settle Plate)" },
  { value: "SurfaceAirSample", label: "Surface Air Sample (Contact Plate)" }
];

// Receive (Department only) -> shell sample -> select Rooms + test types
// here -> confirming IS what generates the TestOrders.
export function EMPreparationPage() {
  const [samples, setSamples] = useState<any[]>([]);
  const [selectedSampleId, setSelectedSampleId] = useState<number | "">("");
  const [rooms, setRooms] = useState<any[]>([]);
  const [checks, setChecks] = useState<Record<string, boolean>>({}); // key: `${roomId}:${testType}`
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => { EMPreparationService.getNeedsPreparation().then(setSamples); }, []);

  const selectSample = async (sampleId: number) => {
    setSelectedSampleId(sampleId);
    setChecks({});
    // In a full build the sample's departmentId would come from the sample
    // record itself; here we re-fetch rooms for all departments and let
    // the user's chosen sample drive which department's rooms show.
    const sample = samples.find((s) => s.sampleId === sampleId);
    if (sample) {
      const deptRooms = await EMPreparationService.getRoomsForDepartment(sample.departmentId);
      setRooms(deptRooms);
    }
  };

  const toggle = (roomId: number, testType: string) => {
    const key = `${roomId}:${testType}`;
    setChecks((c) => ({ ...c, [key]: !c[key] }));
  };

  const confirm = async () => {
    if (!selectedSampleId) return;
    setMessage(null);
    const byRoom: Record<number, string[]> = {};
    Object.entries(checks).forEach(([key, checked]) => {
      if (!checked) return;
      const [roomId, testType] = key.split(":");
      byRoom[Number(roomId)] = [...(byRoom[Number(roomId)] || []), testType];
    });
    const selections = Object.entries(byRoom).map(([roomId, testTypes]) => ({ roomId: Number(roomId), testTypes }));

    try {
      await EMPreparationService.prepare(Number(selectedSampleId), selections);
      setMessage({ text: "Test orders generated.", ok: true });
      EMPreparationService.getNeedsPreparation().then(setSamples);
      setSelectedSampleId("");
      setRooms([]);
      setChecks({});
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="EM Preparation" subtitle="Select rooms and test types for a sample awaiting preparation." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Paper sx={{ p: 2.5, mb: 2 }}>
        <Select displayEmpty fullWidth value={selectedSampleId} onChange={(e) => selectSample(Number(e.target.value))}>
          <MenuItem value=""><em>Select a sample needing preparation</em></MenuItem>
          {samples.map((s) => <MenuItem key={s.sampleId} value={s.sampleId}>{s.referenceNumber} — {s.displayName}</MenuItem>)}
        </Select>
      </Paper>

      {rooms.length > 0 && (
        <Paper sx={{ p: 2.5, overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Room</TableCell>
                {TEST_TYPES.map((t) => <TableCell key={t.value} align="center">{t.label}</TableCell>)}
              </TableRow>
            </TableHead>
            <TableBody>
              {rooms.map((room) => (
                <TableRow key={room.id}>
                  <TableCell>{room.name}</TableCell>
                  {TEST_TYPES.map((t) => (
                    <TableCell key={t.value} align="center">
                      <Checkbox checked={!!checks[`${room.id}:${t.value}`]} onChange={() => toggle(room.id, t.value)} />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
            <Button variant="contained" onClick={confirm}>Confirm Selection — Generate Test Orders</Button>
          </Box>
        </Paper>
      )}
    </>
  );
}
