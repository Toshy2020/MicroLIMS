import { Alert, Checkbox, FormControlLabel, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../i18n";

export interface TitrantDueAcknowledgementProps {
  warnings: string[];
  acknowledged: boolean;
  onAcknowledgedChange: (acknowledged: boolean) => void;
  justification: string;
  onJustificationChange: (justification: string) => void;
}

export function TitrantDueAcknowledgement({
  warnings,
  acknowledged,
  onAcknowledgedChange,
  justification,
  onJustificationChange
}: TitrantDueAcknowledgementProps) {
  const { t } = useTranslation("testingWorkspace");
  const trimmedLen = justification.trim().length;
  const isTouched = justification.length > 0;
  const isInvalid = isTouched && (trimmedLen < 10 || justification.length > 500);
  const ackLabel = t("titrantDue.acknowledge");

  return (
    <Stack spacing={1.5} sx={{ mt: 1 }}>
      <Alert severity="warning">
        {warnings.map((w, idx) => (
          <Typography key={idx} variant="body2" sx={{ fontWeight: 500 }}>
            {w}
          </Typography>
        ))}
      </Alert>
      <FormControlLabel
        control={
          <Checkbox
            checked={acknowledged}
            onChange={(e) => onAcknowledgedChange(e.target.checked)}
            color="warning"
            slotProps={{ input: { "aria-label": ackLabel } }}
          />
        }
        label={ackLabel}
      />
      <TextField
        size="small"
        label={t("titrantDue.justificationLabel")}
        placeholder={t("titrantDue.justificationPlaceholder")}
        value={justification}
        onChange={(e) => onJustificationChange(e.target.value)}
        multiline
        rows={2}
        fullWidth
        required
        error={isInvalid}
        helperText={
          isTouched && trimmedLen < 10
            ? t("titrantDue.tooShortHelper", { count: trimmedLen })
            : t("titrantDue.countHelper", { count: justification.length })
        }
        slotProps={{
          htmlInput: {
            maxLength: 500
          }
        }}
      />
    </Stack>
  );
}
