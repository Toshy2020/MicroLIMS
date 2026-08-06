import { useEffect, useState } from "react";
import { Paper, Box, TextField, Select, MenuItem, Button, Table, TableHead, TableRow, TableCell, TableBody, Alert, Autocomplete, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { OrganismPicker } from "../../../components/OrganismPicker";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { masterDataOptions, evaluationTypeLabel } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";

const EVALUATION_TYPES = ["GrowthPromotion", "IndicationInhibition", "EnrichmentCharacteristics"];
const CHALLENGE_ROLES = ["Inhibition", "Indication"];

// Section Head master data: which organism(s) a dehydrated media product
// must be challenged with under each of the three Media Evaluation types,
// and (for Indication challenges) what the expected colony description
// is. MediaPreparationService.PrepareAsync matches on MaterialName +
// EvaluationType to auto-assign challenges when a lot is prepared.
export function MediaChallengeSpecsPage() {
  const [specs, setSpecs] = useState<any[]>([]);
  const [materialNames, setMaterialNames] = useState<string[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<any | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => masterDataOptions.getMediaChallengeSpecs().then(setSpecs);
  useEffect(() => {
    load();
    // Dehydrated Media stock names, deduplicated, so specs are spelled
    // consistently with what Media Preparation actually offers, instead
    // of being free-typed each time.
    MaterialService.getAll("DehydratedMedia").then((materials: any[]) =>
      setMaterialNames(Array.from(new Set(materials.map((m) => m.materialName))))
    );
  }, []);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const startEdit = (s: any) => {
    setEditingId(s.id);
    setForm({
      materialName: s.materialName, evaluationType: s.evaluationType, organismId: s.organismId,
      challengeRole: s.challengeRole ?? undefined, expectedDescription: s.expectedDescription ?? undefined
    });
    setMessage(null);
  };

  const cancelEdit = () => { setEditingId(null); setForm({}); };

  const save = async () => {
    setMessage(null);
    const payload = {
      materialName: form.materialName ?? "", evaluationType: form.evaluationType,
      organismId: form.organismId, challengeRole: form.evaluationType === "IndicationInhibition" ? form.challengeRole : null,
      expectedDescription: form.challengeRole === "Indication" ? form.expectedDescription : null
    };
    try {
      if (editingId) {
        await masterDataOptions.updateMediaChallengeSpec(editingId, payload);
        setMessage({ text: "Challenge spec updated.", ok: true });
      } else {
        await masterDataOptions.createMediaChallengeSpec(payload);
        setMessage({ text: "Challenge spec saved.", ok: true });
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save.", ok: false });
    }
  };

  const remove = async (spec: any) => {
    setMessage(null);
    try {
      await masterDataOptions.deleteMediaChallengeSpec(spec.id);
      setPendingDelete(null);
      load();
    } catch (e: any) {
      setPendingDelete(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this challenge spec.", ok: false });
    }
  };

  const canSave = form.materialName && form.evaluationType && form.organismId &&
    (form.evaluationType !== "IndicationInhibition" || form.challengeRole);

  return (
    <>
      <PageHeader title="Media Challenge Specs" subtitle="Which organism(s) each dehydrated media product must be challenged with, per evaluation type." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingId ? "Edit Challenge Spec" : "New Challenge Spec"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 2 }}>
          <Autocomplete
            options={materialNames}
            value={form.materialName ?? null}
            onChange={(_e, v) => setField("materialName", v ?? "")}
            renderInput={(params) => <TextField {...params} placeholder="Material Name (e.g. TSA, XLD)" />}
          />
          <Select displayEmpty value={form.evaluationType ?? ""} onChange={(e) => setField("evaluationType", e.target.value)}>
            <MenuItem value=""><em>Evaluation Type</em></MenuItem>
            {EVALUATION_TYPES.map((t) => <MenuItem key={t} value={t}>{evaluationTypeLabel(t)}</MenuItem>)}
          </Select>
          <OrganismPicker value={form.organismId ?? null} onChange={(id) => setField("organismId", id)} />
          {form.evaluationType === "IndicationInhibition" && (
            <Select displayEmpty value={form.challengeRole ?? ""} onChange={(e) => setField("challengeRole", e.target.value)}>
              <MenuItem value=""><em>Challenge Role</em></MenuItem>
              {CHALLENGE_ROLES.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
            </Select>
          )}
          {form.challengeRole === "Indication" && (
            <TextField placeholder="Expected Description" value={form.expectedDescription ?? ""} onChange={(e) => setField("expectedDescription", e.target.value)} />
          )}
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 2 }}>
          {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
          <Button variant="contained" disabled={!canSave} onClick={save}>{editingId ? "Save Changes" : "Save"}</Button>
        </Box>
      </Paper>

      <SectionTitle>Challenge Specs</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>Material</TableCell><TableCell>Evaluation Type</TableCell><TableCell>Organism</TableCell>
            <TableCell>Role</TableCell><TableCell>Expected Description</TableCell><TableCell />
          </TableRow></TableHead>
          <TableBody>
            {specs.map((s) => (
              <TableRow key={s.id}>
                <TableCell>{s.materialName}</TableCell>
                <TableCell>{evaluationTypeLabel(s.evaluationType)}</TableCell>
                <TableCell>{s.organism?.scientificName}</TableCell>
                <TableCell>{s.challengeRole ?? "—"}</TableCell>
                <TableCell>{s.expectedDescription ?? "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(s)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(s)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.materialName} / ${pendingDelete.organism?.scientificName} challenge spec? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete)}
      />
    </>
  );
}
