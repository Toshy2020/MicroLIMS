import { useState } from "react";
import { Box, ButtonBase, Popover, Typography, useTheme } from "@mui/material";
import { MONTH_NAMES, STATUS_LABEL, STATUS_SYMBOL, STATUS_TOKEN, WEEKDAY_LABELS, isWeekend, isoDate } from "../statusDisplay";
import type { OccurrenceSummary } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";

const VISIBLE_PER_DAY = 2;

function OccurrenceChip({ o, onPick }: { o: OccurrenceSummary; onPick: (o: OccurrenceSummary) => void }) {
  const tokens = useTheme().custom.status[STATUS_TOKEN[o.status]];
  return (
    <ButtonBase
      onClick={() => onPick(o)}
      aria-label={`${o.target}, ${STATUS_LABEL[o.status]}`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>{STATUS_SYMBOL[o.status]}</Box>
      <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{o.target}</Box>
    </ButtonBase>
  );
}

// Day cells for one month, Sunday first. An occurrence shows on its target
// date, or on the last day of its window when it has no target date. Days
// with more than two occurrences list the rest in a popover.
export function MonthGrid({
  year, month, occurrences, today, onPick
}: {
  year: number;
  month: number;
  occurrences: OccurrenceSummary[];
  today: string;
  onPick: (o: OccurrenceSummary) => void;
}) {
  const [more, setMore] = useState<{ anchor: HTMLElement; iso: string } | null>(null);
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const lead = first.getUTCDay(); // Sunday = 0

  const byDay = new Map<string, OccurrenceSummary[]>();
  for (const o of occurrences) {
    const key = o.targetDate ?? o.windowEnd;
    byDay.set(key, [...(byDay.get(key) ?? []), o]);
  }

  const cells: (number | null)[] = [...Array(lead).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];
  const moreItems = more ? byDay.get(more.iso) ?? [] : [];

  return (
    <>
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
          const isToday = iso === today;
          return (
            <Box
              key={iso}
              role="group"
              aria-label={`${day} ${MONTH_NAMES[month]}${isToday ? ", today" : ""}, ${items.length} occurrence(s)`}
              sx={{
                minHeight: { xs: 64, sm: 104 }, p: 0.5, border: isToday ? 2 : 1, borderRadius: 1,
                borderColor: isToday ? "primary.main" : "divider",
                bgcolor: isWeekend(i % 7) ? "action.hover" : "transparent"
              }}
            >
              <Box
                component="span"
                sx={{
                  display: "inline-flex", alignItems: "center", justifyContent: "center", minWidth: 22, height: 22, px: 0.5, borderRadius: 11,
                  fontSize: 12, fontWeight: isToday ? 700 : 400,
                  ...(isToday && { bgcolor: "primary.main", color: "primary.contrastText" })
                }}
              >
                {day}
              </Box>
              {items.slice(0, VISIBLE_PER_DAY).map((o) => <OccurrenceChip key={o.id} o={o} onPick={onPick} />)}
              {items.length > VISIBLE_PER_DAY && (
                <ButtonBase
                  aria-haspopup="dialog"
                  onClick={(e) => setMore({ anchor: e.currentTarget, iso })}
                  sx={{ mt: 0.375, px: 0.5, borderRadius: 0.5, fontSize: 12, fontWeight: 600, color: "primary.main", "&:hover": { bgcolor: "action.hover" } }}
                >
                  +{items.length - VISIBLE_PER_DAY} more
                </ButtonBase>
              )}
            </Box>
          );
        })}
      </Box>

      <Popover
        open={more !== null} anchorEl={more?.anchor} onClose={() => setMore(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
      >
        <Box sx={{ p: 1.5, width: 300, maxWidth: "calc(100vw - 32px)" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 600, mb: 0.5 }}>
            {more ? `${Number(more.iso.slice(8))} ${MONTH_NAMES[month]} · ${moreItems.length} occurrences` : ""}
          </Typography>
          {moreItems.map((o) => (
            <ButtonBase
              key={o.id}
              onClick={() => { setMore(null); onPick(o); }}
              sx={{ display: "flex", width: "100%", justifyContent: "space-between", gap: 1, py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
            >
              <Typography sx={{ fontSize: 13 }}>{o.target}</Typography>
              <OccurrenceStatusChip status={o.status} late={o.late} />
            </ButtonBase>
          ))}
        </Box>
      </Popover>
    </>
  );
}
