import { useEffect, useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, Button, Typography, Alert, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReferenceStrainService } from "./services/ReferenceStrainService";
import { masterDataOptions } from "../../../services/masterDataOptions";

export function CryovialsPage() {
  const [strains, setStrains] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [panelMediaId, setPanelMediaId] = useState("");
  const [panelIncubatorId, setPanelIncubatorId] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => ReferenceStrainService.getAll().then(setStrains);
  useEffect(() => {
    load();
    masterDataOptions.getReleasedMedia().then(setReleasedMedia);
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, []);

  const approvedStrains = strains.filter((s) => s.approvalStatus === "Approved");
  const allCryovials = strains.flatMap((s) => (s.cryovials ?? []).map((c: any) => ({ ...c, strainName: s.organismName })));

  const save = async () => {
    setMessage(null);
    try {
      await ReferenceStrainService.prepareCryovials({
        referenceStrainId: form.referenceStrainId, manufacturerName: form.manufacturerName, expiryDate: form.expiryDate,
        numberOfVialsPrepared: Number(form.numberOfVialsPrepared), storageCondition: form.storageCondition, physicalCheckText: form.physicalCheckText,
        discsUsed: Number(form.discsUsed),
        panel: panelMediaId ? [{ mediaId: Number(panelMediaId), incubatorEquipmentId: Number(panelIncubatorId), incubationStart: form.incubationStart, incubationEnd: form.incubationEnd, observationText: form.observationText ?? "" }] : []
      });
      setMessage({ text: "Cryovials prepared.", ok: true });
      setForm({});
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare cryovials.", ok: false });
    }
  };

  const approve = async (id: number, approved: boolean) => { await ReferenceStrainService.approveCryovial(id, approved); load(); };
  const destroy = async (id: number) => { await ReferenceStrainService.destroyCryovial(id); load(); };

  return (
    <>
      <PageHeader title="Cryovials" subtitle="Prepare working vials from an approved Reference Strain." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Prepare Cryovials</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ maxWidth: 320, mb: 2 }}>
          <Select displayEmpty fullWidth value={form.referenceStrainId ?? ""} onChange={(e) => setForm({ ...form, referenceStrainId: e.target.value })}>
            <MenuItem value=""><em>Reference Strain (must be approved)</em></MenuItem>
            {approvedStrains.map((s) => <MenuItem key={s.id} value={s.id}>{s.code} — {s.organismName} ({s.discsRemaining} disc(s) remaining)</MenuItem>)}
          </Select>
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
          <TextField placeholder="# Vials" value={form.numberOfVialsPrepared ?? ""} onChange={(e) => setForm({ ...form, numberOfVialsPrepared: e.target.value })} />
          <TextField placeholder="Discs Used" type="number" value={form.discsUsed ?? ""} onChange={(e) => setForm({ ...form, discsUsed: e.target.value })} />
          <TextField placeholder="Manufacturer" value={form.manufacturerName ?? ""} onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })} />
          <TextField type="date" label="Expiry" InputLabelProps={{ shrink: true }} value={form.expiryDate ?? ""} onChange={(e) => setForm({ ...form, expiryDate: e.target.value })} />
          <TextField placeholder="Storage Condition" value={form.storageCondition ?? ""} onChange={(e) => setForm({ ...form, storageCondition: e.target.value })} />
          <TextField placeholder="Physical Check" value={form.physicalCheckText ?? ""} onChange={(e) => setForm({ ...form, physicalCheckText: e.target.value })} />
        </Box>

        <Typography sx={{ fontWeight: 600, mt: 2, mb: 1 }}>Identity Confirmation (optional row)</Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 2 }}>
          <Select displayEmpty value={panelMediaId} onChange={(e) => setPanelMediaId(e.target.value)}>
            <MenuItem value=""><em>Media</em></MenuItem>
            {releasedMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber}</MenuItem>)}
          </Select>
          <Select displayEmpty value={panelIncubatorId} onChange={(e) => setPanelIncubatorId(e.target.value)}>
            <MenuItem value=""><em>Incubator</em></MenuItem>
            {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name}</MenuItem>)}
          </Select>
          <TextField type="date" size="small" value={form.incubationStart ?? ""} onChange={(e) => setForm({ ...form, incubationStart: e.target.value })} />
          <TextField type="date" size="small" value={form.incubationEnd ?? ""} onChange={(e) => setForm({ ...form, incubationEnd: e.target.value })} />
        </Box>

        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
          <Button variant="contained" onClick={save}>Save</Button>
        </Box>
      </Paper>

      <SectionTitle>Cryovial Review Queue</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Code</TableCell><TableCell>Strain</TableCell><TableCell>Passage</TableCell><TableCell>Status</TableCell><TableCell>Thawed</TableCell><TableCell></TableCell></TableRow></TableHead>
          <TableBody>
            {allCryovials.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.code}</TableCell><TableCell>{c.strainName}</TableCell><TableCell>{c.passageNumber}</TableCell>
                <TableCell><StatusBadge status={c.approvalStatus} /></TableCell>
                <TableCell>{c.thawedAt ? new Date(c.thawedAt).toLocaleString() : "—"}</TableCell>
                <TableCell>
                  {c.approvalStatus === "PendingReview" ? (
                    <>
                      <Button size="small" color="success" onClick={() => approve(c.id, true)}>Approve</Button>
                      <Button size="small" color="error" onClick={() => approve(c.id, false)}>Reject</Button>
                    </>
                  ) : (
                    !c.isDestroyed && <Button size="small" color="error" onClick={() => destroy(c.id)}>Destroy</Button>
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
