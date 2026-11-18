import { useState } from "react";
import {
  Button,
  Typography,
  Box,
  Alert,
  TextField,
  Stack,
  useTheme
} from "@mui/material";
import AssignmentReturnOutlinedIcon from "@mui/icons-material/AssignmentReturnOutlined";
import { useTranslation } from "../i18n";
import { Ltr } from "./Ltr";
import { getErrorMessage } from "../utils/errorMessage";
import { FloatingDialog } from "./FloatingDialog";

interface ReturnToAnalystDialogProps {
  open: boolean;
  testCode: string;
  testDisplayName: string;
  onCancel: () => void;
  onConfirm: (reason?: string) => Promise<void> | void;
}

export function ReturnToAnalystDialog({
  open,
  testCode,
  testDisplayName,
  onCancel,
  onConfirm
}: ReturnToAnalystDialogProps) {
  const { t } = useTranslation();
  const theme = useTheme();
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const trimmed = reason.trim();
      await onConfirm(trimmed ? trimmed : undefined);
      setReason("");
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("components.returnToAnalystDialog.failed")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    if (submitting) return;
    setReason("");
    setError(null);
    onCancel();
  };

  return (
    <FloatingDialog
      open={open}
      onClose={handleCancel}
      maxWidth="sm"
      paperSx={{ borderRadius: 2.5, p: 1 }}
      titleSx={{ display: "flex", alignItems: "center", gap: 1.25, pb: 1 }}
      title={
        <>
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: 36,
              height: 36,
              borderRadius: 2,
              bgcolor: theme.palette.warning.light ?? "warning.light",
              color: theme.palette.warning.dark ?? "warning.dark"
            }}
          >
            <AssignmentReturnOutlinedIcon sx={{ fontSize: 22 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
              {t("components.returnToAnalystDialog.title")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("components.returnToAnalystDialog.subtitle")}
            </Typography>
          </Box>
        </>
      }
      actions={
        <>
          <Button onClick={handleCancel} disabled={submitting}>
            {t("actions.cancel")}
          </Button>
          <Button
            variant="contained"
            color="warning"
            onClick={handleConfirm}
            disabled={submitting}
          >
            {submitting ? t("components.returnToAnalystDialog.returning") : t("components.returnToAnalystDialog.confirm")}
          </Button>
        </>
      }
    >
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}

          <Box
            sx={{
              p: 1.5,
              borderRadius: 1.5,
              bgcolor: "background.default",
              border: "1px solid",
              borderColor: "divider"
            }}
          >
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
              {t("components.returnToAnalystDialog.testOrder")}
            </Typography>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary", mt: 0.25 }}>
              <Ltr>{testCode}</Ltr>: {testDisplayName}
            </Typography>
          </Box>

          <Alert severity="info" sx={{ fontSize: 12 }}>
            {t("components.returnToAnalystDialog.info")}
          </Alert>

          <TextField
            label={t("components.returnToAnalystDialog.reasonLabel")}
            placeholder={t("components.returnToAnalystDialog.reasonPlaceholder")}
            multiline
            rows={3}
            fullWidth
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={submitting}
            autoFocus
          />
        </Stack>
    </FloatingDialog>
  );
}
