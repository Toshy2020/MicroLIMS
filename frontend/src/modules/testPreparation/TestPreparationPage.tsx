import { useEffect, useState } from "react";
import { Box, Paper, Select, MenuItem, TextField, Button, Typography, Alert } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { SamplePreparationService } from "./services/SamplePreparationService";
import { masterDataOptions } from "../../services/masterDataOptions";

const UNITS = ["ml", "gm", "bottle", "cap", "25cm2"];

export function TestPreparationPage() {
  const [samples, setSamples] = useState<any[]>([]);
  const [sampleId, setSampleId] = useState<number | "">("");
  const [diluentTypes, setDiluentTypes] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [neutralizers, setNeutralizers] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ technique: "PourPlate", unit: "ml" });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => {
    SamplePreparationService.getNeedsPreparation().then(setSamples);
    masterDataOptions.getDiluentTypes().then(setDiluentTypes);
    masterDataOptions.getNeutralizers().then(setNeutralizers);
  }, []);

  const selectedSample = samples.find((s) => s.sampleId === sampleId);
  const selectedDiluent = diluentTypes.find((d) => d.id === form.diluentTypeId);

  useEffect(() => {
    if (selectedDiluent?.requiresBatchTracking) {
      masterDataOptions.getReleasedMedia(selectedDiluent.mediaTypeId).then(setReleasedMedia);
    }
  }, [form.diluentTypeId]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const save = async () => {
    setMessage(null);
    try {
      await SamplePreparationService.prepare({
        sampleId: Number(sampleId), amount: Number(form.amount), unit: form.unit, technique: form.technique,
        filtrationVolume: form.filtrationVolume ? Number(form.filtrationVolume) : undefined,
        washingVolume: form.washingVolume ? Number(form.washingVolume) : undefined,
        diluentTypeId: form.diluentTypeId, diluentMediaId: form.diluentMediaId, neutralizerId: form.neutralizerId,
        storageCondition: selectedSample?.category === "Water" ? form.storageCondition : undefined,
        storageTimeHours: form.storageTimeHours ? Number(form.storageTimeHours) : undefined
      });
      setMessage({ text: "Preparation saved.", ok: true });
      SamplePreparationService.getNeedsPreparation().then(setSamples);
      setSampleId(""); setForm({ technique: "PourPlate", unit: "ml" });
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save preparation.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Test Preparation" subtitle="One-time setup before testing can start for a sample." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Paper sx={{ p: 2.5, mb: 2 }}>
        <Select displayEmpty fullWidth value={sampleId} onChange={(e) => setSampleId(Number(e.target.value))}>
          <MenuItem value=""><em>Select a sample</em></MenuItem>
          {samples.map((s) => <MenuItem key={s.sampleId} value={s.sampleId}>{s.referenceNumber} — {s.displayName}</MenuItem>)}
        </Select>
      </Paper>

      {sampleId && (
        <Paper sx={{ p: 2.5 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
            <TextField label="Sample Amount" value={form.amount ?? ""} onChange={(e) => setField("amount", e.target.value)} />
            <Select value={form.unit} onChange={(e) => setField("unit", e.target.value)}>
              {UNITS.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
            </Select>
            <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)}>
              <MenuItem value="PourPlate">Pour Plate</MenuItem>
              <MenuItem value="Filtration">Filtration</MenuItem>
            </Select>
          </Box>

          {form.technique === "Filtration" && (
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
              <TextField label="Filtration Volume (ml)" value={form.filtrationVolume ?? ""} onChange={(e) => setField("filtrationVolume", e.target.value)} />
              <TextField label="Washing Volume (ml)" value={form.washingVolume ?? ""} onChange={(e) => setField("washingVolume", e.target.value)} />
            </Box>
          )}

          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
            <Select displayEmpty value={form.diluentTypeId ?? ""} onChange={(e) => setField("diluentTypeId", e.target.value)}>
              <MenuItem value=""><em>Diluent</em></MenuItem>
              {diluentTypes.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
            </Select>
            {selectedDiluent?.requiresBatchTracking && (
              <Select displayEmpty value={form.diluentMediaId ?? ""} onChange={(e) => setField("diluentMediaId", e.target.value)}>
                <MenuItem value=""><em>Released lot (GPT-released only)</em></MenuItem>
                {releasedMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber} — expires {new Date(m.expiryDate).toLocaleDateString()}</MenuItem>)}
              </Select>
            )}
          </Box>

          <Box sx={{ maxWidth: 300, mt: 2 }}>
            <Select displayEmpty fullWidth value={form.neutralizerId ?? ""} onChange={(e) => setField("neutralizerId", e.target.value)}>
              <MenuItem value=""><em>Neutralizer</em></MenuItem>
              {neutralizers.map((n) => <MenuItem key={n.id} value={n.id}>{n.name}</MenuItem>)}
            </Select>
          </Box>

          {selectedSample?.category === "Water" && (
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
              <Select displayEmpty value={form.storageCondition ?? ""} onChange={(e) => setField("storageCondition", e.target.value)}>
                <MenuItem value=""><em>Storage Condition</em></MenuItem>
                <MenuItem value="Refrigerator">Refrigerator</MenuItem>
                <MenuItem value="RoomTemperature">Room Temperature</MenuItem>
              </Select>
              {form.storageCondition === "Refrigerator" && (
                <TextField label="Storage Time (hours)" value={form.storageTimeHours ?? ""} onChange={(e) => setField("storageTimeHours", e.target.value)} />
              )}
            </Box>
          )}

          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
            <Button variant="contained" onClick={save}>Save Preparation</Button>
          </Box>
        </Paper>
      )}
    </>
  );
}
