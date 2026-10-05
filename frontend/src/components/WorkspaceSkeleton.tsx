import { Box, Skeleton, Stack } from "@mui/material";
import { TableSkeleton } from "./TableSkeleton";

// First-load placeholder for a workspace page: the page's own shape (title,
// KPI tiles, register) in grey, so the layout doesn't jump when data lands.
// Announced to screen readers like LoadingSpinner.
export function WorkspaceSkeleton({ tiles = 4, rows = 8 }: { tiles?: number; rows?: number }) {
  return (
    <Stack spacing={2} role="status" aria-live="polite" aria-label="Loading…">
      <Box>
        <Skeleton variant="text" width={260} height={36} />
        <Skeleton variant="text" width={380} height={20} />
      </Box>
      {tiles > 0 && (
        <Box sx={{ display: "grid", gap: 1.5, gridTemplateColumns: { xs: "repeat(2, minmax(0, 1fr))", md: `repeat(${tiles}, minmax(0, 1fr))` } }}>
          {Array.from({ length: tiles }).map((_, i) => (
            <Skeleton key={i} variant="rounded" height={72} />
          ))}
        </Box>
      )}
      <TableSkeleton rows={rows} cols={6} />
    </Stack>
  );
}
