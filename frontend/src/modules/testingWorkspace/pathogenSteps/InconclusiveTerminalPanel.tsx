import { Box, Alert, Typography } from "@mui/material";

// Per current-step's own advertised next step, GetCurrentStepAsync
// reports the chain as having advanced past confirmatory plating after
// an Inconclusive result - but the biochemical step can never actually
// be submitted for that order (SubmitBiochemicalAsync always refuses a
// non-AllConforming confirmatory result). Do not follow the advertised
// next step literally: render a terminal state instead of a biochemical
// form the analyst cannot submit.
export function InconclusiveTerminalPanel() {
  return (
    <Box>
      <Alert severity="warning" sx={{ mb: 1 }}>
        Confirmatory plating result: <strong>Inconclusive</strong>
      </Alert>
      <Typography variant="body2">
        This result has been flagged for investigation. A retest is required. No further action is available
        to the analyst on this test order until the investigation is resolved.
      </Typography>
    </Box>
  );
}
