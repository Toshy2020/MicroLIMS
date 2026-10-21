import { Alert, Box, ButtonBase, Paper, Typography } from "@mui/material";
import type { DueResult, OccurrenceSummary } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";

// What is still to do this month plus anything overdue. From the warning
// day of the month on it opens with a notice.
export function DuePanel({ due, onPick }: { due: DueResult | null; onPick: (o: OccurrenceSummary) => void }) {
  if (!due) return null;
  return (
    <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
      <Typography sx={{ fontWeight: 600, mb: 1 }}>Due this month</Typography>
      {due.showWarning && due.items.length > 0 && (
        <Alert severity="warning" sx={{ mb: 1 }}>
          It is day {Number(due.today.slice(8))} of the month and {due.items.length} occurrence(s) are still not performed.
        </Alert>
      )}
      {due.items.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Nothing outstanding.</Typography>}
      {due.items.map((o) => (
        <ButtonBase key={o.id} onClick={() => onPick(o)} sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, textAlign: "left" }}>
          <Box>
            <Typography sx={{ fontSize: 14 }}>{o.target}</Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {o.group ? `${o.group} · ` : ""}window ends {o.windowEnd}
            </Typography>
          </Box>
          <OccurrenceStatusChip status={o.status} late={o.late} />
        </ButtonBase>
      ))}
    </Paper>
  );
}
