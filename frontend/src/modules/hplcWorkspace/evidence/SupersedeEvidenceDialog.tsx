import { useState, useRef } from "react";
import { Trans } from "react-i18next";
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
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { HplcEvidenceDto } from "../types";

export interface SupersedeEvidenceDialogProps {
  open: boolean;
  targetEvidence: HplcEvidenceDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function SupersedeEvidenceDialog({
  open,
  targetEvidence,
  onClose,
  onSuccess
}: SupersedeEvidenceDialogProps) {
  const { t } = useTranslation("hplcWorkspace");
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
      await HplcWorkspaceService.supersedeEvidence(
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
              ns="hplcWorkspace"
              values={{ fileName: targetEvidence?.fileName ?? "" }}
              components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
            />
          </Typography>

          <Box>
            <input
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg"
              ref={replaceInputRef}
              onChange={handleFileChange}
              style={{ display: "none" }}
              id="supersede-file-input"
            />
            <label htmlFor="supersede-file-input">
              <Button
                variant="outlined"
                component="span"
                size="small"
                startIcon={<CloudUploadIcon />}
              >
                {t("evidence.supersede.selectFile")}
              </Button>
            </label>
            {replacementFile && (
              <Typography variant="body2" sx={{ mt: 1, fontWeight: 600 }}>
                {t("evidence.supersede.selected", {
                  fileName: replacementFile.name,
                  size: (replacementFile.size / (1024 * 1024)).toFixed(2)
                })}
              </Typography>
            )}
          </Box>

          <TextField
            label={t("evidence.supersede.reasonLabel")}
            placeholder={t("evidence.supersede.reasonPlaceholder")}
            multiline
            rows={3}
            fullWidth
            required
            size="small"
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            helperText={t("evidence.supersede.reasonHelper")}
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button
          onClick={handleClose}
          disabled={superseding}
        >
          {t("evidence.supersede.cancel")}
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleSupersede}
          disabled={!replacementFile || !supersedeReason.trim() || superseding}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {superseding ? t("evidence.supersede.superseding") : t("evidence.supersede.confirm")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
