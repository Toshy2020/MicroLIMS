import React, { useState } from "react";
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
  CircularProgress
} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { Item } from "../services/ItemService";
import { SpecificationService, SpecificationDto } from "../../specifications/services/SpecificationService";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { brandColors } from "../../../../theme";

interface ItemSpecificationsSectionProps {
  item: Item;
  onSpecsChanged: () => void;
}

export const ItemSpecificationsSection: React.FC<ItemSpecificationsSectionProps> = ({
  item,
  onSpecsChanged
}) => {
  const { options: testDefinitions } = useTestDefinitions();

  // Filter to count tests assigned to THIS item only
  const countTestCodes = item.assignedTests
    .map((t) => t.testCode)
    .filter((code) => testDefinitions.find((o) => o.code === code)?.workflowType === "CountTest");

  // Existing specs (already in item.specifications from GET /api/items)
  const existingSpecs = item.specifications ?? [];

  // Add row state
  const [addRow, setAddRow] = useState({
    testCode: "",
    alertLimit: "",
    actionLimit: "",
    specLimit: ""
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Edit state per spec row
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editRow, setEditRow] = useState<Partial<SpecificationDto>>({});

  // Delete state
  const [pendingDelete, setPendingDelete] = useState<{ id: number; testCode: string } | null>(null);

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
        addRow.specLimit.trim()
      );
      setAddRow({ testCode: "", alertLimit: "", actionLimit: "", specLimit: "" });
      onSpecsChanged();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to add specification.");
    } finally {
      setSaving(false);
    }
  };

  // === EDIT spec ===
  const handleEditSave = async (spec: { id?: number; testCode: string }) => {
    if (!spec.id) return;
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
        editRow.specLimit.trim()
      );
      setEditingId(null);
      setEditRow({});
      onSpecsChanged();
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
      onSpecsChanged();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to delete specification.");
    } finally {
      setSaving(false);
    }
  };

  // Available tests to add (excluding tests already configured)
  const availableToAdd = countTestCodes.filter(
    (code) => !existingSpecs.some((s) => s.testCode === code)
  );

  return (
    <Box sx={{ p: 0.5 }}>
      <Typography
        variant="subtitle2"
        sx={{ mb: 1.5, fontWeight: 700, color: brandColors.sectionTitle, textTransform: "uppercase", fontSize: 12, letterSpacing: "0.5px" }}
      >
        Specifications (Count Tests)
      </Typography>

      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 1.5 }}>{error}</Alert>}

      {countTestCodes.length === 0 ? (
        <Typography variant="body2" sx={{ color: "#9ca3af", fontStyle: "italic", py: 1 }}>
          No count tests assigned to this item. Assign TAMC or TYMC tests to configure limits.
        </Typography>
      ) : (
        <Table size="small" sx={{ mb: 1.5, border: "1px solid #e5e7eb", borderRadius: 1, overflow: "hidden" }}>
          <TableHead>
            <TableRow sx={{ backgroundColor: "#f3f4f6" }}>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>Test</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 120 }}>Alert Limit</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 120 }}>Action Limit</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>
                Spec Limit
                <Tooltip title="Pharmacopoeial pass/fail threshold">
                  <InfoOutlinedIcon sx={{ fontSize: 14, ml: 0.5, verticalAlign: "middle", color: "#9ca3af" }} />
                </Tooltip>
              </TableCell>
              <TableCell align="right" sx={{ width: 90 }} />
            </TableRow>
          </TableHead>
          <TableBody>
            {/* Existing specs rows */}
            {existingSpecs.map((spec, idx) => {
              const specId = spec.id ?? idx;
              const isEditing = editingId === specId;

              return (
                <TableRow key={specId} hover sx={{ "&:nth-of-type(even)": { bgcolor: "#fcfcfd" } }}>
                  <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>{spec.testCode}</TableCell>

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
                          placeholder="Spec *"
                          value={editRow.specLimit ?? ""}
                          onChange={(e) => setEditRow((r) => ({ ...r, specLimit: e.target.value }))}
                          sx={{ width: 110 }}
                          required
                        />
                      </TableCell>
                      <TableCell align="right">
                        <IconButton
                          size="small"
                          color="success"
                          onClick={() => handleEditSave(spec)}
                          disabled={saving}
                          title="Save"
                        >
                          <CheckIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          onClick={() => { setEditingId(null); setEditRow({}); }}
                          disabled={saving}
                          title="Cancel"
                        >
                          <CloseIcon fontSize="small" />
                        </IconButton>
                      </TableCell>
                    </>
                  ) : (
                    // Read mode
                    <>
                      <TableCell sx={{ fontSize: 13 }}>{spec.alertLimit || "—"}</TableCell>
                      <TableCell sx={{ fontSize: 13 }}>{spec.actionLimit || "—"}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 13, color: brandColors.sectionTitle }}>
                        {spec.specLimit || "—"}
                      </TableCell>
                      <TableCell align="right">
                        <IconButton
                          size="small"
                          onClick={() => {
                            setEditingId(specId);
                            setEditRow({
                              alertLimit: spec.alertLimit,
                              actionLimit: spec.actionLimit,
                              specLimit: spec.specLimit
                            });
                          }}
                          title="Edit"
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => spec.id && setPendingDelete({ id: spec.id, testCode: spec.testCode })}
                          title="Delete"
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </TableCell>
                    </>
                  )}
                </TableRow>
              );
            })}

            {/* Add new spec row (shown if there are count tests not yet configured) */}
            {availableToAdd.length > 0 && (
              <TableRow sx={{ backgroundColor: "#fafafa" }}>
                <TableCell>
                  <FormControl size="small" sx={{ minWidth: 120 }}>
                    <Select
                      value={addRow.testCode}
                      onChange={(e) => setAddRow((r) => ({ ...r, testCode: e.target.value }))}
                      displayEmpty
                    >
                      <MenuItem value="">
                        <em>Test Code</em>
                      </MenuItem>
                      {availableToAdd.map((code) => (
                        <MenuItem key={code} value={code}>
                          {code}
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
                    placeholder="Spec *"
                    value={addRow.specLimit}
                    onChange={(e) => setAddRow((r) => ({ ...r, specLimit: e.target.value }))}
                    sx={{ width: 110 }}
                    required
                  />
                </TableCell>
                <TableCell align="right">
                  <Button
                    size="small"
                    variant="contained"
                    onClick={handleAdd}
                    disabled={saving || !addRow.testCode || !addRow.specLimit.trim()}
                    sx={{
                      bgcolor: brandColors.sectionTitle,
                      "&:hover": { bgcolor: brandColors.pageTitle },
                      minWidth: 60,
                      fontWeight: 700
                    }}
                  >
                    {saving ? <CircularProgress size={14} color="inherit" /> : "Add"}
                  </Button>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
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
