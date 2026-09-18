import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  Chip,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Alert,
  Checkbox,
  ListItemText,
  OutlinedInput,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import RefreshIcon from "@mui/icons-material/Refresh";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import { PageHeader } from "../../../components/PageHeader";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../theme";
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
  const theme = useTheme();
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
  const [formSectionId, setFormSectionId] = useState<string | number>("");
  const [formCompatibleEquipmentIds, setFormCompatibleEquipmentIds] = useState<number[]>([]);
  const [formIsActive, setFormIsActive] = useState(true);
  const [dialogError, setDialogError] = useState<string | null>(null);
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
    const defaultSecId = mySections.length === 1 ? mySections[0].sectionId : "";
    setFormSectionId(defaultSecId);
    setFormCompatibleEquipmentIds([]);
    setFormIsActive(true);
    setDialogError(null);
    setDialogOpen(true);
  };

  // Open Edit Dialog
  const handleOpenEdit = (col: ChromatographyColumnDto) => {
    setEditingColumn(col);
    setFormCode(col.code);
    setFormName(col.name);
    setFormSerialNumber(col.serialNumber ?? "");
    setFormSectionId(col.sectionId ?? "");
    setFormCompatibleEquipmentIds(col.compatibleEquipment?.map((e) => e.id) ?? []);
    setFormIsActive(col.isActive);
    setDialogError(null);
    setDialogOpen(true);
  };

  // Save Column
  const handleSave = async () => {
    const trimmedCode = formCode.trim().toUpperCase();
    const trimmedName = formName.trim();
    const trimmedSerial = formSerialNumber.trim();

    if (!trimmedCode) {
      setDialogError("Column Code is required.");
      return;
    }
    if (!trimmedName) {
      setDialogError("Column Name is required.");
      return;
    }
    if (trimmedSerial.length > 100) {
      setDialogError("Serial number cannot exceed 100 characters.");
      return;
    }
    if (!editingColumn && mySections.length > 1 && !formSectionId) {
      setDialogError("Laboratory Section is required.");
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      if (editingColumn) {
        await ChromatographyColumnService.update(editingColumn.id, {
          code: trimmedCode,
          name: trimmedName,
          serialNumber: trimmedSerial || null,
          isActive: formIsActive,
          sectionId: formSectionId ? Number(formSectionId) : null,
          compatibleEquipmentIds: formCompatibleEquipmentIds
        });
        toast.success(`Chromatography column "${trimmedCode}" updated.`);
      } else {
        await ChromatographyColumnService.create({
          code: trimmedCode,
          name: trimmedName,
          serialNumber: trimmedSerial || null,
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
        if (!codeMatch && !nameMatch && !serialMatch) return false;
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

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="Chromatography Columns"
        subtitle="Manage HPLC chromatography column master records, column parameters, and instrument compatibility."
      />

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      {/* Filter & Action Toolbar */}
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: { sm: "center" },
            justifyContent: "space-between",
            flexWrap: "wrap"
          }}
        >
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ flexWrap: "wrap", flex: 1 }}>
            <TextField
              size="small"
              placeholder="Search code, name, serial..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 260 }}
            />

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

            <Tooltip title="Refresh">
              <IconButton onClick={loadData} size="small" sx={{ alignSelf: "center" }}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          </Stack>

          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ fontWeight: 600, textTransform: "none", height: 40 }}
          >
            Add Column
          </Button>
        </Stack>
      </Paper>

      {/* Columns Table */}
      <TableContainer
        component={Paper}
        elevation={0}
        sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}
      >
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell sx={{ fontWeight: 600 }}>Code</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Column Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Serial Number</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Compatible HPLC Instruments</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
                    Loading chromatography columns...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredColumns.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                  <ViewColumnIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="body1" sx={{ color: "text.secondary", fontWeight: 500 }}>
                    No chromatography columns found
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.5 }}>
                    {searchQuery || statusFilter !== "ALL" || sectionFilter !== "ALL"
                      ? "Try adjusting your search or filters."
                      : "Click 'Add Column' to register your first chromatography column."}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredColumns.map((col) => {
                const isDeactivatable = col.isActive;
                return (
                  <TableRow key={col.id} hover>
                    <TableCell sx={{ fontFamily: "monospace", fontWeight: 600, fontSize: "0.875rem" }}>
                      {col.code}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 500 }}>{col.name}</TableCell>
                    <TableCell>{col.serialNumber || <Typography variant="body2" color="text.secondary">—</Typography>}</TableCell>
                    <TableCell>
                      <Chip
                        label={resolveSectionDisplay(col.sectionId, col.section)}
                        size="small"
                        variant="outlined"
                        sx={{ fontSize: 12 }}
                      />
                    </TableCell>
                    <TableCell>
                      {col.compatibleEquipment && col.compatibleEquipment.length > 0 ? (
                        <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                          {col.compatibleEquipment.map((eq) => (
                            <Tooltip key={eq.id} title={`${eq.code}: ${eq.name}`}>
                              <Chip
                                label={eq.code}
                                size="small"
                                color="primary"
                                variant="outlined"
                                sx={{ fontSize: 11, height: 22 }}
                              />
                            </Tooltip>
                          ))}
                        </Stack>
                      ) : (
                        <Typography variant="body2" color="text.secondary" sx={{ fontStyle: "italic" }}>
                          None linked
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={col.isActive ? "Active" : "Inactive"}
                        size="small"
                        color={col.isActive ? "success" : "default"}
                        sx={{ fontSize: 11, fontWeight: 600 }}
                      />
                    </TableCell>
                    <TableCell align="right">
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                        <Tooltip title="Edit Column">
                          <IconButton size="small" onClick={() => handleOpenEdit(col)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title={isDeactivatable ? "Deactivate Column" : "Column is already inactive"}>
                          <span>
                            <IconButton
                              size="small"
                              color="error"
                              disabled={!isDeactivatable}
                              onClick={() => setColumnToDeactivate(col)}
                            >
                              <BlockIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </Stack>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create / Edit Floating Dialog */}
      <FloatingDialog
        open={dialogOpen}
        title={editingColumn ? `Edit Column: ${editingColumn.code}` : "Add Chromatography Column"}
        onClose={() => setDialogOpen(false)}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} disabled={saving} sx={{ textTransform: "none" }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleSave}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingColumn ? "Save Changes" : "Create Column"}
            </Button>
          </>
        }
      >
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {dialogError && (
            <Alert severity="error" onClose={() => setDialogError(null)}>
              {dialogError}
            </Alert>
          )}

          <TextField
            label="Column Code"
            value={formCode}
            onChange={(e) => setFormCode(e.target.value.toUpperCase())}
            required
            fullWidth
            size="small"
            placeholder="e.g. COL-C18-01"
            helperText="Unique alphanumeric identifier (e.g. COL-C18-01)"
          />

          <TextField
            label="Column Name"
            value={formName}
            onChange={(e) => setFormName(e.target.value)}
            required
            fullWidth
            size="small"
            placeholder="e.g. Hypersil BDS C18 5um 4.6x250mm"
            helperText="Full descriptive name including stationary phase and dimensions"
          />

          <TextField
            label="Serial Number"
            value={formSerialNumber}
            onChange={(e) => setFormSerialNumber(e.target.value)}
            fullWidth
            size="small"
            placeholder="e.g. SN-09823481"
            helperText="Manufacturer serial number (max 100 characters)"
          />

          {/* Laboratory Section Select */}
          {(!editingColumn && mySections.length > 1) || (editingColumn && sections.length > 0) ? (
            <FormControl fullWidth size="small" required={!editingColumn && mySections.length > 1}>
              <InputLabel id="column-form-section-label">Laboratory Section</InputLabel>
              <Select
                labelId="column-form-section-label"
                label="Laboratory Section"
                value={formSectionId}
                onChange={(e) => {
                  const newSecId = e.target.value;
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
        </Stack>
      </FloatingDialog>

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
    </Box>
  );
}
