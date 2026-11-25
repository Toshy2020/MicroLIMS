import { useEffect, useState } from "react";
import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  ListItemText,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { FormDialog } from "../../components/lab";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useTranslation } from "../../i18n";
import { competencyService } from "../../services/competencyService";

export interface OverrideCandidate {
  userId: number;
  fullName: string;
  reason?: string;
}

export interface OverrideDialogProps {
  open: boolean;
  testOrderId: number;
  testLabel: string;
  candidates: OverrideCandidate[];
  defaultAnalystUserId?: number;
  onClose: () => void;
  onGranted: (overrideId: number) => void;
}

export function OverrideDialog({
  open,
  testOrderId,
  testLabel,
  candidates,
  defaultAnalystUserId,
  onClose,
  onGranted
}: OverrideDialogProps) {
  const { t } = useTranslation("competency");
  const [analystUserId, setAnalystUserId] = useState<number | "">("");
  const [reason, setReason] = useState("");
  const [fieldErrors, setFieldErrors] = useState<{ analyst?: string; reason?: string }>({});
  const [signingOpen, setSigningOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const initialAnalyst =
      defaultAnalystUserId ??
      (candidates.length > 0 ? candidates[0].userId : "");
    setAnalystUserId(initialAnalyst);
    setReason("");
    setFieldErrors({});
    setSigningOpen(false);
  }, [open, defaultAnalystUserId, candidates]);

  const handleFormSubmit = () => {
    const nextErrors: { analyst?: string; reason?: string } = {};
    if (analystUserId === "") {
      nextErrors.analyst = t("override.validation.analystRequired");
    }
    if (!reason.trim()) {
      nextErrors.reason = t("override.validation.reasonRequired");
    }
    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;
    setSigningOpen(true);
  };

  const handleSignConfirm = async (password: string) => {
    const overrideId = await competencyService.grantOverride(testOrderId, {
      analystUserId: Number(analystUserId),
      reason: reason.trim(),
      password
    });
    toast.success(t("override.success"));
    setSigningOpen(false);
    onGranted(overrideId);
    onClose();
  };

  return (
    <>
      <FormDialog
        open={open && !signingOpen}
        title={t("override.title")}
        onClose={onClose}
        onSubmit={handleFormSubmit}
        submitLabel={t("override.continueToSign")}
      >
        <Stack spacing={2}>
          <Box>
            <Typography variant="caption" color="text.secondary">
              {t("override.testLabel")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {testLabel}
            </Typography>
          </Box>

          <FormControl fullWidth size="small" required error={Boolean(fieldErrors.analyst)}>
            <InputLabel id="override-analyst-label">{t("override.analystLabel")}</InputLabel>
            <Select<number | "">
              labelId="override-analyst-label"
              label={t("override.analystLabel")}
              value={analystUserId}
              onChange={(e) => setAnalystUserId(e.target.value === "" ? "" : Number(e.target.value))}
            >
              {candidates.map((c) => (
                <MenuItem key={c.userId} value={c.userId}>
                  {c.reason ? (
                    <ListItemText
                      primary={c.fullName}
                      secondary={c.reason}
                      slotProps={{ secondary: { variant: "caption", color: "text.secondary" } }}
                    />
                  ) : (
                    c.fullName
                  )}
                </MenuItem>
              ))}
            </Select>
            {fieldErrors.analyst && <FormHelperText>{fieldErrors.analyst}</FormHelperText>}
          </FormControl>

          <TextField
            label={t("override.reasonLabel")}
            placeholder={t("override.reasonPlaceholder")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
            multiline
            rows={3}
            fullWidth
            error={Boolean(fieldErrors.reason)}
            helperText={fieldErrors.reason}
          />
        </Stack>
      </FormDialog>

      {open && signingOpen && (
        <SignatureDialog
          open
          meaningStatement={t("override.meaningStatement", { lng: "en" })}
          meaningStatementTranslation={t("override.meaningStatementAr")}
          onCancel={() => setSigningOpen(false)}
          onConfirm={handleSignConfirm}
        />
      )}
    </>
  );
}
