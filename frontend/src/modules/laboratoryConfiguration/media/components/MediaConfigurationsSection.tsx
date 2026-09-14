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
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../../theme";
import {
  masterDataOptions,
  evaluationTypeLabel,
} from "../../../../services/masterDataOptions";
import {
  MediaConfigurationItem,
  MediaProductOption,
} from "../types/mediaConfigurationTypes";
import { OrganismOption } from "../../../../hooks/useOrganisms";
import { MediaConfigurationDialog } from "../dialogs/MediaConfigurationDialog";

interface MediaConfigurationsSectionProps {
  product: MediaProductOption;
  configurations: MediaConfigurationItem[];
  organisms: OrganismOption[];
  onUpdated: (successMessage?: string) => void;
  isManager: boolean;
}

export function MediaConfigurationsSection({
  product,
  configurations,
  organisms,
  onUpdated,
  isManager,
}: MediaConfigurationsSectionProps) {
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
        `Media configuration for "${product.name}" (${evaluationTypeLabel(pendingDeleteConfig.evaluationType)}) deleted.`
      );
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "Could not delete this media configuration.");
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
          Configurations
        </Typography>
        {isManager && (
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            Add Configuration
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
              ? "No configurations yet. Add one to define how this medium is evaluated."
              : "No configurations yet."}
          </Typography>
        </Paper>
      ) : (
        <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell sx={{ width: 40 }} />
              <TableCell>Evaluation Type</TableCell>
              <TableCell>Incubation</TableCell>
              <TableCell>Temperature</TableCell>
              <TableCell>Recovery</TableCell>
              <TableCell>Challenge Organisms</TableCell>
              {isManager && <TableCell align="right">Actions</TableCell>}
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
                        <IconButton size="small" onClick={() => toggleRow(config.id)}>
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
                          label={`${challengeCount} organism${challengeCount > 1 ? "s" : ""}`}
                          onClick={() => toggleRow(config.id)}
                          sx={{ cursor: "pointer", fontWeight: 600, fontSize: 11 }}
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
                          <Tooltip title="Edit configuration">
                            <IconButton size="small" onClick={() => handleOpenEdit(config)}>
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Delete configuration">
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
                              Challenge Organisms
                            </Typography>
                            <Table size="small">
                              <TableHead>
                                <TableRow sx={tableHeadSx}>
                                  <TableCell>Scientific Name</TableCell>
                                  <TableCell>ATCC Number</TableCell>
                                  <TableCell>Role</TableCell>
                                  <TableCell>Expected Description</TableCell>
                                  <TableCell>Initial Inoculum</TableCell>
                                </TableRow>
                              </TableHead>
                              <TableBody>
                                {config.challenges.map((c) => (
                                  <TableRow key={c.id}>
                                    <TableCell sx={{ fontWeight: 500 }}>
                                      {c.organism?.scientificName ?? `Organism #${c.organismId}`}
                                    </TableCell>
                                    <TableCell>{c.organism?.atccNumber ?? "—"}</TableCell>
                                    <TableCell>{c.challengeRole ?? "—"}</TableCell>
                                    <TableCell>{c.expectedDescription ?? "—"}</TableCell>
                                    <TableCell>{c.initialInoculum ?? "—"}</TableCell>
                                  </TableRow>
                                ))}
                              </TableBody>
                            </Table>
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
      )}

      {/* Confirmation Dialog for Delete */}
      <ConfirmationDialog
        open={pendingDeleteConfig != null}
        message={
          pendingDeleteConfig
            ? `Delete media configuration for "${product.name}" (${evaluationTypeLabel(pendingDeleteConfig.evaluationType)})? This cannot be undone.`
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
        organisms={organisms}
        onClose={() => setConfigDialogOpen(false)}
        onSuccess={() => {
          setConfigDialogOpen(false);
          onUpdated(
            editingConfig
              ? "Media configuration updated successfully."
              : "Media configuration created successfully."
          );
        }}
      />
    </Box>
  );
}
