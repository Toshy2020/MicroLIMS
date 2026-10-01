import { CircularProgress, Box, Typography } from "@mui/material";

// Section/page loading indicator. Announced to screen readers (role
// "status"), with an optional visible label for loads that take a moment
// ("Loading samples…") so a blank pause never reads as a broken page.
export function LoadingSpinner({ label = "Loading…", showLabel = false }: { label?: string; showLabel?: boolean }) {
  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 1.5, p: 4, minHeight: 120 }}
    >
      <CircularProgress size={32} aria-label={label} />
      {showLabel && <Typography variant="body2" sx={{ color: "text.secondary" }}>{label}</Typography>}
    </Box>
  );
}
