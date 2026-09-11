import React, { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  FormControl,
  FormLabel,
  RadioGroup,
  FormControlLabel,
  Radio,
  Alert,
  CircularProgress
} from "@mui/material";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import InsertDriveFileOutlinedIcon from "@mui/icons-material/InsertDriveFileOutlined";
import { documentControlService } from "../services/documentControlService";
import type { FileRole } from "../types/documentControlTypes";

interface FileUploadDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  revisionId: number;
  revisionNumber: string;
  companyDocumentCode: string;
  hasExistingActiveFile?: boolean;
}

export function FileUploadDialog({
  open,
  onClose,
  onSuccess,
  revisionId,
  revisionNumber,
  companyDocumentCode,
  hasExistingActiveFile = false
}: FileUploadDialogProps) {
  const [fileRole, setFileRole] = useState<FileRole>("ControlledPdf");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();

      if (fileRole === "ControlledPdf" && ext !== ".pdf") {
        setError("Controlled document file must be in PDF format (.pdf).");
        setSelectedFile(null);
        return;
      }
      if (fileRole === "SourceFile" && ext !== ".docx" && ext !== ".doc") {
        setError("Source editable file must be in Word format (.docx or .doc).");
        setSelectedFile(null);
        return;
      }

      setError(null);
      setSelectedFile(file);
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) {
      setError("Please select a file to upload.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await documentControlService.uploadFile(revisionId, fileRole, selectedFile);
      setSelectedFile(null);
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to upload file.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setSelectedFile(null);
      setError(null);
      onClose();
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {hasExistingActiveFile ? "Replace Draft File" : "Upload Draft File"}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Document: <strong>{companyDocumentCode}</strong> | Revision: <strong>{revisionNumber} (Draft)</strong>
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
        {hasExistingActiveFile && (
          <Alert severity="info">
            An active file already exists for this draft revision. Uploading a new file will set it as the active version while permanently retaining previous copies in the audit trail.
          </Alert>
        )}

        {error && <Alert severity="error">{error}</Alert>}

        <FormControl component="fieldset">
          <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 700, mb: 0.5 }}>
            File Role
          </FormLabel>
          <RadioGroup
            row
            value={fileRole}
            onChange={(e) => {
              setFileRole(e.target.value as FileRole);
              setSelectedFile(null);
              setError(null);
            }}
          >
            <FormControlLabel
              value="ControlledPdf"
              control={<Radio size="small" />}
              label="Controlled Document (PDF)"
            />
            <FormControlLabel
              value="SourceFile"
              control={<Radio size="small" />}
              label="Editable Source (Word .docx)"
            />
          </RadioGroup>
        </FormControl>

        <Box
          sx={{
            border: "2px dashed",
            borderColor: selectedFile ? "primary.main" : "divider",
            borderRadius: 2,
            p: 3,
            textAlign: "center",
            bgcolor: selectedFile ? "action.hover" : "background.paper",
            cursor: "pointer",
            transition: "all 0.2s"
          }}
          onClick={() => document.getElementById("file-input-control")?.click()}
        >
          <input
            id="file-input-control"
            type="file"
            accept={fileRole === "ControlledPdf" ? ".pdf" : ".docx,.doc"}
            style={{ display: "none" }}
            onChange={handleFileChange}
          />
          <CloudUploadOutlinedIcon sx={{ fontSize: 40, color: "primary.main", mb: 1 }} />
          <Typography variant="body1" sx={{ fontWeight: 600 }}>
            {selectedFile ? selectedFile.name : "Click or browse to choose a file"}
          </Typography>
          <Typography variant="caption" color="text.secondary" display="block">
            {fileRole === "ControlledPdf" ? "Accepts .pdf format (max 50 MB)" : "Accepts .docx or .doc format (max 50 MB)"}
          </Typography>
        </Box>

        {selectedFile && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, p: 1.5, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <InsertDriveFileOutlinedIcon color="primary" />
            <Box sx={{ flexGrow: 1, minWidth: 0 }}>
              <Typography variant="body2" sx={{ fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {selectedFile.name}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {formatBytes(selectedFile.size)}
              </Typography>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={handleUpload}
          variant="contained"
          disabled={!selectedFile || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <CloudUploadOutlinedIcon />}
        >
          {loading ? "Uploading & Hashing..." : "Upload & Compute SHA-256"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
