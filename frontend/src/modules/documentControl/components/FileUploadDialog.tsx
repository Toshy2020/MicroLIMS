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
import { clickable } from "../../../utils/clickable";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

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
  const { t } = useTranslation("documentControl");
  const [fileRole, setFileRole] = useState<FileRole>("ControlledPdf");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();

      if (fileRole === "ControlledPdf" && ext !== ".pdf") {
        setError(t("fileUpload.pdfFormatError"));
        setSelectedFile(null);
        return;
      }
      if (fileRole === "SourceFile" && ext !== ".docx" && ext !== ".doc") {
        setError(t("fileUpload.wordFormatError"));
        setSelectedFile(null);
        return;
      }

      setError(null);
      setSelectedFile(file);
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) {
      setError(t("fileUpload.selectFileError"));
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await documentControlService.uploadFile(revisionId, fileRole, selectedFile);
      setSelectedFile(null);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("fileUpload.uploadError")));
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
    if (bytes === 0) return `0 ${t("fileUpload.units.bytes")}`;
    const k = 1024;
    const sizes = [
      t("fileUpload.units.bytes"),
      t("fileUpload.units.kb"),
      t("fileUpload.units.mb"),
      t("fileUpload.units.gb")
    ];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {hasExistingActiveFile ? t("fileUpload.replaceTitle") : t("fileUpload.uploadTitle")}
        </Typography>
        <Typography variant="caption" sx={{
          color: "text.secondary"
        }}>
          <Trans
            i18nKey="fileUpload.subtitle"
            ns="documentControl"
            values={{ code: companyDocumentCode, rev: revisionNumber }}
            components={{ strong: <strong /> }}
          />
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
        {hasExistingActiveFile && (
          <Alert severity="info">
            {t("fileUpload.replaceInfo")}
          </Alert>
        )}

        {error && <Alert severity="error">{error}</Alert>}

        <FormControl component="fieldset">
          <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 700, mb: 0.5 }}>
            {t("fileUpload.fileRoleLabel")}
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
              label={t("fileUpload.controlledPdfOption")}
            />
            <FormControlLabel
              value="SourceFile"
              control={<Radio size="small" />}
              label={t("fileUpload.sourceDocxOption")}
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
          {...clickable(() => document.getElementById("file-input-control")?.click(), { label: t("fileUpload.chooseFileAria") })}
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
            {selectedFile ? selectedFile.name : t("fileUpload.clickToBrowse")}
          </Typography>
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block"
            }}>
            {fileRole === "ControlledPdf" ? t("fileUpload.acceptsPdf") : t("fileUpload.acceptsWord")}
          </Typography>
        </Box>

        {selectedFile && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, p: 1.5, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <InsertDriveFileOutlinedIcon color="primary" />
            <Box sx={{ flexGrow: 1, minWidth: 0 }}>
              <Typography variant="body2" sx={{ fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {selectedFile.name}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {formatBytes(selectedFile.size)}
              </Typography>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleUpload}
          variant="contained"
          disabled={!selectedFile || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <CloudUploadOutlinedIcon />}
        >
          {loading ? t("fileUpload.uploading") : t("fileUpload.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
