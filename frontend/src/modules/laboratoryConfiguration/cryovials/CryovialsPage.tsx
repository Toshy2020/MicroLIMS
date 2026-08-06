import { useEffect, useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, Button, Typography, Alert, Table, TableHead, TableRow, TableCell, TableBody, IconButton } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { CryovialService } from "./services/CryovialService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";

interface PanelRow { mediaId: string; incubatorEquipmentId: string; incubationStart: string; incubationEnd: string; observationText: string }
const emptyRow = (): PanelRow => ({ mediaId: "", incubatorEquipmentId: "", incubationStart: "", incubationEnd: "", observationText: "" });

// Cryovial batches are prepared directly from a LyophilizedMicroorganism
// Material row (Inventory Materials Stock) - there is no separate
// reference-strain receiving step. The identity-confirmation panel
// (at least one row) is mandatory, since it's the only place identity
// is confirmed before a batch can be approved for GPT use.
export function CryovialsPage() {
  const [cryovials, setCryovials] = useState<any[]>([]);
  const [materials, setMaterials] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [panel, setPanel] = useState<PanelRow[]>([emptyRow()]);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ cryovial: any; approved: boolean } | null>(null);

  const load = () => CryovialService.getAll().then(setCryovials);
  const loadMaterials = () => MaterialService.getAll("LyophilizedMicroorganism").then(setMaterials);
  useEffect(() => {
    load();
    loadMaterials();
    masterDataOptions.getReleasedMedia().then(setReleasedMedia);
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, []);

  // Only stock that is actually usable (not expired, not depleted) is
  // offered - CryovialService.PrepareCryovialsAsync re-checks this
  // server-side regardless, this is just so the analyst doesn't pick
  // something that will get rejected.
  const usableMaterials = materials.filter((m) => m.status === "InStock");
  const selectedMaterial = usableMaterials.find((m) => m.id === form.materialId);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const updateRow = (i: number, k: keyof PanelRow, v: string) => setPanel((p) => p.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const addRow = () => setPanel((p) => [...p, emptyRow()]);
  const removeRow = (i: number) => setPanel((p) => (p.length > 1 ? p.filter((_, idx) => idx !== i) : p));

  const save = async () => {
    setMessage(null);
    try {
      await CryovialService.prepare({
        materialId: form.materialId, numberOfVialsPrepared: Number(form.numberOfVialsPrepared), expiryDate: form.expiryDate,
        storageCondition: form.storageCondition, physicalCheckText: form.physicalCheckText, discsUsed: Number(form.discsUsed),
        panel: panel.map((r) => ({
          mediaId: Number(r.mediaId), incubatorEquipmentId: Number(r.incubatorEquipmentId),
          incubationStart: r.incubationStart, incubationEnd: r.incubationEnd, observationText: r.observationText
        }))
      });
      setMessage({ text: "Cryovial batch prepared.", ok: true });
      setForm({}); setPanel([emptyRow()]);
      load();
      loadMaterials();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare cryovial batch.", ok: false });
    }
  };

  // Throws on failure so SignatureDialog can surface the server's message
  // (wrong password, segregation violation) and keep itself open.
  const confirmDecision = async (password: string) => {
    if (!pendingDecision) return;
    await CryovialService.approve(pendingDecision.cryovial.id, pendingDecision.approved, password);
    setMessage({
      text: `Batch ${pendingDecision.cryovial.code} ${pendingDecision.approved ? "approved for use" : "rejected and destroyed"}.`,
      ok: pendingDecision.approved
    });
    setPendingDecision(null);
    load();
  };
  const destroy = async (id: number) => { await CryovialService.destroy(id); load(); };
  const thaw = async (id: number) => { await CryovialService.thawVial(id); load(); };

  return (
    <>
      <PageHeader title="Cryovials" subtitle="Prepare working cryovial batches from an approved lyophilized microorganism material." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Prepare Cryovial Batch</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ maxWidth: 420, mb: 2 }}>
          <Select displayEmpty fullWidth value={form.materialId ?? ""} onChange={(e) => setField("materialId", e.target.value)}>
            <MenuItem value=""><em>Lyophilized Microorganism Stock (Inventory)</em></MenuItem>
            {usableMaterials.map((m) => (
              <MenuItem key={m.id} value={m.id}>{m.materialName} — batch {m.batchNumber} ({m.quantityRemaining} {m.unit} left)</MenuItem>
            ))}
          </Select>
          {selectedMaterial && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
              Organism: {selectedMaterial.organism?.scientificName ?? "— (set an Organism on this Material first)"}
              {selectedMaterial.organism?.atccNumber ? ` (ATCC ${selectedMaterial.organism.atccNumber})` : ""}
              {" — "}{selectedMaterial.manufacturerName}
            </Typography>
          )}
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
          <TextField placeholder="# Vials Prepared" value={form.numberOfVialsPrepared ?? ""} onChange={(e) => setField("numberOfVialsPrepared", e.target.value)} />
          <TextField placeholder="Discs Used" type="number" value={form.discsUsed ?? ""} onChange={(e) => setField("discsUsed", e.target.value)} />
          <TextField type="date" label="Expiry" InputLabelProps={{ shrink: true }} value={form.expiryDate ?? ""} onChange={(e) => setField("expiryDate", e.target.value)} />
          <TextField placeholder="Storage Condition" value={form.storageCondition ?? ""} onChange={(e) => setField("storageCondition", e.target.value)} />
          <TextField placeholder="Physical Check" value={form.physicalCheckText ?? ""} onChange={(e) => setField("physicalCheckText", e.target.value)} />
        </Box>

        <Typography sx={{ fontWeight: 600, mt: 2, mb: 1 }}>Identity Confirmation Panel</Typography>
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

      <SectionTitle>Cryovial Review Queue</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow>
            <TableCell>Code</TableCell><TableCell>Organism</TableCell><TableCell>Status</TableCell><TableCell>Vials</TableCell><TableCell></TableCell>
          </TableRow></TableHead>
          <TableBody>
            {cryovials.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.code}</TableCell>
                <TableCell>{c.organism?.scientificName ?? c.organismNameSnapshot}</TableCell>
                <TableCell><StatusBadge status={c.approvalStatus} /></TableCell>
                <TableCell>
                  {c.vialsRemaining} of {c.numberOfVialsPrepared} vials
                  {c.vialsRemaining === 0 && <Box sx={{ mt: 0.5 }}><StatusBadge status="Depleted" /></Box>}
                </TableCell>
                <TableCell>
                  {c.approvalStatus === "PendingReview" ? (
                    <>
                      <Button size="small" color="success" onClick={() => setPendingDecision({ cryovial: c, approved: true })}>Approve</Button>
                      <Button size="small" color="error" onClick={() => setPendingDecision({ cryovial: c, approved: false })}>Reject</Button>
                    </>
                  ) : (
                    <>
                      {c.approvalStatus === "Approved" && !c.isDestroyed && !isExpired(c.expiryDate) && c.vialsRemaining > 0 && (
                        <Button size="small" variant="outlined" onClick={() => thaw(c.id)} sx={{ mr: 1 }}>Thaw Vial</Button>
                      )}
                      {!c.isDestroyed && <Button size="small" color="error" onClick={() => destroy(c.id)}>Destroy</Button>}
                    </>
                  )}
                  <Button size="small" onClick={() => window.open(`/cryovials/${c.id}/report`, "_blank", "noopener")}>Record</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {pendingDecision && (
        <SignatureDialog
          open
          meaningStatement={pendingDecision.approved
            ? `I confirm the identity of cryovial batch ${pendingDecision.cryovial.code} and approve it for use.`
            : `I am rejecting cryovial batch ${pendingDecision.cryovial.code} - it will be destroyed.`}
          onCancel={() => setPendingDecision(null)}
          onConfirm={confirmDecision}
        />
      )}
    </>
  );
}

const isExpired = (expiryDate: string) => new Date(expiryDate) <= new Date();
