import { useEffect, useState } from "react";
import {
  Box,
  Paper,
  Typography,
  Stack,
  Button,
  Divider,
  Tabs,
  Tab,
  Alert,
  Tooltip,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  useTheme,
  TableContainer
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import HowToRegOutlinedIcon from "@mui/icons-material/HowToRegOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { Link } from "react-router-dom";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { StatusBadge } from "../../../../components/StatusBadge";
import { evaluationTypeLabel } from "../../../../services/masterDataOptions";
import { workflowEventLabel } from "../../../../utils/workflowEventLabel";
import { MediaSummary } from "../types/mediaSummaryTypes";
import { apiClient } from "../../../../services/apiClient";
import { lifecycleOf } from "./MediaLotKpiCards";
import { MediaStoragePanel } from "./MediaStoragePanel";
import { PreparationWhereUsedPanel } from "../../../testPreparation/components/PreparationWhereUsedPanel";
import { tableHeadSx } from "../../../../theme";
import { useAuth } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { GatedAction } from "../../../../components/GatedAction";
import { useActionAvailability } from "../../../../hooks/useActionAvailability";
import { PrintLabelButton } from "../../../../components/labels/PrintLabelButton";
import { useTrackRecent } from "../../../navigation/useTrackRecent";

function formatDateDDMMYY(value: string | number | Date | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "—";
  const day = String(d.getDate()).padStart(2, "0");
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const year = String(d.getFullYear()).slice(-2);
  return `${day}/${month}/${year}`;
}

function formatDateTimeDDMMYY(value: string | number | Date | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "—";
  const day = String(d.getDate()).padStart(2, "0");
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const year = String(d.getFullYear()).slice(-2);
  const hours = String(d.getHours()).padStart(2, "0");
  const minutes = String(d.getMinutes()).padStart(2, "0");
  return `${day}/${month}/${year} ${hours}:${minutes}`;
}

interface Props {
  lot: any;
  awaitingApprovalIds: Set<number>;
  onClose: () => void;
  onViewRecord: (lotId: number) => void;
  onViewAuditHistory: (lotId: number) => void;
  onOpenEvaluation: (evaluationId: number) => void;
  onRequestReleaseDecision: (lot: any, approved: boolean) => void;
  onMarkOutOfStock?: (lot: any) => void;
  evaluationsList: any[];
}

export function SelectedMediaLotWorkspace({
  lot,
  awaitingApprovalIds,
  onClose,
  onViewRecord,
  onViewAuditHistory,
  onOpenEvaluation,
  onRequestReleaseDecision,
  onMarkOutOfStock,
  evaluationsList
}: Props) {
  const { t } = useTranslation("labConfig");
  const { permissions } = useAuth();
  const theme = useTheme();
  useTrackRecent("media", lot?.id);
  // Rejecting a lot has no availability answer; it keeps the permission it always had.
  const canRelease = permissions.includes(PERMISSIONS.MEDIA_RELEASE);
  const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
  // The server's answer about releasing this lot (and why not), asked only while it awaits approval.
  const availability = useActionAvailability("media", lifecycle === "Awaiting Approval" ? (lot?.id ?? null) : null);

  const [activeTab, setActiveTab] = useState(0);
  const [summary, setSummary] = useState<MediaSummary | null>(null);
  // Without this a failed summary showed "—" for preparer, autoclave and
  // approver - which reads as "not recorded" on a GMP lot record.
  const [summaryFailed, setSummaryFailed] = useState(false);

  useEffect(() => {
    if (!lot?.id) return;
    setSummaryFailed(false);
    apiClient
      .get(`/media/${lot.id}/summary`)
      .then((r) => setSummary(r.data.data))
      .catch(() => {
        setSummary(null);
        setSummaryFailed(true);
      });
  }, [lot?.id]);

  // Evaluations matching this media lot
  const matchingEvaluations = evaluationsList.filter((e) => e.mediaId === lot.id || e.media?.id === lot.id);

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        border: "1.5px solid",
        borderColor: "divider",
        borderRadius: 2.5,
        bgcolor: "background.paper",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 2,
        overflowY: "auto"
      }}
    >
      {summaryFailed && (
        <Alert severity="error">
          {t("media.selectedLotWorkspace.summaryFailed")}
        </Alert>
      )}
      {/* Header: Lot Number, Media Type, Status, and Actions */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1.5 }}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main, lineHeight: 1.2 }}>
              {lot.lotNumber}
            </Typography>
            {(lot.material?.materialName || summary?.materialName) && (
              <Typography sx={{ fontSize: 14, color: "text.secondary", fontWeight: 600 }}>
                {lot.material?.materialName || summary?.materialName}
              </Typography>
            )}
            <StatusBadge status={lifecycle} />
          </Box>

          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {t("media.selectedLotWorkspace.preparedMeta", {
              prepared: formatDateDDMMYY(lot.preparedAt),
              expiry: formatDateDDMMYY(lot.expiryDate),
              id: lot.id
            })}
          </Typography>
        </Box>

        <Tooltip title={t("media.selectedLotWorkspace.closeTooltip")}>
          <Button
            size="small"
            variant="outlined"
            onClick={onClose}
            startIcon={<CloseIcon sx={{ fontSize: 16 }} />}
            sx={{
              borderColor: "divider",
              color: "text.secondary",
              fontWeight: 600,
              fontSize: 12,
              whiteSpace: "nowrap",
              "&:hover": { borderColor: "text.secondary", bgcolor: "background.default" }
            }}
          >
            {t("media.selectedLotWorkspace.deselect")}
          </Button>
        </Tooltip>
      </Box>

      {/* Action Toolbar */}
      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        sx={{
          flexWrap: "wrap",
          pt: 0.5
        }}>
        <Button
          component={Link}
          to={`/media/${lot.id}/report`}
          target="_blank"
          rel="noopener"
          size="small"
          variant="outlined"
          startIcon={<DescriptionOutlinedIcon sx={{ fontSize: 16 }} />}
          sx={{
            borderColor: theme.custom.status.info.border,
            color: theme.custom.status.info.text,
            fontSize: 12,
            fontWeight: 600,
            bgcolor: theme.custom.status.info.bg,
            "&:hover": { bgcolor: theme.custom.status.info.border, borderColor: theme.custom.status.info.text }
          }}
        >
          {t("media.selectedLotWorkspace.viewLotRecord")}
        </Button>

        <Button
          size="small"
          variant="outlined"
          startIcon={<HistoryOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={() => onViewAuditHistory(lot.id)}
          sx={{
            borderColor: "divider",
            color: "text.secondary",
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "background.paper",
            "&:hover": { bgcolor: "background.default" }
          }}
        >
          {t("media.selectedLotWorkspace.auditHistory")}
        </Button>

        <PrintLabelButton
          target={{
            entityType: "MediaLot",
            entityId: lot.id,
            code: lot.lotNumber ?? "",
            displayName: lot.material?.materialName || summary?.materialName || lot.lotNumber || t("media.selectedLotWorkspace.mediaLotFallback")
          }}
        />

        {lifecycle === "Awaiting Approval" && (
          <>
            <GatedAction action="media.release" availability={availability}>
              <Button
                size="small"
                variant="contained"
                color="success"
                startIcon={<HowToRegOutlinedIcon sx={{ fontSize: 16 }} />}
                onClick={() => onRequestReleaseDecision(lot, true)}
                sx={{ fontWeight: 700, fontSize: 12 }}
              >
                {t("media.selectedLotWorkspace.releaseLot")}
              </Button>
            </GatedAction>
            {canRelease && (
              <Button
                size="small"
                variant="outlined"
                color="error"
                startIcon={<BlockOutlinedIcon sx={{ fontSize: 16 }} />}
                onClick={() => onRequestReleaseDecision(lot, false)}
                sx={{ fontWeight: 700, fontSize: 12 }}
              >
                {t("media.selectedLotWorkspace.rejectLot")}
              </Button>
            )}
          </>
        )}

        {lifecycle === "Released" && onMarkOutOfStock && (
          <Button
            size="small"
            variant="outlined"
            color="warning"
            startIcon={<InventoryOutlinedIcon sx={{ fontSize: 16 }} />}
            onClick={() => onMarkOutOfStock(lot)}
            sx={{ fontWeight: 700, fontSize: 12 }}
          >
            {t("media.selectedLotWorkspace.markOutOfStock")}
          </Button>
        )}
      </Stack>

      <Divider />

      {/* Tabs Bar */}
      <Tabs
        value={activeTab}
        onChange={(_, val) => setActiveTab(val)}
        sx={{
          minHeight: 38,
          "& .MuiTab-root": {
            minHeight: 38,
            py: 0.75,
            fontSize: 13,
            fontWeight: 600,
            textTransform: "none"
          },
          "& .Mui-selected": {
            color: theme.palette.primary.main,
            fontWeight: 700
          },
          "& .MuiTabs-indicator": {
            backgroundColor: theme.palette.primary.main
          }
        }}
      >
        <Tab label={t("media.selectedLotWorkspace.tabs.preparationOverview")} />
        <Tab label={t("media.selectedLotWorkspace.tabs.evaluations", {
            count: matchingEvaluations.length || (summary?.evaluation ? 1 : 0)
          })} />
        <Tab label={t("media.selectedLotWorkspace.tabs.releaseAuditTrail")} />
        {lot.isReleasedForUse && <Tab label={t("media.selectedLotWorkspace.tabs.storage")} />}
      </Tabs>

      {/* TAB 3: STORAGE (released lots only) */}
      {activeTab === 3 && lot.isReleasedForUse && <MediaStoragePanel lot={lot} />}

      {/* TAB 0: OVERVIEW */}
      {activeTab === 0 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          {/* Section 1: Preparation Info */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              {t("media.selectedLotWorkspace.preparationDetailsHeading")}
            </Typography>
            <Box
              sx={{
                p: 1.5,
                borderRadius: 2,
                bgcolor: "background.default",
                border: "1px solid",
                borderColor: "divider",
                display: "grid",
                gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)" },
                gap: 1.5
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.preparedDate")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateDDMMYY(lot.preparedAt)}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.dehydratedStock")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.material?.materialName || summary?.materialName || "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.manufacturerBatch")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.material
                    ? t("media.selectedLotWorkspace.manufacturerBatchValue", {
                        manufacturer: lot.material.manufacturerName || "—",
                        batch: lot.material.batchNumber
                      })
                    : summary?.manufacturerName
                    ? t("media.selectedLotWorkspace.manufacturerBatchValue", {
                        manufacturer: summary.manufacturerName,
                        batch: summary.manufacturerLot
                      })
                    : "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.totalWeight")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {t("media.selectedLotWorkspace.weightWithUnit", { weight: lot.totalWeight })}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.totalVolume")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.totalVolume}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.phAt25")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.ph}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.preparedBy")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {summary?.preparedByName || "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.preparedOn")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateTimeDDMMYY(lot.preparedAt)}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.expiryDate")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateDDMMYY(lot.expiryDate)}
                </Typography>
              </Box>
            </Box>
          </Box>

          {/* Section 2: Sterilization Info */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              {t("media.selectedLotWorkspace.sterilizationHeading")}
            </Typography>
            <Box
              sx={{
                p: 1.5,
                borderRadius: 2,
                bgcolor: "background.default",
                border: "1px solid",
                borderColor: "divider",
                display: "grid",
                gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)" },
                gap: 1.5
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.autoclaveEquipment")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {summary?.autoclaveName || t("media.selectedLotWorkspace.autoclaveFallback", { id: lot.autoclaveEquipmentId })}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.programLoad")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.autoclaveProgram}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.loadType")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.loadType}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.temperature")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {t("media.selectedLotWorkspace.temperatureValue", { temp: lot.temperature })}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.cycleTime")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {t("media.selectedLotWorkspace.cycleTimeValue", { minutes: lot.cycleTime })}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.cycleNumber")}</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  #{lot.cycleNumber}
                </Typography>
              </Box>
            </Box>
          </Box>

          {summary && summary.additives?.length > 0 && (
            <Box
              sx={{ p: 1.5, borderRadius: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}
              data-testid="media-lot-additives"
            >
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.75 }}>{t("media.selectedLotWorkspace.additivesHeading")}</Typography>
              {summary.additives.map((a, i) => (
                <Typography key={i} sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {a.name} ({a.code}): {a.quantity} {a.unit}
                  <Box component="span" sx={{ fontWeight: 400, color: "text.secondary" }}>
                    {" "}· {a.kind === "StockLot" ? t("media.selectedLotWorkspace.additiveStockLot") : t("media.selectedLotWorkspace.additivePreparedSolution")}
                  </Box>
                </Typography>
              ))}
            </Box>
          )}

          <PreparationWhereUsedPanel lot={{ mediaId: lot.id }} />
        </Stack>
      )}

      {/* TAB 1: EVALUATIONS */}
      {activeTab === 1 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
              {t("media.selectedLotWorkspace.assignedEvaluations")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("media.selectedLotWorkspace.assignedEvaluationsSubtitle")}
            </Typography>
          </Box>

          {matchingEvaluations.length > 0 ? (
            matchingEvaluations.map((ev) => (
              <Paper
                key={ev.id}
                elevation={0}
                sx={{
                  p: 2,
                  borderRadius: 2,
                  border: "1.5px solid",
                  borderColor: "divider",
                  bgcolor: "background.paper"
                }}
              >
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
                  <Box>
                    <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                      {evaluationTypeLabel(ev.evaluationType)}
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {t("media.selectedLotWorkspace.evaluationMeta", { id: ev.id, date: formatDateDDMMYY(ev.assignedAt) })}
                    </Typography>
                  </Box>

                  <Box sx={{ display: "flex", gap: 1 }}>
                    <StatusBadge status={ev.status} />
                    {ev.outcome && <StatusBadge status={ev.outcome} />}
                  </Box>
                </Box>

                {/* Challenges Summary */}
                {ev.challenges && ev.challenges.length > 0 && (
                  <Box sx={{ mt: 1.5, mb: 1.5 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.75 }}>
                      {t("media.selectedLotWorkspace.challengesHeading")}
                    </Typography>
                    <TableContainer>
                      <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
                        <TableHead>
                          <TableRow sx={tableHeadSx}>
                            <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.organism")}</TableCell>
                            <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.cryovial")}</TableCell>
                            <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.incubation")}</TableCell>
                            <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.outcome")}</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {ev.challenges.map((c: any) => (
                            <TableRow key={c.id}>
                              <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                                {c.organism?.scientificName || c.organismName || "—"}
                                {c.challengeRole ? ` (${c.challengeRole})` : ""}
                              </TableCell>
                              <TableCell sx={{ fontSize: 12 }}>
                                {c.cryovial?.code || c.cryovialCode || t("media.selectedLotWorkspace.pending")}
                              </TableCell>
                              <TableCell sx={{ fontSize: 12 }}>
                                {c.incubation
                                  ? `${c.incubation.temperature}°C, ${c.incubation.duration}h`
                                  : c.temperature
                                  ? `${c.temperature}°C, ${c.duration}h`
                                  : t("media.selectedLotWorkspace.pending")}
                              </TableCell>
                              <TableCell sx={{ fontSize: 12 }}>
                                {c.outcome ? (
                                  <StatusBadge status={c.outcome} />
                                ) : (
                                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.pending")}</Typography>
                                )}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </TableContainer>
                  </Box>
                )}

                <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 1, borderTop: "1px solid", borderColor: "divider" }}>
                  <Button
                    size="small"
                    variant="contained"
                    endIcon={<ArrowForwardIcon sx={{ fontSize: 14 }} />}
                    onClick={() => onOpenEvaluation(ev.id)}
                    color="primary"
                    sx={{ fontSize: 12, fontWeight: 700 }}
                  >
                    {ev.status === "Completed" ? t("media.selectedLotWorkspace.viewEvaluationWorkflow") : t("media.selectedLotWorkspace.openContinueEvaluation")}
                  </Button>
                </Box>
              </Paper>
            ))
          ) : summary?.evaluation ? (
            <Paper
              elevation={0}
              sx={{
                p: 2,
                borderRadius: 2,
                border: "1.5px solid",
                borderColor: "divider",
                bgcolor: "background.paper"
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
                <Box>
                  <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                    {evaluationTypeLabel(summary.evaluation.evaluationType)}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {t("media.selectedLotWorkspace.assignedOn", { date: formatDateDDMMYY(summary.evaluation.assignedAt) })}
                  </Typography>
                </Box>

                <Box sx={{ display: "flex", gap: 1 }}>
                  <StatusBadge status={summary.evaluation.status} />
                  {summary.evaluation.outcome && <StatusBadge status={summary.evaluation.outcome} />}
                </Box>
              </Box>

              {/* Challenges Summary */}
              {summary.evaluation.challenges && summary.evaluation.challenges.length > 0 && (
                <Box sx={{ mt: 1.5, mb: 1.5 }}>
                  <TableContainer>
                    <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
                      <TableHead>
                        <TableRow sx={tableHeadSx}>
                          <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.organism")}</TableCell>
                          <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.cryovial")}</TableCell>
                          <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.incubation")}</TableCell>
                          <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.selectedLotWorkspace.columns.outcome")}</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {summary.evaluation.challenges.map((c, idx) => (
                          <TableRow key={idx}>
                            <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                              {c.organismName}
                              {c.challengeRole ? ` (${c.challengeRole})` : ""}
                            </TableCell>
                            <TableCell sx={{ fontSize: 12 }}>{c.cryovialCode || t("media.selectedLotWorkspace.pending")}</TableCell>
                            <TableCell sx={{ fontSize: 12 }}>
                              {c.temperature ? `${c.temperature}°C, ${c.duration}h` : t("media.selectedLotWorkspace.pending")}
                            </TableCell>
                            <TableCell sx={{ fontSize: 12 }}>
                              {c.outcome ? (
                                <StatusBadge status={c.outcome} />
                              ) : (
                                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.selectedLotWorkspace.pending")}</Typography>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                </Box>
              )}
            </Paper>
          ) : (
            <Alert severity="info">
              {t("media.selectedLotWorkspace.noEvaluationsInfo")}
            </Alert>
          )}
        </Stack>
      )}

      {/* TAB 2: RELEASE & AUDIT */}
      {activeTab === 2 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              {t("media.selectedLotWorkspace.releaseStatusHeading")}
            </Typography>

            <Paper
              elevation={0}
              sx={{
                p: 2,
                borderRadius: 2,
                border: "1px solid",
                borderColor: "divider",
                bgcolor: lot.isReleasedForUse
                  ? theme.custom.status.notDetected.bg
                  : lifecycle === "Rejected"
                  ? theme.custom.status.detected.bg
                  : "background.default"
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1 }}>
                <StatusBadge status={lifecycle} />
                <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                  {lot.isReleasedForUse
                    ? t("media.selectedLotWorkspace.releasedForRoutineTesting")
                    : lifecycle === "Awaiting Approval"
                    ? t("media.selectedLotWorkspace.awaitingSectionHeadSignature")
                    : lifecycle === "Rejected"
                    ? t("media.selectedLotWorkspace.rejectedFromRoutineUse")
                    : t("media.selectedLotWorkspace.pendingEvaluationCompletion")}
                </Typography>
              </Box>

              {lot.isReleasedForUse && summary?.approvedByName && (
                <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, mt: 0.5 }}>
                  <Trans
                    i18nKey="media.selectedLotWorkspace.electronicallySignedBy"
                    ns="labConfig"
                    values={{
                      name: summary.approvedByName,
                      date: summary.approvedAt ? formatDateTimeDDMMYY(summary.approvedAt) : "—"
                    }}
                    components={{ bold: <strong /> }}
                  />
                </Typography>
              )}

              {lifecycle === "Awaiting Approval" && (
                <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text, mt: 0.5 }}>
                  {t("media.selectedLotWorkspace.awaitingSignatureDesc")}
                </Typography>
              )}
            </Paper>
          </Box>

          {/* Signatures Trail */}
          {summary?.signatures && summary.signatures.length > 0 && (
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
                {t("media.selectedLotWorkspace.electronicSignaturesHeading")}
              </Typography>
              <Stack spacing={1}>
                {summary.signatures.map((sig, idx) => (
                  <Paper
                    key={idx}
                    variant="outlined"
                    sx={{ p: 1.5, borderRadius: 1.5, bgcolor: "background.default" }}
                  >
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                      {sig.printedName} ({sig.role})
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {t("media.selectedLotWorkspace.signedByOn", {
                        meaning: sig.meaning,
                        username: sig.username,
                        date: formatDateTimeDDMMYY(sig.signedAt)
                      })}
                    </Typography>
                    {sig.comment && (
                      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
                        {t("media.selectedLotWorkspace.commentPrefix", { comment: sig.comment })}
                      </Typography>
                    )}
                  </Paper>
                ))}
              </Stack>
            </Box>
          )}

          {/* Timeline */}
          {summary?.timeline && summary.timeline.length > 0 && (
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
                {t("media.selectedLotWorkspace.lifecycleTimelineHeading")}
              </Typography>
              <Stack spacing={1}>
                {summary.timeline.map((ev, idx) => (
                  <Box key={idx} sx={{ display: "flex", gap: 1.5, alignItems: "flex-start" }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary", width: 140, flexShrink: 0 }}>
                      {formatDateTimeDDMMYY(ev.timestamp)}
                    </Typography>
                    <Box>
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                        {t("media.selectedLotWorkspace.timelineEventBy", {
                          event: workflowEventLabel(ev.eventType, ev.decision, t),
                          user: ev.performedByName || t("media.selectedLotWorkspace.systemUser")
                        })}
                      </Typography>
                      {ev.comment && (
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                          {ev.comment}
                        </Typography>
                      )}
                    </Box>
                  </Box>
                ))}
              </Stack>
            </Box>
          )}
        </Stack>
      )}
    </Paper>
  );
}
