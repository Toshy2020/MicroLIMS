import { Skeleton, Stack } from "@mui/material";

// Loading placeholder for a dialog / panel body: a heading line, then
// `blocks` content lines. Announced to screen readers like LoadingSpinner.
export function DialogBodySkeleton({ blocks = 4, blockHeight = 40 }: { blocks?: number; blockHeight?: number }) {
  return (
    <Stack spacing={1.5} role="status" aria-live="polite" aria-label="Loading…" sx={{ py: 1 }}>
      <Skeleton variant="text" width="45%" height={28} />
      {Array.from({ length: blocks }).map((_, i) => (
        <Skeleton key={i} variant="rounded" height={blockHeight} />
      ))}
    </Stack>
  );
}
