import { Alert, Button } from "@mui/material";
import { useTranslation } from "../../i18n";

export interface PartialOutageBannerProps {
  sources: string[];
  onRetry?: () => void;
}

export function PartialOutageBanner({ sources, onRetry }: PartialOutageBannerProps) {
  const { t } = useTranslation();
  if (sources.length === 0) return null;

  return (
    <Alert
      severity="warning"
      sx={{ borderRadius: 2 }}
      action={
        onRetry ? (
          <Button size="small" color="inherit" onClick={onRetry} sx={{ textTransform: "none", fontWeight: 600 }}>
            {t("actions.retry")}
          </Button>
        ) : undefined
      }
    >
      {t("outage.someDataFailed", { sources: sources.join(t("listSeparator")) })}
    </Alert>
  );
}
