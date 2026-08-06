import { useState } from "react";
import { Paper, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { useOrganisms, OrganismOption } from "../../../hooks/useOrganisms";

// The Organism master list - the canonical ScientificName/AtccNumber
// referenced everywhere an organism is assigned (Media Challenge Specs,
// Material, Cryovial, Media Evaluation) via OrganismPicker. This is what
// fixes MediaEvaluationEngine.SelectCryovialAsync's organism matching:
// comparing OrganismId (int) instead of free-typed OrganismName strings.
export function OrganismsPage() {
  const { options, addNew, update, remove } = useOrganisms();
  const [scientificName, setScientificName] = useState("");
  const [atccNumber, setAtccNumber] = useState("");
  const [commonName, setCommonName] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<OrganismOption | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const startEdit = (o: OrganismOption) => {
    setEditingId(o.id);
    setScientificName(o.scientificName);
    setAtccNumber(o.atccNumber ?? "");
    setCommonName(o.commonName ?? "");
    setMessage(null);
  };

  const cancelEdit = () => { setEditingId(null); setScientificName(""); setAtccNumber(""); setCommonName(""); };

  const save = async () => {
    setMessage(null);
    if (!scientificName) {
      setMessage({ text: "Scientific Name is required.", ok: false });
      return;
    }
    try {
      if (editingId) {
        await update(editingId, scientificName, atccNumber || null, commonName || null);
        setMessage({ text: `Organism "${scientificName}" updated.`, ok: true });
      } else {
        await addNew(scientificName, atccNumber || null);
        setMessage({ text: `Organism "${scientificName}" added.`, ok: true });
      }
      cancelEdit();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this organism.`, ok: false });
    }
  };

  const deleteOrganism = async (o: OrganismOption) => {
    setMessage(null);
    try {
      await remove(o.id);
      setPendingDelete(null);
    } catch (e: any) {
      setPendingDelete(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this organism.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Organisms" subtitle="The canonical organism list referenced by Media Challenge Specs, Materials, and Cryovials." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingId ? "Edit Organism" : "Add Organism"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Scientific Name" placeholder="e.g. Escherichia coli" value={scientificName} onChange={(e) => setScientificName(e.target.value)} sx={{ minWidth: 240 }} />
          <TextField size="small" label="ATCC No." placeholder="e.g. 25922" value={atccNumber} onChange={(e) => setAtccNumber(e.target.value)} sx={{ minWidth: 160 }} />
          <TextField size="small" label="Common Name (optional)" value={commonName} onChange={(e) => setCommonName(e.target.value)} sx={{ minWidth: 200 }} />
          {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
          <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add Organism"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>All Organisms</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table size="small">
          <TableHead><TableRow><TableCell>Scientific Name</TableCell><TableCell>ATCC No.</TableCell><TableCell>Common Name</TableCell><TableCell></TableCell></TableRow></TableHead>
          <TableBody>
            {options.map((o) => (
              <TableRow key={o.id}>
                <TableCell>{o.scientificName}</TableCell>
                <TableCell>{o.atccNumber ?? "—"}</TableCell>
                <TableCell>{o.commonName ?? "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(o)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(o)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete organism "${pendingDelete.scientificName}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteOrganism(pendingDelete)}
      />
    </>
  );
}
