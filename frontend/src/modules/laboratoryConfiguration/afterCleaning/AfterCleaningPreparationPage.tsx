import { useEffect, useState } from "react";
import { Box, Paper, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Select, MenuItem, Alert } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { AfterCleaningPreparationService } from "./services/AfterCleaningPreparationService";

interface PartConfig { id: number; testType: string; testCode: string }
interface Part { id: number; name: string; configs?: PartConfig[] }

// Receive (Machine only) -> shell sample -> select Parts + test types
// here -> confirming IS what generates the TestOrders (no collective
// sample concept - every checked cell is independent).
export function AfterCleaningPreparationPage() {
  const [samples, setSamples] = useState<any[]>([]);
  const [selectedSampleId, setSelectedSampleId] = useState<number | "">("");
  const [parts, setParts] = useState<Part[]>([]);
  const [testTypes, setTestTypes] = useState<string[]>([]);
  const [checks, setChecks] = useState<Record<string, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => { AfterCleaningPreparationService.getNeedsPreparation().then(setSamples); }, []);

  const selectSample = async (sampleId: number) => {
    setSelectedSampleId(sampleId);
    setChecks({});
    const sample = samples.find((s) => s.sampleId === sampleId);
    if (!sample) return;
    const machineParts: Part[] = await AfterCleaningPreparationService.getPartsForMachine(sample.machineId);
    const withConfigs = await Promise.all(
      machineParts.map(async (p) => ({ ...p, configs: await AfterCleaningPreparationService.getPartConfigurations(p.id) }))
    );
    setParts(withConfigs);
    const allTypes = Array.from(new Set(withConfigs.flatMap((p) => p.configs?.map((c: PartConfig) => c.testType) ?? [])));
    setTestTypes(allTypes);
  };

  const hasConfig = (part: Part, testType: string) => part.configs?.some((c) => c.testType === testType);
  const toggle = (partId: number, testType: string) => {
    const key = `${partId}:${testType}`;
    setChecks((c) => ({ ...c, [key]: !c[key] }));
  };

  const confirm = async () => {
    if (!selectedSampleId) return;
    setMessage(null);
    const byPart: Record<number, string[]> = {};
    Object.entries(checks).forEach(([key, checked]) => {
      if (!checked) return;
      const [partId, testType] = key.split(":");
      byPart[Number(partId)] = [...(byPart[Number(partId)] || []), testType];
    });
    const selections = Object.entries(byPart).map(([machinePartId, testTypes]) => ({ machinePartId: Number(machinePartId), testTypes }));

    try {
      await AfterCleaningPreparationService.prepare(Number(selectedSampleId), selections);
      setMessage({ text: "Test orders generated.", ok: true });
      AfterCleaningPreparationService.getNeedsPreparation().then(setSamples);
      setSelectedSampleId(""); setParts([]); setTestTypes([]); setChecks({});
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="After Cleaning Preparation" subtitle="Select machine parts and test types for a sample awaiting preparation." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Paper sx={{ p: 2.5, mb: 2 }}>
        <Select displayEmpty fullWidth value={selectedSampleId} onChange={(e) => selectSample(Number(e.target.value))}>
          <MenuItem value=""><em>Select a sample needing preparation</em></MenuItem>
          {samples.map((s) => <MenuItem key={s.sampleId} value={s.sampleId}>{s.referenceNumber} — {s.displayName}</MenuItem>)}
        </Select>
      </Paper>

      {parts.length > 0 && (
        <Paper sx={{ p: 2.5, overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Part</TableCell>
                {testTypes.map((t) => <TableCell key={t} align="center">{t}</TableCell>)}
              </TableRow>
            </TableHead>
            <TableBody>
              {parts.map((part) => (
                <TableRow key={part.id}>
                  <TableCell>{part.name}</TableCell>
                  {testTypes.map((t) => (
                    <TableCell key={t} align="center">
                      {hasConfig(part, t) && <Checkbox checked={!!checks[`${part.id}:${t}`]} onChange={() => toggle(part.id, t)} />}
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
