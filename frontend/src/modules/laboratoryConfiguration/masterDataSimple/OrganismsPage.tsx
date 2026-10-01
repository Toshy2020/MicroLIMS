import { useMemo, useState } from "react";
import { Paper, TextField, Button, Stack, Alert } from "@mui/material";
import { SectionTitle } from "../../../components/SectionTitle";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { LabPage, FilterBar, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
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
  const [description, setDescription] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<OrganismOption | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [search, setSearch] = useState("");

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) =>
      [o.scientificName, o.atccNumber, o.commonName, o.description].some((v) => (v ?? "").toLowerCase().includes(q))
    );
  }, [options, search]);

  const columns: RegisterColumn<OrganismOption>[] = [
    { key: "scientificName", label: "Scientific Name", sortable: true },
    { key: "atccNumber", label: "ATCC No.", sortable: true, render: (o) => <span style={{ fontFamily: monospaceFontFamily }}>{o.atccNumber ?? "—"}</span> },
    { key: "commonName", label: "Common Name", sortable: true, render: (o) => o.commonName ?? "—" },
    {
      key: "description",
      label: "Description",
      render: (o) => <span style={{ display: "block", maxWidth: 320, whiteSpace: "normal", wordBreak: "break-word" }}>{o.description ?? "—"}</span>
    }
  ];

  const startEdit = (o: OrganismOption) => {
    setEditingId(o.id);
    setScientificName(o.scientificName);
    setAtccNumber(o.atccNumber ?? "");
    setCommonName(o.commonName ?? "");
    setDescription(o.description ?? "");
    setMessage(null);
  };

  const cancelEdit = () => { setEditingId(null); setScientificName(""); setAtccNumber(""); setCommonName(""); setDescription(""); };

  const save = async () => {
    setMessage(null);
    if (!scientificName) {
      setMessage({ text: "Scientific Name is required.", ok: false });
      return;
    }
    try {
      if (editingId) {
        await update(editingId, scientificName, atccNumber || null, commonName || null, description || null);
        setMessage({ text: `Organism "${scientificName}" updated.`, ok: true });
      } else {
        await addNew(scientificName, atccNumber || null, commonName || null, description || null);
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
      <LabPage
        title="Organisms"
        subtitle="The canonical organism list referenced by Media Challenge Specs, Materials, and Cryovials."
      >
        {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}

        <div>
          <SectionTitle>{editingId ? "Edit Organism" : "Add Organism"}</SectionTitle>
          <Paper sx={{ p: 2.5 }}>
            <Stack
              direction="row"
              spacing={1.5}
              sx={{
                flexWrap: "wrap",
                alignItems: "center"
              }}>
              <TextField size="small" label="Scientific Name" placeholder="e.g. Escherichia coli" value={scientificName} onChange={(e) => setScientificName(e.target.value)} sx={{ minWidth: 240 }} />
              <TextField size="small" label="ATCC No." placeholder="e.g. 25922" value={atccNumber} onChange={(e) => setAtccNumber(e.target.value)} sx={{ minWidth: 160 }} />
              <TextField size="small" label="Common Name (optional)" value={commonName} onChange={(e) => setCommonName(e.target.value)} sx={{ minWidth: 200 }} />
              <TextField size="small" label="Description (optional)" value={description} onChange={(e) => setDescription(e.target.value)} sx={{ minWidth: 260, flex: 1 }} />
              {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
              <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add Organism"}</Button>
            </Stack>
          </Paper>
        </div>

        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder="Search by name, ATCC no., description..."
          resultCount={visible.length}
        />

        <RegisterTable
          columns={columns}
          rows={visible}
          getRowId={(o) => o.id}
          rowActions={(o) => [
            { label: "Edit", onClick: () => startEdit(o) },
            { label: "Delete", danger: true, onClick: () => setPendingDelete(o) }
          ]}
          empty={
            search.trim()
              ? { title: "No organisms match the search", description: "Clear the search to see all organisms." }
              : { title: "No organisms yet", description: "Add the first organism using the form above." }
          }
        />
      </LabPage>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete organism "${pendingDelete.scientificName}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteOrganism(pendingDelete)}
      />
    </>
  );
}
