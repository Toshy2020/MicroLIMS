import {
  Button, Box, Typography,
  Grid, Paper, Table, TableHead, TableRow, TableCell, TableBody, Chip,
  Stack, useTheme,
  TableContainer
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import BiocontrolIcon from "@mui/icons-material/Coronavirus";
import AcUnitIcon from "@mui/icons-material/AcUnit";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import HistoryToggleOffIcon from "@mui/icons-material/HistoryToggleOff";
import HubIcon from "@mui/icons-material/Hub";
import { ReferenceStrainDetail } from "../types/referenceStrainTypes";
import { brandColors } from "../../../theme";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";

interface ReferenceStrainDetailDialogProps {
  open: boolean;
  onClose: () => void;
  detail: ReferenceStrainDetail | null;
}

function formatDate(iso?: string | null): string {
  if (!iso || iso.startsWith("0001")) return "—";
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

export function ReferenceStrainDetailDialog({ open, onClose, detail }: ReferenceStrainDetailDialogProps) {
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
            <BiocontrolIcon sx={{ color: theme.palette.primary.main }} />
            <Box>
              <Typography sx={{ fontSize: 17, fontWeight: 800, color: theme.palette.primary.main }}>
                {t("referenceStrain.detail.title")}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
                {t("referenceStrain.detail.subtitle")}<strong>{detail.cryovialCode}</strong> • {detail.strainName} {detail.atccNumber ? `(ATCC ${detail.atccNumber})` : ""}
              </Typography>
            </Box>
          </Box>
          <Stack direction="row" spacing={1} sx={{ mr: 1 }}>
            <Chip
              label={detail.approvalStatus}
              color={
                detail.approvalStatus === "Approved"
                  ? "success"
                  : detail.approvalStatus === "Rejected"
                  ? "error"
                  : "warning"
              }
              sx={{ fontWeight: 700 }}
            />
            {detail.isDestroyed && (
              <Chip label={t("referenceStrain.detail.destroyed")} color="error" variant="outlined" sx={{ fontWeight: 700 }} />
            )}
          </Stack>
        </Box>
      }
      actions={
        <Button onClick={onClose} variant="outlined" startIcon={<CloseIcon />}>
          {t("referenceStrain.detail.close")}
        </Button>
      }
    >
      {/* Batch Identity & Source Provenance */}
      <Paper sx={{ p: 2, mb: 2.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
          {t("referenceStrain.detail.section1")}
        </Typography>
        <Grid container spacing={2}>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.strainOrganism")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
              {detail.strainName} {detail.atccNumber ? `(ATCC ${detail.atccNumber})` : ""}
            </Typography>
            {detail.strainCode && (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("referenceStrain.detail.strainCodeLine", {
                  code: detail.strainCode,
                  collection: detail.cultureCollection,
                  number: detail.strainNumber ? ` ${detail.strainNumber}` : ""
                })}
              </Typography>
            )}
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.sourceDisc")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.sourceMaterialName} {t("referenceStrain.detail.batchLabel", { batch: detail.sourceMaterialBatchNumber || "—" })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.discReceipt")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {formatDate(detail.sourceMaterialReceivingDate)} {t("referenceStrain.detail.receivedCount", { count: detail.sourceMaterialQuantityReceived })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.manufacturerName")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {detail.manufacturerName || "—"}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.prepExpiryDate")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
              {formatDate(detail.preparedAt)} • {t("referenceStrain.detail.expPrefix", { date: formatDate(detail.expiryDate) })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.vialsPreparedRemaining")}</Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: detail.vialsRemaining > 0 ? brandColors.ok : brandColors.err }}>
              {t("referenceStrain.detail.vialsSummary", { remaining: detail.vialsRemaining, prepared: detail.numberOfVialsPrepared })}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.storagePhysicalCheck")}</Typography>
            <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
              {detail.storageCondition || "—"} {detail.physicalCheckConfirmed ? t("referenceStrain.detail.physicalCheckConfirmed") : (detail.physicalCheckText ? `(${detail.physicalCheckText})` : "")} {detail.physicalCheckConfirmed && detail.physicalCheckText ? t("referenceStrain.detail.notesPrefix", { notes: detail.physicalCheckText }) : ""}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.preparedBy")}</Typography>
            <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
              {detail.preparedByName}, {formatDateTime(detail.preparedAt)}
            </Typography>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 4
            }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.detail.approvedBy")}</Typography>
            <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
              {detail.approvedByName ? t("referenceStrain.detail.approvedByOn", { name: detail.approvedByName, date: formatDateTime(detail.approvedAt) }) : t("referenceStrain.detail.pendingApproval")}
            </Typography>
          </Grid>
        </Grid>
      </Paper>

      {/* Identity Confirmation Panel */}
      <Box sx={{ mb: 2.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
          <FactCheckIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
          <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
            {t("referenceStrain.detail.section2", { count: detail.identityConfirmations.length })}
          </Typography>
        </Box>

        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.mediaLotVerified")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.incubator")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.incubationWindow")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.purityMorphology")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.identityConfirmations.map((i) => (
                <TableRow key={i.id} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>
                    {i.mediaLotNumber || "—"} {i.mediaName ? `(${i.mediaName})` : ""}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{i.incubatorName || "—"}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {formatDate(i.incubationStart)} – {formatDate(i.incubationEnd)}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    <strong>{i.observationText}</strong>
                  </TableCell>
                </TableRow>
              ))}
              {detail.identityConfirmations.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} sx={{ textAlign: "center", py: 2, color: "text.secondary", fontSize: 12 }}>
                    {t("referenceStrain.detail.noIdentityConfirmations")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>

      {/* Thaw History */}
      <Box sx={{ mb: 2.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
          <HistoryToggleOffIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
          <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
            {t("referenceStrain.detail.section3", { count: detail.thawHistory.length })}
          </Typography>
        </Box>

        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.thawedDateTime")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.thawedByAnalyst")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.notesPurpose")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.thawHistory.map((thaw) => (
                <TableRow key={thaw.id} hover>
                  <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{formatDateTime(thaw.thawedAt)}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{thaw.thawedByName}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{thaw.notes || "—"}</TableCell>
                </TableRow>
              ))}
              {detail.thawHistory.length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} sx={{ textAlign: "center", py: 2, color: "text.secondary", fontSize: 12 }}>
                    {t("referenceStrain.detail.noThawEvents")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>

      {/* Usage Log: Primary (Direct GPT Challenges) */}
      <Box sx={{ mb: 2.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
          <AcUnitIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
          <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
            {t("referenceStrain.detail.section4", { count: detail.directUsageLog.length })}
          </Typography>
        </Box>

        <TableContainer>
          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.mediaLotQualified")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.mediaType")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.evaluationType")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.challengeRole")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.outcome")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("referenceStrain.detail.readByDate")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.directUsageLog.map((u) => (
                <TableRow key={u.challengeId} hover>
                  <TableCell sx={{ fontWeight: 700, color: theme.palette.primary.main, fontSize: 12 }}>
                    {u.mediaLotNumber}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{u.mediaType}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{u.evaluationType}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{u.challengeRole || t("referenceStrain.detail.standardChallenge")}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={u.outcome || t("referenceStrain.detail.pending")}
                      color={u.outcome === "Conform" ? "success" : u.outcome === "NonConform" ? "error" : "default"}
                      sx={{ fontWeight: 700, fontSize: 12, height: 20 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {u.readByName ? `${u.readByName} (${formatDateTime(u.readAt)})` : t("referenceStrain.detail.pending")}
                  </TableCell>
                </TableRow>
              ))}
              {detail.directUsageLog.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: "center", py: 2, color: "text.secondary", fontSize: 12 }}>
                    {t("referenceStrain.detail.noUsageLog")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>

      {/* Secondary Indirect Rollup */}
      <Paper sx={{ p: 2, bgcolor: theme.custom.status.purple.bg, border: "1px solid", borderColor: theme.custom.status.purple.text, borderRadius: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1.5 }}>
          <HubIcon sx={{ color: theme.palette.primary.main, fontSize: 24, mt: 0.25 }} />
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 800, color: theme.palette.primary.main, mb: 0.5 }}>
              {t("referenceStrain.detail.downstreamTitle")}
            </Typography>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
              {detail.indirectUsageSummary}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 0.5 }}>
              <Trans
                i18nKey="referenceStrain.detail.downstreamStats"
                ns="reports"
                values={{ lots: detail.distinctQualifiedMediaLotsCount, orders: detail.indirectTestOrdersCount }}
                components={{ strong: <strong /> }}
              />
            </Typography>
          </Box>
        </Box>
      </Paper>
    </FloatingDialog>
  );
}
