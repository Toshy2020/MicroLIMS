import { useEffect, useState } from "react";
import { Paper, Box, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Alert, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";

// MediaType is a fixed set of 4 rows, one per MediaClass - no create/
// delete here, only editing the GPT pass/fail rules for each class.
// Per-organism challenge specs now live on the Media Challenge Specs
// page (they're keyed by Material, not MediaType - see MediaChallengeSpec.cs).
export function MediaTypesPage() {
  const [list, setList] = useState<any[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editForm, setEditForm] = useState<Record<string, any>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => masterDataOptions.getMediaTypes().then(setList);
  useEffect(() => { load(); }, []);

  const startEdit = (m: any) => {
    setEditingId(m.id);
    setEditForm({
      incubationMinHours: m.incubationMinHours, incubationMaxHours: m.incubationMaxHours,
      requiredTemperatureMin: m.requiredTemperatureMin, requiredTemperatureMax: m.requiredTemperatureMax,
      approvedTestCodes: (m.approvedTestCodes ?? []).join(", "),
      recoveryPercentMin: m.recoveryPercentMin ?? "", recoveryPercentMax: m.recoveryPercentMax ?? ""
    });
    setMessage(null);
  };
  const cancelEdit = () => { setEditingId(null); setEditForm({}); };

  const save = async (m: any) => {
    try {
      await masterDataOptions.updateMediaType(m.id, {
        incubationMinHours: Number(editForm.incubationMinHours), incubationMaxHours: Number(editForm.incubationMaxHours),
        requiredTemperatureMin: Number(editForm.requiredTemperatureMin), requiredTemperatureMax: Number(editForm.requiredTemperatureMax),
        approvedTestCodes: (editForm.approvedTestCodes ?? "").split(",").map((s: string) => s.trim()).filter(Boolean),
        recoveryPercentMin: editForm.recoveryPercentMin === "" ? null : Number(editForm.recoveryPercentMin),
        recoveryPercentMax: editForm.recoveryPercentMax === "" ? null : Number(editForm.recoveryPercentMax)
      });
      setMessage({ text: "Media type saved.", ok: true });
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Media Types" subtitle="The GPT pass/fail rules for each of the 4 media classes." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Media Types</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Class</TableCell><TableCell>Approved Test Codes</TableCell><TableCell>Incubation</TableCell>
              <TableCell>Temp Range</TableCell><TableCell>Recovery% Band</TableCell><TableCell></TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {list.map((m) => (
              <TableRow key={m.id}>
                {editingId === m.id ? (
                  <>
                    <TableCell>{mediaClassLabel(m.class)}</TableCell>
                    <TableCell>
                      <TextField size="small" placeholder="Comma sep" value={editForm.approvedTestCodes ?? ""} onChange={(e) => setEditForm({ ...editForm, approvedTestCodes: e.target.value })} />
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: "flex", gap: 1 }}>
                        <TextField size="small" placeholder="Min hrs" sx={{ width: 90 }} value={editForm.incubationMinHours ?? ""} onChange={(e) => setEditForm({ ...editForm, incubationMinHours: e.target.value })} />
                        <TextField size="small" placeholder="Max hrs" sx={{ width: 90 }} value={editForm.incubationMaxHours ?? ""} onChange={(e) => setEditForm({ ...editForm, incubationMaxHours: e.target.value })} />
                      </Box>
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: "flex", gap: 1 }}>
                        <TextField size="small" placeholder="Min °C" sx={{ width: 90 }} value={editForm.requiredTemperatureMin ?? ""} onChange={(e) => setEditForm({ ...editForm, requiredTemperatureMin: e.target.value })} />
                        <TextField size="small" placeholder="Max °C" sx={{ width: 90 }} value={editForm.requiredTemperatureMax ?? ""} onChange={(e) => setEditForm({ ...editForm, requiredTemperatureMax: e.target.value })} />
                      </Box>
                    </TableCell>
                    <TableCell>
                      {m.class === "GeneralAgar" ? (
                        <Box sx={{ display: "flex", gap: 1 }}>
                          <TextField size="small" placeholder="Min %" sx={{ width: 90 }} value={editForm.recoveryPercentMin ?? ""} onChange={(e) => setEditForm({ ...editForm, recoveryPercentMin: e.target.value })} />
                          <TextField size="small" placeholder="Max %" sx={{ width: 90 }} value={editForm.recoveryPercentMax ?? ""} onChange={(e) => setEditForm({ ...editForm, recoveryPercentMax: e.target.value })} />
                        </Box>
                      ) : <em>N/A</em>}
                    </TableCell>
                    <TableCell align="right">
                      <Button size="small" onClick={cancelEdit} sx={{ mr: 1 }}>Cancel</Button>
                      <Button size="small" variant="contained" onClick={() => save(m)}>Save</Button>
                    </TableCell>
                  </>
                ) : (
                  <>
                    <TableCell>{mediaClassLabel(m.class)}</TableCell>
                    <TableCell>{(m.approvedTestCodes ?? []).join(", ")}</TableCell>
                    <TableCell>{m.incubationMinHours}–{m.incubationMaxHours}h</TableCell>
                    <TableCell>{m.requiredTemperatureMin}–{m.requiredTemperatureMax}°C</TableCell>
                    <TableCell>{m.class === "GeneralAgar" ? `${m.recoveryPercentMin ?? "—"}–${m.recoveryPercentMax ?? "—"}%` : <em>N/A</em>}</TableCell>
                    <TableCell align="right">
                      <IconButton size="small" onClick={() => startEdit(m)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    </TableCell>
                  </>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
