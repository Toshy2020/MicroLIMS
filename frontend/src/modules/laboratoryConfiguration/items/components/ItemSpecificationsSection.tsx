import React, { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TextField,
  Select,
  MenuItem,
  FormControl,
  Button,
  IconButton,
  Tooltip,
  Alert,
  CircularProgress,
  Stack,
  useTheme
} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { Item } from "../services/ItemService";
import { SpecificationService, SpecificationDto } from "../../specifications/services/SpecificationService";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { brandColors } from "../../../../theme";

interface ItemSpecificationsSectionProps {
  item: Item;
  onSpecsChanged?: () => void;
}

export const ItemSpecificationsSection: React.FC<ItemSpecificationsSectionProps> = ({
  item,
  onSpecsChanged
}) => {
  const theme = useTheme();

  const [specs, setSpecs] = useState<SpecificationDto[]>(item.specifications ?? []);
  const [loading, setLoading] = useState(false);

  // Add row state
  const [addRow, setAddRow] = useState({
    testCode: "",
    alertLimit: "",
    actionLimit: "",
    specLimit: "",
    unit: ""
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Edit state per spec row
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editRow, setEditRow] = useState<Partial<SpecificationDto>>({});

  // Delete state
  const [pendingDelete, setPendingDelete] = useState<{ id: number; testCode: string } | null>(null);

  const loadSpecs = async () => {
    if (!item?.id) return;
    setLoading(true);
    try {
      const data = await SpecificationService.getForItem(item.id);
      if (Array.isArray(data)) {
        setSpecs(data);
      }
    } catch {
      // Fallback to item.specifications if direct fetch fails
      setSpecs(item.specifications ?? []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setSpecs(item.specifications ?? []);
    loadSpecs();
    setEditingId(null);
    setEditRow({});
    setError(null);
    setAddRow({ testCode: "", alertLimit: "", actionLimit: "", specLimit: "", unit: "" });
  }, [item.id]);

  const assignedTests = item.assignedTests ?? [];
  const assignableTestCodes = assignedTests.map((t) => t.testCode);

  // Available tests to add (excluding tests already configured)
  const availableToAdd = assignableTestCodes.filter(
    (code) => !specs.some((s) => s.testCode === code)
  );

  // === ADD spec ===
  const handleAdd = async () => {
    if (!addRow.testCode || !addRow.specLimit.trim()) {
      setError("Test code and specification limit are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await SpecificationService.create(
        item.id,
        addRow.testCode,
        addRow.alertLimit ? addRow.alertLimit.trim() : "",
        addRow.actionLimit ? addRow.actionLimit.trim() : "",
        addRow.specLimit.trim(),
        addRow.unit ? addRow.unit.trim() : ""
      );
      setAddRow({ testCode: "", alertLimit: "", actionLimit: "", specLimit: "", unit: "" });
      await loadSpecs();
      onSpecsChanged?.();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to add specification.");
    } finally {
      setSaving(false);
    }
  };

  // === EDIT spec ===
  const handleEditSave = async (spec: SpecificationDto) => {
    if (spec.id == null) return;
    if (!editRow.specLimit || !editRow.specLimit.trim()) {
      setError("Specification limit is required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await SpecificationService.update(
        spec.id,
        spec.testCode,
        editRow.alertLimit ? editRow.alertLimit.trim() : "",
        editRow.actionLimit ? editRow.actionLimit.trim() : "",
        editRow.specLimit.trim(),
        editRow.unit ? editRow.unit.trim() : ""
      );
      setEditingId(null);
      setEditRow({});
      await loadSpecs();
      onSpecsChanged?.();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to update specification.");
    } finally {
      setSaving(false);
    }
  };

  // === DELETE spec ===
  const handleDeleteConfirm = async () => {
    if (!pendingDelete) return;
    setSaving(true);
    setError(null);
    try {
      await SpecificationService.remove(pendingDelete.id);
      setPendingDelete(null);
      await loadSpecs();
      onSpecsChanged?.();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to delete specification.");
    } finally {
      setSaving(false);
    }
  };

  const getTestDisplayName = (testCode: string) => {
    const match = assignedTests.find((t) => t.testCode === testCode);
    return match?.displayName && match.displayName !== testCode ? `${match.displayName} (${testCode})` : testCode;
  };

  return (
    <Box sx={{ p: 0.5 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
        <Typography
          variant="subtitle2"
          sx={{ fontWeight: 700, color: theme.palette.primary.main, textTransform: "uppercase", fontSize: 12, letterSpacing: "0.5px" }}
        >
          Pharmacopoeial Specifications & Limits ({specs.length})
        </Typography>
        {loading && <CircularProgress size={16} />}
      </Stack>

      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 1.5 }}>{error}</Alert>}

      {assignableTestCodes.length === 0 ? (
        <Alert severity="warning" sx={{ py: 1, fontSize: 13 }}>
          This item has no assigned tests yet. Assign tests under the <strong>Assigned Tests</strong> tab or edit the item before configuring specifications.
        </Alert>
      ) : (
        <Box>
          <Table size="small" sx={{ mb: 2, border: "1px solid", borderColor: "divider", borderRadius: 1, overflow: "hidden" }}>
            <TableHead>
              <TableRow sx={{ backgroundColor: "background.default" }}>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 200 }}>Assigned Test</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>Alert Limit</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>Action Limit</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>
                  Specification Limit
                  <Tooltip title="Pharmacopoeial pass/fail threshold">
                    <InfoOutlinedIcon sx={{ fontSize: 14, ml: 0.5, verticalAlign: "middle", color: "text.secondary" }} />
                  </Tooltip>
                </TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>Unit</TableCell>
                <TableCell align="right" sx={{ width: 100 }}>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {/* Existing specs rows */}
              {specs.map((spec, idx) => {
                const specId = spec.id ?? idx;
                const isEditing = editingId === specId;

                return (
                  <TableRow key={specId} hover sx={{ "&:nth-of-type(even)": { bgcolor: "background.default" } }}>
                    <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>
                      {getTestDisplayName(spec.testCode)}
                    </TableCell>

                    {isEditing ? (
                      // Edit mode
                      <>
                        <TableCell>
                          <TextField
                            size="small"
                            placeholder="Alert"
                            value={editRow.alertLimit ?? ""}
                            onChange={(e) => setEditRow((r) => ({ ...r, alertLimit: e.target.value }))}
                            sx={{ width: 100 }}
                          />
                        </TableCell>
                        <TableCell>
                          <TextField
                            size="small"
                            placeholder="Action"
                            value={editRow.actionLimit ?? ""}
                            onChange={(e) => setEditRow((r) => ({ ...r, actionLimit: e.target.value }))}
                            sx={{ width: 100 }}
                          />
                        </TableCell>
                        <TableCell>
                          <TextField
                            size="small"
                            placeholder="Specification *"
                            value={editRow.specLimit ?? ""}
                            onChange={(e) => setEditRow((r) => ({ ...r, specLimit: e.target.value }))}
                            fullWidth
                            required
                          />
                        </TableCell>
                        <TableCell>
                          <TextField
                            size="small"
                            placeholder="Unit (e.g. g)"
                            value={editRow.unit ?? ""}
                            onChange={(e) => setEditRow((r) => ({ ...r, unit: e.target.value }))}
                            sx={{ width: 100 }}
                          />
                        </TableCell>
                        <TableCell align="right">
                          <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                            <IconButton
                              size="small"
                              color="success"
                              onClick={() => handleEditSave(spec)}
                              disabled={saving}
                              title="Save Changes"
                            >
                              <CheckIcon fontSize="small" />
                            </IconButton>
                            <IconButton
                              size="small"
                              onClick={() => { setEditingId(null); setEditRow({}); }}
                              disabled={saving}
                              title="Cancel Edit"
                            >
                              <CloseIcon fontSize="small" />
                            </IconButton>
                          </Stack>
                        </TableCell>
                      </>
                    ) : (
                      // Read mode
                      <>
                        <TableCell sx={{ fontSize: 13 }}>{spec.alertLimit || "—"}</TableCell>
                        <TableCell sx={{ fontSize: 13 }}>{spec.actionLimit || "—"}</TableCell>
                        <TableCell sx={{ fontWeight: 700, fontSize: 13, color: theme.palette.primary.main }}>
                          {spec.specLimit || "—"}
                        </TableCell>
                        <TableCell sx={{ fontSize: 13 }}>{spec.unit || "—"}</TableCell>
                        <TableCell align="right">
                          <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                            <IconButton
                              size="small"
                              onClick={() => {
                                setEditingId(specId);
                                setEditRow({
                                  alertLimit: spec.alertLimit,
                                  actionLimit: spec.actionLimit,
                                  specLimit: spec.specLimit,
                                  unit: spec.unit
                                });
                              }}
                              title="Edit Specification"
                            >
                              <EditIcon fontSize="small" />
                            </IconButton>
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => spec.id != null && setPendingDelete({ id: spec.id, testCode: spec.testCode })}
                              title="Delete Specification"
                            >
                              <DeleteIcon fontSize="small" />
                            </IconButton>
                          </Stack>
                        </TableCell>
                      </>
                    )}
                  </TableRow>
                );
              })}

              {/* Add new spec row (shown if there are assigned tests not yet configured) */}
              {availableToAdd.length > 0 && (
                <TableRow sx={{ backgroundColor: "background.default" }}>
                  <TableCell>
                    <FormControl size="small" fullWidth>
                      <Select
                        value={addRow.testCode}
                        onChange={(e) => setAddRow((r) => ({ ...r, testCode: e.target.value }))}
                        displayEmpty
                      >
                        <MenuItem value="">
                          <em>Select Test Code *</em>
                        </MenuItem>
                        {availableToAdd.map((code) => (
                          <MenuItem key={code} value={code}>
                            {getTestDisplayName(code)}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      placeholder="Alert"
                      value={addRow.alertLimit}
                      onChange={(e) => setAddRow((r) => ({ ...r, alertLimit: e.target.value }))}
                      sx={{ width: 100 }}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      placeholder="Action"
                      value={addRow.actionLimit}
                      onChange={(e) => setAddRow((r) => ({ ...r, actionLimit: e.target.value }))}
                      sx={{ width: 100 }}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      placeholder="Specification Limit *"
                      value={addRow.specLimit}
                      onChange={(e) => setAddRow((r) => ({ ...r, specLimit: e.target.value }))}
                      fullWidth
                      required
                    />
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      placeholder="Unit (e.g. g)"
                      value={addRow.unit}
                      onChange={(e) => setAddRow((r) => ({ ...r, unit: e.target.value }))}
                      sx={{ width: 100 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
                      variant="contained"
                      startIcon={saving ? <CircularProgress size={14} color="inherit" /> : <AddIcon />}
                      onClick={handleAdd}
                      disabled={saving || !addRow.testCode || !addRow.specLimit.trim()}
                      sx={{
                        bgcolor: brandColors.sectionTitle,
                        "&:hover": { bgcolor: brandColors.pageTitle },
                        minWidth: 80,
                        fontWeight: 700,
                        textTransform: "none"
                      }}
                    >
                      Add
                    </Button>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>

          {specs.length === 0 && availableToAdd.length > 0 && (
            <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic", mb: 1.5 }}>
              No specifications defined yet. Select an assigned test above to add its Alert/Action/Specification limits.
            </Typography>
          )}

          {availableToAdd.length === 0 && specs.length > 0 && (
            <Alert severity="success" sx={{ py: 0.5, fontSize: 12 }}>
              All assigned tests for <strong>{item.name}</strong> have specifications configured.
            </Alert>
          )}
        </Box>
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testCode} specification for "${item.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={handleDeleteConfirm}
      />
    </Box>
  );
};
