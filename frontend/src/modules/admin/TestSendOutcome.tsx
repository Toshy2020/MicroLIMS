import { Alert, Stack, Typography } from "@mui/material";
import { TestSendResult } from "./services/IntegrationsService";
import { useTranslation } from "../../i18n";

// Per-recipient outcome of a test email or test alert.
export function TestSendOutcome({ result }: { result: TestSendResult }) {
  const { t } = useTranslation("admin");
  return (
    <Stack spacing={0.5} aria-label={t("testSend.region")}>
      <Alert severity={result.sent === result.recipientCount ? "success" : "warning"}>
        {t("testSend.delivered", { sent: result.sent, count: result.recipientCount })}
      </Alert>
      {result.outcomes.map((o) => (
        <Typography key={o.recipient} variant="body2">
          {o.recipient}: {o.outcome}{o.error ? ` - ${o.error}` : ""}
        </Typography>
      ))}
    </Stack>
  );
}
