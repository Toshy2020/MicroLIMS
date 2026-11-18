import {
  Button, Box, Typography,
  Grid, Paper, Table, TableHead, TableRow, TableCell, TableBody, Chip, useTheme,
  TableContainer
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ScienceIcon from "@mui/icons-material/Science";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import { MediaGptDetail } from "../types/mediaGptTypes";
import { brandColors } from "../../../theme";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";

interface MediaGptDetailDialogProps {
  open: boolean;
  onClose: () => void;
  detail: MediaGptDetail | null;
}

function formatDate(iso?: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

function formatDateTime(iso?: string | null): string {
  if (!iso) return "—";
  return formatDateTimeWithTimeZone(iso);
}

export function MediaGptDetailDialog({ open, onClose, detail }: MediaGptDetailDialogProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();

  if (!detail) return null;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1 }}
      title={
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flex: 1, minWidth: 0 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <ScienceIcon sx={{ color: theme.palette.primary.main }} />
            <Box>
              <Typography sx={{ fontSize: 17, fontWeight: 800, color: theme.palette.primary.main }}>
                {t("mediaGpt.detail.title")}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
                {t("mediaGpt.detail.lotNumber")} <strong>{detail.lotNumber}</strong> • {detail.mediaType}
              </Typography>
            </Box>
          </Box>
          <Chip
            label={detail.evaluationOutcome || detail.evaluationStatus}
            color={
              detail.evaluationOutcome === "Conform"
                ? "success"
                : detail.evaluationOutcome === "NonConform"
                ? "error"
                : "warning"
            }
            sx={{ fontWeight: 700, mr: 1 }}
          />
        </Box>
      }
      actions={
        <Button onClick={onClose} variant="outlined" startIcon={<CloseIcon />}>
          {t("mediaGpt.detail.close")}
        </Button>
      }
    >
      {/* Lot Identity & Preparation Metadata */}
      <Paper sx={{ p: 2, mb: 2.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
          {t("mediaGpt.detail.section1")}
        </Typography>
        <Grid container spacing={2}>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.mediaTypeName")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 700 }}>{detail.mediaType}</Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.sourceMaterial")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.manufacturerName} {t("mediaGpt.detail.lotLabel", { lot: detail.manufacturerLot || "—" })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.prepExpiryDate")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {formatDate(detail.preparedAt)} • {t("mediaGpt.detail.expPrefix", { date: formatDate(detail.expiryDate) })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.totalWeightVolume")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.totalWeight} g / {detail.totalVolume}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.autoclaveProgramLoad")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.autoclaveName || "—"} ({detail.autoclaveProgram}) • {detail.loadType || "—"}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.tempCyclePh")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.temperature}°C ({detail.cycleTime} {t("mediaGpt.detail.cycleUnit")}, #{detail.cycleNumber}) • pH {detail.ph}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.preparedBy")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.preparedByName}, {formatDateTime(detail.preparedAt)}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("mediaGpt.detail.releaseStatusDecidedBy")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              <span style={{ color: detail.isReleasedForUse ? brandColors.ok : "inherit" }}>
                {detail.approvalStatus} {detail.isReleasedForUse && t("mediaGpt.detail.releasedForRoutine")}
              </span>
              {detail.approvedByName && ` ${t("mediaGpt.detail.decidedByOn", { name: detail.approvedByName, date: formatDateTime(detail.approvedAt) })}`}
            </Typography>
          </Grid>
        </Grid>
      </Paper>

      {/* Evaluation Summary */}
      <Box sx={{ mb: 2 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
            {t("mediaGpt.detail.section2", { count: detail.challenges.length })}
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {t("mediaGpt.detail.evaluationTypeLabel")} <strong>{detail.evaluationType}</strong>
            {detail.evaluationCompletedByName && ` ${t("mediaGpt.detail.completedByOn", { name: detail.evaluationCompletedByName, date: formatDateTime(detail.evaluationCompletedAt) })}`}
          </Typography>
        </Box>

        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.organismAtcc")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.strainSource")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.inoculum")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.actualResults")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.acceptanceCriteria")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.outcome")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("mediaGpt.detail.columns.analystReadDate")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.challenges.map((c) => {
                const isConform = c.outcome === "Conform";
                const isNonConform = c.outcome === "NonConform";

                return (
                  <TableRow key={c.id} hover>
                    <TableCell>
                      <Typography sx={{ fontSize: 12.5, fontWeight: 700 }}>{c.organismName}</Typography>
                      {c.atccNumber && (
                        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                          ATCC {c.atccNumber} {c.challengeRole ? `(${c.challengeRole})` : ""}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {c.strainSource || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {c.initialInoculum}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {/* GPT: Recovery % */}
                      {c.recoveryPercent !== null && (
                        <Box>
                          <strong>{c.recoveryPercent}%</strong> {t("mediaGpt.detail.recovery")}
                          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                            {t("mediaGpt.detail.newOldRef", { newCount: c.newMediaCount, oldCount: c.oldMediaCount, refLot: c.referenceMediaLot || "—" })}
                          </Typography>
                        </Box>
                      )}
                      {/* Inhibition: Growth observed */}
                      {c.growthObserved !== null && (
                        <Box>
                          {t("mediaGpt.detail.growthLabel")} <strong>{c.growthObserved ? t("mediaGpt.detail.observed") : t("mediaGpt.detail.noGrowth")}</strong>
                        </Box>
                      )}
                      {/* Indication: Observed Description */}
                      {c.observedDescription && (
                        <Box>
                          {t("mediaGpt.detail.observedLabel")} <em>{c.observedDescription}</em>
                        </Box>
                      )}
                      {/* Enrichment: Turbid */}
                      {c.isTurbid !== null && (
                        <Box>
                          {t("mediaGpt.detail.appearanceLabel")} <strong>{c.isTurbid ? t("mediaGpt.detail.turbid") : t("mediaGpt.detail.clear")}</strong>
                        </Box>
                      )}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {c.expectedMinRecoveryPercent !== null && c.expectedMaxRecoveryPercent !== null && (
                        <span>{c.expectedMinRecoveryPercent}% – {c.expectedMaxRecoveryPercent}%</span>
                      )}
                      {c.growthObserved !== null && <span>{t("mediaGpt.detail.noGrowthExpected")}</span>}
                      {c.expectedDescription && <span>{t("mediaGpt.detail.expectedLabel", { expected: c.expectedDescription })}</span>}
                      {c.isTurbid !== null && <span>{t("mediaGpt.detail.turbidGrowthExpected")}</span>}
                    </TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        icon={isConform ? <CheckCircleIcon sx={{ "&&": { fontSize: 14 } }} /> : isNonConform ? <CancelIcon sx={{ "&&": { fontSize: 14 } }} /> : undefined}
                        label={c.outcome || t("mediaGpt.detail.pending")}
                        color={isConform ? "success" : isNonConform ? "error" : "default"}
                        sx={{ fontWeight: 700, fontSize: 12, height: 22 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {c.readByName ? (
                        <>
                          <div>{c.readByName}</div>
                          <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
                            {formatDateTime(c.readAt)}
                          </Typography>
                        </>
                      ) : (
                        <Typography component="span" sx={{ color: "text.secondary" }}>{t("mediaGpt.detail.pending")}</Typography>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
              {detail.challenges.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} sx={{ textAlign: "center", py: 3, color: "text.secondary" }}>
                    {t("mediaGpt.detail.noChallenges")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    </FloatingDialog>
  );
}
