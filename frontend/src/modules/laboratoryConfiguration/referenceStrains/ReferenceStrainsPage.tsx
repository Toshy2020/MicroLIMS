import { useEffect, useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, Button, Typography, Alert, Table, TableHead, TableRow, TableCell, TableBody, IconButton } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReferenceStrainService } from "./services/ReferenceStrainService";
import { masterDataOptions } from "../../../services/masterDataOptions";

interface PanelRow { mediaId: string; incubatorEquipmentId: string; incubationStart: string; incubationEnd: string; observationText: string }
const emptyRow = (): PanelRow => ({ mediaId: "", incubatorEquipmentId: "", incubationStart: "", incubationEnd: "", observationText: "" });

export function ReferenceStrainsPage() {
  const [strains, setStrains] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [panel, setPanel] = useState<PanelRow[]>([emptyRow()]);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => ReferenceStrainService.getAll().then(setStrains);
  useEffect(() => {
    load();
    masterDataOptions.getReleasedMedia().then(setReleasedMedia);
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, []);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const updateRow = (i: number, k: keyof PanelRow, v: string) => setPanel((p) => p.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const addRow = () => setPanel((p) => [...p, emptyRow()]);
  const removeRow = (i: number) => setPanel((p) => (p.length > 1 ? p.filter((_, idx) => idx !== i) : p));

  const save = async () => {
    setMessage(null);
    try {
      await ReferenceStrainService.receive({
        organismName: form.organismName, atccNumber: form.atccNumber, numberOfDiscs: Number(form.numberOfDiscs),
        manufacturerName: form.manufacturerName, expiryDate: form.expiryDate, storageCondition: form.storageCondition,
        physicalCheckText: form.physicalCheckText,
        panel: panel.filter((r) => r.mediaId).map((r) => ({
          mediaId: Number(r.mediaId), incubatorEquipmentId: Number(r.incubatorEquipmentId),
          incubationStart: r.incubationStart, incubationEnd: r.incubationEnd, observationText: r.observationText
        }))
      });
      setMessage({ text: "Reference strain saved.", ok: true });
      setForm({}); setPanel([emptyRow()]);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save.", ok: false });
    }
  };

  const approve = async (id: number, approved: boolean) => {
    await ReferenceStrainService.approve(id, approved);
    load();
  };

  return (
    <>
      <PageHeader title="Reference Strains" subtitle="Receive strains, confirm identity, and approve for cryovial preparation." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Receive Reference Strain</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2, mb: 2 }}>
          <TextField placeholder="Organism" value={form.organismName ?? ""} onChange={(e) => setField("organismName", e.target.value)} />
          <TextField placeholder="ATCC No." value={form.atccNumber ?? ""} onChange={(e) => setField("atccNumber", e.target.value)} />
          <TextField placeholder="# Discs" value={form.numberOfDiscs ?? ""} onChange={(e) => setField("numberOfDiscs", e.target.value)} />
          <TextField placeholder="Manufacturer" value={form.manufacturerName ?? ""} onChange={(e) => setField("manufacturerName", e.target.value)} />
          <TextField type="date" label="Expiry" InputLabelProps={{ shrink: true }} value={form.expiryDate ?? ""} onChange={(e) => setField("expiryDate", e.target.value)} />
          <TextField placeholder="Storage Condition" value={form.storageCondition ?? ""} onChange={(e) => setField("storageCondition", e.target.value)} />
          <TextField placeholder="Physical Check" value={form.physicalCheckText ?? ""} onChange={(e) => setField("physicalCheckText", e.target.value)} />
        </Box>

        <Typography sx={{ fontWeight: 600, mb: 1 }}>Identity Confirmation Panel</Typography>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>Media (GPT-released)</TableCell><TableCell>Incubator</TableCell><TableCell>Start</TableCell><TableCell>End</TableCell><TableCell>Observation</TableCell><TableCell></TableCell>
          </TableRow></TableHead>
          <TableBody>
            {panel.map((row, i) => (
              <TableRow key={i}>
                <TableCell>
                  <Select size="small" fullWidth displayEmpty value={row.mediaId} onChange={(e) => updateRow(i, "mediaId", e.target.value)}>
                    <MenuItem value=""><em>Media</em></MenuItem>
                    {releasedMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber}</MenuItem>)}
                  </Select>
                </TableCell>
                <TableCell>
                  <Select size="small" fullWidth displayEmpty value={row.incubatorEquipmentId} onChange={(e) => updateRow(i, "incubatorEquipmentId", e.target.value)}>
                    <MenuItem value=""><em>Incubator</em></MenuItem>
                    {incubators.map((i2) => <MenuItem key={i2.id} value={i2.id}>{i2.name}</MenuItem>)}
                  </Select>
                </TableCell>
                <TableCell><TextField size="small" type="date" value={row.incubationStart} onChange={(e) => updateRow(i, "incubationStart", e.target.value)} /></TableCell>
                <TableCell><TextField size="small" type="date" value={row.incubationEnd} onChange={(e) => updateRow(i, "incubationEnd", e.target.value)} /></TableCell>
                <TableCell><TextField size="small" value={row.observationText} onChange={(e) => updateRow(i, "observationText", e.target.value)} /></TableCell>
                <TableCell><IconButton size="small" onClick={() => removeRow(i)}><CloseIcon fontSize="small" /></IconButton></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Button size="small" onClick={addRow} sx={{ mt: 1 }}>+ Add Media Row</Button>

        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
          <Button variant="contained" onClick={save}>Save</Button>
        </Box>
      </Paper>

      <SectionTitle>RS Review Queue</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Code</TableCell><TableCell>Organism</TableCell><TableCell>ATCC</TableCell><TableCell>Discs Remaining</TableCell><TableCell>Status</TableCell><TableCell></TableCell></TableRow></TableHead>
          <TableBody>
            {strains.map((s) => (
              <TableRow key={s.id}>
                <TableCell>{s.code}</TableCell><TableCell>{s.organismName}</TableCell><TableCell>{s.atccNumber}</TableCell>
                <TableCell><StatusBadge status={s.approvalStatus} /></TableCell>
                <TableCell>
                  {s.approvalStatus === "PendingReview" && (
                    <>
                      <Button size="small" color="success" onClick={() => approve(s.id, true)}>Approve</Button>
                      <Button size="small" color="error" onClick={() => approve(s.id, false)}>Reject</Button>
                    </>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
