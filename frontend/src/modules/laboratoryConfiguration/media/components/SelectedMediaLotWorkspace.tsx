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
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import HowToRegOutlinedIcon from "@mui/icons-material/HowToRegOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { Link } from "react-router-dom";
import { StatusBadge } from "../../../../components/StatusBadge";
import { mediaClassLabel, evaluationTypeLabel } from "../../../../services/masterDataOptions";
import { MediaSummary } from "../types/mediaSummaryTypes";
import { apiClient } from "../../../../services/apiClient";
import { lifecycleOf } from "./MediaLotKpiCards";
import { brandColors } from "../../../../theme";
import { useAuth } from "../../../../contexts/AuthContext";

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
  const { role } = useAuth();
  const theme = useTheme();
  const canRelease = role === "SectionHead" || role === "SystemAdministrator";
  const lifecycle = lifecycleOf(lot, awaitingApprovalIds);

  const [activeTab, setActiveTab] = useState(0);
  const [summary, setSummary] = useState<MediaSummary | null>(null);

  useEffect(() => {
    if (!lot?.id) return;
    apiClient
      .get(`/media/${lot.id}/summary`)
      .then((r) => setSummary(r.data.data))
      .catch(() => setSummary(null));
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
            Prepared: {formatDateDDMMYY(lot.preparedAt)} · Expiry: {formatDateDDMMYY(lot.expiryDate)} · ID #{lot.id}
          </Typography>
        </Box>

        <Tooltip title="Close Workspace (Return to Full Register)">
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
            Deselect
          </Button>
        </Tooltip>
      </Box>

      {/* Action Toolbar */}
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ pt: 0.5 }}>
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
          View Lot Record
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
          Audit History
        </Button>

        {lifecycle === "Awaiting Approval" && canRelease && (
          <>
            <Button
              size="small"
              variant="contained"
              color="success"
              startIcon={<HowToRegOutlinedIcon sx={{ fontSize: 16 }} />}
              onClick={() => onRequestReleaseDecision(lot, true)}
              sx={{ fontWeight: 700, fontSize: 12 }}
            >
              Release Lot
            </Button>
            <Button
              size="small"
              variant="outlined"
              color="error"
              startIcon={<BlockOutlinedIcon sx={{ fontSize: 16 }} />}
              onClick={() => onRequestReleaseDecision(lot, false)}
              sx={{ fontWeight: 700, fontSize: 12 }}
            >
              Reject Lot
            </Button>
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
            Mark Out of Stock
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
            backgroundColor: brandColors.sectionTitle
          }
        }}
      >
        <Tab label="Preparation Overview" />
        <Tab label={`Evaluations (${matchingEvaluations.length || (summary?.evaluation ? 1 : 0)})`} />
        <Tab label="Release & Audit Trail" />
      </Tabs>

      {/* TAB 0: OVERVIEW */}
      {activeTab === 0 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          {/* Section 1: Preparation Info */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              PREPARATION DETAILS
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
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Prepared Date</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateDDMMYY(lot.preparedAt)}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Dehydrated Stock</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.material?.materialName || summary?.materialName || "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Manufacturer / Batch</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.material
                    ? `${lot.material.manufacturerName || "—"} (Batch: ${lot.material.batchNumber})`
                    : summary?.manufacturerName
                    ? `${summary.manufacturerName} (Batch: ${summary.manufacturerLot})`
                    : "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Total Weight</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.totalWeight} g
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Total Volume</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.totalVolume}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>pH (at 25°C)</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.ph}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Prepared By</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {summary?.preparedByName || "—"}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Prepared On</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateTimeDDMMYY(lot.preparedAt)}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Expiry Date</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {formatDateDDMMYY(lot.expiryDate)}
                </Typography>
              </Box>
            </Box>
          </Box>

          {/* Section 2: Sterilization Info */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              AUTOCLAVE STERILIZATION PARAMETERS
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
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Autoclave Equipment</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {summary?.autoclaveName || `Autoclave #${lot.autoclaveEquipmentId}`}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Program / Load</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.autoclaveProgram}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Load Type</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.loadType}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Temperature</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.temperature} °C
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Cycle Time</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {lot.cycleTime} minutes
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Cycle Number</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  #{lot.cycleNumber}
                </Typography>
              </Box>
            </Box>
          </Box>
        </Stack>
      )}

      {/* TAB 1: EVALUATIONS */}
      {activeTab === 1 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
              Assigned Evaluations
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              GPT, Sterility, Indication/Inhibition, and Enrichment evaluations
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
                      Evaluation #{ev.id} · Assigned on: {formatDateDDMMYY(ev.assignedAt)}
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
                    <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.75 }}>
                      CHALLENGE ORGANISMS & STATUS:
                    </Typography>
                    <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
                      <TableHead>
                        <TableRow sx={{ bgcolor: "background.default" }}>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Organism</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Cryovial</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Incubation</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Outcome</TableCell>
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
                              {c.cryovial?.code || c.cryovialCode || "Pending"}
                            </TableCell>
                            <TableCell sx={{ fontSize: 12 }}>
                              {c.incubation
                                ? `${c.incubation.temperature}°C, ${c.incubation.duration}h`
                                : c.temperature
                                ? `${c.temperature}°C, ${c.duration}h`
                                : "Pending"}
                            </TableCell>
                            <TableCell sx={{ fontSize: 12 }}>
                              {c.outcome ? (
                                <StatusBadge status={c.outcome} />
                              ) : (
                                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Pending</Typography>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </Box>
                )}

                <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 1, borderTop: "1px solid", borderColor: "divider" }}>
                  <Button
                    size="small"
                    variant="contained"
                    endIcon={<ArrowForwardIcon sx={{ fontSize: 14 }} />}
                    onClick={() => onOpenEvaluation(ev.id)}
                    sx={{
                      bgcolor: brandColors.sectionTitle,
                      fontSize: 12,
                      fontWeight: 700,
                      "&:hover": { bgcolor: "#632273" }
                    }}
                  >
                    {ev.status === "Completed" ? "View Evaluation Workflow" : "Open / Continue Evaluation"}
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
                    Assigned on: {formatDateDDMMYY(summary.evaluation.assignedAt)}
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
                  <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
                    <TableHead>
                      <TableRow sx={{ bgcolor: "background.default" }}>
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Organism</TableCell>
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Cryovial</TableCell>
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Incubation</TableCell>
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Outcome</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {summary.evaluation.challenges.map((c, idx) => (
                        <TableRow key={idx}>
                          <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                            {c.organismName}
                            {c.challengeRole ? ` (${c.challengeRole})` : ""}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>{c.cryovialCode || "Pending"}</TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {c.temperature ? `${c.temperature}°C, ${c.duration}h` : "Pending"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {c.outcome ? (
                              <StatusBadge status={c.outcome} />
                            ) : (
                              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Pending</Typography>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
            </Paper>
          ) : (
            <Alert severity="info">
              No evaluations currently assigned to this media lot. Make sure Media Challenge Specs are configured for this media class.
            </Alert>
          )}
        </Stack>
      )}

      {/* TAB 2: RELEASE & AUDIT */}
      {activeTab === 2 && (
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              RELEASE & AUTHORIZATION STATUS
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
                    ? "Released for Use in Routine Testing"
                    : lifecycle === "Awaiting Approval"
                    ? "Awaiting Section Head Release Signature"
                    : lifecycle === "Rejected"
                    ? "Rejected from Routine Use"
                    : "Pending Media Evaluation Completion"}
                </Typography>
              </Box>

              {lot.isReleasedForUse && summary?.approvedByName && (
                <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, mt: 0.5 }}>
                  Electronically signed & approved by <strong>{summary.approvedByName}</strong> on{" "}
                  {summary.approvedAt ? formatDateTimeDDMMYY(summary.approvedAt) : "—"}
                </Typography>
              )}

              {lifecycle === "Awaiting Approval" && (
                <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text, mt: 0.5 }}>
                  This media lot has satisfied all evaluation criteria (Conform) and is waiting for a Section Head signature to release for laboratory use.
                </Typography>
              )}
            </Paper>
          </Box>

          {/* Signatures Trail */}
          {summary?.signatures && summary.signatures.length > 0 && (
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
                ELECTRONIC SIGNATURES
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
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      {sig.meaning} · Signed by @{sig.username} on {formatDateTimeDDMMYY(sig.signedAt)}
                    </Typography>
                    {sig.comment && (
                      <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5 }}>
                        Comment: {sig.comment}
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
                LIFECYCLE TIMELINE
              </Typography>
              <Stack spacing={1}>
                {summary.timeline.map((ev, idx) => (
                  <Box key={idx} sx={{ display: "flex", gap: 1.5, alignItems: "flex-start" }}>
                    <Typography sx={{ fontSize: 11, color: "text.secondary", width: 140, flexShrink: 0 }}>
                      {formatDateTimeDDMMYY(ev.timestamp)}
                    </Typography>
                    <Box>
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                        {ev.eventType} — {ev.performedByName || "System"}
                      </Typography>
                      {ev.comment && (
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
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
