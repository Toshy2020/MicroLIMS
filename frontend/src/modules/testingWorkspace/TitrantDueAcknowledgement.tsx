import { Alert, Checkbox, FormControlLabel, Stack, TextField, Typography } from "@mui/material";

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
  const trimmedLen = justification.trim().length;
  const isTouched = justification.length > 0;
  const isInvalid = isTouched && (trimmedLen < 10 || justification.length > 500);

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
            slotProps={{ input: { "aria-label": "I acknowledge this titrant is due for standardization" } }}
          />
        }
        label="I acknowledge this titrant is due for standardization"
      />
      <TextField
        size="small"
        label="Justification for using due titrant"
        placeholder="Explain why this due titrant is being used for this analysis (10–500 characters)"
        value={justification}
        onChange={(e) => onJustificationChange(e.target.value)}
        multiline
        rows={2}
        fullWidth
        required
        error={isInvalid}
        helperText={
          isTouched && trimmedLen < 10
            ? `Justification must be at least 10 characters (${trimmedLen}/500)`
            : `${justification.length}/500 characters (minimum 10)`
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
