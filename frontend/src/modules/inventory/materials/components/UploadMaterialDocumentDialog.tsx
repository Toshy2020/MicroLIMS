import { useRef, useState } from "react";
import {
  Box,
  Button,
  MenuItem,
  Select,
  FormControl,
  InputLabel,
  Typography,
  Alert,
  LinearProgress,
  FormHelperText
} from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { useTranslation } from "../../../../i18n";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialDocumentType } from "../types/materialTypes";
import { MATERIAL_DOCUMENT_TYPE_LABELS } from "../types/materialTypes"; // keys only; the labels come from the inventory texts
import { clickable } from "../../../../utils/clickable";

const ALLOWED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tiff"];
const MAX_SIZE_BYTES = 25 * 1024 * 1024; // 25 MB (frontend pre-validation; backend is authoritative)

interface Props {
  open: boolean;
  materialId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function UploadMaterialDocumentDialog({ open, materialId, onClose, onSuccess }: Props) {
  const { t } = useTranslation("inventory");
  const fileRef = useRef<HTMLInputElement>(null);
  const [docType, setDocType] = useState<MaterialDocumentType>("COA");
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0] ?? null;
    setFileError(null);
    if (!selected) { setFile(null); return; }

    const ext = selected.name.substring(selected.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setFileError(t("materials.upload.unsupported", { ext }));
      setFile(null);
      return;
    }
    if (selected.size > MAX_SIZE_BYTES) {
      setFileError(t("materials.upload.tooBig"));
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const handleSubmit = async () => {
    if (!file) { setFileError(t("materials.upload.selectFile")); return; }
    setSubmitting(true);
    setError(null);
    try {
      await MaterialService.uploadDocument(materialId, docType, file);
      onSuccess();
    } catch (caught: unknown) {
      const err = caught as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("materials.upload.failed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setFile(null);
    setFileError(null);
    setError(null);
    setDocType("COA");
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title={t("materials.upload.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>{t("common.cancel")}</Button>
          <Button
            id="upload-doc-submit"
            variant="contained"
            onClick={handleSubmit}
            disabled={submitting || !file}
          >
            {submitting ? t("materials.upload.uploading") : t("materials.upload.submit")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {/* Document Type */}
      <FormControl fullWidth size="small" sx={{ mb: 2 }}>
        <InputLabel id="doc-type-label">{t("materials.upload.docType")}</InputLabel>
        <Select
          labelId="doc-type-label"
          id="doc-type-select"
          value={docType}
          label={t("materials.upload.docType")}
          onChange={(e) => setDocType(e.target.value as MaterialDocumentType)}
        >
          {(Object.keys(MATERIAL_DOCUMENT_TYPE_LABELS) as MaterialDocumentType[]).map((dt) => (
            <MenuItem key={dt} value={dt}>{t(`docTypes.${dt}`)}</MenuItem>
          ))}
        </Select>
      </FormControl>

      {/* File input */}
      <input
        ref={fileRef}
        type="file"
        id="doc-file-input"
        accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
        style={{ display: "none" }}
        onChange={handleFileChange}
      />

      <Box
        sx={{
          border: "2px dashed",
          borderColor: fileError ? "error.main" : "divider",
          borderRadius: 2,
          p: 2.5,
          textAlign: "center",
          cursor: "pointer",
          "&:hover": { borderColor: "primary.main", bgcolor: "action.hover" }
        }}
        onClick={() => fileRef.current?.click()}
        {...clickable(() => fileRef.current?.click(), { label: t("materials.upload.chooseAria") })}
      >
        <UploadFileIcon sx={{ color: "text.secondary", mb: 0.5, fontSize: 32 }} />
        {file ? (
          <>
            <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{file.name}</Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {(file.size / 1024 / 1024).toFixed(2)} MB
            </Typography>
          </>
        ) : (
          <>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("materials.upload.clickToSelect")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
              {t("materials.upload.formats")}
            </Typography>
          </>
        )}
      </Box>
      {fileError && <FormHelperText error sx={{ mt: 0.5 }}>{fileError}</FormHelperText>}
    </FloatingDialog>
  );
}
