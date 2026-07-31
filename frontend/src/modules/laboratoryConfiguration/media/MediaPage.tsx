import { useEffect, useState } from "react";
import { Box, Paper, Table, TableHead, TableRow, TableCell, TableBody, TextField, Select, MenuItem, Button, Alert } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";

export function MediaPage() {
  const [lots, setLots] = useState<any[]>([]);
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [autoclaves, setAutoclaves] = useState<any[]>([]);
  const [dehydratedMedia, setDehydratedMedia] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => MediaPreparationService.getAll().then(setLots);
  useEffect(() => {
    load();
    masterDataOptions.getMediaTypes().then(setMediaTypes);
    masterDataOptions.getEquipment("Autoclave").then(setAutoclaves);
    MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia);
  }, []);

  // Only stock that is actually usable (not expired, not depleted) is
  // offered - MediaPreparationService.PrepareAsync re-checks this
  // server-side regardless, this is just so the analyst doesn't pick
  // something that will get rejected.
  const usableStock = dehydratedMedia.filter((m) => m.status === "InStock");

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const save = async () => {
    setMessage(null);
    try {
      await MediaPreparationService.prepare({
        mediaTypeId: form.mediaTypeId, materialId: form.materialId, manufacturerLot: form.manufacturerLot, manufacturerName: form.manufacturerName,
        totalWeight: Number(form.totalWeight), totalVolume: form.totalVolume, autoclaveEquipmentId: form.autoclaveEquipmentId,
        autoclaveProgram: form.autoclaveProgram, loadType: form.loadType, temperature: Number(form.temperature),
        cycleTime: Number(form.cycleTime), cycleNumber: Number(form.cycleNumber), ph: Number(form.ph), expiryDate: form.expiryDate
      });
      setMessage({ text: "Media lot prepared.", ok: true });
      setForm({});
      load();
      MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare media.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Media Preparation" subtitle="The full prepared-lot record — autoclave, cycle, pH." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Prepared Lot</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
          <Select displayEmpty value={form.mediaTypeId ?? ""} onChange={(e) => setField("mediaTypeId", e.target.value)}>
            <MenuItem value=""><em>Media Type</em></MenuItem>
            {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{m.name} ({m.code})</MenuItem>)}
          </Select>
          <TextField placeholder="Manufacturer Lot" value={form.manufacturerLot ?? ""} onChange={(e) => setField("manufacturerLot", e.target.value)} />
          <TextField placeholder="Manufacturer Name" value={form.manufacturerName ?? ""} onChange={(e) => setField("manufacturerName", e.target.value)} />
          <Select displayEmpty value={form.materialId ?? ""} onChange={(e) => setField("materialId", e.target.value)}>
            <MenuItem value=""><em>Dehydrated Media Stock (Inventory)</em></MenuItem>
            {usableStock.map((m) => (
              <MenuItem key={m.id} value={m.id}>{m.materialName} — batch {m.batchNumber} ({m.quantityRemaining} {m.unit} left)</MenuItem>
            ))}
          </Select>
          <TextField placeholder="Total Weight" value={form.totalWeight ?? ""} onChange={(e) => setField("totalWeight", e.target.value)} />
          <TextField placeholder="Total Volume" value={form.totalVolume ?? ""} onChange={(e) => setField("totalVolume", e.target.value)} />
          <Select displayEmpty value={form.autoclaveEquipmentId ?? ""} onChange={(e) => setField("autoclaveEquipmentId", e.target.value)}>
            <MenuItem value=""><em>Autoclave</em></MenuItem>
            {autoclaves.map((a) => <MenuItem key={a.id} value={a.id}>{a.name}</MenuItem>)}
          </Select>
          <TextField placeholder="Program / Load" value={form.autoclaveProgram ?? ""} onChange={(e) => setField("autoclaveProgram", e.target.value)} />
          <TextField placeholder="Load Type" value={form.loadType ?? ""} onChange={(e) => setField("loadType", e.target.value)} />
          <TextField placeholder="Temperature" value={form.temperature ?? ""} onChange={(e) => setField("temperature", e.target.value)} />
          <TextField placeholder="Cycle Time" value={form.cycleTime ?? ""} onChange={(e) => setField("cycleTime", e.target.value)} />
          <TextField placeholder="Cycle Number" value={form.cycleNumber ?? ""} onChange={(e) => setField("cycleNumber", e.target.value)} />
          <TextField placeholder="pH" value={form.ph ?? ""} onChange={(e) => setField("ph", e.target.value)} />
          <TextField type="date" label="Expiry" InputLabelProps={{ shrink: true }} value={form.expiryDate ?? ""} onChange={(e) => setField("expiryDate", e.target.value)} />
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
          <Button variant="contained" onClick={save}>Save</Button>
        </Box>
      </Paper>

      <SectionTitle>Prepared Lots</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Lot</TableCell><TableCell>Prepared</TableCell><TableCell>Expiry</TableCell><TableCell>GPT Stage</TableCell></TableRow></TableHead>
          <TableBody>
            {lots.map((m) => (
              <TableRow key={m.id}>
                <TableCell>{m.lotNumber}</TableCell>
                <TableCell>{new Date(m.preparedAt).toLocaleDateString()}</TableCell>
                <TableCell>{new Date(m.expiryDate).toLocaleDateString()}</TableCell>
                <TableCell><StatusBadge status={m.gptStage} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
