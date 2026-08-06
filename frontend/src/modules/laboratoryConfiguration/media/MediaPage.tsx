import { useEffect, useState } from "react";
import { Box, Paper, Table, TableHead, TableRow, TableCell, TableBody, TextField, Select, MenuItem, Button, Alert, Typography } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";

// Where a lot sits in its lifecycle. A Conform evaluation only makes a
// lot eligible - "Awaiting Approval" is the state between passing
// evaluation and a Section Head signing for its release.
function lifecycleOf(lot: any, awaitingApprovalIds: Set<number>): string {
  if (lot.isReleasedForUse) return "Released";
  if (lot.approvalStatus === "Rejected" || lot.status === "QuarantineFailed") return "Quarantined";
  if (awaitingApprovalIds.has(lot.id)) return "Awaiting Approval";
  return "Pending Evaluation";
}

export function MediaPage() {
  const { role } = useAuth();
  const canRelease = role === "SectionHead" || role === "SystemAdministrator";

  const [lots, setLots] = useState<any[]>([]);
  const [awaitingApprovalIds, setAwaitingApprovalIds] = useState<Set<number>>(new Set());
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [autoclaves, setAutoclaves] = useState<any[]>([]);
  const [dehydratedMedia, setDehydratedMedia] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ lot: any; approved: boolean } | null>(null);

  const load = () => {
    MediaPreparationService.getAll().then(setLots);
    MediaPreparationService.getAwaitingApproval()
      .then((queue: any[]) => setAwaitingApprovalIds(new Set(queue.map((m) => m.id))))
      .catch(() => setAwaitingApprovalIds(new Set()));
  };

  useEffect(() => {
    load();
    masterDataOptions.getMediaTypes().then(setMediaTypes);
    masterDataOptions.getEquipment("Autoclave").then(setAutoclaves);
    MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia);
  }, []);

  // Throws on failure so SignatureDialog can surface the server's message
  // (wrong password, segregation violation) and keep itself open.
  const confirmDecision = async (password: string) => {
    if (!pendingDecision) return;
    await MediaPreparationService.decideRelease(pendingDecision.lot.id, password, pendingDecision.approved);
    setMessage({
      text: `Lot ${pendingDecision.lot.lotNumber} ${pendingDecision.approved ? "released for use" : "quarantined"}.`,
      ok: pendingDecision.approved
    });
    setPendingDecision(null);
    load();
  };

  // Only stock that is actually usable (not expired, not depleted) is
  // offered - MediaPreparationService.PrepareAsync re-checks this
  // server-side regardless, this is just so the analyst doesn't pick
  // something that will get rejected.
  const usableStock = dehydratedMedia.filter((m) => m.status === "InStock");
  const selectedMaterial = usableStock.find((m) => m.id === form.materialId);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const save = async () => {
    setMessage(null);
    try {
      await MediaPreparationService.prepare({
        mediaTypeId: form.mediaTypeId, materialId: form.materialId,
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
            {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
          </Select>
          <Box>
            <Select displayEmpty fullWidth value={form.materialId ?? ""} onChange={(e) => setField("materialId", e.target.value)}>
              <MenuItem value=""><em>Dehydrated Media Stock (Inventory)</em></MenuItem>
              {usableStock.map((m) => (
                <MenuItem key={m.id} value={m.id}>{m.materialName} — batch {m.batchNumber} ({m.quantityRemaining} {m.unit} left)</MenuItem>
              ))}
            </Select>
            {selectedMaterial && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                {selectedMaterial.manufacturerName} — batch {selectedMaterial.batchNumber}
              </Typography>
            )}
          </Box>
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
          <TableHead><TableRow>
            <TableCell>Lot</TableCell><TableCell>Prepared</TableCell><TableCell>Expiry</TableCell>
            <TableCell>Status</TableCell><TableCell>Release</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {lots.map((m) => {
              const lifecycle = lifecycleOf(m, awaitingApprovalIds);
              return (
                <TableRow key={m.id}>
                  <TableCell>{m.lotNumber}</TableCell>
                  <TableCell>{new Date(m.preparedAt).toLocaleDateString()}</TableCell>
                  <TableCell>{new Date(m.expiryDate).toLocaleDateString()}</TableCell>
                  <TableCell><StatusBadge status={lifecycle} /></TableCell>
                  <TableCell>
                    {lifecycle === "Awaiting Approval" && canRelease && (
                      <>
                        <Button size="small" color="success" onClick={() => setPendingDecision({ lot: m, approved: true })}>Release</Button>
                        <Button size="small" color="error" onClick={() => setPendingDecision({ lot: m, approved: false })}>Reject</Button>
                      </>
                    )}
                    {lifecycle === "Released" && m.approvedAt && (
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        Signed {new Date(m.approvedAt).toLocaleDateString()}
                      </Typography>
                    )}
                    <Button size="small" onClick={() => window.open(`/media/${m.id}/report`, "_blank", "noopener")}>Record</Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </Paper>

      {pendingDecision && (
        <SignatureDialog
          open
          meaningStatement={pendingDecision.approved
            ? `I am releasing media lot ${pendingDecision.lot.lotNumber} for use in routine testing.`
            : `I am rejecting media lot ${pendingDecision.lot.lotNumber} - it will be quarantined.`}
          onCancel={() => setPendingDecision(null)}
          onConfirm={confirmDecision}
        />
      )}
    </>
  );
}
