import type { ReactNode } from "react";
import { Box, useTheme } from "@mui/material";
import type { StatusTone } from "../../../theme/statusTokens";

// The shared status pill look, with a leading symbol so a status is never
// told by colour alone.
export function StatusPill({ tone, symbol, size = "medium", children }: {
  tone: StatusTone;
  symbol?: string;
  size?: "small" | "medium";
  children: ReactNode;
}) {
  const tokens = useTheme().custom.status[tone];
  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex", alignItems: "center", gap: 0.5, px: size === "small" ? 0.75 : 1, py: 0.125, borderRadius: 5,
        fontSize: size === "small" ? 11 : 12, fontWeight: 600, lineHeight: 1.5, whiteSpace: "nowrap", verticalAlign: "middle",
        color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`
      }}
    >
      {symbol && <Box component="span" aria-hidden="true">{symbol}</Box>}
      {children}
    </Box>
  );
}
