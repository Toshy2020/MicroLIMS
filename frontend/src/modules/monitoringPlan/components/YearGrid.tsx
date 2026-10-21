import { Box, ButtonBase, Typography } from "@mui/material";
import { MONTH_NAMES } from "../statusDisplay";
import type { YearSummary } from "../types";

// Twelve month tiles; picking one drills into that month's days.
export function YearGrid({ summary, onPick }: { summary: YearSummary | null; onPick: (month: number) => void }) {
  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5 }}>
      {MONTH_NAMES.map((name, i) => {
        const m = summary?.months.find((x) => x.month === i + 1);
        return (
          <ButtonBase
            key={name}
            onClick={() => onPick(i)}
            sx={{
              p: 1.5, textAlign: "left", display: "block", border: 1, borderColor: "divider", borderRadius: 2,
              "&:hover": { bgcolor: "action.hover" }
            }}
          >
            <Typography sx={{ fontWeight: 600 }}>{name}</Typography>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {m && m.total > 0 ? `${m.performed} of ${m.total} performed` : "Nothing planned"}
            </Typography>
            {m && m.total > 0 && (
              <Typography sx={{ fontSize: 12 }}>
                {m.overdue > 0 && <Box component="span" sx={{ color: "error.main", mr: 1 }}>{m.overdue} overdue</Box>}
                {m.inProgress > 0 && <Box component="span" sx={{ color: "info.main", mr: 1 }}>{m.inProgress} in progress</Box>}
                {m.upcoming > 0 && <Box component="span" sx={{ color: "text.secondary", mr: 1 }}>{m.upcoming} upcoming</Box>}
                {m.skipped > 0 && <Box component="span" sx={{ color: "text.secondary" }}>{m.skipped} skipped</Box>}
              </Typography>
            )}
          </ButtonBase>
        );
      })}
    </Box>
  );
}
