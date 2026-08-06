import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Button, Typography, Alert, Box, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { TestCodePickerMulti } from "../../../components/TestCodePickerMulti";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { WaterConfigService } from "./services/WaterConfigService";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

// Sampling Points + their assigned tests - read by WaterWorkflowEngine
// on every water sample receipt (Frozen Principle #1, Water domain).
export function WaterConfigPage() {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [code, setCode] = useState("");
  const [location, setLocation] = useState("");
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SamplingPoint | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => WaterConfigService.getSamplingPoints().then(setPoints).catch(() => setPoints([]));
  useEffect(() => { load(); }, []);

  const startEdit = (p: SamplingPoint) => {
    setEditingId(p.id);
    setCode(p.code);
    setLocation(p.location);
    setTestCodes(p.assignedTestCodes);
    setMessage(null);
  };

  const cancelEdit = () => { setEditingId(null); setCode(""); setLocation(""); setTestCodes([]); };

  const save = async () => {
    if (!code) return;
    setMessage(null);
    try {
      if (editingId) {
        await WaterConfigService.updateSamplingPoint(editingId, code, location, testCodes);
        setMessage({ text: "Sampling point updated.", ok: true });
      } else {
        await WaterConfigService.createSamplingPoint(code, location, testCodes);
        setMessage({ text: "Sampling point created.", ok: true });
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? `Could not ${editingId ? "update" : "create"} sampling point.`, ok: false });
    }
  };

  const remove = async (p: SamplingPoint) => {
    setMessage(null);
    try {
      await WaterConfigService.deleteSamplingPoint(p.id);
      setPendingDelete(null);
      load();
    } catch (e: any) {
      setPendingDelete(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this sampling point.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Water" subtitle="Sampling points and the tests assigned to each." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingId ? "Edit Sampling Point" : "New Sampling Point"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Point Code" value={code} onChange={(e) => setCode(e.target.value)} />
          <TextField size="small" label="Location" value={location} onChange={(e) => setLocation(e.target.value)} />
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned Tests" sx={{ minWidth: 300 }} />
          {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
          <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add Sampling Point"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>Sampling Points</SectionTitle>
      {points.length === 0 ? (
        <Typography sx={{ color: "#9ca3af", fontSize: 13 }}>No sampling points configured yet.</Typography>
      ) : (
        <Stack spacing={1}>
          {points.map((p) => (
            <Paper key={p.id} sx={{ p: 2 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
                <Box>
                  <Typography sx={{ fontWeight: 700 }}>{p.code}</Typography>
                  <Typography variant="body2" color="text.secondary">{p.location}</Typography>
                </Box>
                <Stack direction="row" spacing={2} alignItems="center">
                  <Typography variant="body2" color="text.secondary">{p.assignedTestCodes.join(", ")}</Typography>
                  <Stack direction="row">
                    <IconButton size="small" onClick={() => startEdit(p)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDelete(p)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </Stack>
                </Stack>
              </Stack>
            </Paper>
          ))}
        </Stack>
      )}

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete sampling point "${pendingDelete.code}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete)}
      />
    </>
  );
}
