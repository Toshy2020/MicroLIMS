import {
  Button, Box, Typography,
  Grid, Paper, Table, TableHead, TableRow, TableCell, TableBody, Chip, Divider,
  Stack, useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ScienceIcon from "@mui/icons-material/Science";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import { MediaGptDetail } from "../types/mediaGptTypes";
import { brandColors } from "../../../theme";
import { FloatingDialog } from "../../../components/FloatingDialog";

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
  try {
    const d = new Date(iso);
    return `${d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })} ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  } catch {
    return iso;
  }
}

export function MediaGptDetailDialog({ open, onClose, detail }: MediaGptDetailDialogProps) {
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
                Media Lot Traceability & GPT Details
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12 }}>
                Lot Number: <strong>{detail.lotNumber}</strong> • {detail.mediaType}
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
          Close
        </Button>
      }
    >
        {/* Lot Identity & Preparation Metadata */}
        <Paper sx={{ p: 2, mb: 2.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
            1. Lot Identity & Autoclave Preparation
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Media Type / Name</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700 }}>{detail.mediaType}</Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Source Dehydrated Material</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.manufacturerName} (Lot: {detail.manufacturerLot || "—"})
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Preparation / Expiry Date</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {formatDate(detail.preparedAt)} • Exp: {formatDate(detail.expiryDate)}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Total Weight / Volume</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.totalWeight} g / {detail.totalVolume}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Autoclave / Program / Load</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.autoclaveName || "—"} ({detail.autoclaveProgram}) • {detail.loadType || "—"}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Temp / Cycle / pH</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.temperature}°C ({detail.cycleTime} min, #{detail.cycleNumber}) • pH {detail.ph}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={6}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Prepared By</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                {detail.preparedByName} — {formatDateTime(detail.preparedAt)}
              </Typography>
            </Grid>
            <Grid item xs={12} sm={6}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Release Status / Decided By</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                <span style={{ color: detail.isReleasedForUse ? brandColors.ok : "inherit" }}>
                  {detail.approvalStatus} {detail.isReleasedForUse && "(Released for Routine Testing)"}
                </span>
                {detail.approvedByName && ` by ${detail.approvedByName} on ${formatDateTime(detail.approvedAt)}`}
              </Typography>
            </Grid>
          </Grid>
        </Paper>

        {/* Evaluation Summary */}
        <Box sx={{ mb: 2 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
              2. Growth Promotion & Challenge Results ({detail.challenges.length} Strains)
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Evaluation Type: <strong>{detail.evaluationType}</strong>
              {detail.evaluationCompletedByName && ` • Completed by ${detail.evaluationCompletedByName} on ${formatDate(detail.evaluationCompletedAt)}`}
            </Typography>
          </Box>

          <Table size="small" sx={{ border: "1px solid", borderColor: "divider" }}>
            <TableHead sx={{ bgcolor: theme.palette.mode === "dark" ? "grey.800" : "grey.100" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Organism / ATCC</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Strain Source</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Inoculum</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Actual Results</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Acceptance Criteria</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Outcome</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11.5 }}>Analyst / Read Date</TableCell>
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
                          <strong>{c.recoveryPercent}%</strong> Recovery
                          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 10.5 }}>
                            New: {c.newMediaCount} / Old: {c.oldMediaCount} (Ref: {c.referenceMediaLot || "—"})
                          </Typography>
                        </Box>
                      )}
                      {/* Inhibition: Growth observed */}
                      {c.growthObserved !== null && (
                        <Box>
                          Growth: <strong>{c.growthObserved ? "Observed" : "No Growth"}</strong>
                        </Box>
                      )}
                      {/* Indication: Observed Description */}
                      {c.observedDescription && (
                        <Box>
                          Observed: <em>{c.observedDescription}</em>
                        </Box>
                      )}
                      {/* Enrichment: Turbid */}
                      {c.isTurbid !== null && (
                        <Box>
                          Appearance: <strong>{c.isTurbid ? "Turbid" : "Clear"}</strong>
                        </Box>
                      )}
                    </TableCell>
                    <TableCell sx={{ fontSize: 11.5, color: "text.secondary" }}>
                      {c.expectedMinRecoveryPercent !== null && c.expectedMaxRecoveryPercent !== null && (
                        <span>{c.expectedMinRecoveryPercent}% – {c.expectedMaxRecoveryPercent}%</span>
                      )}
                      {c.growthObserved !== null && <span>No growth expected</span>}
                      {c.expectedDescription && <span>Expected: {c.expectedDescription}</span>}
                      {c.isTurbid !== null && <span>Turbid growth expected</span>}
                    </TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        icon={isConform ? <CheckCircleIcon sx={{ "&&": { fontSize: 14 } }} /> : isNonConform ? <CancelIcon sx={{ "&&": { fontSize: 14 } }} /> : undefined}
                        label={c.outcome || "Pending"}
                        color={isConform ? "success" : isNonConform ? "error" : "default"}
                        sx={{ fontWeight: 700, fontSize: 10.5, height: 22 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 11.5 }}>
                      {c.readByName ? (
                        <>
                          <div>{c.readByName}</div>
                          <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 10.5 }}>
                            {formatDateTime(c.readAt)}
                          </Typography>
                        </>
                      ) : (
                        <span style={{ color: "#9ca3af" }}>Pending</span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
              {detail.challenges.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} sx={{ textAlign: "center", py: 3, color: "text.secondary" }}>
                    No challenge organism entries recorded for this lot.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Box>
    </FloatingDialog>
  );
}
