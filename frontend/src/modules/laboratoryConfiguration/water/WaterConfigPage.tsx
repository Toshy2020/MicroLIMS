import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Button, Typography, Alert, Box } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { WaterConfigService } from "./services/WaterConfigService";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

// Sampling Points + their assigned tests - read by WaterWorkflowEngine
// on every water sample receipt (Frozen Principle #1, Water domain).
export function WaterConfigPage() {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [code, setCode] = useState("");
  const [location, setLocation] = useState("");
  const [testCodes, setTestCodes] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => WaterConfigService.getSamplingPoints().then(setPoints).catch(() => setPoints([]));
  useEffect(() => { load(); }, []);

  const create = async () => {
    if (!code) return;
    setMessage(null);
    try {
      await WaterConfigService.createSamplingPoint(code, location, testCodes.split(",").map((t) => t.trim()).filter(Boolean));
      setMessage({ text: "Sampling point created.", ok: true });
      setCode(""); setLocation(""); setTestCodes("");
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not create sampling point.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Water" subtitle="Sampling points and the tests assigned to each." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Sampling Point</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Point Code" value={code} onChange={(e) => setCode(e.target.value)} />
          <TextField size="small" label="Location" value={location} onChange={(e) => setLocation(e.target.value)} />
          <TextField size="small" label="Assigned Tests (comma-separated)" value={testCodes} onChange={(e) => setTestCodes(e.target.value)} sx={{ minWidth: 260 }} />
          <Button variant="contained" onClick={create}>Add Sampling Point</Button>
        </Stack>
      </Paper>

      <SectionTitle>Sampling Points</SectionTitle>
      {points.length === 0 ? (
        <Typography sx={{ color: "#9ca3af", fontSize: 13 }}>No sampling points configured yet.</Typography>
      ) : (
        <Stack spacing={1}>
          {points.map((p) => (
            <Paper key={p.id} sx={{ p: 2 }}>
              <Stack direction="row" justifyContent="space-between">
                <Box>
                  <Typography sx={{ fontWeight: 700 }}>{p.code}</Typography>
                  <Typography variant="body2" color="text.secondary">{p.location}</Typography>
                </Box>
                <Typography variant="body2" color="text.secondary">{p.assignedTestCodes.join(", ")}</Typography>
              </Stack>
            </Paper>
          ))}
        </Stack>
      )}
    </>
  );
}
