import { useEffect, useState } from "react";
import { Paper, Box, TextField, Select, MenuItem, Button, Table, TableHead, TableRow, TableCell, TableBody, Alert } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { apiClient } from "../../../services/apiClient";

const CLASSES = ["GeneralAgar", "GeneralBroth", "SelectiveAgar", "SelectiveBroth"];

export function MediaTypesPage() {
  const [list, setList] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ class: "GeneralAgar" });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [indicForm, setIndicForm] = useState<Record<string, any>>({});

  const load = () => masterDataOptions.getMediaTypes().then(setList);
  useEffect(() => { load(); }, []);

  const save = async () => {
    try {
      await apiClient.post("/masterdata/media-types", {
        name: form.name, code: form.code, class: form.class,
        incubationMinHours: Number(form.incubationMinHours), incubationMaxHours: Number(form.incubationMaxHours),
        requiredTemperatureMin: Number(form.requiredTemperatureMin), requiredTemperatureMax: Number(form.requiredTemperatureMax),
        approvedTestCodes: (form.approvedTestCodes ?? "").split(",").map((s: string) => s.trim()).filter(Boolean),
        recoveryPercentMin: form.recoveryPercentMin ? Number(form.recoveryPercentMin) : null,
        recoveryPercentMax: form.recoveryPercentMax ? Number(form.recoveryPercentMax) : null
      });
      setMessage({ text: "Media type saved.", ok: true });
      setForm({ class: "GeneralAgar" });
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save.", ok: false });
    }
  };

  const saveIndication = async () => {
    await apiClient.post("/masterdata/expected-indication-results", {
      mediaTypeId: indicForm.mediaTypeId, organismName: indicForm.organismName, expectedDescription: indicForm.expectedDescription
    });
    setIndicForm({});
    setMessage({ text: "Expected indication result saved.", ok: true });
  };

  return (
    <>
      <PageHeader title="Media Types" subtitle="The reusable media definitions Media Preparation lots reference." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Media Type</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
          <TextField placeholder="Name" value={form.name ?? ""} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <TextField placeholder="Code" value={form.code ?? ""} onChange={(e) => setForm({ ...form, code: e.target.value })} />
          <Select value={form.class} onChange={(e) => setForm({ ...form, class: e.target.value })}>
            {CLASSES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </Select>
          <TextField placeholder="Approved Test Codes (comma sep)" value={form.approvedTestCodes ?? ""} onChange={(e) => setForm({ ...form, approvedTestCodes: e.target.value })} />
          <TextField placeholder="Incubation Min (hrs)" value={form.incubationMinHours ?? ""} onChange={(e) => setForm({ ...form, incubationMinHours: e.target.value })} />
          <TextField placeholder="Incubation Max (hrs)" value={form.incubationMaxHours ?? ""} onChange={(e) => setForm({ ...form, incubationMaxHours: e.target.value })} />
          <TextField placeholder="Required Temp Min" value={form.requiredTemperatureMin ?? ""} onChange={(e) => setForm({ ...form, requiredTemperatureMin: e.target.value })} />
          <TextField placeholder="Required Temp Max" value={form.requiredTemperatureMax ?? ""} onChange={(e) => setForm({ ...form, requiredTemperatureMax: e.target.value })} />
          {form.class === "GeneralAgar" && (
            <>
              <TextField placeholder="Recovery% Min" value={form.recoveryPercentMin ?? ""} onChange={(e) => setForm({ ...form, recoveryPercentMin: e.target.value })} />
              <TextField placeholder="Recovery% Max" value={form.recoveryPercentMax ?? ""} onChange={(e) => setForm({ ...form, recoveryPercentMax: e.target.value })} />
            </>
          )}
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}><Button variant="contained" onClick={save}>Save</Button></Box>
      </Paper>

      <SectionTitle>Expected Indication Results (Selective Media)</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2 }}>
          <Select displayEmpty value={indicForm.mediaTypeId ?? ""} onChange={(e) => setIndicForm({ ...indicForm, mediaTypeId: e.target.value })}>
            <MenuItem value=""><em>Media Type</em></MenuItem>
            {list.filter((m) => m.class?.includes("Selective")).map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
          </Select>
          <TextField placeholder="Organism" value={indicForm.organismName ?? ""} onChange={(e) => setIndicForm({ ...indicForm, organismName: e.target.value })} />
          <TextField placeholder="Expected Description" value={indicForm.expectedDescription ?? ""} onChange={(e) => setIndicForm({ ...indicForm, expectedDescription: e.target.value })} />
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}><Button variant="outlined" onClick={saveIndication}>Add</Button></Box>
      </Paper>

      <SectionTitle>Media Types</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Name</TableCell><TableCell>Code</TableCell><TableCell>Class</TableCell><TableCell>Incubation</TableCell><TableCell>Temp Range</TableCell></TableRow></TableHead>
          <TableBody>
            {list.map((m) => (
              <TableRow key={m.id}>
                <TableCell>{m.name}</TableCell><TableCell>{m.code}</TableCell><TableCell>{m.class}</TableCell>
                <TableCell>{m.incubationMinHours}–{m.incubationMaxHours}h</TableCell>
                <TableCell>{m.requiredTemperatureMin}–{m.requiredTemperatureMax}°C</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
