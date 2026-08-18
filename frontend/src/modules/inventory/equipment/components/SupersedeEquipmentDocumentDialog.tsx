import { useRef, useState } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Alert,
  LinearProgress,
  FormHelperText
} from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentDocument } from "../types/equipmentTypes";
import { EQUIPMENT_DOCUMENT_TYPE_LABELS } from "../types/equipmentTypes";

const ALLOWED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tiff"];
const MAX_SIZE_BYTES = 25 * 1024 * 1024;

interface Props {
  open: boolean;
  document: EquipmentDocument;
  equipmentId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function SupersedeEquipmentDocumentDialog({ open, document, equipmentId, onClose, onSuccess }: Props) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0] ?? null;
    setFileError(null);
    if (!selected) {
      setFile(null);
      return;
    }

    const ext = selected.name.substring(selected.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setFileError(`File type '${ext}' is not supported. Allowed: PDF, JPG, JPEG, PNG, WEBP, TIFF.`);
      setFile(null);
      return;
    }
    if (selected.size > MAX_SIZE_BYTES) {
      setFileError("File exceeds the 25 MB limit.");
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const handleSubmit = async () => {
    setReasonError(false);
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }
    if (!file) {
      setFileError("Please select a replacement file.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await EquipmentInventoryService.supersedeDocument(document.id, equipmentId, file, reason.trim());
      onSuccess();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Supersession failed. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setFile(null);
    setFileError(null);
    setReason("");
    setReasonError(false);
    setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title="Supersede Calibration Certificate"
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>
            Cancel
          </Button>
          <Button
            id="supersede-equip-doc-submit"
            variant="contained"
            color="warning"
            onClick={handleSubmit}
            disabled={submitting || !file || !reason.trim()}
          >
            {submitting ? "Superseding…" : "Supersede"}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="info" sx={{ mb: 2, fontSize: 12 }}>
        The existing certificate ({document.originalFileName}) will be marked <strong>Superseded</strong> and remain historically accessible.
        A new Current certificate will be created from the replacement file.
      </Alert>

      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>
        Document Type: <strong>{EQUIPMENT_DOCUMENT_TYPE_LABELS[document.documentType]}</strong>
      </Typography>

      {/* Reason */}
      <TextField
        id="supersede-equip-reason"
        label="Reason for Supersession *"
        fullWidth
        size="small"
        multiline
        rows={2}
        value={reason}
        onChange={(e) => {
          setReason(e.target.value);
          setReasonError(false);
        }}
        error={reasonError}
        helperText={reasonError ? "A reason is required." : undefined}
        sx={{ mb: 2 }}
      />

      {/* Replacement file */}
      <input
        ref={fileRef}
        type="file"
        id="supersede-equip-file-input"
        accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
        style={{ display: "none" }}
        onChange={handleFileChange}
      />
      <Box
        sx={{
          border: "2px dashed",
          borderColor: fileError ? "error.main" : "divider",
          borderRadius: 2,
          p: 2,
          textAlign: "center",
          cursor: "pointer",
          "&:hover": { borderColor: "primary.main", bgcolor: "action.hover" }
        }}
        onClick={() => fileRef.current?.click()}
      >
        <UploadFileIcon sx={{ color: "text.secondary", fontSize: 28 }} />
        {file ? (
          <>
            <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{file.name}</Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              {(file.size / 1024 / 1024).toFixed(2)} MB
            </Typography>
          </>
        ) : (
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Click to select the replacement certificate (PDF, JPG, PNG, WEBP, TIFF · max 25 MB)
          </Typography>
        )}
      </Box>
      {fileError && <FormHelperText error sx={{ mt: 0.5 }}>{fileError}</FormHelperText>}
    </FloatingDialog>
  );
}
