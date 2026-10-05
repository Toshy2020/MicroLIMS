import type { ReactNode } from "react";
import { Box, ButtonBase, Paper, Typography, useTheme } from "@mui/material";
import { Link } from "react-router-dom";
import { StatusTone } from "../../theme/statusTokens";

export interface SummaryTile {
  label: string;
  value: string | number;
  // A tone draws attention (e.g. gaps to fix); omit for a plain count. It
  // colours the icon chip, or the number when there is no icon.
  tone?: StatusTone;
  icon?: ReactNode;
  // Small text after the number ("samples").
  caption?: string;
  // Tooltip and screen-reader explanation of what the tile counts.
  hint?: string;
  // A tile acts as a filter (onClick, with `active` while it filters) or as
  // a link to the list behind the number (to).
  onClick?: () => void;
  active?: boolean;
  to?: string;
}

// The one KPI tile in the app: label, number, optional icon chip. Workspaces,
// dashboards, inventories and configuration pages all draw their counts
// with it, so a number looks the same wherever it appears.
export function SummaryTiles({ tiles }: { tiles: SummaryTile[] }) {
  const theme = useTheme();
  const activeTokens = theme.custom.status.purple;
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {
          xs: "repeat(2, minmax(0, 1fr))",
          sm: `repeat(${Math.min(tiles.length, 3)}, minmax(0, 1fr))`,
          md: `repeat(${tiles.length}, minmax(0, 1fr))`
        },
        gap: 1.5,
        mb: 2.5
      }}
    >
      {tiles.map((tile) => {
        const t = tile.tone ? theme.custom.status[tile.tone] : null;
        const interactive = !!(tile.onClick || tile.to);
        const active = !!tile.active;
        const valueColor = active ? activeTokens.text : t && !tile.icon ? t.text : "text.primary";
        const content = (
          <>
            <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 0.5, width: "100%" }}>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600, lineHeight: 1.3, color: active ? activeTokens.text : "text.secondary" }}>
                {tile.label}
              </Typography>
              {tile.icon && (
                <Box
                  aria-hidden
                  sx={{
                    display: "grid",
                    placeItems: "center",
                    width: 28,
                    height: 28,
                    flexShrink: 0,
                    borderRadius: 1.5,
                    color: t?.text ?? "primary.main",
                    bgcolor: t?.bg ?? "action.hover",
                    "& svg": { fontSize: 18 }
                  }}
                >
                  {tile.icon}
                </Box>
              )}
            </Box>
            <Box sx={{ display: "flex", alignItems: "baseline", gap: 0.75 }}>
              <Typography sx={{ fontSize: 24, fontWeight: 700, lineHeight: 1.1, color: valueColor }}>{tile.value}</Typography>
              {tile.caption && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{tile.caption}</Typography>}
              {active && <Typography sx={{ fontSize: 12, fontWeight: 600, color: activeTokens.text }}>Filtering</Typography>}
            </Box>
          </>
        );
        const sx = {
          p: 1.75,
          minHeight: 84,
          width: "100%",
          borderRadius: 2,
          border: active ? "2px solid" : "1px solid",
          borderColor: active ? activeTokens.border : "divider",
          bgcolor: active ? activeTokens.bg : "background.paper",
          boxShadow: "none",
          textAlign: "left" as const,
          display: "flex",
          flexDirection: "column" as const,
          justifyContent: "space-between",
          alignItems: "flex-start",
          gap: 1,
          // A zero is not worth pulling the eye toward, but the tile stays
          // usable so the filter or list is still reachable.
          opacity: interactive && tile.value === 0 && !active ? 0.7 : 1
        };
        const interactiveSx = {
          ...sx,
          cursor: "pointer",
          transition: theme.transitions.create(["border-color", "box-shadow"], { duration: 150 }),
          "&:hover": { borderColor: activeTokens.border, boxShadow: theme.shadows[2] },
          // .text, not .border: the border token is too pale for a 3:1 focus ring.
          "&.Mui-focusVisible": { outline: `2px solid ${activeTokens.text}`, outlineOffset: 2 }
        };
        const label = tile.hint
          ? `${tile.label}: ${tile.value}${tile.caption ? ` ${tile.caption}` : ""}. ${tile.hint}.`
          : undefined;
        if (tile.to) {
          return (
            <ButtonBase key={tile.label} component={Link} to={tile.to} title={tile.hint} aria-label={label} sx={interactiveSx}>
              {content}
            </ButtonBase>
          );
        }
        if (tile.onClick) {
          return (
            <ButtonBase
              key={tile.label}
              onClick={tile.onClick}
              title={tile.hint}
              aria-label={label}
              aria-pressed={tile.active === undefined ? undefined : active}
              sx={interactiveSx}
            >
              {content}
            </ButtonBase>
          );
        }
        return (
          <Paper key={tile.label} title={tile.hint} sx={sx}>
            {content}
          </Paper>
        );
      })}
    </Box>
  );
}
