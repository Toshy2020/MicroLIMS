import { useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Link,
  Typography,
  useTheme,
} from "@mui/material";
import {
  MasterRecordType,
  UsageSummary,
  masterDataUsageService,
} from "../../services/masterDataUsageService";

export interface WhereUsedPanelProps {
  type: MasterRecordType;
  id: number;
}

type LoadState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "loaded"; summary: UsageSummary };

export function WhereUsedPanel({ type, id }: WhereUsedPanelProps) {
  const theme = useTheme();
  const [retryToken, setRetryToken] = useState(0);
  const [state, setState] = useState<LoadState>({ status: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    setState({ status: "loading" });

    masterDataUsageService
      .getUsage(type, id, controller.signal)
      .then((summary) => {
        if (!controller.signal.aborted) {
          setState({ status: "loaded", summary });
        }
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof Error && err.message
            ? err.message
            : "Could not load where-used information.";
        setState({ status: "error", message });
      });

    return () => controller.abort();
  }, [type, id, retryToken]);

  const openTone = theme.custom.status.action;

  return (
    <Box
      component="section"
      aria-label="Where used"
      sx={{
        p: 2,
        border: 1,
        borderColor: "divider",
        borderRadius: 1,
        bgcolor: "background.paper",
      }}
    >
      <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
        Where used
      </Typography>

      {state.status === "loading" && (
        <Box
          role="status"
          aria-busy="true"
          sx={{ display: "flex", alignItems: "center", gap: 1 }}
        >
          <CircularProgress size={16} />
          <Typography variant="body2" color="text.secondary">
            Checking where this record is used...
          </Typography>
        </Box>
      )}

      {state.status === "error" && (
        <Alert
          severity="error"
          action={
            <Button
              type="button"
              color="inherit"
              size="small"
              onClick={() => setRetryToken((n) => n + 1)}
            >
              Retry
            </Button>
          }
        >
          {state.message}
        </Alert>
      )}

      {state.status === "loaded" && (() => {
        const visibleLines = state.summary.lines.filter((line) => line.count > 0);
        if (visibleLines.length === 0) {
          return (
            <Typography variant="body2" color="text.secondary">
              Not used anywhere
            </Typography>
          );
        }

        return (
          <Box
            component="ul"
            sx={{
              listStyle: "none",
              p: 0,
              m: 0,
              display: "flex",
              flexDirection: "column",
              gap: 0.75,
            }}
          >
            {visibleLines.map((line) => (
              <Box
                component="li"
                key={line.key}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: 1.5,
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0 }}>
                  {line.listRoute ? (
                    <Link
                      component={RouterLink}
                      to={line.listRoute}
                      variant="body2"
                      underline="hover"
                    >
                      {line.label}
                    </Link>
                  ) : (
                    <Typography variant="body2">{line.label}</Typography>
                  )}

                  {line.isOpenWork && (
                    <Box
                      component="span"
                      sx={{
                        px: 0.75,
                        py: 0.125,
                        borderRadius: 1,
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        lineHeight: 1.4,
                        bgcolor: openTone.bg,
                        color: openTone.text,
                        border: "1px solid",
                        borderColor: openTone.border,
                      }}
                    >
                      Open
                    </Box>
                  )}
                </Box>

                <Typography
                  variant="body2"
                  sx={{ fontVariantNumeric: "tabular-nums", fontWeight: 600 }}
                >
                  {line.count}
                </Typography>
              </Box>
            ))}
          </Box>
        );
      })()}
    </Box>
  );
}
