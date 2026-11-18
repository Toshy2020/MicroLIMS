import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Button, Chip } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("masterData");
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
      setError(e.response?.data?.message ?? e.message ?? t("referenceOrganisms.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { load(); }, [load]);

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return strains;
    return strains.filter((s) =>
      [s.code, s.organismScientificName, s.cultureCollection, s.strainNumber, s.ownerSectionName].some((v) => (v ?? "").toLowerCase().includes(q))
    );
  }, [strains, search]);

  const columns: RegisterColumn<ReferenceOrganism>[] = [
    { key: "code", label: t("referenceOrganisms.columns.code"), sortable: true, render: (s) => <span style={{ fontFamily: monospaceFontFamily }}>{s.code}</span> },
    { key: "organismScientificName", label: t("referenceOrganisms.columns.species"), sortable: true, render: (s) => s.organismScientificName ?? t("common.dash") },
    { key: "cultureCollection", label: t("referenceOrganisms.columns.collection"), sortable: true },
    { key: "strainNumber", label: t("referenceOrganisms.columns.strainNumber"), sortable: true, render: (s) => <span style={{ fontFamily: monospaceFontFamily }}>{s.strainNumber ?? t("common.dash")}</span> },
    { key: "ownerSectionName", label: t("referenceOrganisms.columns.ownerLab"), sortable: true, render: (s) => s.ownerSectionName ?? s.ownerSectionCode ?? t("common.dash") },
    {
      key: "isActive",
      label: t("referenceOrganisms.columns.status"),
      sortable: true,
      render: (s) => <Chip label={s.isActive ? t("common.active") : t("common.inactive")} size="small" color={s.isActive ? "success" : "default"} variant="outlined" sx={{ fontSize: 12 }} />
    }
  ];

  const confirmToggle = async () => {
    if (!toToggle) return;
    const next = !toToggle.isActive;
    try {
      await ReferenceOrganismService.setActive(toToggle.id, next);
      toast.success(
        next
          ? t("referenceOrganisms.toggledToast_activated", { code: toToggle.code })
          : t("referenceOrganisms.toggledToast_deactivated", { code: toToggle.code })
      );
      setToToggle(null);
      await load();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setToToggle(null);
      toast.error(e.response?.data?.message ?? e.message ?? t("referenceOrganisms.toggleError"));
    }
  };

  const addButton = canManage ? (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditing(null); setDialogOpen(true); }}>
      {t("referenceOrganisms.addButton")}
    </Button>
  ) : undefined;

  return (
    <>
      {error && <Alert severity="error">{error}</Alert>}
      <FilterBar
        search={search}
        onSearch={setSearch}
        placeholder={t("referenceOrganisms.searchPlaceholder")}
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
          { label: t("common.edit"), onClick: () => { setEditing(s); setDialogOpen(true); }, disabled: !canManage || !s.canEdit },
          ...(s.hasStock ? [{ label: t("referenceOrganisms.changeCodeAction"), onClick: () => setToRecode(s), disabled: !canManage || !s.canEdit }] : []),
          s.isActive
            ? { label: t("common.deactivate"), onClick: () => setToToggle(s), danger: true, disabled: !canManage || !s.canEdit }
            : { label: t("common.activate"), onClick: () => setToToggle(s), disabled: !canManage || !s.canEdit }
        ]}
        empty={
          search.trim()
            ? { title: t("referenceOrganisms.emptyMatchingTitle"), description: t("referenceOrganisms.emptyMatchingDesc") }
            : { title: t("referenceOrganisms.emptyTitle"), description: t("referenceOrganisms.emptyDesc"), action: addButton }
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
        onSuccess={() => { toast.success(t("referenceOrganisms.codeChangedToast")); load(); }}
      />

      <ConfirmationDialog
        open={Boolean(toToggle)}
        title={toToggle?.isActive ? t("referenceOrganisms.deactivateTitle") : t("referenceOrganisms.activateTitle")}
        message={
          toToggle?.isActive
            ? t("referenceOrganisms.deactivateMessage", { code: toToggle.code, name: toToggle.name })
            : t("referenceOrganisms.activateMessage", { code: toToggle?.code ?? "", name: toToggle?.name ?? "" })
        }
        destructive={toToggle?.isActive}
        confirmText={toToggle?.isActive ? t("common.deactivate") : t("common.activate")}
        onConfirm={confirmToggle}
        onCancel={() => setToToggle(null)}
      />
    </>
  );
}
