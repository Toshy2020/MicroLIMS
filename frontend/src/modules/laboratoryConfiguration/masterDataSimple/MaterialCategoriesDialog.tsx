import { useCallback, useEffect, useState } from "react";
import { Alert, Button, Chip, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { RegisterTable, RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import { MaterialCategory, MaterialCategoryService } from "./services/MaterialCategoryService";
import { MaterialCategoryFormDialog } from "./MaterialCategoryFormDialog";

interface MaterialCategoriesDialogProps {
  open: boolean;
  sections: LaboratorySection[];
  onClose: () => void;
  // Called after a category was created or changed, so the page can refresh its pickers.
  onChanged: () => void;
}

// The categories of the Other Materials group. Everyone can read them; only users with
// MaterialCategories.Manage see the write actions (the server enforces the same).
export function MaterialCategoriesDialog({ open, sections, onClose, onChanged }: MaterialCategoriesDialogProps) {
  const { t } = useTranslation("masterData");
  const canManage = useHasPermission(PERMISSIONS.MATERIAL_CATEGORIES_MANAGE);
  const [categories, setCategories] = useState<MaterialCategory[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<MaterialCategory | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setCategories(await MaterialCategoryService.getAll());
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? t("materialCategories.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  const openForm = (category: MaterialCategory | null) => {
    setEditing(category);
    setFormOpen(true);
  };

  const handleToggleActive = async (category: MaterialCategory) => {
    const next = !category.isActive;
    try {
      await MaterialCategoryService.setActive(category.id, next);
      toast.success(
        next
          ? t("materialCategories.toggledToast_activated", { name: category.name })
          : t("materialCategories.toggledToast_deactivated", { name: category.name })
      );
      await load();
      onChanged();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? t("materialCategories.toggleError"));
    }
  };

  const sectionsLabel = (c: MaterialCategory) =>
    c.sectionIds.length === 0
      ? t("materialCategories.allSections")
      : c.sectionIds.map((id) => sections.find((s) => s.sectionId === id)?.sectionName ?? t("materialCategories.sectionFallback", { id })).join(", ");

  const ruleChip = (label: string, color: "default" | "primary" | "warning" = "primary") => (
    <Chip key={label} label={label} size="small" color={color} variant="outlined" sx={{ fontSize: 12 }} />
  );

  const columns: RegisterColumn<MaterialCategory>[] = [
    {
      key: "codePrefix",
      label: t("materialCategories.columns.prefix"),
      sortable: true,
      render: (c) => <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{c.codePrefix}-</Typography>
    },
    { key: "name", label: t("materialCategories.columns.name"), sortable: true, render: (c) => c.name },
    { key: "defaultUnit", label: t("materialCategories.columns.defaultUnit"), sortable: true, render: (c) => c.defaultUnit },
    { key: "defaultStorageCondition", label: t("materialCategories.columns.defaultStorage"), render: (c) => c.defaultStorageCondition || t("common.dash") },
    {
      key: "rules",
      label: t("materialCategories.columns.rules"),
      render: (c) => (
        <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: "wrap" }}>
          {c.coaRequired && ruleChip(t("materialCategories.rules.coaRequired"), "warning")}
          {c.approvalBeforeUse && ruleChip(t("materialCategories.rules.approvalBeforeUse"), "warning")}
          {c.expiryRequired && ruleChip(t("materialCategories.rules.expiryRequired"))}
          {c.quantityTracked && ruleChip(t("materialCategories.rules.quantityTracked"))}
          {c.labelRequired && ruleChip(t("materialCategories.rules.labelRequired"))}
        </Stack>
      )
    },
    { key: "sections", label: t("materialCategories.columns.sections"), sortValue: sectionsLabel, render: sectionsLabel },
    {
      key: "isActive",
      label: t("materialCategories.columns.status"),
      sortable: true,
      sortValue: (c) => (c.isActive ? 0 : 1),
      render: (c) => <StatusBadge status={c.isActive ? "Active" : "Inactive"} label={c.isActive ? t("common.active") : t("common.inactive")} />
    }
  ];

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => openForm(null)} sx={{ fontWeight: 600, textTransform: "none" }}>
      {t("materialCategories.addCategory")}
    </Button>
  );

  return (
    <>
      <FloatingDialog
        open={open}
        title={t("materialCategories.title")}
        onClose={onClose}
        maxWidth="lg"
        actions={
          <>
            {canManage && addButton}
            <Button onClick={onClose}>{t("common.close")}</Button>
          </>
        }
      >
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            {t("materialCategories.subtitle")}
          </Typography>
          {error && <Alert severity="error">{error}</Alert>}
          <RegisterTable
            columns={columns}
            rows={categories}
            getRowId={(c) => c.id}
            loading={loading}
            onRowClick={canManage ? (c) => openForm(c) : undefined}
            rowActions={
              canManage
                ? (c) => [
                    { label: t("common.edit"), onClick: () => openForm(c) },
                    c.isActive
                      ? { label: t("common.deactivate"), onClick: () => handleToggleActive(c), danger: true }
                      : { label: t("common.activate"), onClick: () => handleToggleActive(c) }
                  ]
                : undefined
            }
            empty={{
              title: t("materialCategories.emptyTitle"),
              description: canManage ? t("materialCategories.emptyDescCanManage") : t("materialCategories.emptyDescReadOnly")
            }}
          />
        </Stack>
      </FloatingDialog>

      <MaterialCategoryFormDialog
        open={open && formOpen}
        category={editing}
        sections={sections}
        onClose={() => setFormOpen(false)}
        onSuccess={(saved) => {
          toast.success(t("materialCategories.savedToast", { name: saved.name }));
          void load();
          onChanged();
        }}
      />
    </>
  );
}
