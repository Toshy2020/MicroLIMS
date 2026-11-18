import { useState, useRef } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  Stack
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import { Trans, useTranslation } from "react-i18next";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpEvidenceDto } from "../types";
import { Ltr } from "../../../components/Ltr";

export interface IcpSupersedeEvidenceDialogProps {
  open: boolean;
  targetEvidence: IcpEvidenceDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function IcpSupersedeEvidenceDialog({
  open,
  targetEvidence,
  onClose,
  onSuccess
}: IcpSupersedeEvidenceDialogProps) {
  const { t } = useTranslation("icpWorkspace");
  const replaceInputRef = useRef<HTMLInputElement>(null);
  const [replacementFile, setReplacementFile] = useState<File | null>(null);
  const [supersedeReason, setSupersedeReason] = useState("");
  const [superseding, setSuperseding] = useState(false);
  const [supersedeError, setSupersedeError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSupersedeError(null);
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      if (file.size > MAX_FILE_SIZE_BYTES) {
        setSupersedeError(t("evidence.supersede.fileTooLarge"));
        setReplacementFile(null);
        return;
      }
      setReplacementFile(file);
    }
  };

  const handleClose = () => {
    if (superseding) return;
    setReplacementFile(null);
    setSupersedeReason("");
    setSupersedeError(null);
    onClose();
  };

  const handleSupersede = async () => {
    if (!targetEvidence || !replacementFile || !supersedeReason.trim()) return;

    setSuperseding(true);
    setSupersedeError(null);
    try {
      await IcpWorkspaceService.supersedeEvidence(
        targetEvidence.id,
        replacementFile,
        supersedeReason.trim()
      );
      handleClose();
      onSuccess();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setSupersedeError(e.response?.data?.message ?? e.message ?? t("evidence.supersede.failed"));
    } finally {
      setSuperseding(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        {t("evidence.supersede.title")}
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {supersedeError && <Alert severity="error">{supersedeError}</Alert>}

          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            <Trans
              i18nKey="evidence.supersede.replacing"
              ns="icpWorkspace"
              values={{ fileName: targetEvidence?.fileName ?? "" }}
              components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
            />
          </Typography>

          <Box>
            <input
              type="file"
              ref={replaceInputRef}
              style={{ display: "none" }}
              onChange={handleFileChange}
              accept=".pdf,.png,.jpg,.jpeg,.txt,.csv"
            />
            <Button
              variant="outlined"
              startIcon={<CloudUploadIcon />}
              onClick={() => replaceInputRef.current?.click()}
              sx={{ textTransform: "none" }}
            >
              {replacementFile ? <Ltr>{replacementFile.name}</Ltr> : t("evidence.supersede.selectFile")}
            </Button>
          </Box>

          <TextField
            label={t("evidence.supersede.reasonLabel")}
            placeholder={t("evidence.supersede.reasonPlaceholder")}
            multiline
            rows={3}
            fullWidth
            required
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 500 } }}
            helperText={t("evidence.supersede.charCount", { count: supersedeReason.length })}
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={superseding}>
          {t("evidence.supersede.cancel")}
        </Button>
        <Button
          variant="contained"
          color="warning"
          onClick={handleSupersede}
          disabled={!replacementFile || !supersedeReason.trim() || superseding}
        >
          {superseding ? t("evidence.supersede.uploading") : t("evidence.supersede.confirm")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
