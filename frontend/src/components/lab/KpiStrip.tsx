import { Box, Skeleton } from "@mui/material";
import { SummaryTiles, SummaryTile } from "../configHierarchy/SummaryTiles";

interface KpiStripProps {
  tiles: SummaryTile[];
  loading?: boolean;
}

export function KpiStrip({ tiles, loading }: KpiStripProps) {
  if (!loading) return <SummaryTiles tiles={tiles} />;
  // Same grid as SummaryTiles so the layout doesn't jump when data arrives.
  const count = Math.max(tiles.length, 1);
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "repeat(2, minmax(0, 1fr))", md: `repeat(${count}, minmax(0, 1fr))` },
        gap: 1.5,
        mb: 2.5
      }}
    >
      {Array.from({ length: count }).map((_, i) => (
        <Skeleton key={i} variant="rounded" height={72} />
      ))}
    </Box>
  );
}
