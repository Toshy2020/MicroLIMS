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
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentDocumentType } from "../types/equipmentTypes";
import { EQUIPMENT_DOCUMENT_TYPE_LABELS } from "../types/equipmentTypes";
import { clickable } from "../../../../utils/clickable";
import { useTranslation } from "../../../../i18n";

const ALLOWED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tiff"];
const MAX_SIZE_BYTES = 25 * 1024 * 1024; // 25 MB

interface Props {
  open: boolean;
  equipmentId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function UploadEquipmentDocumentDialog({ open, equipmentId, onClose, onSuccess }: Props) {
  const { t } = useTranslation("inventory");
  const fileRef = useRef<HTMLInputElement>(null);
  const [docType, setDocType] = useState<EquipmentDocumentType>("CalibrationCertificate");
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
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
      setFileError(t("equipment.docDialogs.unsupportedType", { ext }));
      setFile(null);
      return;
    }
    if (selected.size > MAX_SIZE_BYTES) {
      setFileError(t("equipment.docDialogs.maxSizeError"));
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const handleSubmit = async () => {
    if (!file) {
      setFileError(t("equipment.docDialogs.selectFileError"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await EquipmentInventoryService.uploadDocument(equipmentId, docType, file);
      onSuccess();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string } } };
      setError(apiErr?.response?.data?.message ?? t("equipment.docDialogs.upload.failed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setFile(null);
    setFileError(null);
    setError(null);
    setDocType("CalibrationCertificate");
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title={t("equipment.docDialogs.upload.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            id="upload-equip-doc-submit"
            variant="contained"
            onClick={handleSubmit}
            disabled={submitting || !file}
          >
            {submitting ? t("equipment.docDialogs.upload.uploading") : t("equipment.docDialogs.upload.submit")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {/* Document Type */}
      <FormControl fullWidth size="small" sx={{ mb: 2 }}>
        <InputLabel id="equip-doc-type-label">{t("equipment.docDialogs.upload.docType")}</InputLabel>
        <Select
          labelId="equip-doc-type-label"
          id="equip-doc-type-select"
          value={docType}
          label={t("equipment.docDialogs.upload.docType")}
          onChange={(e) => setDocType(e.target.value as EquipmentDocumentType)}
        >
          {(Object.keys(EQUIPMENT_DOCUMENT_TYPE_LABELS) as EquipmentDocumentType[]).map((docKey) => (
            <MenuItem key={docKey} value={docKey}>
              {t(`equipment.documents.types.${docKey}`)}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {/* File input */}
      <input
        ref={fileRef}
        type="file"
        id="equip-doc-file-input"
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
        {...clickable(() => fileRef.current?.click(), { label: t("equipment.docDialogs.upload.chooseAria") })}
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
              {t("equipment.docDialogs.upload.clickToSelect")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
              {t("equipment.docDialogs.allowedTypesHint")}
            </Typography>
          </>
        )}
      </Box>
      {fileError && <FormHelperText error sx={{ mt: 0.5 }}>{fileError}</FormHelperText>}
    </FloatingDialog>
  );
}
