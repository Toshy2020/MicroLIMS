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
  TableContainer
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "../../../../i18n";
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
  const { t } = useTranslation("labConfig");
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
      onUpdated(
        t("media.incubationConditionsSection.deleteSuccess", {
          label: incubationConditionLabel(conditionToDelete)
        })
      );
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? t("media.incubationConditionsSection.deleteError"));
      setPendingDeleteCondition(null);
    }
  };

  const handleDialogSuccess = () => {
    const isEditing = editingCondition != null;
    setDialogOpen(false);
    onUpdated(
      isEditing
        ? t("media.incubationConditionsSection.updateSuccess")
        : t("media.incubationConditionsSection.addSuccess")
    );
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
          {t("media.incubationConditionsSection.title")}
        </Typography>
        {isManager && (
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            {t("media.incubationConditionsSection.addCondition")}
          </Button>
        )}
      </Stack>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {t("media.incubationConditionsSection.subtitle")}
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
              ? t("media.incubationConditionsSection.emptyManagerDesc")
              : t("media.incubationConditionsSection.emptyDesc")}
          </Typography>
        </Paper>
      ) : (
        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>{t("media.incubationConditionsSection.columns.incubation")}</TableCell>
                <TableCell>{t("media.incubationConditionsSection.columns.temperature")}</TableCell>
                <TableCell>{t("media.incubationConditionsSection.columns.usedBy")}</TableCell>
                {isManager && <TableCell align="right">{t("media.incubationConditionsSection.columns.actions")}</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {conditions.map((condition) => {
                const inUse = condition.configurationCount + condition.stepMediaCount > 0;
                const usedByParts: string[] = [];

                if (condition.configurationCount > 0) {
                  usedByParts.push(
                    t("media.incubationConditionsSection.configurationCount", {
                      count: condition.configurationCount
                    })
                  );
                }

                if (condition.stepMediaCount > 0) {
                  usedByParts.push(
                    t("media.incubationConditionsSection.stepMediaCount", {
                      count: condition.stepMediaCount
                    })
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
                        <Chip size="small" variant="outlined" label={t("media.incubationConditionsSection.notUsed")} />
                      ) : (
                        usedByParts.join(" · ")
                      )}
                    </TableCell>
                    {isManager && (
                      <TableCell align="right">
                        <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                          {inUse ? (
                            <Tooltip title={t("media.incubationConditionsSection.inUseTooltip")}>
                              <span>
                                <IconButton
                                  aria-label={t("media.incubationConditionsSection.inUseTooltip")}
                                  size="small"
                                  disabled
                                >
                                  <EditIcon fontSize="small" />
                                </IconButton>
                              </span>
                            </Tooltip>
                          ) : (
                            <Tooltip title={t("media.incubationConditionsSection.editTitle")}>
                              <IconButton size="small" onClick={() => handleOpenEdit(condition)}>
                                <EditIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          )}
                          {inUse ? (
                            <Tooltip title={t("media.incubationConditionsSection.inUseTooltip")}>
                              <span>
                                <IconButton
                                  aria-label={t("media.incubationConditionsSection.inUseTooltip")}
                                  size="small"
                                  color="error"
                                  disabled
                                >
                                  <DeleteOutlineIcon fontSize="small" />
                                </IconButton>
                              </span>
                            </Tooltip>
                          ) : (
                            <Tooltip title={t("media.incubationConditionsSection.deleteTitle")}>
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
        </TableContainer>
      )}

      <ConfirmationDialog
        open={pendingDeleteCondition != null}
        message={
          pendingDeleteCondition
            ? t("media.incubationConditionsSection.deleteConfirm", {
                label: incubationConditionLabel(pendingDeleteCondition),
                name: product.name
              })
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
