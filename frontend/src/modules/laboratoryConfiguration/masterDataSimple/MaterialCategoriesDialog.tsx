import { useCallback, useEffect, useState } from "react";
import { Alert, Button, Chip, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
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
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not load material categories.");
    } finally {
      setLoading(false);
    }
  }, []);

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
      toast.success(`Category "${category.name}" ${next ? "activated" : "deactivated"}.`);
      await load();
      onChanged();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? "Could not update category status.");
    }
  };

  const sectionsLabel = (c: MaterialCategory) =>
    c.sectionIds.length === 0
      ? "All sections"
      : c.sectionIds.map((id) => sections.find((s) => s.sectionId === id)?.sectionName ?? `Section #${id}`).join(", ");

  const ruleChip = (label: string, color: "default" | "primary" | "warning" = "primary") => (
    <Chip key={label} label={label} size="small" color={color} variant="outlined" sx={{ fontSize: 12 }} />
  );

  const columns: RegisterColumn<MaterialCategory>[] = [
    {
      key: "codePrefix",
      label: "Prefix",
      sortable: true,
      render: (c) => <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{c.codePrefix}-</Typography>
    },
    { key: "name", label: "Name", sortable: true, render: (c) => c.name },
    { key: "defaultUnit", label: "Default unit", sortable: true, render: (c) => c.defaultUnit },
    { key: "defaultStorageCondition", label: "Default storage", render: (c) => c.defaultStorageCondition || "—" },
    {
      key: "rules",
      label: "Rules",
      render: (c) => (
        <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: "wrap" }}>
          {c.coaRequired && ruleChip("COA required", "warning")}
          {c.approvalBeforeUse && ruleChip("Approval before use", "warning")}
          {c.expiryRequired && ruleChip("Expiry required")}
          {c.quantityTracked && ruleChip("Quantity tracked")}
          {c.labelRequired && ruleChip("Label required")}
        </Stack>
      )
    },
    { key: "sections", label: "Sections", sortValue: sectionsLabel, render: sectionsLabel },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (c) => (c.isActive ? 0 : 1),
      render: (c) => <StatusBadge status={c.isActive ? "Active" : "Inactive"} />
    }
  ];

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => openForm(null)} sx={{ fontWeight: 600, textTransform: "none" }}>
      Add Category
    </Button>
  );

  return (
    <>
      <FloatingDialog
        open={open}
        title="Material Categories"
        onClose={onClose}
        maxWidth="lg"
        actions={
          <>
            {canManage && addButton}
            <Button onClick={onClose}>Close</Button>
          </>
        }
      >
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            Categories of Other Materials carry the rules applied to their master entries and stock lots. A Section Head
            creates or changes one with a reason and an electronic signature; changes apply to lots received afterwards.
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
                    { label: "Edit", onClick: () => openForm(c) },
                    c.isActive
                      ? { label: "Deactivate", onClick: () => handleToggleActive(c), danger: true }
                      : { label: "Activate", onClick: () => handleToggleActive(c) }
                  ]
                : undefined
            }
            empty={{
              title: "No material categories",
              description: canManage ? "Add the first category of Other Materials." : "A Section Head adds categories of Other Materials."
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
          toast.success(`Material category "${saved.name}" saved.`);
          void load();
          onChanged();
        }}
      />
    </>
  );
}
