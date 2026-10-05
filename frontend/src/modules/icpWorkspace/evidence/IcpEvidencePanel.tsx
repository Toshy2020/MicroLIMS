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
import DownloadIcon from "@mui/icons-material/Download";
import { IcpReportUploadPanel } from "./IcpReportUploadPanel";
import { IcpSupersedeEvidenceDialog } from "./IcpSupersedeEvidenceDialog";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import type { IcpRunDto, IcpEvidenceDto } from "../types";

export interface IcpEvidencePanelProps {
  run: IcpRunDto;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function IcpEvidencePanel({
  run,
  canOperate,
  onRunUpdated
}: IcpEvidencePanelProps) {
  const theme = useTheme();
  const [filterContext, setFilterContext] = useState<string>("all");
  const [viewingId, setViewingId] = useState<number | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [supersedeOpen, setSupersedeOpen] = useState(false);
  const [targetEvidence, setTargetEvidence] = useState<IcpEvidenceDto | null>(null);

  const allEvidence = run.evidence || [];

  const filteredEvidence = allEvidence.filter((e) => {
    if (filterContext === "all") return true;
    return e.context.toLowerCase() === filterContext.toLowerCase();
  });

  const handleView = async (id: number) => {
    setViewingId(id);
    try {
      await IcpWorkspaceService.openEvidenceInNewTab(id);
    } finally {
      setViewingId(null);
    }
  };

  const handleDownload = async (id: number) => {
    setDownloadingId(id);
    try {
      const { blob, fileName } = await IcpWorkspaceService.downloadEvidence(id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } finally {
      setDownloadingId(null);
    }
  };

  const handleOpenSupersede = (item: IcpEvidenceDto) => {
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
          overflow: "hidden",
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1.5 }}>
            <Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Run Evidence & Instrument Reports ({allEvidence.length})
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Complete audit trail of calibration, sample, and general run report files.
              </Typography>
            </Box>

            <Tabs
              value={filterContext}
              onChange={(_, val) => setFilterContext(val)}
              sx={{ minHeight: 36, "& .MuiTab-root": { minHeight: 36, py: 0.5, textTransform: "none", fontSize: 13 } }}
            >
              <Tab label={`All (${allEvidence.length})`} value="all" />
              <Tab
                label={`Calibration (${allEvidence.filter((e) => e.context === "Calibration").length})`}
                value="calibration"
              />
              <Tab
                label={`Run (${allEvidence.filter((e) => e.context === "Run").length})`}
                value="run"
              />
              <Tab
                label={`Sample (${allEvidence.filter((e) => e.context === "Sample").length})`}
                value="sample"
              />
            </Tabs>
          </Box>
        </Box>

        {filteredEvidence.length === 0 ? (
          <Box sx={{ p: 4, textAlign: "center" }}>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              No evidence files attached for this category.
            </Typography>
          </Box>
        ) : (
          <TableContainer sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={{ width: 40 }}></TableCell>
                  <TableCell>File Name</TableCell>
                  <TableCell>Context</TableCell>
                  <TableCell>Kind</TableCell>
                  <TableCell>Uploaded By</TableCell>
                  <TableCell>Uploaded At</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredEvidence.map((e) => (
                  <TableRow
                    key={e.id}
                    sx={{
                      opacity: e.isCurrent ? 1 : 0.6,
                      backgroundColor: e.isCurrent ? "inherit" : theme.palette.action.hover
                    }}
                  >
                    <TableCell>{getFileIcon(e.contentType)}</TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: e.isCurrent ? 600 : 400 }}>
                        {e.fileName}
                      </Typography>
                      {e.supersedeReason && (
                        <Typography variant="caption" sx={{ color: "error.main", display: "block" }}>
                          Superseded: {e.supersedeReason}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" sx={{ fontWeight: 600 }}>
                        {e.context}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        {e.kind}
                      </Typography>
                    </TableCell>
                    <TableCell>{e.uploadedByUserName ?? "Unknown"}</TableCell>
                    <TableCell>{new Date(e.uploadedAt).toLocaleString()}</TableCell>
                    <TableCell>
                      {e.isCurrent ? (
                        <StatusBadge status="Completed" label="Current" />
                      ) : (
                        <StatusBadge status="Abandoned" label="Superseded" />
                      )}
                    </TableCell>
                    <TableCell align="right">
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                        <Tooltip title="View in New Tab">
                          <span>
                            <IconButton
                              size="small"
                              onClick={() => handleView(e.id)}
                              disabled={viewingId === e.id}
                            >
                              {viewingId === e.id ? (
                                <CircularProgress size={16} />
                              ) : (
                                <VisibilityIcon fontSize="small" />
                              )}
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Download File">
                          <span>
                            <IconButton
                              size="small"
                              onClick={() => handleDownload(e.id)}
                              disabled={downloadingId === e.id}
                            >
                              {downloadingId === e.id ? (
                                <CircularProgress size={16} />
                              ) : (
                                <DownloadIcon fontSize="small" />
                              )}
                            </IconButton>
                          </span>
                        </Tooltip>
                        {e.isCurrent && canOperate && run.status === "Open" && (
                          <Tooltip title="Supersede File">
                            <IconButton
                              size="small"
                              color="warning"
                              onClick={() => handleOpenSupersede(e)}
                            >
                              <SwapHorizIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>

      {/* Upload Additional Run Evidence Section */}
      {canOperate && run.status === "Open" && (
        <IcpReportUploadPanel
          runId={run.id}
          context="Run"
          kind="Other"
          title="Attach Run Supporting Document"
          description="Attach supplemental run evidence, system logs, or analyst notes."
          onChanged={onRunUpdated}
        />
      )}

      {/* Supersede Dialog */}
      <IcpSupersedeEvidenceDialog
        open={supersedeOpen}
        targetEvidence={targetEvidence}
        onClose={() => setSupersedeOpen(false)}
        onSuccess={onRunUpdated}
      />
    </Stack>
  );
}
