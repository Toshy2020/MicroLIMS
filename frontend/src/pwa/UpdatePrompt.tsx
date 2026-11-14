import { Box, Button, Paper, Typography } from "@mui/material";
import { useBenchDialogOpen } from "./benchDialogState";
import { useServiceWorkerUpdate } from "./registerServiceWorker";

/**
 * Polite, non-intrusive prompt shown when a new service worker is waiting.
 * Suppressed while any BenchDialog is open so unsaved bench input is never interrupted.
 */
export function UpdatePrompt() {
  const { updateAvailable, applyUpdate } = useServiceWorkerUpdate();
  const benchDialogOpen = useBenchDialogOpen();

  if (!updateAvailable || benchDialogOpen) {
    return null;
  }

  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{
        position: "fixed",
        bottom: 16,
        right: 16,
        zIndex: (theme) => theme.zIndex.snackbar,
        maxWidth: "calc(100vw - 32px)",
      }}
    >
      <Paper
        elevation={3}
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 2,
          px: 2,
          py: 1.5,
          border: 1,
          borderColor: (theme) => theme.custom.status.info.border,
          backgroundColor: (theme) => theme.custom.status.info.bg,
          color: (theme) => theme.custom.status.info.text,
        }}
      >
        <Typography variant="body2" sx={{ fontWeight: 500 }}>
          A new version is available.
        </Typography>
        <Button type="button" variant="contained" size="small" onClick={applyUpdate}>
          Reload
        </Button>
      </Paper>
    </Box>
  );
}
