import { useState, useEffect, useCallback } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  CircularProgress,
  IconButton,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import {
  SupplierAttributeService,
  SupplierStatedAttributeDto,
} from "../services/SupplierAttributeService";
import { useHasPermission } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";

export interface SupplierAttributesSectionProps {
  itemId: number;
  grades: string[];
}

export function SupplierAttributesSection({ itemId, grades }: SupplierAttributesSectionProps) {
  const [attributes, setAttributes] = useState<SupplierStatedAttributeDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canManage = useHasPermission(PERMISSIONS.MASTER_DATA_MANAGE);

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingAttr, setEditingAttr] = useState<SupplierStatedAttributeDto | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [statedValue, setStatedValue] = useState("");
  const [grade, setGrade] = useState("");

  const loadAttributes = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await SupplierAttributeService.listForItem(itemId);
      setAttributes(data);
    } catch {
      setError("Failed to load supplier-stated attributes.");
    } finally {
      setLoading(false);
    }
  }, [itemId]);

  useEffect(() => {
    loadAttributes();
  }, [loadAttributes]);

  const handleOpenAdd = () => {
    setEditingAttr(null);
    setName("");
    setStatedValue("");
    setGrade("");
    setFormError(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (attr: SupplierStatedAttributeDto) => {
    setEditingAttr(attr);
    setName(attr.name);
    setStatedValue(attr.statedValue);
    setGrade(attr.grade || "");
    setFormError(null);
    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    if (submitting) return;
    setDialogOpen(false);
    setEditingAttr(null);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setFormError("Name is required.");
      return;
    }
    if (!statedValue.trim()) {
      setFormError("Stated value is required.");
      return;
    }

    setSubmitting(true);
    setFormError(null);
    try {
      if (editingAttr) {
        await SupplierAttributeService.update(editingAttr.id, {
          itemId,
          name: name.trim(),
          statedValue: statedValue.trim(),
          grade: grade.trim() ? grade.trim() : null,
          sortOrder: editingAttr.sortOrder,
        });
      } else {
        await SupplierAttributeService.create(itemId, {
          name: name.trim(),
          statedValue: statedValue.trim(),
          grade: grade.trim() ? grade.trim() : null,
        });
      }
      setDialogOpen(false);
      setEditingAttr(null);
      await loadAttributes();
    } catch (err: any) {
      setFormError(err?.response?.data?.message ?? "Failed to save supplier-stated attribute.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm("Are you sure you want to delete this supplier-stated attribute?")) return;
    try {
      await SupplierAttributeService.delete(id);
      await loadAttributes();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Failed to delete supplier-stated attribute.");
    }
  };

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Supplier-Stated Attributes
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
            Certificate of Analysis attributes and values stated directly by the supplier.
          </Typography>
        </Box>
        {canManage && (
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Add Supplier Attribute
          </Button>
        )}
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={24} />
        </Box>
      ) : attributes.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{ p: 3, textAlign: "center", color: "text.secondary", bgcolor: "action.hover" }}
        >
          <Typography variant="body2">No supplier-stated attributes configured for this item.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: "action.hover" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Name</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Stated Value</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Grade</TableCell>
                {canManage && (
                  <TableCell align="right" sx={{ fontWeight: 700 }}>
                    Actions
                  </TableCell>
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {attributes.map((a) => (
                <TableRow key={a.id}>
                  <TableCell sx={{ fontWeight: 600 }}>{a.name}</TableCell>
                  <TableCell>{a.statedValue}</TableCell>
                  <TableCell>{a.grade || "All grades"}</TableCell>
                  {canManage && (
                    <TableCell align="right">
                      <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                        <IconButton
                          size="small"
                          aria-label="Edit"
                          onClick={() => handleOpenEdit(a)}
                          title="Edit"
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          color="error"
                          aria-label="Delete"
                          onClick={() => handleDelete(a.id)}
                          title="Delete"
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Stack>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Add / Edit Dialog */}
      <Dialog open={dialogOpen} onClose={handleCloseDialog} maxWidth="xs" fullWidth>
        <form onSubmit={handleSave}>
          <DialogTitle sx={{ fontWeight: 700 }}>
            {editingAttr ? "Edit Supplier Attribute" : "Add Supplier Attribute"}
          </DialogTitle>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ mt: 0.5 }}>
              {formError && <Alert severity="error">{formError}</Alert>}

              <TextField
                label="Name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                fullWidth
                size="small"
                slotProps={{ htmlInput: { maxLength: 200 } }}
                autoFocus
              />

              <TextField
                label="Stated Value"
                value={statedValue}
                onChange={(e) => setStatedValue(e.target.value)}
                required
                fullWidth
                size="small"
                slotProps={{ htmlInput: { maxLength: 500 } }}
              />

              <TextField
                select
                slotProps={{ select: { native: true } }}
                label="Grade"
                value={grade}
                onChange={(e) => setGrade(e.target.value)}
                fullWidth
                size="small"
              >
                <option value="">All grades</option>
                {grades.map((g) => (
                  <option key={g} value={g}>
                    {g}
                  </option>
                ))}
              </TextField>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseDialog} disabled={submitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              {submitting ? "Saving..." : "Save"}
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </Box>
  );
}
