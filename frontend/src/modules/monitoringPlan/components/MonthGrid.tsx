import { Box, ButtonBase, Typography } from "@mui/material";
import { WEEKDAY_LABELS, isWeekend, isoDate, STATUS_TONE } from "../statusDisplay";
import type { OccurrenceSummary } from "../types";

// Day cells for one month, Sunday first. An occurrence shows on its target
// date, or on the last day of its window when it has no target date.
export function MonthGrid({
  year, month, occurrences, today, onPick
}: {
  year: number;
  month: number;
  occurrences: OccurrenceSummary[];
  today: string;
  onPick: (o: OccurrenceSummary) => void;
}) {
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const lead = first.getUTCDay(); // Sunday = 0

  const byDay = new Map<string, OccurrenceSummary[]>();
  for (const o of occurrences) {
    const key = o.targetDate ?? o.windowEnd;
    byDay.set(key, [...(byDay.get(key) ?? []), o]);
  }

  const cells: (number | null)[] = [...Array(lead).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: "repeat(7, minmax(0, 1fr))", gap: 0.5 }}>
      {WEEKDAY_LABELS.map((d, i) => (
        <Typography key={d} sx={{ fontSize: 12, fontWeight: 600, textAlign: "center", color: isWeekend(i) ? "text.disabled" : "text.secondary" }}>
          {d}
        </Typography>
      ))}
      {cells.map((day, i) => {
        if (day === null) return <Box key={`b${i}`} />;
        const iso = isoDate(year, month, day);
        const items = byDay.get(iso) ?? [];
        return (
          <Box
            key={iso}
            sx={{
              minHeight: 84, p: 0.5, border: 1, borderRadius: 1,
              borderColor: iso === today ? "primary.main" : "divider",
              bgcolor: isWeekend(i % 7) ? "action.hover" : "transparent"
            }}
          >
            <Typography sx={{ fontSize: 12, fontWeight: iso === today ? 700 : 400 }}>{day}</Typography>
            {items.slice(0, 3).map((o) => (
              <ButtonBase
                key={o.id}
                onClick={() => onPick(o)}
                sx={{
                  display: "block", width: "100%", textAlign: "left", fontSize: 11, px: 0.5, mt: 0.25, borderRadius: 0.5,
                  overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
                  bgcolor: (t) => {
                    const tone = STATUS_TONE[o.status];
                    return tone === "default" ? t.palette.action.selected : t.palette[tone].light;
                  },
                  color: (t) => {
                    const tone = STATUS_TONE[o.status];
                    return tone === "default" ? t.palette.text.primary : t.palette[tone].contrastText;
                  }
                }}
              >
                {o.target}
              </ButtonBase>
            ))}
            {items.length > 3 && <Typography sx={{ fontSize: 11, color: "text.secondary" }}>+{items.length - 3} more</Typography>}
          </Box>
        );
      })}
    </Box>
  );
}
