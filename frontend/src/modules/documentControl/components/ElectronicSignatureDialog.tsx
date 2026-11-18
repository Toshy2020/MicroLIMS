import React, { useState } from "react";
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
  CircularProgress,
  Paper,
  Divider,
  InputAdornment,
  IconButton
} from "@mui/material";
import LockIcon from "@mui/icons-material/Lock";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

interface ElectronicSignatureDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (password: string) => Promise<void>;
  documentCode: string;
  documentTitle: string;
  revisionNumber: string;
  signerFullName: string;
  signerUsername: string;
  signerRole?: string;
  meaning?: string;
  meaningTranslation?: string;
}

export const ElectronicSignatureDialog: React.FC<ElectronicSignatureDialogProps> = ({
  open,
  onClose,
  onConfirm,
  documentCode,
  documentTitle,
  revisionNumber,
  signerFullName,
  signerUsername,
  signerRole,
  meaning = "Approved",
  meaningTranslation
}) => {
  const { t } = useTranslation("documentControl");
  const resolvedSignerRole = signerRole ?? t("approval.designatedApproverRole");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSign = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!password) {
      setError(t("signatureDialog.passwordRequired"));
      return;
    }

    try {
      setSigning(true);
      setError(null);
      await onConfirm(password);
      setPassword("");
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("signatureDialog.authFailed")));
    } finally {
      setSigning(false);
    }
  };

  const handleClose = () => {
    if (!signing) {
      setPassword("");
      setError(null);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1, borderBottom: "1px solid", borderColor: "divider" }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t("signatureDialog.title")}
          </Typography>
        </Box>
      </DialogTitle>

      <form onSubmit={handleSign}>
        <DialogContent sx={{ pt: 2.5 }}>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          {/* Legal / Regulatory Notice */}
          <Alert
            severity="warning"
            icon={<LockIcon fontSize="inherit" />}
            sx={{
              mb: 2.5,
              bgcolor: (theme) => theme.custom.status.inconclusive.bg,
              border: "1px solid",
              borderColor: (theme) => theme.custom.status.inconclusive.border}}
          >
            <Typography variant="caption" sx={{ fontWeight: 600, display: "block" }}>
              {t("signatureDialog.statutoryTitle")}
            </Typography>
            <Typography variant="caption">
              {t("signatureDialog.statutoryBody")}
            </Typography>
          </Alert>

          {/* Signature Manifest Card */}
          <Paper variant="outlined" sx={{ p: 2, mb: 2.5, bgcolor: "background.default"}}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                fontWeight: 700,
                textTransform: "uppercase",
                letterSpacing: 0.5,
                display: "block",
                mb: 1
              }}>
              {t("signatureDialog.attributionTitle")}
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.documentCode")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{documentCode}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.revisionNumber")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("common.rev", { number: revisionNumber })}</Typography>
              </Box>
              <Box sx={{ gridColumn: "span 2" }}>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.documentTitle")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>{documentTitle}</Typography>
              </Box>
            </Box>
            <Divider sx={{ my: 1.5 }} />
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.signerName")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{signerFullName}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.username")}</Typography>
                <Typography variant="body2">{signerUsername}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.signerRole")}</Typography>
                <Typography variant="body2">{resolvedSignerRole}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("signatureDialog.signatureMeaning")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 700, color: "success.main" }}>
                  {meaningTranslation && meaningTranslation !== meaning ? `${meaning} (${meaningTranslation})` : meaning}
                </Typography>
              </Box>
            </Box>
          </Paper>

          {/* Password Re-Entry Field */}
          <TextField
            label={t("signatureDialog.passwordLabel")}
            type={showPassword ? "text" : "password"}
            required
            fullWidth
            autoFocus
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={signing}
            placeholder={t("signatureDialog.passwordPlaceholder")}
            helperText={t("signatureDialog.passwordHelper")}
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label={t("signatureDialog.togglePasswordAria")}
                      onClick={() => setShowPassword(!showPassword)}
                      edge="end"
                    >
                      {showPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  </InputAdornment>
                )
              }
            }}
          />
        </DialogContent>

        <DialogActions sx={{ px: 3, py: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Button onClick={handleClose} disabled={signing}>
            {t("common.cancel")}
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="success"
            disabled={signing || !password.trim()}
            startIcon={signing ? <CircularProgress size={16} color="inherit" /> : <LockIcon />}
          >
            {signing ? t("signatureDialog.signing") : t("signatureDialog.submit")}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
};