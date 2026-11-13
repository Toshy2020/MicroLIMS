import { Alert, Button } from "@mui/material";

export interface PartialOutageBannerProps {
  sources: string[];
  onRetry?: () => void;
}

export function PartialOutageBanner({ sources, onRetry }: PartialOutageBannerProps) {
  if (sources.length === 0) return null;

  return (
    <Alert
      severity="warning"
      sx={{ borderRadius: 2 }}
      action={
        onRetry ? (
          <Button size="small" color="inherit" onClick={onRetry} sx={{ textTransform: "none", fontWeight: 600 }}>
            Retry
          </Button>
        ) : undefined
      }
    >
      {`Some data could not be loaded: ${sources.join(", ")}.`}
    </Alert>
  );
}
