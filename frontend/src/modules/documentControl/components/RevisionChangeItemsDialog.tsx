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
  Paper,
  Tooltip,
  useTheme,
  TableContainer
} from "@mui/material";
// Removal deactivates and retains the record rather than deleting it, so the
// control must not carry a delete affordance (DC-URS-184, FS-1a-170).
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlined";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import { documentRevisionService } from "../services/documentRevisionService";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../theme";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import type {
  RevisionChangeItemDto
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
  const { t } = useTranslation("documentControl");
  const theme = useTheme();
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
  const [itemToDelete, setItemToDelete] = useState<number | null>(null);

  const resetForm = () => {
    setSectionNumber("");
    setSectionTitle("");
    setDescription("");
    setRationale("");
    setCategory("Modification");
  };

  useEffect(() => {
    if (open) {
      loadItems();
    }
  }, [open, revisionId]);

  const loadItems = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await documentRevisionService.getChangeItems(revisionId);
      setItems(data);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("changeItems.loadError")));
    } finally {
      setLoading(false);
    }
  };

  const handleAddItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim()) {
      setError(t("changeItems.descriptionRequired"));
      return;
    }
    setAdding(true);
    setError(null);
    try {
      await documentRevisionService.addChangeItem(revisionId, {
        sectionNumber: sectionNumber.trim(),
        sectionTitle: sectionTitle.trim(),
        descriptionOfChange: description.trim(),
        changeRationale: rationale.trim(),
        changeCategory: category
      });
      resetForm();
      setShowAdd(false);
      await loadItems();
      if (onChanged) onChanged();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("changeItems.addError")));
    } finally {
      setAdding(false);
    }
  };

  const handleDeleteItem = (itemId: number) => {
    setItemToDelete(itemId);
  };

  const confirmDeleteItem = async () => {
    if (!itemToDelete) return;
    const id = itemToDelete;
    setItemToDelete(null);
    try {
      await documentRevisionService.deactivateChangeItem(id);
      await loadItems();
      if (onChanged) onChanged();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("changeItems.removeError")));
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
      }, item.version);
      await loadItems();
      if (onChanged) onChanged();
    } catch (_err: unknown) {
      setError(t("changeItems.statusError"));
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle sx={{ fontWeight: 600, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span>{t("changeItems.title", { rev: revisionNumber })}</span>
        {isEditable && !showAdd && (
          <Button
            startIcon={<AddCircleOutlineIcon />}
            variant="contained"
            size="small"
            onClick={() => setShowAdd(true)}
          >
            {t("changeItems.addButton")}
          </Button>
        )}
      </DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        {showAdd && (
          <Paper variant="outlined" sx={{ p: 2.5, mb: 3, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>
              {t("changeItems.addSectionTitle")}
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 2fr 1.5fr", gap: 2, mb: 2 }}>
              <TextField
                label={t("changeItems.sectionNumLabel")}
                size="small"
                value={sectionNumber}
                onChange={(e) => setSectionNumber(e.target.value)}
                placeholder={t("changeItems.sectionNumPlaceholder")}
                required
              />
              <TextField
                label={t("changeItems.sectionTitleLabel")}
                size="small"
                value={sectionTitle}
                onChange={(e) => setSectionTitle(e.target.value)}
                placeholder={t("changeItems.sectionTitlePlaceholder")}
                required
              />
              <FormControl size="small">
                <InputLabel>{t("changeItems.categoryLabel")}</InputLabel>
                <Select
                  value={category}
                  label={t("changeItems.categoryLabel")}
                  onChange={(e) => setCategory(e.target.value)}
                  inputProps={{ "aria-label": t("changeItems.categoryLabel") }}
                >
                  {CATEGORIES.map((c) => (
                    <MenuItem key={c} value={c}>{t(`changeItems.categories.${c}`)}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>
            <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 2 }}>
              <TextField
                label={t("changeItems.descriptionLabel")}
                multiline
                rows={2}
                size="small"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder={t("changeItems.descriptionPlaceholder")}
                required
              />
              <TextField
                label={t("changeItems.rationaleLabel")}
                multiline
                rows={2}
                size="small"
                value={rationale}
                onChange={(e) => setRationale(e.target.value)}
                placeholder={t("changeItems.rationalePlaceholder")}
                required
              />
            </Box>
            <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
              <Button size="small" onClick={() => { setShowAdd(false); resetForm(); }}>
                {t("common.cancel")}
              </Button>
              <Button
                size="small"
                variant="contained"
                onClick={handleAddItem}
                disabled={adding}
              >
                {adding ? <CircularProgress size={18} /> : t("changeItems.saveItem")}
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
            {t("changeItems.empty")}
          </Alert>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.section")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.category")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.description")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.rationale")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.origin")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("changeItems.columns.status")}</TableCell>
                  {isEditable && <TableCell sx={{ fontWeight: 600, textAlign: "right" }}>{t("changeItems.columns.actions")}</TableCell>}
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell sx={{ fontWeight: 600 }}>
                      {item.sectionNumber} {item.sectionTitle}
                    </TableCell>
                    <TableCell>
                      <Chip label={item.changeCategory} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 260 }}>{item.descriptionOfChange}</TableCell>
                    <TableCell sx={{ maxWidth: 220 }}>{item.changeRationale}</TableCell>
                    <TableCell>
                      {item.originatingReviewFindingId ? (
                        <Chip label={t("changeItems.findingChip", { id: item.originatingReviewFindingId })} size="small" color="secondary" />
                      ) : (
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("changeItems.authorAdded")}</Typography>
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
                        <Tooltip title={t("changeItems.removeTooltip")}>
                          <IconButton size="small" color="error" onClick={() => handleDeleteItem(item.id)} aria-label={t("changeItems.removeAria")}>
                            <RemoveCircleOutlineIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose}>{t("changeItems.close")}</Button>
      </DialogActions>

      <ConfirmationDialog
        open={itemToDelete !== null}
        title={t("changeItems.confirmTitle")}
        message={t("changeItems.confirmMessage")}
        confirmText={t("changeItems.confirmButton")}
        destructive
        onConfirm={confirmDeleteItem}
        onCancel={() => setItemToDelete(null)}
      />
    </Dialog>
  );
};
