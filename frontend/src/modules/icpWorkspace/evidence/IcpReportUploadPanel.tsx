import { useState, useRef } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  CircularProgress,
  Stack,
  useTheme,
  IconButton,
  Tooltip
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import VisibilityIcon from "@mui/icons-material/Visibility";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ImageIcon from "@mui/icons-material/Image";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { IcpSupersedeEvidenceDialog } from "./IcpSupersedeEvidenceDialog";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import type {
  IcpEvidenceDto,
  IcpEvidenceContext,
  IcpEvidenceKind
} from "../types";

export interface IcpReportUploadPanelProps {
  runId: number;
  runSampleId?: number;
  context: IcpEvidenceContext;
  kind: IcpEvidenceKind;
  evidenceList?: IcpEvidenceDto[];
  onChanged?: () => void;
  title?: string;
  description?: string;
  disabled?: boolean;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function IcpReportUploadPanel({
  runId,
  runSampleId,
  context,
  kind,
  evidenceList = [],
  onChanged,
  title = "Calibration Report Upload",
  description = "Upload the instrument instrument report file (Syngistix or instrument raw export). Required before calibration confirmation.",
  disabled = false
}: IcpReportUploadPanelProps) {
  const theme = useTheme();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [viewingId, setViewingId] = useState<number | null>(null);

  const [supersedeOpen, setSupersedeOpen] = useState(false);
  const [targetEvidence, setTargetEvidence] = useState<IcpEvidenceDto | null>(null);

  const relevantEvidence = evidenceList.filter((e) => {
    if (e.context !== context) return false;
    if (runSampleId !== undefined && runSampleId !== null) {
      return e.icpRunSampleId === runSampleId;
    }
    return true;
  });

  const currentCount = relevantEvidence.filter((e) => e.isCurrent).length;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null);
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      if (file.size > MAX_FILE_SIZE_BYTES) {
        setError("File exceeds the 25 MB limit.");
        setSelectedFile(null);
        return;
      }
      setSelectedFile(file);
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) return;

    setUploading(true);
    setError(null);
    try {
      await IcpWorkspaceService.uploadEvidence(
        runId,
        selectedFile,
        context,
        kind,
        runSampleId
      );
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      onChanged?.();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Upload failed.");
    } finally {
      setUploading(false);
    }
  };

  const handleView = async (id: number) => {
    setViewingId(id);
    try {
      await IcpWorkspaceService.openEvidenceInNewTab(id);
    } finally {
      setViewingId(null);
    }
  };

  const getFileIcon = (contentType: string) => {
    if (contentType.includes("pdf")) return <PictureAsPdfIcon color="error" fontSize="small" />;
    if (contentType.includes("image")) return <ImageIcon color="primary" fontSize="small" />;
    return <InsertDriveFileIcon color="action" fontSize="small" />;
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            {title}
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {description}
          </Typography>
        </Box>
        {currentCount > 0 && (
          <StatusBadge status="Completed" label={`${currentCount} Attached`} />
        )}
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Upload action area */}
      {!disabled && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 2,
            p: 2,
            mb: relevantEvidence.length > 0 ? 2 : 0,
            borderRadius: 1.5,
            border: `1px dashed ${theme.palette.divider}`,
            backgroundColor: theme.palette.action.hover
          }}
        >
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: "none" }}
            onChange={handleFileChange}
            accept=".pdf,.png,.jpg,.jpeg,.txt,.csv"
          />
          <Button
            variant="outlined"
            startIcon={<CloudUploadIcon />}
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            sx={{ textTransform: "none" }}
          >
            {selectedFile ? "Choose Different File" : "Select File"}
          </Button>

          {selectedFile && (
            <Typography variant="body2" sx={{ fontWeight: 600, flexGrow: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {selectedFile.name} ({(selectedFile.size / 1024 / 1024).toFixed(2)} MB)
            </Typography>
          )}

          {selectedFile && (
            <Button
              variant="contained"
              onClick={handleUpload}
              disabled={uploading}
              sx={{ textTransform: "none" }}
            >
              {uploading ? <CircularProgress size={20} color="inherit" /> : "Upload"}
            </Button>
          )}
        </Box>
      )}

      {/* Evidence table */}
      {relevantEvidence.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
          <Table size="small">
            <TableHead sx={tableHeadSx}>
              <TableRow>
                <TableCell sx={{ width: 40 }}></TableCell>
                <TableCell>File Name</TableCell>
                <TableCell>Uploaded By</TableCell>
                <TableCell>Uploaded At</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {relevantEvidence.map((e) => (
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
                    <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title="View File">
                        <span>
                          <IconButton
                            size="small"
                            onClick={() => handleView(e.id)}
                            aria-label={`View ${e.fileName}`}
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
                      {e.isCurrent && !disabled && (
                        <Tooltip title="Supersede (Replace) File">
                          <IconButton
                            size="small"
                            color="warning"
                            onClick={() => {
                              setTargetEvidence(e);
                              setSupersedeOpen(true);
                            }}
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

      <IcpSupersedeEvidenceDialog
        open={supersedeOpen}
        targetEvidence={targetEvidence}
        onClose={() => setSupersedeOpen(false)}
        onSuccess={() => onChanged?.()}
      />
    </Paper>
  );
}
