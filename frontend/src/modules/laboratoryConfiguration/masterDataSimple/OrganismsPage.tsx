import { useMemo, useState } from "react";
import { Paper, TextField, Button, Stack, Alert, Tabs, Tab } from "@mui/material";
import { SectionTitle } from "../../../components/SectionTitle";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { LabPage, FilterBar, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { useOrganisms, OrganismOption } from "../../../hooks/useOrganisms";
import { ReferenceOrganismsSection } from "./ReferenceOrganismsSection";

// The Organism master list - the canonical ScientificName/AtccNumber
// referenced everywhere an organism is assigned (Media Challenge Specs,
// Material, Cryovial, Media Evaluation) via OrganismPicker. This is what
// fixes MediaEvaluationEngine.EnsureCryovialFitsChallengeAsync's organism matching:
// comparing OrganismId (int) instead of free-typed OrganismName strings.
export function OrganismsPage() {
  const { t } = useTranslation("masterData");
  const { options, addNew, update, remove } = useOrganisms();
  const [scientificName, setScientificName] = useState("");
  const [atccNumber, setAtccNumber] = useState("");
  const [commonName, setCommonName] = useState("");
  const [description, setDescription] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<OrganismOption | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<"species" | "strains">("species");

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) =>
      [o.scientificName, o.atccNumber, o.commonName, o.description].some((v) => (v ?? "").toLowerCase().includes(q))
    );
  }, [options, search]);

  const columns: RegisterColumn<OrganismOption>[] = [
    { key: "scientificName", label: t("organisms.columns.scientificName"), sortable: true },
    { key: "atccNumber", label: t("organisms.columns.atccNumber"), sortable: true, render: (o) => <span style={{ fontFamily: monospaceFontFamily }}>{o.atccNumber ?? t("common.dash")}</span> },
    { key: "commonName", label: t("organisms.columns.commonName"), sortable: true, render: (o) => o.commonName ?? t("common.dash") },
    {
      key: "description",
      label: t("organisms.columns.description"),
      render: (o) => <span style={{ display: "block", maxWidth: 320, whiteSpace: "normal", wordBreak: "break-word" }}>{o.description ?? t("common.dash")}</span>
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
      setMessage({ text: t("organisms.scientificNameRequired"), ok: false });
      return;
    }
    try {
      if (editingId) {
        await update(editingId, scientificName, atccNumber || null, commonName || null, description || null);
        setMessage({ text: t("organisms.updatedMsg", { name: scientificName }), ok: true });
      } else {
        await addNew(scientificName, atccNumber || null, commonName || null, description || null);
        setMessage({ text: t("organisms.addedMsg", { name: scientificName }), ok: true });
      }
      cancelEdit();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setMessage({ text: err?.response?.data?.message ?? (editingId ? t("organisms.updateError") : t("organisms.addError")), ok: false });
    }
  };

  const deleteOrganism = async (o: OrganismOption) => {
    setMessage(null);
    try {
      await remove(o.id);
      setPendingDelete(null);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setPendingDelete(null);
      setMessage({ text: err?.response?.data?.message ?? t("organisms.deleteError"), ok: false });
    }
  };

  return (
    <>
      <LabPage
        title={t("organisms.title")}
        subtitle={t("organisms.subtitle")}
      >
        <Tabs value={tab} onChange={(_, v) => setTab(v)}>
          <Tab value="species" label={t("organisms.tabs.species")} />
          <Tab value="strains" label={t("organisms.tabs.strains")} />
        </Tabs>

        {tab === "strains" && <ReferenceOrganismsSection species={options} />}

        {tab === "species" && message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}

        {tab === "species" && <>
        <div>
          <SectionTitle>{editingId ? t("organisms.editTitle") : t("organisms.addTitle")}</SectionTitle>
          <Paper sx={{ p: 2.5 }}>
            <Stack useFlexGap
              direction="row"
              spacing={1.5}
              sx={{
                flexWrap: "wrap",
                alignItems: "center"
              }}>
              <TextField size="small" label={t("organisms.scientificNameLabel")} placeholder={t("organisms.scientificNamePlaceholder")} value={scientificName} onChange={(e) => setScientificName(e.target.value)} sx={{ minWidth: 240 }} />
              <TextField size="small" label={t("organisms.atccLabel")} placeholder={t("organisms.atccPlaceholder")} value={atccNumber} onChange={(e) => setAtccNumber(e.target.value)} sx={{ minWidth: 160 }} />
              <TextField size="small" label={t("organisms.commonNameLabel")} value={commonName} onChange={(e) => setCommonName(e.target.value)} sx={{ minWidth: 200 }} />
              <TextField size="small" label={t("organisms.descriptionLabel")} value={description} onChange={(e) => setDescription(e.target.value)} sx={{ minWidth: 260, flex: 1 }} />
              {editingId && <Button onClick={cancelEdit}>{t("common.cancel")}</Button>}
              <Button variant="contained" onClick={save}>{editingId ? t("common.saveChanges") : t("organisms.addButton")}</Button>
            </Stack>
          </Paper>
        </div>

        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder={t("organisms.searchPlaceholder")}
          resultCount={visible.length}
        />

        <RegisterTable
          columns={columns}
          rows={visible}
          getRowId={(o) => o.id}
          rowActions={(o) => [
            { label: t("common.edit"), onClick: () => startEdit(o) },
            { label: t("organisms.deleteAction"), danger: true, onClick: () => setPendingDelete(o) }
          ]}
          empty={
            search.trim()
              ? { title: t("organisms.emptyMatchingTitle"), description: t("organisms.emptyMatchingDesc") }
              : { title: t("organisms.emptyTitle"), description: t("organisms.emptyDesc") }
          }
        />
        </>}
      </LabPage>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? t("organisms.deleteConfirm", { name: pendingDelete.scientificName }) : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteOrganism(pendingDelete)}
      />
    </>
  );
}
