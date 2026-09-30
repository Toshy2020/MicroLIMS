import { Box, Paper, Typography, useTheme } from "@mui/material";
import { StatusTone } from "../../theme/statusTokens";

export interface SummaryTile {
  label: string;
  value: string | number;
  // A tone draws attention (e.g. gaps to fix); omit for a plain count.
  tone?: StatusTone;
  onClick?: () => void;
}

export function SummaryTiles({ tiles }: { tiles: SummaryTile[] }) {
  const theme = useTheme();
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "repeat(2, minmax(0, 1fr))", md: `repeat(${tiles.length}, minmax(0, 1fr))` },
        gap: 1.5,
        mb: 2.5
      }}
    >
      {tiles.map((tile) => {
        const t = tile.tone ? theme.custom.status[tile.tone] : null;
        const content = (
          <>
            <Typography sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: t?.text ?? "text.secondary" }}>
              {tile.label}
            </Typography>
            <Typography sx={{ fontSize: 22, fontWeight: 700, color: t?.text ?? "text.primary" }}>{tile.value}</Typography>
          </>
        );
        const sx = {
          p: 1.75,
          borderRadius: 1.5,
          border: "1px solid",
          borderColor: t?.border ?? "divider",
          bgcolor: t?.bg ?? "background.paper",
          boxShadow: "none",
          textAlign: "left" as const,
          display: "flex",
          flexDirection: "column" as const,
          gap: 0.25
        };
        return tile.onClick ? (
          <Paper key={tile.label} component="button" type="button" onClick={tile.onClick} sx={{ ...sx, cursor: "pointer", font: "inherit" }}>
            {content}
          </Paper>
        ) : (
          <Paper key={tile.label} sx={sx}>
            {content}
          </Paper>
        );
      })}
    </Box>
  );
}
