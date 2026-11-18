import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Typography,
  Button,
  Select,
  MenuItem,
  FormControl,
  FormHelperText,
  InputLabel,
  Chip,
  Stack,
  Tooltip,
  Alert,
  Checkbox,
  ListItemText,
  OutlinedInput,
  TextField
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable, RegisterColumn, FormDialog } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import {
  ChromatographyColumnService,
  ChromatographyColumnDto
} from "./services/ChromatographyColumnService";
import { EquipmentConfigurationService } from "./services/EquipmentConfigurationService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";

export interface ChromatographyEquipmentOption {
  id: number;
  name: string;
  code: string;
  type?: string | number;
  location?: string | null;
  vendor?: string | null;
  cdsSoftware?: string | number | null;
  sectionId?: number | null;
}

// Chromatography instruments a column can be fitted to: liquid (HPLC) and gas (GC).
const CHROMATOGRAPHY_TYPES = ["Hplc", "Gc"] as const;

export function chromatographyTypeLabel(type: ChromatographyEquipmentOption["type"]): string {
  const t = String(type ?? "").toLowerCase();
  if (t === "gc") return "GC";
  if (t === "hplc") return "HPLC";
  return "";
}

export function ChromatographyColumnsPage() {
  const { t } = useTranslation("masterData");
  const { sections, sectionName } = useLaboratorySections();

  const [columns, setColumns] = useState<ChromatographyColumnDto[]>([]);
  const [chromatographyEquipment, setChromatographyEquipment] = useState<ChromatographyEquipmentOption[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingColumn, setEditingColumn] = useState<ChromatographyColumnDto | null>(null);
  const [formCode, setFormCode] = useState("");
  const [formName, setFormName] = useState("");
  const [formSerialNumber, setFormSerialNumber] = useState("");
  const [formUspDesignation, setFormUspDesignation] = useState("");
  const [formSectionId, setFormSectionId] = useState<string | number>("");
  const [formCompatibleEquipmentIds, setFormCompatibleEquipmentIds] = useState<number[]>([]);
  const [formIsActive, setFormIsActive] = useState(true);
  const [dialogError, setDialogError] = useState<string | null>(null);
  // Client-side validation errors, shown on the field instead of the top alert.
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<"code" | "name" | "serial" | "usp" | "section", string>>>({});
  const [saving, setSaving] = useState(false);

  // Deactivate Confirmation Dialog
  const [columnToDeactivate, setColumnToDeactivate] = useState<ChromatographyColumnDto | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [colData, eqData, mySecData] = await Promise.all([
        ChromatographyColumnService.getAll(),
        Promise.all(
          CHROMATOGRAPHY_TYPES.map((t) => EquipmentConfigurationService.getEquipmentList(t).catch(() => []))
        ).then((lists) => lists.flat()),
        getMySections().catch(() => [])
      ]);
      setColumns(Array.isArray(colData) ? colData : []);
      setChromatographyEquipment(Array.isArray(eqData) ? eqData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load chromatography columns data:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? t("chromatographyColumns.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Available HPLC / GC equipment filtered by current dialog section
  const availableEquipmentForSection = useMemo(() => {
    const list = formSectionId
      ? chromatographyEquipment.filter((eq) => eq.sectionId === Number(formSectionId))
      : chromatographyEquipment;
    return [...list].sort((a, b) =>
      chromatographyTypeLabel(a.type).localeCompare(chromatographyTypeLabel(b.type)) || a.code.localeCompare(b.code));
  }, [chromatographyEquipment, formSectionId]);

  // Open Add Dialog
  const handleOpenAdd = () => {
    setEditingColumn(null);
    setFormCode("");
    setFormName("");
    setFormSerialNumber("");
    setFormUspDesignation("");
    const defaultSecId = mySections.length === 1 ? mySections[0].sectionId : "";
    setFormSectionId(defaultSecId);
    setFormCompatibleEquipmentIds([]);
    setFormIsActive(true);
    setDialogError(null);
    setFieldErrors({});
    setDialogOpen(true);
  };

  // Open Edit Dialog
  const handleOpenEdit = (col: ChromatographyColumnDto) => {
    setEditingColumn(col);
    setFormCode(col.code);
    setFormName(col.name);
    setFormSerialNumber(col.serialNumber ?? "");
    setFormUspDesignation(col.uspDesignation ?? "");
    setFormSectionId(col.sectionId ?? "");
    setFormCompatibleEquipmentIds(col.compatibleEquipment?.map((e) => e.id) ?? []);
    setFormIsActive(col.isActive);
    setDialogError(null);
    setFieldErrors({});
    setDialogOpen(true);
  };

  // Save Column
  const handleSave = async () => {
    const trimmedCode = formCode.trim().toUpperCase();
    const trimmedName = formName.trim();
    const trimmedSerial = formSerialNumber.trim();
    const trimmedUsp = formUspDesignation.trim();

    const errors: typeof fieldErrors = {};
    if (!trimmedCode) errors.code = t("chromatographyColumns.form.codeRequired");
    if (!trimmedName) errors.name = t("chromatographyColumns.form.nameRequired");
    if (trimmedSerial.length > 100) errors.serial = t("chromatographyColumns.form.serialMax");
    if (trimmedUsp.length > 10) errors.usp = t("chromatographyColumns.form.uspMax");
    if (!editingColumn && mySections.length > 1 && !formSectionId) errors.section = t("chromatographyColumns.form.sectionRequired");
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSaving(true);
    setDialogError(null);
    try {
      if (editingColumn) {
        await ChromatographyColumnService.update(editingColumn.id, {
          code: trimmedCode,
          name: trimmedName,
          serialNumber: trimmedSerial || null,
          uspDesignation: trimmedUsp || null,
          isActive: formIsActive,
          sectionId: formSectionId ? Number(formSectionId) : null,
          compatibleEquipmentIds: formCompatibleEquipmentIds
        }, editingColumn.version);
        toast.success(t("chromatographyColumns.updatedToast", { code: trimmedCode }));
      } else {
        await ChromatographyColumnService.create({
          code: trimmedCode,
          name: trimmedName,
          serialNumber: trimmedSerial || null,
          uspDesignation: trimmedUsp || null,
          sectionId: formSectionId ? Number(formSectionId) : null,
          compatibleEquipmentIds: formCompatibleEquipmentIds
        });
        toast.success(t("chromatographyColumns.createdToast", { code: trimmedCode }));
      }
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = errorObj.response?.data?.message ?? errorObj.message ?? t("chromatographyColumns.saveError");
      setDialogError(msg);
    } finally {
      setSaving(false);
    }
  };

  // Confirm Deactivate
  const handleConfirmDeactivate = async () => {
    if (!columnToDeactivate) return;
    setDeactivating(true);
    try {
      await ChromatographyColumnService.deactivate(columnToDeactivate.id);
      toast.success(t("chromatographyColumns.deactivatedToast", { code: columnToDeactivate.code }));
      setColumnToDeactivate(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? t("chromatographyColumns.deactivateError"));
    } finally {
      setDeactivating(false);
    }
  };

  // Filtered list
  const filteredColumns = useMemo(() => {
    return columns.filter((col) => {
      // Search
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const codeMatch = col.code.toLowerCase().includes(q);
        const nameMatch = col.name.toLowerCase().includes(q);
        const serialMatch = col.serialNumber ? col.serialNumber.toLowerCase().includes(q) : false;
        const uspMatch = col.uspDesignation ? col.uspDesignation.toLowerCase().includes(q) : false;
        if (!codeMatch && !nameMatch && !serialMatch && !uspMatch) return false;
      }

      // Status
      if (statusFilter === "ACTIVE" && !col.isActive) return false;
      if (statusFilter === "INACTIVE" && col.isActive) return false;

      // Section
      if (sectionFilter !== "ALL" && String(col.sectionId) !== sectionFilter) {
        return false;
      }

      return true;
    });
  }, [columns, searchQuery, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (sectionId: number, colSection?: ChromatographyColumnDto["section"]) => {
    const fromHook = sectionName(sectionId);
    if (fromHook) return fromHook;
    if (colSection?.name) return colSection.name;
    if (colSection?.sectionName) return colSection.sectionName;
    const foundSec = sections.find((s) => s.sectionId === sectionId);
    if (foundSec?.sectionName) return foundSec.sectionName;
    return t("chromatographyColumns.sectionFallback", { id: sectionId });
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      {t("chromatographyColumns.addColumn")}
    </Button>
  );

  const tableColumns: RegisterColumn<ChromatographyColumnDto>[] = [
    {
      key: "code",
      label: t("chromatographyColumns.columns.code"),
      sortable: true,
      render: (col) => (
        <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{col.code}</Typography>
      )
    },
    { key: "name", label: t("chromatographyColumns.columns.name"), sortable: true },
    {
      key: "uspDesignation",
      label: t("chromatographyColumns.columns.usp"),
      sortable: true,
      render: (col) =>
        col.uspDesignation ? (
          <Chip label={col.uspDesignation} size="small" variant="outlined" sx={{ fontWeight: 600, fontSize: 12 }} />
        ) : (
          <Typography variant="body2" color="text.secondary">{t("common.dash")}</Typography>
        )
    },
    {
      key: "serialNumber",
      label: t("chromatographyColumns.columns.serialNumber"),
      sortable: true,
      render: (col) => col.serialNumber || <Typography variant="body2" color="text.secondary">{t("common.dash")}</Typography>
    },
    {
      key: "section",
      label: t("chromatographyColumns.columns.section"),
      sortable: true,
      sortValue: (col) => resolveSectionDisplay(col.sectionId, col.section),
      render: (col) => <Chip label={resolveSectionDisplay(col.sectionId, col.section)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "compatibleEquipment",
      label: t("chromatographyColumns.columns.compatibleEquipment"),
      render: (col) =>
        col.compatibleEquipment && col.compatibleEquipment.length > 0 ? (
          <Stack useFlexGap direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }}>
            {col.compatibleEquipment.map((eq) => (
              <Tooltip key={eq.id} title={`${eq.code}: ${eq.name}`}>
                <Chip label={eq.code} size="small" color="primary" variant="outlined" sx={{ fontSize: 12, height: 22 }} />
              </Tooltip>
            ))}
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary" sx={{ fontStyle: "italic" }}>{t("chromatographyColumns.noneLinked")}</Typography>
        )
    },
    {
      key: "isActive",
      label: t("chromatographyColumns.columns.status"),
      sortable: true,
      sortValue: (col) => (col.isActive ? 0 : 1),
      render: (col) => <StatusBadge status={col.isActive ? "Active" : "Inactive"} label={col.isActive ? t("common.active") : t("common.inactive")} />
    }
  ];

  const filtersActive = Boolean(searchQuery.trim()) || statusFilter !== "ALL" || sectionFilter !== "ALL";

  return (
    <LabPage
      title={t("chromatographyColumns.title")}
      subtitle={t("chromatographyColumns.subtitle")}
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder={t("chromatographyColumns.searchPlaceholder")}
          resultCount={filteredColumns.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="column-status-filter-label">{t("common.status")}</InputLabel>
            <Select
              labelId="column-status-filter-label"
              label={t("common.status")}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
            >
              <MenuItem value="ALL">{t("common.allStatuses")}</MenuItem>
              <MenuItem value="ACTIVE">{t("common.activeOnly")}</MenuItem>
              <MenuItem value="INACTIVE">{t("common.inactiveOnly")}</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="column-section-filter-label">{t("common.section")}</InputLabel>
            <Select
              labelId="column-section-filter-label"
              label={t("common.section")}
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">{t("common.allSections")}</MenuItem>
              {sections.map((sec) => (
                <MenuItem key={sec.sectionId} value={String(sec.sectionId)}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </FilterBar>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}

      <RegisterTable
        columns={tableColumns}
        rows={filteredColumns}
        getRowId={(col) => col.id}
        loading={loading}
        onRowClick={handleOpenEdit}
        rowActions={(col) => [
          { label: t("common.edit"), onClick: () => handleOpenEdit(col) },
          { label: t("common.deactivate"), onClick: () => setColumnToDeactivate(col), disabled: !col.isActive, danger: true }
        ]}
        empty={
          filtersActive
            ? { title: t("chromatographyColumns.emptyFilteredTitle"), description: t("chromatographyColumns.emptyFilteredDesc") }
            : { title: t("chromatographyColumns.emptyTitle"), description: t("chromatographyColumns.emptyDesc"), action: addButton }
        }
      />

      {/* Create / Edit Floating Dialog */}
      <FormDialog
        open={dialogOpen}
        title={editingColumn ? t("chromatographyColumns.editDialogTitle", { code: editingColumn.code }) : t("chromatographyColumns.addDialogTitle")}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleSave}
        submitLabel={saving ? t("chromatographyColumns.saving") : editingColumn ? t("common.saveChanges") : t("chromatographyColumns.createColumn")}
        submitting={saving}
        error={dialogError}
      >
          <TextField
            label={t("chromatographyColumns.form.codeLabel")}
            value={formCode}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, code: undefined })); setFormCode(e.target.value.toUpperCase()); }}
            required
            fullWidth
            size="small"
            placeholder={t("chromatographyColumns.form.codePlaceholder")}
            error={!!fieldErrors.code}
            helperText={fieldErrors.code ?? t("chromatographyColumns.form.codeHelper")}
          />

          <TextField
            label={t("chromatographyColumns.form.nameLabel")}
            value={formName}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, name: undefined })); setFormName(e.target.value); }}
            required
            fullWidth
            size="small"
            placeholder={t("chromatographyColumns.form.namePlaceholder")}
            error={!!fieldErrors.name}
            helperText={fieldErrors.name ?? t("chromatographyColumns.form.nameHelper")}
          />

          <TextField
            label={t("chromatographyColumns.form.serialLabel")}
            value={formSerialNumber}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, serial: undefined })); setFormSerialNumber(e.target.value); }}
            fullWidth
            size="small"
            placeholder={t("chromatographyColumns.form.serialPlaceholder")}
            error={!!fieldErrors.serial}
            helperText={fieldErrors.serial ?? t("chromatographyColumns.form.serialHelper")}
          />

          <TextField
            label={t("chromatographyColumns.form.uspLabel")}
            value={formUspDesignation}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, usp: undefined })); setFormUspDesignation(e.target.value.slice(0, 10)); }}
            fullWidth
            size="small"
            placeholder={t("chromatographyColumns.form.uspPlaceholder")}
            slotProps={{ htmlInput: { maxLength: 10 } }}
            error={!!fieldErrors.usp}
            helperText={fieldErrors.usp ?? t("chromatographyColumns.form.uspHelper")}
          />

          {/* Laboratory Section Select */}
          {(!editingColumn && mySections.length > 1) || (editingColumn && sections.length > 0) ? (
            <FormControl fullWidth size="small" required={!editingColumn && mySections.length > 1} error={!!fieldErrors.section}>
              <InputLabel id="column-form-section-label">{t("chromatographyColumns.form.sectionLabel")}</InputLabel>
              <Select
                labelId="column-form-section-label"
                label={t("chromatographyColumns.form.sectionLabel")}
                value={formSectionId}
                onChange={(e) => {
                  const newSecId = e.target.value;
                  setFieldErrors((fe) => ({ ...fe, section: undefined }));
                  setFormSectionId(newSecId);
                  // Reset compatible equipment if they don't belong to the newly selected section
                  if (newSecId) {
                    const secIdNum = Number(newSecId);
                    setFormCompatibleEquipmentIds((prev) =>
                      prev.filter((id) => {
                        const eq = chromatographyEquipment.find((e) => e.id === id);
                        return eq && eq.sectionId === secIdNum;
                      })
                    );
                  }
                }}
              >
                {(!editingColumn && mySections.length > 1 ? mySections : sections).map((sec) => (
                  <MenuItem key={sec.sectionId} value={sec.sectionId}>
                    {sec.sectionName}
                  </MenuItem>
                ))}
              </Select>
              {fieldErrors.section && <FormHelperText>{fieldErrors.section}</FormHelperText>}
            </FormControl>
          ) : null}

          {/* Compatible HPLC / GC Instruments Multi-Select */}
          <FormControl fullWidth size="small">
            <InputLabel id="compatible-equipment-select-label">{t("chromatographyColumns.form.compatibleLabel")}</InputLabel>
            <Select
              labelId="compatible-equipment-select-label"
              multiple
              value={formCompatibleEquipmentIds}
              onChange={(e) => {
                const val = e.target.value;
                setFormCompatibleEquipmentIds(typeof val === "string" ? val.split(",").map(Number) : (val as number[]));
              }}
              input={<OutlinedInput label={t("chromatographyColumns.form.compatibleLabel")} />}
              renderValue={(selectedIds) => (
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {selectedIds.map((id) => {
                    const eq = chromatographyEquipment.find((item) => item.id === id);
                    return (
                      <Chip
                        key={id}
                        label={eq ? `${eq.code} (${eq.name})` : `#${id}`}
                        size="small"
                        onMouseDown={(e) => e.stopPropagation()}
                        onDelete={() => {
                          setFormCompatibleEquipmentIds((prev) => prev.filter((item) => item !== id));
                        }}
                      />
                    );
                  })}
                </Box>
              )}
            >
              {availableEquipmentForSection.length === 0 ? (
                <MenuItem disabled value="">
                  <em>{t("chromatographyColumns.form.noInstrumentsInSection")}</em>
                </MenuItem>
              ) : (
                availableEquipmentForSection.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    <Checkbox checked={formCompatibleEquipmentIds.includes(eq.id)} size="small" />
                    <ListItemText
                      primary={`${chromatographyTypeLabel(eq.type) ? `[${chromatographyTypeLabel(eq.type)}] ` : ""}${eq.code} - ${eq.name}`}
                      secondary={eq.location ? t("chromatographyColumns.form.locationSecondary", { location: eq.location }) : undefined}
                    />
                  </MenuItem>
                ))
              )}
            </Select>
          </FormControl>

          {editingColumn && (
            <FormControl size="small">
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Checkbox
                  checked={formIsActive}
                  onChange={(e) => setFormIsActive(e.target.checked)}
                  id="column-is-active-check"
                />
                <Typography
                  component="label"
                  htmlFor="column-is-active-check"
                  variant="body2"
                  sx={{ cursor: "pointer", fontWeight: 500 }}
                >
                  {t("chromatographyColumns.form.isActiveLabel")}
                </Typography>
              </Stack>
            </FormControl>
          )}
      </FormDialog>

      {/* Deactivate Confirmation Dialog */}
      <ConfirmationDialog
        open={Boolean(columnToDeactivate)}
        title={t("chromatographyColumns.deactivateTitle")}
        message={t("chromatographyColumns.deactivateMessage", {
          code: columnToDeactivate?.code ?? "",
          name: columnToDeactivate?.name ?? ""
        })}
        destructive
        confirmText={deactivating ? t("common.deactivating") : t("common.deactivate")}
        onConfirm={handleConfirmDeactivate}
        onCancel={() => setColumnToDeactivate(null)}
      />
    </LabPage>
  );
}
