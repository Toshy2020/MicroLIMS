import {
  Button, Box, Typography,
  Grid, Paper, Table, TableHead, TableRow, TableCell, TableBody, Chip, Divider,
  Stack, Alert, useTheme
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
  if (!iso || iso.startsWith("0001")) return "—";
  try {
    const d = new Date(iso);
    return `${d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })} ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  } catch {
    return iso;
  }
}

export function ReferenceStrainDetailDialog({ open, onClose, detail }: ReferenceStrainDetailDialogProps) {
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
                Reference Strain Working Culture Batch Record
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
                Code: <strong>{detail.cryovialCode}</strong> • {detail.strainName} {detail.atccNumber ? `(ATCC ${detail.atccNumber})` : ""}
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
              <Chip label="Destroyed" color="error" variant="outlined" sx={{ fontWeight: 700 }} />
            )}
          </Stack>
        </Box>
      }
      actions={
        <Button onClick={onClose} variant="outlined" startIcon={<CloseIcon />}>
          Close
        </Button>
      }
    >
        {/* Batch Identity & Source Provenance */}
        <Paper sx={{ p: 2, mb: 2.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
            1. Batch Identity & Source Provenance
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Strain Organism</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                {detail.strainName} {detail.atccNumber ? `(ATCC ${detail.atccNumber})` : ""}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Source Microorganism Disc</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.sourceMaterialName} (Batch: {detail.sourceMaterialBatchNumber || "—"})
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Disc Receipt Date & Qty</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {formatDate(detail.sourceMaterialReceivingDate)} ({detail.sourceMaterialQuantityReceived} received)
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Manufacturer Name</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.manufacturerName || "—"}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Preparation / Expiry Date</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {formatDate(detail.preparedAt)} • Exp: {formatDate(detail.expiryDate)}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Vials Prepared / Remaining</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: detail.vialsRemaining > 0 ? brandColors.ok : brandColors.err }}>
                {detail.vialsRemaining} remaining of {detail.numberOfVialsPrepared} prepared
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Storage Condition / Physical Check</Typography>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
                {detail.storageCondition || "—"} {detail.physicalCheckConfirmed ? "• Physical check confirmed" : (detail.physicalCheckText ? `(${detail.physicalCheckText})` : "")} {detail.physicalCheckConfirmed && detail.physicalCheckText ? `(Notes: ${detail.physicalCheckText})` : ""}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Prepared By</Typography>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
                {detail.preparedByName} — {formatDateTime(detail.preparedAt)}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Approved By</Typography>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>
                {detail.approvedByName ? `${detail.approvedByName} on ${formatDate(detail.approvedAt)}` : "Pending Approval"}
              </Typography>
            </Grid>
          </Grid>
        </Paper>

        {/* Identity Confirmation Panel */}
        <Box sx={{ mb: 2.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <FactCheckIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
            <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
              2. Identity Confirmation Panel ({detail.identityConfirmations.length} Entries)
            </Typography>
          </Box>

          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Media Lot (Verified Against)</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Incubator</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Incubation Window</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Purity & Morphology Observation</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.identityConfirmations.map((i) => (
                <TableRow key={i.id} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>
                    {i.mediaLotNumber || "—"} {i.mediaName ? `(${i.mediaName})` : ""}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{i.incubatorName || "—"}</TableCell>
                  <TableCell sx={{ fontSize: 11.5 }}>
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
                    No identity confirmation panel rows recorded for this batch.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Box>

        {/* Thaw History */}
        <Box sx={{ mb: 2.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <HistoryToggleOffIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
            <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
              3. Thaw History ({detail.thawHistory.length} Events)
            </Typography>
          </Box>

          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Thawed Date & Time</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Thawed By Analyst</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Notes / Qualification Purpose</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.thawHistory.map((t) => (
                <TableRow key={t.id} hover>
                  <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{formatDateTime(t.thawedAt)}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{t.thawedByName}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{t.notes || "—"}</TableCell>
                </TableRow>
              ))}
              {detail.thawHistory.length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} sx={{ textAlign: "center", py: 2, color: "text.secondary", fontSize: 12 }}>
                    No thaw events recorded yet for this cryovial batch.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Box>

        {/* Usage Log: Primary (Direct GPT Challenges) */}
        <Box sx={{ mb: 2.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <AcUnitIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
            <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: theme.palette.primary.main }}>
              4. Primary Usage Log: Media & GPT Qualification Challenges ({detail.directUsageLog.length} Runs)
            </Typography>
          </Box>

          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Media Lot Qualified</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Media Type</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Evaluation Type</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Challenge Role</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Outcome</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Read By / Date</TableCell>
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
                  <TableCell sx={{ fontSize: 12 }}>{u.challengeRole || "Standard Challenge"}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={u.outcome || "Pending"}
                      color={u.outcome === "Conform" ? "success" : u.outcome === "NonConform" ? "error" : "default"}
                      sx={{ fontWeight: 700, fontSize: 10.5, height: 20 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 11.5 }}>
                    {u.readByName ? `${u.readByName} (${formatDate(u.readAt)})` : "Pending"}
                  </TableCell>
                </TableRow>
              ))}
              {detail.directUsageLog.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: "center", py: 2, color: "text.secondary", fontSize: 12 }}>
                    This strain batch has not been used in any Media/GPT challenges yet.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Box>

        {/* Secondary Indirect Rollup */}
        <Paper sx={{ p: 2, bgcolor: theme.custom.status.purple.bg, border: "1px solid", borderColor: theme.custom.status.purple.text, borderRadius: 1.5 }}>
          <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1.5 }}>
            <HubIcon sx={{ color: theme.palette.primary.main, fontSize: 24, mt: 0.25 }} />
            <Box>
              <Typography sx={{ fontSize: 13, fontWeight: 800, color: theme.palette.primary.main, mb: 0.5 }}>
                Downstream Routine Testing Impact (Secondary Indirect Rollup)
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                {detail.indirectUsageSummary}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 0.5 }}>
                Distinct qualified media lots: <strong>{detail.distinctQualifiedMediaLotsCount}</strong> • Routine test orders that consumed these released lots: <strong>{detail.indirectTestOrdersCount}</strong>
              </Typography>
            </Box>
          </Box>
        </Paper>
    </FloatingDialog>
  );
}
