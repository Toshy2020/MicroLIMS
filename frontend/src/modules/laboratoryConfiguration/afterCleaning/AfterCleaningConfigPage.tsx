import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box, Chip, Checkbox, FormControlLabel } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { AfterCleaningConfigService } from "./services/AfterCleaningConfigService";

export function AfterCleaningConfigPage() {
  const [machines, setMachines] = useState<any[]>([]);
  const [machineName, setMachineName] = useState("");
  const [partName, setPartName] = useState("");
  const [machineId, setMachineId] = useState("");
  const [configForm, setConfigForm] = useState<Record<string, any>>({ testType: "Swab", isPathogenTest: false });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => AfterCleaningConfigService.getMachines().then(setMachines);
  useEffect(() => { load(); }, []);

  const allParts = machines.flatMap((m) => (m.parts ?? []).map((p: any) => ({ ...p, machineName: m.name })));

  const createMachine = async () => { await AfterCleaningConfigService.createMachine(machineName); setMachineName(""); setMessage({ text: "Machine created.", ok: true }); load(); };
  const createPart = async () => { await AfterCleaningConfigService.createMachinePart(partName, Number(machineId)); setPartName(""); setMessage({ text: "Part added.", ok: true }); load(); };
  const createConfig = async () => {
    await AfterCleaningConfigService.createPartConfiguration(
      Number(configForm.machinePartId), configForm.testType, configForm.testCode ?? configForm.testType,
      configForm.alertLimit ?? "", configForm.actionLimit ?? "", configForm.specLimit ?? "", !!configForm.isPathogenTest
    );
    setMessage({ text: "Test configuration added.", ok: true }); load();
  };

  return (
    <>
      <PageHeader title="After Cleaning" subtitle="Machines, parts, and per-part test limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Machine</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2}>
          <TextField size="small" label="Machine Name" value={machineName} onChange={(e) => setMachineName(e.target.value)} />
          <Button variant="outlined" onClick={createMachine}>Add Machine</Button>
        </Stack>
      </Paper>

      <SectionTitle>New Part</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <Select size="small" displayEmpty value={machineId} onChange={(e) => setMachineId(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Machine</em></MenuItem>
            {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
          </Select>
          <TextField size="small" label="Part Name" value={partName} onChange={(e) => setPartName(e.target.value)} />
          <Button variant="outlined" onClick={createPart}>Add Part</Button>
        </Stack>
      </Paper>

      <SectionTitle>Part Test Configuration</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 2 }}>
          <Select displayEmpty value={configForm.machinePartId ?? ""} onChange={(e) => setConfigForm({ ...configForm, machinePartId: e.target.value })}>
            <MenuItem value=""><em>Part</em></MenuItem>
            {allParts.map((p) => <MenuItem key={p.id} value={p.id}>{p.machineName} — {p.name}</MenuItem>)}
          </Select>
          <Select value={configForm.testType} onChange={(e) => setConfigForm({ ...configForm, testType: e.target.value })}>
            <MenuItem value="Swab">Swab</MenuItem><MenuItem value="Rinse">Rinse</MenuItem><MenuItem value="Pathogen">Pathogen</MenuItem>
          </Select>
          <TextField placeholder="Test Code (e.g. TAMC, PATHOGEN_ECOLI)" value={configForm.testCode ?? ""} onChange={(e) => setConfigForm({ ...configForm, testCode: e.target.value })} />
          <TextField placeholder="Alert" value={configForm.alertLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, alertLimit: e.target.value })} />
          <TextField placeholder="Action" value={configForm.actionLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, actionLimit: e.target.value })} />
          <TextField placeholder="Spec" value={configForm.specLimit ?? ""} onChange={(e) => setConfigForm({ ...configForm, specLimit: e.target.value })} />
        </Box>
        <FormControlLabel control={<Checkbox checked={!!configForm.isPathogenTest} onChange={(e) => setConfigForm({ ...configForm, isPathogenTest: e.target.checked })} />} label="This is a pathogen test (not Swab/Rinse TAMC)" />
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}><Button variant="outlined" onClick={createConfig}>Add Configuration</Button></Box>
      </Paper>

      <SectionTitle>Machines</SectionTitle>
      {machines.map((m) => (
        <Paper key={m.id} sx={{ p: 2, mb: 1 }}>
          <Typography sx={{ fontWeight: 700, mb: 1 }}>{m.name}</Typography>
          <Stack direction="row" spacing={1} flexWrap="wrap">
            {(m.parts ?? []).map((p: any) => <Chip key={p.id} label={p.name} size="small" />)}
          </Stack>
        </Paper>
      ))}
    </>
  );
}
