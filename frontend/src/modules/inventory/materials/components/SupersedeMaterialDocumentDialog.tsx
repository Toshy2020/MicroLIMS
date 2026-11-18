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
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { Ltr } from "../../../../components/Ltr";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialDocument } from "../types/materialTypes";
import { clickable } from "../../../../utils/clickable";

const ALLOWED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tiff"];
const MAX_SIZE_BYTES = 25 * 1024 * 1024;

interface Props {
  open: boolean;
  document: MaterialDocument;
  materialId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function SupersedeMaterialDocumentDialog({ open, document, materialId, onClose, onSuccess }: Props) {
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
    setReasonError(false);
    if (!reason.trim()) { setReasonError(true); return; }
    if (!file) { setFileError(t("materials.supersede.selectFile")); return; }

    setSubmitting(true);
    setError(null);
    try {
      await MaterialService.supersedeDocument(document.id, materialId, file, reason.trim());
      onSuccess();
    } catch (caught: unknown) {
      const err = caught as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("materials.supersede.failed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setFile(null); setFileError(null); setReason(""); setReasonError(false); setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title={t("materials.supersede.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>{t("common.cancel")}</Button>
          <Button
            id="supersede-doc-submit"
            variant="contained"
            color="warning"
            onClick={handleSubmit}
            disabled={submitting || !file || !reason.trim()}
          >
            {submitting ? t("materials.supersede.submitting") : t("materials.supersede.submit")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="info" sx={{ mb: 2, fontSize: 12 }}>
        <Trans i18nKey="materials.supersede.info" ns="inventory" values={{ name: document.originalFileName }} components={{ b: <strong />, ltr: <Ltr>{null}</Ltr> }} />
      </Alert>

      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>
        <Trans i18nKey="materials.supersede.docType" ns="inventory" values={{ type: t(`docTypes.${document.documentType}`) }} components={{ b: <strong /> }} />
      </Typography>

      {/* Reason */}
      <TextField
        id="supersede-reason"
        label={t("materials.supersede.reason")}
        fullWidth
        size="small"
        multiline
        rows={2}
        value={reason}
        onChange={(e) => { setReason(e.target.value); setReasonError(false); }}
        error={reasonError}
        helperText={reasonError ? t("materials.supersede.reasonRequired") : undefined}
        sx={{ mb: 2 }}
      />

      {/* Replacement file */}
      <input
        ref={fileRef}
        type="file"
        id="supersede-file-input"
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
        {...clickable(() => fileRef.current?.click(), { label: t("materials.supersede.chooseAria") })}
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
            {t("materials.supersede.clickToSelect")}
          </Typography>
        )}
      </Box>
      {fileError && <FormHelperText error sx={{ mt: 0.5 }}>{fileError}</FormHelperText>}
    </FloatingDialog>
  );
}
