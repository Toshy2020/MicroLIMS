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
import { clickable } from "../../../../utils/clickable";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";

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
  const { t } = useTranslation("inventory");
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
    setReasonError(false);
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }
    if (!file) {
      setFileError(t("equipment.docDialogs.selectReplacementError"));
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await EquipmentInventoryService.supersedeDocument(document.id, equipmentId, file, reason.trim());
      onSuccess();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string } } };
      setError(apiErr?.response?.data?.message ?? t("equipment.docDialogs.supersede.failed"));
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
      title={t("equipment.docDialogs.supersede.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            id="supersede-equip-doc-submit"
            variant="contained"
            color="warning"
            onClick={handleSubmit}
            disabled={submitting || !file || !reason.trim()}
          >
            {submitting ? t("equipment.docDialogs.supersede.submitting") : t("equipment.docDialogs.supersede.submit")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="info" sx={{ mb: 2, fontSize: 12 }}>
        <Trans
          i18nKey="equipment.docDialogs.supersede.notice"
          ns="inventory"
          values={{ fileName: document.originalFileName }}
          components={{ bold: <strong /> }}
        />
      </Alert>

      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>
        <Trans
          i18nKey="equipment.docDialogs.supersede.docType"
          ns="inventory"
          values={{ type: t(`equipment.documents.types.${document.documentType}`) }}
          components={{ bold: <strong /> }}
        />
      </Typography>

      {/* Reason */}
      <TextField
        id="supersede-equip-reason"
        label={t("equipment.docDialogs.supersede.reasonLabel")}
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
        helperText={reasonError ? t("equipment.docDialogs.reasonRequired") : undefined}
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
        {...clickable(() => fileRef.current?.click(), { label: t("equipment.docDialogs.supersede.chooseAria") })}
      >
        <UploadFileIcon sx={{ color: "text.secondary", fontSize: 28 }} />
        {file ? (
          <>
            <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{file.name}</Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {(file.size / 1024 / 1024).toFixed(2)} MB
            </Typography>
          </>
        ) : (
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {t("equipment.docDialogs.supersede.clickToSelect")}
          </Typography>
        )}
      </Box>
      {fileError && <FormHelperText error sx={{ mt: 0.5 }}>{fileError}</FormHelperText>}
    </FloatingDialog>
  );
}
