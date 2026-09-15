import { useState } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Button,
  IconButton,
  Tooltip,
  Alert,
  Chip,
  Stack,
  Paper,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../../theme";
import {
  masterDataOptions,
  incubationConditionLabel,
} from "../../../../services/masterDataOptions";
import {
  MediaProductOption,
  MediaIncubationConditionOption,
} from "../types/mediaConfigurationTypes";
import { MediaIncubationConditionDialog } from "../dialogs/MediaIncubationConditionDialog";

interface MediaIncubationConditionsSectionProps {
  product: MediaProductOption;
  conditions: MediaIncubationConditionOption[];
  onUpdated: (successMessage?: string) => void;
  isManager: boolean;
}

export function MediaIncubationConditionsSection(props: MediaIncubationConditionsSectionProps) {
  const { product, conditions, onUpdated, isManager } = props;
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCondition, setEditingCondition] = useState<MediaIncubationConditionOption | null>(null);
  const [pendingDeleteCondition, setPendingDeleteCondition] = useState<MediaIncubationConditionOption | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleOpenAdd = () => {
    setEditingCondition(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (condition: MediaIncubationConditionOption) => {
    setEditingCondition(condition);
    setDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!pendingDeleteCondition) return;
    const conditionToDelete = pendingDeleteCondition;
    setError(null);
    try {
      await masterDataOptions.deleteMediaIncubationCondition(conditionToDelete.id);
      setPendingDeleteCondition(null);
      onUpdated(`Incubation condition ${incubationConditionLabel(conditionToDelete)} deleted.`);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "Could not delete this incubation condition.");
      setPendingDeleteCondition(null);
    }
  };

  const handleDialogSuccess = () => {
    const isEditing = editingCondition != null;
    setDialogOpen(false);
    onUpdated(isEditing ? "Incubation condition updated." : "Incubation condition added.");
  };

  return (
    <Box>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center",
          mb: 1,
        }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
          Incubation Conditions
        </Typography>
        {isManager && (
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            Add Condition
          </Button>
        )}
      </Stack>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Time and temperature pairs this medium can be incubated at. The evaluation configuration and Test Master step media each pick one. A condition in use can't be edited or deleted - add a new one instead.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {conditions.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{
            p: 3,
            textAlign: "center",
            border: "1px dashed",
            borderColor: "divider",
            bgcolor: "action.hover",
          }}
        >
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {isManager
              ? "No incubation conditions yet. Add one before configuring this medium or using it in Test Master."
              : "No incubation conditions yet."}
          </Typography>
        </Paper>
      ) : (
        <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell>Incubation</TableCell>
              <TableCell>Temperature</TableCell>
              <TableCell>Used by</TableCell>
              {isManager && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {conditions.map((condition) => {
              const inUse = condition.configurationCount + condition.stepMediaCount > 0;
              const usedByParts: string[] = [];

              if (condition.configurationCount > 0) {
                usedByParts.push(
                  `${condition.configurationCount} configuration${
                    condition.configurationCount === 1 ? "" : "s"
                  }`
                );
              }

              if (condition.stepMediaCount > 0) {
                usedByParts.push(
                  `${condition.stepMediaCount} ${
                    condition.stepMediaCount === 1 ? "step medium" : "step media"
                  }`
                );
              }

              return (
                <TableRow key={condition.id} hover>
                  <TableCell>
                    {condition.incubationMinHours}–{condition.incubationMaxHours} h
                  </TableCell>
                  <TableCell>
                    {condition.temperatureMin}–{condition.temperatureMax} °C
                  </TableCell>
                  <TableCell>
                    {usedByParts.length === 0 ? (
                      <Chip size="small" variant="outlined" label="Not used" />
                    ) : (
                      usedByParts.join(" · ")
                    )}
                  </TableCell>
                  {isManager && (
                    <TableCell align="right">
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                        {inUse ? (
                          <Tooltip title="In use - add a new condition instead">
                            <span>
                              <IconButton size="small" disabled>
                                <EditIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        ) : (
                          <Tooltip title="Edit condition">
                            <IconButton size="small" onClick={() => handleOpenEdit(condition)}>
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        {inUse ? (
                          <Tooltip title="In use - add a new condition instead">
                            <span>
                              <IconButton size="small" color="error" disabled>
                                <DeleteOutlineIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        ) : (
                          <Tooltip title="Delete condition">
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => setPendingDeleteCondition(condition)}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Stack>
                    </TableCell>
                  )}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}

      <ConfirmationDialog
        open={pendingDeleteCondition != null}
        message={
          pendingDeleteCondition
            ? `Delete the incubation condition ${incubationConditionLabel(pendingDeleteCondition)} for "${product.name}"? This cannot be undone.`
            : ""
        }
        onCancel={() => setPendingDeleteCondition(null)}
        onConfirm={handleDeleteConfirm}
      />

      <MediaIncubationConditionDialog
        open={dialogOpen}
        product={product}
        conditionToEdit={editingCondition}
        onClose={() => setDialogOpen(false)}
        onSuccess={handleDialogSuccess}
      />
    </Box>
  );
}
