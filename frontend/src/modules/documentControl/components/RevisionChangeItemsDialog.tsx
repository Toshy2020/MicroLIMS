import React, { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Box,
  Typography,
  Alert,
  CircularProgress,
  Chip,
  IconButton,
  MenuItem,
  Select,
  FormControl,
  InputLabel,
  Paper
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutline";
import { documentRevisionService } from "../services/documentRevisionService";
import type {
  RevisionChangeItemDto,
  AddChangeItemRequest
} from "../types/documentControlTypes";

interface RevisionChangeItemsDialogProps {
  open: boolean;
  onClose: () => void;
  revisionId: number;
  revisionNumber: string;
  isEditable: boolean;
  onChanged?: () => void;
}

const CATEGORIES = ["Addition", "Deletion", "Modification", "Clarification", "Correction", "Administrative"];

export const RevisionChangeItemsDialog: React.FC<RevisionChangeItemsDialogProps> = ({
  open,
  onClose,
  revisionId,
  revisionNumber,
  isEditable,
  onChanged
}) => {
  const [items, setItems] = useState<RevisionChangeItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Adding item form
  const [showAdd, setShowAdd] = useState<boolean>(false);
  const [sectionNumber, setSectionNumber] = useState<string>("");
  const [sectionTitle, setSectionTitle] = useState<string>("");
  const [description, setDescription] = useState<string>("");
  const [rationale, setRationale] = useState<string>("");
  const [category, setCategory] = useState<string>("Modification");
  const [adding, setAdding] = useState<boolean>(false);

  useEffect(() => {
    if (open) {
      loadItems();
      setShowAdd(false);
      resetForm();
    }
  }, [open, revisionId]);

  const loadItems = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await documentRevisionService.getChangeItems(revisionId);
      setItems(res);
    } catch (err: any) {
      setError("Failed to load revision change items.");
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setSectionNumber("");
    setSectionTitle("");
    setDescription("");
    setRationale("");
    setCategory("Modification");
  };

  const handleAddItem = async () => {
    if (!sectionNumber.trim() || !sectionTitle.trim() || !description.trim() || !rationale.trim()) {
      setError("All fields are required to add a change item.");
      return;
    }

    setAdding(true);
    setError(null);
    try {
      const req: AddChangeItemRequest = {
        sectionNumber: sectionNumber.trim(),
        sectionTitle: sectionTitle.trim(),
        descriptionOfChange: description.trim(),
        changeRationale: rationale.trim(),
        changeCategory: category
      };
      await documentRevisionService.addChangeItem(revisionId, req);
      resetForm();
      setShowAdd(false);
      await loadItems();
      if (onChanged) onChanged();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to add change item.");
    } finally {
      setAdding(false);
    }
  };

  const handleDeleteItem = async (itemId: number) => {
    if (!window.confirm("Are you sure you want to delete this change item?")) return;
    try {
      await documentRevisionService.deleteChangeItem(itemId);
      await loadItems();
      if (onChanged) onChanged();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to delete change item.");
    }
  };

  const handleToggleStatus = async (item: RevisionChangeItemDto) => {
    const nextStatus = item.status === "Addressed" ? "Draft" : "Addressed";
    try {
      await documentRevisionService.updateChangeItem(item.id, {
        sectionNumber: item.sectionNumber,
        sectionTitle: item.sectionTitle,
        descriptionOfChange: item.descriptionOfChange,
        changeRationale: item.changeRationale,
        changeCategory: item.changeCategory,
        status: nextStatus
      });
      await loadItems();
      if (onChanged) onChanged();
    } catch (err: any) {
      setError("Failed to update status.");
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle sx={{ fontWeight: 600, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span>Structured Change Items — Rev {revisionNumber}</span>
        {isEditable && !showAdd && (
          <Button
            startIcon={<AddCircleOutlineIcon />}
            variant="contained"
            size="small"
            onClick={() => setShowAdd(true)}
          >
            Add Change Item
          </Button>
        )}
      </DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        {showAdd && (
          <Paper variant="outlined" sx={{ p: 2.5, mb: 3, bgcolor: "grey.50" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>
              Add New Section Change Item
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 2fr 1.5fr", gap: 2, mb: 2 }}>
              <TextField
                label="Section #"
                size="small"
                value={sectionNumber}
                onChange={(e) => setSectionNumber(e.target.value)}
                placeholder="e.g. 4.2"
                required
              />
              <TextField
                label="Section Title"
                size="small"
                value={sectionTitle}
                onChange={(e) => setSectionTitle(e.target.value)}
                placeholder="e.g. Incubation Conditions"
                required
              />
              <FormControl size="small">
                <InputLabel>Category</InputLabel>
                <Select
                  value={category}
                  label="Category"
                  onChange={(e) => setCategory(e.target.value)}
                >
                  {CATEGORIES.map((c) => (
                    <MenuItem key={c} value={c}>{c}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>
            <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 2 }}>
              <TextField
                label="Description of Change *"
                multiline
                rows={2}
                size="small"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Detailed description of what is changing in this section..."
                required
              />
              <TextField
                label="Change Rationale *"
                multiline
                rows={2}
                size="small"
                value={rationale}
                onChange={(e) => setRationale(e.target.value)}
                placeholder="Regulatory, scientific, or compendial reason for this modification..."
                required
              />
            </Box>
            <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
              <Button size="small" onClick={() => { setShowAdd(false); resetForm(); }}>
                Cancel
              </Button>
              <Button
                size="small"
                variant="contained"
                onClick={handleAddItem}
                disabled={adding}
              >
                {adding ? <CircularProgress size={18} /> : "Save Item"}
              </Button>
            </Box>
          </Paper>
        )}

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
            <CircularProgress />
          </Box>
        ) : items.length === 0 ? (
          <Alert severity="info">
            No structured change items recorded for this revision yet.
          </Alert>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Category</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Description</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Rationale</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Origin</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                {isEditable && <TableCell sx={{ fontWeight: 600, textAlign: "right" }}>Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell sx={{ fontWeight: 600 }}>
                    {item.sectionNumber} — {item.sectionTitle}
                  </TableCell>
                  <TableCell>
                    <Chip label={item.changeCategory} size="small" variant="outlined" />
                  </TableCell>
                  <TableCell sx={{ maxWidth: 260 }}>{item.descriptionOfChange}</TableCell>
                  <TableCell sx={{ maxWidth: 220 }}>{item.changeRationale}</TableCell>
                  <TableCell>
                    {item.originatingReviewFindingId ? (
                      <Chip label={`Finding #${item.originatingReviewFindingId}`} size="small" color="secondary" />
                    ) : (
                      <Typography variant="caption" color="text.secondary">Author Added</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={item.status}
                      size="small"
                      color={item.status === "Addressed" ? "success" : "default"}
                      onClick={isEditable ? () => handleToggleStatus(item) : undefined}
                      sx={{ cursor: isEditable ? "pointer" : "default" }}
                    />
                  </TableCell>
                  {isEditable && (
                    <TableCell sx={{ textAlign: "right" }}>
                      <IconButton size="small" color="error" onClick={() => handleDeleteItem(item.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
};
