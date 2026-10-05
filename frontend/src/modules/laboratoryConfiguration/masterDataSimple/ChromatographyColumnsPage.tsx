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

export function ChromatographyColumnsPage() {
  const { sections, sectionName } = useLaboratorySections();

  const [columns, setColumns] = useState<ChromatographyColumnDto[]>([]);
  const [hplcEquipment, setHplcEquipment] = useState<ChromatographyEquipmentOption[]>([]);
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
        EquipmentConfigurationService.getEquipmentList("Hplc").catch(() => []),
        getMySections().catch(() => [])
      ]);
      setColumns(Array.isArray(colData) ? colData : []);
      setHplcEquipment(Array.isArray(eqData) ? eqData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load chromatography columns data:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not load chromatography columns.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Available HPLC equipment filtered by current dialog section
  const availableHplcForSection = useMemo(() => {
    if (!formSectionId) return hplcEquipment;
    const secIdNum = Number(formSectionId);
    return hplcEquipment.filter((eq) => eq.sectionId === secIdNum);
  }, [hplcEquipment, formSectionId]);

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
    if (!trimmedCode) errors.code = "Column Code is required.";
    if (!trimmedName) errors.name = "Column Name is required.";
    if (trimmedSerial.length > 100) errors.serial = "Serial number cannot exceed 100 characters.";
    if (trimmedUsp.length > 10) errors.usp = "USP designation cannot exceed 10 characters.";
    if (!editingColumn && mySections.length > 1 && !formSectionId) errors.section = "Laboratory Section is required.";
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
        toast.success(`Chromatography column "${trimmedCode}" updated.`);
      } else {
        await ChromatographyColumnService.create({
          code: trimmedCode,
          name: trimmedName,
          serialNumber: trimmedSerial || null,
          uspDesignation: trimmedUsp || null,
          sectionId: formSectionId ? Number(formSectionId) : null,
          compatibleEquipmentIds: formCompatibleEquipmentIds
        });
        toast.success(`Chromatography column "${trimmedCode}" created.`);
      }
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = errorObj.response?.data?.message ?? errorObj.message ?? "Could not save chromatography column.";
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
      toast.success(`Column "${columnToDeactivate.code}" deactivated.`);
      setColumnToDeactivate(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? "Could not deactivate column.");
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
    return `Section #${sectionId}`;
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      Add Column
    </Button>
  );

  const tableColumns: RegisterColumn<ChromatographyColumnDto>[] = [
    {
      key: "code",
      label: "Code",
      sortable: true,
      render: (col) => (
        <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{col.code}</Typography>
      )
    },
    { key: "name", label: "Column Name", sortable: true },
    {
      key: "uspDesignation",
      label: "USP",
      sortable: true,
      render: (col) =>
        col.uspDesignation ? (
          <Chip label={col.uspDesignation} size="small" variant="outlined" sx={{ fontWeight: 600, fontSize: 12 }} />
        ) : (
          <Typography variant="body2" color="text.secondary">—</Typography>
        )
    },
    {
      key: "serialNumber",
      label: "Serial Number",
      sortable: true,
      render: (col) => col.serialNumber || <Typography variant="body2" color="text.secondary">—</Typography>
    },
    {
      key: "section",
      label: "Section",
      sortable: true,
      sortValue: (col) => resolveSectionDisplay(col.sectionId, col.section),
      render: (col) => <Chip label={resolveSectionDisplay(col.sectionId, col.section)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "compatibleEquipment",
      label: "Compatible HPLC Instruments",
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
          <Typography variant="body2" color="text.secondary" sx={{ fontStyle: "italic" }}>None linked</Typography>
        )
    },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (col) => (col.isActive ? 0 : 1),
      render: (col) => <StatusBadge status={col.isActive ? "Active" : "Inactive"} />
    }
  ];

  const filtersActive = Boolean(searchQuery.trim()) || statusFilter !== "ALL" || sectionFilter !== "ALL";

  return (
    <LabPage
      title="Chromatography Columns"
      subtitle="Manage HPLC chromatography column master records, column parameters, and instrument compatibility."
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder="Search code, name, serial, USP..."
          resultCount={filteredColumns.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="column-status-filter-label">Status</InputLabel>
            <Select
              labelId="column-status-filter-label"
              label="Status"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
            >
              <MenuItem value="ALL">All Statuses</MenuItem>
              <MenuItem value="ACTIVE">Active Only</MenuItem>
              <MenuItem value="INACTIVE">Inactive Only</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="column-section-filter-label">Section</InputLabel>
            <Select
              labelId="column-section-filter-label"
              label="Section"
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">All Sections</MenuItem>
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
          { label: "Edit", onClick: () => handleOpenEdit(col) },
          { label: "Deactivate", onClick: () => setColumnToDeactivate(col), disabled: !col.isActive, danger: true }
        ]}
        empty={
          filtersActive
            ? { title: "No chromatography columns found", description: "Try adjusting your search or filters." }
            : { title: "No chromatography columns found", description: "Register your first chromatography column.", action: addButton }
        }
      />

      {/* Create / Edit Floating Dialog */}
      <FormDialog
        open={dialogOpen}
        title={editingColumn ? `Edit Column: ${editingColumn.code}` : "Add Chromatography Column"}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleSave}
        submitLabel={saving ? "Saving..." : editingColumn ? "Save Changes" : "Create Column"}
        submitting={saving}
        error={dialogError}
      >
          <TextField
            label="Column Code"
            value={formCode}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, code: undefined })); setFormCode(e.target.value.toUpperCase()); }}
            required
            fullWidth
            size="small"
            placeholder="e.g. COL-C18-01"
            error={!!fieldErrors.code}
            helperText={fieldErrors.code ?? "Unique alphanumeric identifier (e.g. COL-C18-01)"}
          />

          <TextField
            label="Column Name"
            value={formName}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, name: undefined })); setFormName(e.target.value); }}
            required
            fullWidth
            size="small"
            placeholder="e.g. Hypersil BDS C18 5um 4.6x250mm"
            error={!!fieldErrors.name}
            helperText={fieldErrors.name ?? "Full descriptive name including stationary phase and dimensions"}
          />

          <TextField
            label="Serial Number"
            value={formSerialNumber}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, serial: undefined })); setFormSerialNumber(e.target.value); }}
            fullWidth
            size="small"
            placeholder="e.g. SN-09823481"
            error={!!fieldErrors.serial}
            helperText={fieldErrors.serial ?? "Manufacturer serial number (max 100 characters)"}
          />

          <TextField
            label="USP Designation"
            value={formUspDesignation}
            onChange={(e) => { setFieldErrors((fe) => ({ ...fe, usp: undefined })); setFormUspDesignation(e.target.value.slice(0, 10)); }}
            fullWidth
            size="small"
            placeholder="L1"
            slotProps={{ htmlInput: { maxLength: 10 } }}
            error={!!fieldErrors.usp}
            helperText={fieldErrors.usp ?? "USP packing code (e.g. L1 for C18, L7 for C8, L11 for Phenyl)"}
          />

          {/* Laboratory Section Select */}
          {(!editingColumn && mySections.length > 1) || (editingColumn && sections.length > 0) ? (
            <FormControl fullWidth size="small" required={!editingColumn && mySections.length > 1} error={!!fieldErrors.section}>
              <InputLabel id="column-form-section-label">Laboratory Section</InputLabel>
              <Select
                labelId="column-form-section-label"
                label="Laboratory Section"
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
                        const eq = hplcEquipment.find((e) => e.id === id);
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

          {/* Compatible HPLC Instruments Multi-Select */}
          <FormControl fullWidth size="small">
            <InputLabel id="compatible-equipment-select-label">Compatible HPLC Instruments</InputLabel>
            <Select
              labelId="compatible-equipment-select-label"
              multiple
              value={formCompatibleEquipmentIds}
              onChange={(e) => {
                const val = e.target.value;
                setFormCompatibleEquipmentIds(typeof val === "string" ? val.split(",").map(Number) : (val as number[]));
              }}
              input={<OutlinedInput label="Compatible HPLC Instruments" />}
              renderValue={(selectedIds) => (
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {selectedIds.map((id) => {
                    const eq = hplcEquipment.find((item) => item.id === id);
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
              {availableHplcForSection.length === 0 ? (
                <MenuItem disabled value="">
                  <em>No HPLC instruments found in this section</em>
                </MenuItem>
              ) : (
                availableHplcForSection.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    <Checkbox checked={formCompatibleEquipmentIds.includes(eq.id)} size="small" />
                    <ListItemText
                      primary={`${eq.code} - ${eq.name}`}
                      secondary={eq.location ? `Location: ${eq.location}` : undefined}
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
                  Column is Active
                </Typography>
              </Stack>
            </FormControl>
          )}
      </FormDialog>

      {/* Deactivate Confirmation Dialog */}
      <ConfirmationDialog
        open={Boolean(columnToDeactivate)}
        title="Deactivate Chromatography Column"
        message={`Are you sure you want to deactivate chromatography column "${columnToDeactivate?.code} - ${columnToDeactivate?.name}"?`}
        destructive
        confirmText={deactivating ? "Deactivating..." : "Deactivate"}
        onConfirm={handleConfirmDeactivate}
        onCancel={() => setColumnToDeactivate(null)}
      />
    </LabPage>
  );
}
