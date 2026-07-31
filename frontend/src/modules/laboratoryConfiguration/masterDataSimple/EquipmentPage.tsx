import { useEffect, useState } from "react";
import { Paper, TextField, Button, Select, MenuItem, Table, TableHead, TableRow, TableCell, TableBody, Box } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { apiClient } from "../../../services/apiClient";

const TYPES = ["Incubator", "Autoclave", "LafCabinet", "BiologicalSafetyCabinet", "WaterBath", "Other"];

export function EquipmentPage() {
  const [list, setList] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ type: "Incubator" });

  const load = () => masterDataOptions.getEquipment().then(setList);
  useEffect(() => { load(); }, []);

  const save = async () => {
    await apiClient.post("/masterdata/equipment", {
      name: form.name, code: form.code, type: form.type, location: form.location,
      setPointTemperature: form.setPointTemperature ? Number(form.setPointTemperature) : null,
      calibrationDueDate: form.calibrationDueDate || null
    });
    setForm({ type: "Incubator" });
    load();
  };

  return (
    <>
      <PageHeader title="Equipment" subtitle="Incubators, autoclaves, LAF cabinets — with calibration tracking." />
      <SectionTitle>New Equipment</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
          <TextField placeholder="Name" value={form.name ?? ""} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <TextField placeholder="Code" value={form.code ?? ""} onChange={(e) => setForm({ ...form, code: e.target.value })} />
          <Select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
            {TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </Select>
          <TextField placeholder="Location" value={form.location ?? ""} onChange={(e) => setForm({ ...form, location: e.target.value })} />
          {form.type === "Incubator" && (
            <>
              <TextField placeholder="Set Point Temp" value={form.setPointTemperature ?? ""} onChange={(e) => setForm({ ...form, setPointTemperature: e.target.value })} />
              <TextField type="date" label="Calibration Due" InputLabelProps={{ shrink: true }} value={form.calibrationDueDate ?? ""} onChange={(e) => setForm({ ...form, calibrationDueDate: e.target.value })} />
            </>
          )}
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}><Button variant="contained" onClick={save}>Save</Button></Box>
      </Paper>

      <SectionTitle>Equipment</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Code</TableCell><TableCell>Name</TableCell><TableCell>Type</TableCell><TableCell>Set Point</TableCell><TableCell>Calibration Due</TableCell></TableRow></TableHead>
          <TableBody>
            {list.map((e) => (
              <TableRow key={e.id}>
                <TableCell>{e.code}</TableCell><TableCell>{e.name}</TableCell><TableCell>{e.type}</TableCell>
                <TableCell>{e.setPointTemperature ?? "—"}</TableCell>
                <TableCell>{e.calibrationDueDate ? new Date(e.calibrationDueDate).toLocaleDateString() : "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
