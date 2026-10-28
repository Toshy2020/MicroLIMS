import { Alert, Stack, Typography } from "@mui/material";
import { TestSendResult } from "./services/IntegrationsService";

// Per-recipient outcome of a test email or test alert.
export function TestSendOutcome({ result }: { result: TestSendResult }) {
  return (
    <Stack spacing={0.5} aria-label="Test send result">
      <Alert severity={result.sent === result.recipientCount ? "success" : "warning"}>
        Delivered to {result.sent} of {result.recipientCount} recipient{result.recipientCount === 1 ? "" : "s"}.
      </Alert>
      {result.outcomes.map((o) => (
        <Typography key={o.recipient} variant="body2">
          {o.recipient}: {o.outcome}{o.error ? ` - ${o.error}` : ""}
        </Typography>
      ))}
    </Stack>
  );
}
