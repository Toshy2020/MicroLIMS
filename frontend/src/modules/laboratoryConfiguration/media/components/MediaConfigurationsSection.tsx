import { Fragment, useState } from "react";
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
  Collapse,
  Chip,
  Stack,
  Paper,
  TableContainer
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import { useTranslation } from "../../../../i18n";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../../theme";
import {
  masterDataOptions,
  evaluationTypeLabel,
} from "../../../../services/masterDataOptions";
import {
  MediaConfigurationItem,
  MediaIncubationConditionOption,
  MediaProductOption,
} from "../types/mediaConfigurationTypes";
import { OrganismOption } from "../../../../hooks/useOrganisms";
import { MediaConfigurationDialog } from "../dialogs/MediaConfigurationDialog";

interface MediaConfigurationsSectionProps {
  product: MediaProductOption;
  conditions: MediaIncubationConditionOption[];
  configurations: MediaConfigurationItem[];
  organisms: OrganismOption[];
  onUpdated: (successMessage?: string) => void;
  isManager: boolean;
}

export function MediaConfigurationsSection({
  product,
  conditions,
  configurations,
  organisms,
  onUpdated,
  isManager,
}: MediaConfigurationsSectionProps) {
  const { t } = useTranslation("labConfig");
  const [expandedRows, setExpandedRows] = useState<Record<number, boolean>>({});
  const [configDialogOpen, setConfigDialogOpen] = useState(false);
  const [editingConfig, setEditingConfig] = useState<MediaConfigurationItem | null>(null);
  const [pendingDeleteConfig, setPendingDeleteConfig] = useState<MediaConfigurationItem | null>(null);
  const [error, setError] = useState<string | null>(null);

  const toggleRow = (id: number) => {
    setExpandedRows((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const handleOpenAdd = () => {
    setEditingConfig(null);
    setConfigDialogOpen(true);
  };

  const handleOpenEdit = (config: MediaConfigurationItem) => {
    setEditingConfig(config);
    setConfigDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!pendingDeleteConfig) return;
    setError(null);
    try {
      await masterDataOptions.deleteMediaConfiguration(pendingDeleteConfig.id);
      setPendingDeleteConfig(null);
      onUpdated(
        t("media.configurationsSection.deleteSuccess", {
          name: product.name,
          evaluationType: evaluationTypeLabel(pendingDeleteConfig.evaluationType)
        })
      );
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? t("media.configurationsSection.deleteError"));
      setPendingDeleteConfig(null);
    }
  };

  return (
    <Box>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "center",
          mb: 2,
        }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
          {t("media.configurationsSection.title")}
        </Typography>
        {/* A medium has a single evaluation configuration - edit it rather than adding another. */}
        {isManager && configurations.length === 0 && (
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            {t("media.configurationsSection.addConfiguration")}
          </Button>
        )}
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {configurations.length === 0 ? (
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
              ? t("media.configurationsSection.emptyManagerDesc")
              : t("media.configurationsSection.emptyDesc")}
          </Typography>
        </Paper>
      ) : (
        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell sx={{ width: 40 }} />
                <TableCell>{t("media.configurationsSection.columns.evaluationType")}</TableCell>
                <TableCell>{t("media.configurationsSection.columns.incubation")}</TableCell>
                <TableCell>{t("media.configurationsSection.columns.temperature")}</TableCell>
                <TableCell>{t("media.configurationsSection.columns.recovery")}</TableCell>
                <TableCell>{t("media.configurationsSection.columns.challengeOrganisms")}</TableCell>
                {isManager && <TableCell align="right">{t("media.configurationsSection.columns.actions")}</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {configurations.map((config) => {
                const isExpanded = !!expandedRows[config.id];
                const challengeCount = config.challenges?.length ?? 0;

                return (
                  <Fragment key={config.id}>
                    <TableRow hover sx={{ "& > *": { borderBottom: isExpanded ? "unset" : undefined } }}>
                      <TableCell sx={{ width: 40, p: 0.5 }}>
                        {challengeCount > 0 && (
                          <IconButton
                            aria-label={
                              isExpanded
                                ? t("media.configurationsSection.collapseAriaLabel")
                                : t("media.configurationsSection.expandAriaLabel")
                            }
                            aria-expanded={Boolean(isExpanded)}
                            size="small"
                            onClick={() => toggleRow(config.id)}
                          >
                            {isExpanded ? <KeyboardArrowUpIcon fontSize="small" /> : <KeyboardArrowDownIcon fontSize="small" />}
                          </IconButton>
                        )}
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>
                        <Chip
                          size="small"
                          label={evaluationTypeLabel(config.evaluationType)}
                          variant="outlined"
                          sx={{ fontSize: 12 }}
                        />
                      </TableCell>
                      <TableCell>
                        {config.incubationMinHours}–{config.incubationMaxHours} h
                      </TableCell>
                      <TableCell>
                        {config.temperatureMin}–{config.temperatureMax} °C
                      </TableCell>
                      <TableCell>
                        {config.recoveryPercentMin != null && config.recoveryPercentMax != null
                          ? `${config.recoveryPercentMin}–${config.recoveryPercentMax}%`
                          : "—"}
                      </TableCell>
                      <TableCell>
                        {challengeCount > 0 ? (
                          <Chip
                            size="small"
                            label={t("media.configurationsSection.organismCount", { count: challengeCount })}
                            onClick={() => toggleRow(config.id)}
                            sx={{ cursor: "pointer", fontWeight: 600, fontSize: 12 }}
                          />
                        ) : (
                          <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
                            —
                          </Typography>
                        )}
                      </TableCell>
                      {isManager && (
                        <TableCell align="right">
                          <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                            <Tooltip title={t("media.configurationsSection.editTitle")}>
                              <IconButton size="small" onClick={() => handleOpenEdit(config)}>
                                <EditIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                            <Tooltip title={t("media.configurationsSection.deleteTitle")}>
                              <IconButton
                                size="small"
                                color="error"
                                onClick={() => setPendingDeleteConfig(config)}
                              >
                                <DeleteOutlineIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          </Stack>
                        </TableCell>
                      )}
                    </TableRow>

                    {challengeCount > 0 && (
                      <TableRow>
                        <TableCell style={{ paddingBottom: 0, paddingTop: 0 }} colSpan={isManager ? 7 : 6}>
                          <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                            <Box sx={{ p: 2, bgcolor: "background.default", borderRadius: 1, my: 1 }}>
                              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1, fontSize: 12 }}>
                                {t("media.configurationsSection.columns.challengeOrganisms")}
                              </Typography>
                              <TableContainer>
                                <Table size="small">
                                  <TableHead>
                                    <TableRow sx={tableHeadSx}>
                                      <TableCell>{t("media.configurationsSection.innerColumns.scientificName")}</TableCell>
                                      <TableCell>{t("media.configurationsSection.innerColumns.atccNumber")}</TableCell>
                                      <TableCell>{t("media.configurationsSection.innerColumns.role")}</TableCell>
                                      <TableCell>{t("media.configurationsSection.innerColumns.expectedDescription")}</TableCell>
                                      <TableCell>{t("media.configurationsSection.innerColumns.initialInoculum")}</TableCell>
                                    </TableRow>
                                  </TableHead>
                                  <TableBody>
                                    {config.challenges.map((c) => (
                                      <TableRow key={c.id}>
                                        <TableCell sx={{ fontWeight: 500 }}>
                                          {c.organism?.scientificName ??
                                            t("media.configurationsSection.organismFallback", { id: c.organismId })}
                                        </TableCell>
                                        <TableCell>{c.organism?.atccNumber ?? "—"}</TableCell>
                                        <TableCell>{c.challengeRole ?? "—"}</TableCell>
                                        <TableCell>{c.expectedDescription ?? "—"}</TableCell>
                                        <TableCell>{c.initialInoculum ?? "—"}</TableCell>
                                      </TableRow>
                                    ))}
                                  </TableBody>
                                </Table>
                              </TableContainer>
                            </Box>
                          </Collapse>
                        </TableCell>
                      </TableRow>
                    )}
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Confirmation Dialog for Delete */}
      <ConfirmationDialog
        open={pendingDeleteConfig != null}
        message={
          pendingDeleteConfig
            ? t("media.configurationsSection.deleteConfirm", {
                name: product.name,
                evaluationType: evaluationTypeLabel(pendingDeleteConfig.evaluationType)
              })
            : ""
        }
        onCancel={() => setPendingDeleteConfig(null)}
        onConfirm={handleDeleteConfirm}
      />

      {/* Dialog for Add / Edit Media Configuration */}
      <MediaConfigurationDialog
        open={configDialogOpen}
        product={product}
        configToEdit={editingConfig}
        conditions={conditions}
        organisms={organisms}
        onClose={() => setConfigDialogOpen(false)}
        onSuccess={() => {
          setConfigDialogOpen(false);
          onUpdated(
            editingConfig
              ? t("media.configurationsSection.updateSuccess")
              : t("media.configurationsSection.addSuccess")
          );
        }}
      />
    </Box>
  );
}
