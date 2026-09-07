import { ReactNode } from "react";
import { Alert, AlertTitle, Box, Button } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import { LoadingSpinner } from "../../../components/LoadingSpinner";

interface Props {
  loading: boolean;
  error: string | null;
  // True once there is something to render. Kept separate from `error` so a
  // dashboard that loaded once can keep showing its data through a failed
  // background refresh instead of throwing the screen away.
  hasData: boolean;
  onRetry: () => void;
  children: ReactNode;
}

// Every dashboard used to gate its whole render on `!data`, and either
// discarded the error (`.catch(() => null)`) or never read the one useApi
// already returned. The result was that any failed request left the screen as
// a spinner forever - no message, no retry - which is exactly what happened
// when the API returned 503 on all four dashboard endpoints.
export function DashboardStateGate({ loading, error, hasData, onRetry, children }: Props) {
  if (hasData) return <>{children}</>;

  if (loading) return <LoadingSpinner />;

  if (error) {
    return (
      <Alert
        severity="error"
        sx={{ borderRadius: 2, alignItems: "center" }}
        action={
          <Button
            size="small"
            variant="outlined"
            color="inherit"
            startIcon={<RefreshIcon />}
            onClick={onRetry}
            sx={{ textTransform: "none", fontWeight: 600, whiteSpace: "nowrap" }}
          >
            Retry
          </Button>
        }
      >
        <AlertTitle sx={{ fontWeight: 700, mb: 0.25 }}>Dashboard could not be loaded</AlertTitle>
        {error}
      </Alert>
    );
  }

  // Loaded, no error, still nothing to show.
  return (
    <Box
      sx={{
        py: 8,
        px: 3,
        textAlign: "center",
        border: "1px dashed",
        borderColor: "divider",
        borderRadius: 2,
        color: "text.secondary",
        fontSize: 14
      }}
    >
      No dashboard data is available yet.
    </Box>
  );
}
