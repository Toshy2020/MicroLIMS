import { useState } from "react";
import {
  Box,
  Paper,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  Tooltip,
  Stack,
  Tabs,
  Tab,
  CircularProgress,
  useTheme
} from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ImageIcon from "@mui/icons-material/Image";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
import { ReportUploadPanel } from "./ReportUploadPanel";
import { SupersedeEvidenceDialog } from "./SupersedeEvidenceDialog";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { HplcRunDto, HplcEvidenceDto } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface ChromatogramEvidencePanelProps {
  run: HplcRunDto;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function ChromatogramEvidencePanel({
  run,
  canOperate,
  onRunUpdated
}: ChromatogramEvidencePanelProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const [filterContext, setFilterContext] = useState<string>("all");
  const [viewingId, setViewingId] = useState<number | null>(null);
  const [supersedeOpen, setSupersedeOpen] = useState(false);
  const [targetEvidence, setTargetEvidence] = useState<HplcEvidenceDto | null>(null);

  const allEvidence = run.evidence || [];

  const filteredEvidence = allEvidence.filter((e) => {
    if (filterContext === "all") return true;
    return e.context.toLowerCase() === filterContext.toLowerCase();
  });

  const handleView = async (id: number) => {
    setViewingId(id);
    try {
      await HplcWorkspaceService.openEvidenceInNewTab(id);
    } finally {
      setViewingId(null);
    }
  };

  const handleOpenSupersede = (item: HplcEvidenceDto) => {
    setTargetEvidence(item);
    setSupersedeOpen(true);
  };

  const getFileIcon = (contentType: string) => {
    if (contentType.includes("pdf")) return <PictureAsPdfIcon color="error" fontSize="small" />;
    if (contentType.includes("image")) return <ImageIcon color="primary" fontSize="small" />;
    return <InsertDriveFileIcon color="action" fontSize="small" />;
  };

  return (
    <Stack spacing={2.5}>
      {/* Evidence Table */}
      <Paper
        elevation={0}
        sx={{
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          overflow: "hidden"
        }}
      >
        <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1.5 }}>
            <Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t("evidence.chromatogram.title", { count: allEvidence.length })}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("evidence.chromatogram.subtitle")}
              </Typography>
            </Box>

            <Tabs
              value={filterContext}
              onChange={(_, val) => setFilterContext(val)}
              textColor="primary"
              indicatorColor="primary"
              sx={{ minHeight: 36 }}
            >
              <Tab value="all" label={t("evidence.chromatogram.tabs.all")} sx={{ minHeight: 36, textTransform: "none", py: 0.5 }} />
              <Tab value="sst" label={t("evidence.chromatogram.tabs.sst")} sx={{ minHeight: 36, textTransform: "none", py: 0.5 }} />
              <Tab value="sample" label={t("evidence.chromatogram.tabs.sample")} sx={{ minHeight: 36, textTransform: "none", py: 0.5 }} />
              <Tab value="run" label={t("evidence.chromatogram.tabs.run")} sx={{ minHeight: 36, textTransform: "none", py: 0.5 }} />
            </Tabs>
          </Box>
        </Box>

        <TableContainer>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{t("evidence.chromatogram.columns.fileName")}</TableCell>
                <TableCell>{t("evidence.chromatogram.columns.context")}</TableCell>
                <TableCell>{t("evidence.chromatogram.columns.kind")}</TableCell>
                <TableCell>{t("evidence.chromatogram.columns.uploadedBy")}</TableCell>
                <TableCell>{t("evidence.chromatogram.columns.uploadedAt")}</TableCell>
                <TableCell>{t("evidence.chromatogram.columns.status")}</TableCell>
                <TableCell align="right">{t("evidence.chromatogram.columns.actions")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filteredEvidence.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} sx={{ textAlign: "center", py: 4, color: "text.secondary" }}>
                    {t("evidence.chromatogram.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                filteredEvidence.map((e) => {
                  const canSupersede =
                    e.isCurrent && canOperate && run.status === "Open";

                  return (
                    <TableRow key={e.id} hover>
                      <TableCell>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                          {getFileIcon(e.contentType)}
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            <Ltr>{e.fileName}</Ltr>
                          </Typography>
                        </Box>
                        {e.supersedeReason && (
                          <Typography
                            variant="caption"
                            sx={{ color: "error.main", display: "block", mt: 0.5 }}
                          >
                            {t("evidence.chromatogram.supersededPrefix", { reason: e.supersedeReason })}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <Chip label={e.context} size="small" variant="outlined" />
                      </TableCell>
                      <TableCell>{e.kind}</TableCell>
                      <TableCell>{e.uploadedByUserName ?? "—"}</TableCell>
                      <TableCell>
                        <Ltr>{formatDateTimeWithTimeZone(e.uploadedAt)}</Ltr>
                      </TableCell>
                      <TableCell>
                        <StatusBadge
                          status={e.isCurrent ? "Active" : "Superseded"}
                          label={e.isCurrent ? t("evidence.chromatogram.current") : t("evidence.chromatogram.superseded")}
                        />
                      </TableCell>
                      <TableCell align="right">
                        <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                          <Tooltip title={t("evidence.chromatogram.viewTooltip")}>
                            <span>
                              <IconButton
                                size="small"
                                color="primary"
                                onClick={() => handleView(e.id)}
                                disabled={viewingId === e.id}
                                aria-label={t("evidence.chromatogram.viewAria", { fileName: e.fileName })}
                              >
                                {viewingId === e.id ? (
                                  <CircularProgress size={16} />
                                ) : (
                                  <VisibilityIcon fontSize="small" />
                                )}
                              </IconButton>
                            </span>
                          </Tooltip>

                          {canSupersede && (
                            <Tooltip title={t("evidence.chromatogram.supersedeTooltip")}>
                              <IconButton
                                size="small"
                                color="warning"
                                onClick={() => handleOpenSupersede(e)}
                                aria-label={t("evidence.chromatogram.supersedeAria", { fileName: e.fileName })}
                              >
                                <SwapHorizIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          )}
                        </Stack>
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      {/* Upload general run evidence */}
      <Paper
        elevation={0}
        sx={{
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            {t("evidence.chromatogram.uploadGeneralTitle")}
          </Typography>
        </Box>
        <Box sx={{ p: 2 }}>
          <ReportUploadPanel
            runId={run.id}
            context="Run"
            kind="Other"
            evidenceList={run.evidence}
            onChanged={onRunUpdated}
          />
        </Box>
      </Paper>

      {/* Supersede Evidence Dialog */}
      <SupersedeEvidenceDialog
        open={supersedeOpen}
        targetEvidence={targetEvidence}
        onClose={() => setSupersedeOpen(false)}
        onSuccess={() => {
          setSupersedeOpen(false);
          onRunUpdated();
        }}
      />
    </Stack>
  );
}
