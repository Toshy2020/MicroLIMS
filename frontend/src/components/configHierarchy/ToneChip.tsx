import { Box, useTheme } from "@mui/material";
import { ConfigBadge } from "./types";

// Small status pill in one of the shared status tones, so light and dark
// mode both come from theme.custom.status.
export function ToneChip({ label, tone, strong }: ConfigBadge & { strong?: boolean }) {
  const theme = useTheme();
  const t = theme.custom.status[tone];
  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        px: 1,
        py: "2px",
        borderRadius: 999,
        fontSize: 12,
        fontWeight: strong ? 700 : 600,
        lineHeight: 1.5,
        color: t.text,
        bgcolor: t.bg,
        border: "1px solid",
        borderColor: t.border,
        whiteSpace: "nowrap"
      }}
    >
      {label}
    </Box>
  );
}
