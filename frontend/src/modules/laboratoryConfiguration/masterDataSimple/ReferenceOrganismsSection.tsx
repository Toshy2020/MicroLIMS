import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Button, Chip } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { FilterBar, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { monospaceFontFamily } from "../../../theme/palette";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import type { OrganismOption } from "../../../hooks/useOrganisms";
import { ReferenceOrganismService, ReferenceOrganism } from "./services/ReferenceOrganismService";
import { ReferenceOrganismDialog } from "./ReferenceOrganismDialog";
import { ChangeReferenceOrganismCodeDialog } from "./ChangeReferenceOrganismCodeDialog";

// Reference strains (culture collection + strain number) under the organism species list.
// Lyophilized stock lots and cryovials are received against a strain.
export function ReferenceOrganismsSection({ species }: { species: OrganismOption[] }) {
  const { permissions } = useAuth();
  const canManage = permissions.includes(PERMISSIONS.MASTER_DATA_MANAGE);
  const { sections } = useLaboratorySections();
  const [strains, setStrains] = useState<ReferenceOrganism[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ReferenceOrganism | null>(null);
  const [toRecode, setToRecode] = useState<ReferenceOrganism | null>(null);
  const [toToggle, setToToggle] = useState<ReferenceOrganism | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setStrains(await ReferenceOrganismService.getAll());
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load reference organisms.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return strains;
    return strains.filter((s) =>
      [s.code, s.organismScientificName, s.cultureCollection, s.strainNumber, s.ownerSectionName].some((v) => (v ?? "").toLowerCase().includes(q))
    );
  }, [strains, search]);

  const columns: RegisterColumn<ReferenceOrganism>[] = [
    { key: "code", label: "Code", sortable: true, render: (s) => <span style={{ fontFamily: monospaceFontFamily }}>{s.code}</span> },
    { key: "organismScientificName", label: "Species", sortable: true, render: (s) => s.organismScientificName ?? "—" },
    { key: "cultureCollection", label: "Collection", sortable: true },
    { key: "strainNumber", label: "Strain No.", sortable: true, render: (s) => <span style={{ fontFamily: monospaceFontFamily }}>{s.strainNumber ?? "—"}</span> },
    { key: "ownerSectionName", label: "Owner Lab", sortable: true, render: (s) => s.ownerSectionName ?? s.ownerSectionCode ?? "—" },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      render: (s) => <Chip label={s.isActive ? "Active" : "Inactive"} size="small" color={s.isActive ? "success" : "default"} variant="outlined" sx={{ fontSize: 12 }} />
    }
  ];

  const confirmToggle = async () => {
    if (!toToggle) return;
    const next = !toToggle.isActive;
    try {
      await ReferenceOrganismService.setActive(toToggle.id, next);
      toast.success(`Reference organism "${toToggle.code}" ${next ? "activated" : "deactivated"}.`);
      setToToggle(null);
      await load();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setToToggle(null);
      toast.error(e.response?.data?.message ?? e.message ?? "Could not update the reference organism status.");
    }
  };

  const addButton = canManage ? (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditing(null); setDialogOpen(true); }}>
      Add Reference Organism
    </Button>
  ) : undefined;

  return (
    <>
      {error && <Alert severity="error">{error}</Alert>}
      <FilterBar
        search={search}
        onSearch={setSearch}
        placeholder="Search by code, species, collection, strain no...."
        resultCount={visible.length}
        onRefresh={load}
        refreshing={loading}
      >
        {addButton}
      </FilterBar>

      <RegisterTable
        columns={columns}
        rows={visible}
        getRowId={(s) => s.id}
        loading={loading}
        rowActions={(s) => [
          { label: "Edit", onClick: () => { setEditing(s); setDialogOpen(true); }, disabled: !canManage || !s.canEdit },
          ...(s.hasStock ? [{ label: "Change code", onClick: () => setToRecode(s), disabled: !canManage || !s.canEdit }] : []),
          s.isActive
            ? { label: "Deactivate", onClick: () => setToToggle(s), danger: true, disabled: !canManage || !s.canEdit }
            : { label: "Activate", onClick: () => setToToggle(s), disabled: !canManage || !s.canEdit }
        ]}
        empty={
          search.trim()
            ? { title: "No reference organisms match the search", description: "Clear the search to see all strains." }
            : { title: "No reference organisms yet", description: "Add the first strain (species + culture collection + strain number).", action: addButton }
        }
      />

      <ReferenceOrganismDialog
        open={dialogOpen}
        strain={editing}
        species={species}
        sections={sections}
        onClose={() => setDialogOpen(false)}
        onSaved={(message) => { toast.success(message); load(); }}
      />

      <ChangeReferenceOrganismCodeDialog
        open={Boolean(toRecode)}
        strain={toRecode}
        onClose={() => setToRecode(null)}
        onSuccess={() => { toast.success("Reference organism code changed."); load(); }}
      />

      <ConfirmationDialog
        open={Boolean(toToggle)}
        title={toToggle?.isActive ? "Deactivate Reference Organism" : "Activate Reference Organism"}
        message={
          toToggle?.isActive
            ? `Deactivate "${toToggle.code} - ${toToggle.name}"? Inactive strains cannot be chosen for new stock lots.`
            : `Activate "${toToggle?.code} - ${toToggle?.name}"?`
        }
        destructive={toToggle?.isActive}
        confirmText={toToggle?.isActive ? "Deactivate" : "Activate"}
        onConfirm={confirmToggle}
        onCancel={() => setToToggle(null)}
      />
    </>
  );
}
