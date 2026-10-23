import { useState } from "react";
import { Box, ButtonBase, Popover, Typography, useTheme } from "@mui/material";
import {
  EQUIPMENT_STATUS_LABEL,
  EQUIPMENT_STATUS_SYMBOL,
  EQUIPMENT_STATUS_TOKEN,
  MONTH_NAMES,
  STATUS_LABEL,
  STATUS_SYMBOL,
  STATUS_TOKEN,
  WEEKDAY_LABELS,
  isWeekend,
  isoDate
} from "../statusDisplay";
import type { CalendarEventResponse, EquipmentTaskItem, OccurrenceSummary } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";
import { StatusPill } from "./StatusPill";

const VISIBLE_PER_DAY = 2;

type GridItem =
  | { kind: "occurrence"; data: OccurrenceSummary }
  | { kind: "task"; data: EquipmentTaskItem }
  | { kind: "event"; data: CalendarEventResponse };

function OccurrenceChip({ o, onPick }: { o: OccurrenceSummary; onPick: (o: OccurrenceSummary) => void }) {
  const tokens = useTheme().custom.status[STATUS_TOKEN[o.status]];
  return (
    <ButtonBase
      onClick={(e) => {
        e.stopPropagation();
        onPick(o);
      }}
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

function EquipmentTaskChip({
  t,
  onPick
}: {
  t: EquipmentTaskItem;
  onPick?: (t: EquipmentTaskItem) => void;
}) {
  const tokens = useTheme().custom.status[EQUIPMENT_STATUS_TOKEN[t.status]];
  return (
    <ButtonBase
      onClick={(e) => {
        e.stopPropagation();
        onPick?.(t);
      }}
      aria-label={`${t.equipmentCode}, ${t.type}, ${EQUIPMENT_STATUS_LABEL[t.status]}`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>
        {EQUIPMENT_STATUS_SYMBOL[t.status]}
      </Box>
      <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
        {t.equipmentCode} · {t.type === "Calibration" ? "Cal" : "PM"}
      </Box>
    </ButtonBase>
  );
}

function EventChip({
  e,
  onPick
}: {
  e: CalendarEventResponse;
  onPick?: (e: CalendarEventResponse) => void;
}) {
  const theme = useTheme();
  const tokens = e.reportPending ? theme.custom.status.inconclusive : theme.custom.status.info;
  return (
    <ButtonBase
      onClick={(ev) => {
        ev.stopPropagation();
        onPick?.(e);
      }}
      aria-label={`${e.title}, Event`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>
        ★
      </Box>
      <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
        {e.title}
      </Box>
    </ButtonBase>
  );
}

export interface MonthGridProps {
  year: number;
  month: number;
  occurrences: OccurrenceSummary[];
  tasks?: EquipmentTaskItem[];
  events?: CalendarEventResponse[];
  today: string;
  onPick: (o: OccurrenceSummary) => void;
  onPickTask?: (t: EquipmentTaskItem) => void;
  onPickEvent?: (e: CalendarEventResponse) => void;
  onAddEvent?: (date: string) => void;
}

// Day cells for one month, Sunday first. Shows monitoring occurrences,
// equipment calibration/PM tasks, and calendar events.
// Clicking an empty day calls onAddEvent(date).
export function MonthGrid({
  year,
  month,
  occurrences,
  tasks = [],
  events = [],
  today,
  onPick,
  onPickTask,
  onPickEvent,
  onAddEvent
}: MonthGridProps) {
  const [more, setMore] = useState<{ anchor: HTMLElement; iso: string } | null>(null);
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const lead = first.getUTCDay(); // Sunday = 0

  const byDay = new Map<string, GridItem[]>();

  for (const o of occurrences) {
    const key = o.targetDate ?? o.windowEnd;
    byDay.set(key, [...(byDay.get(key) ?? []), { kind: "occurrence", data: o }]);
  }

  for (const t of tasks) {
    const key = t.dueDate;
    byDay.set(key, [...(byDay.get(key) ?? []), { kind: "task", data: t }]);
  }

  const monthStr = `${year}-${String(month + 1).padStart(2, "0")}`;
  const monthEvents: CalendarEventResponse[] = [];
  for (const e of events) {
    if (e.date) {
      byDay.set(e.date, [...(byDay.get(e.date) ?? []), { kind: "event", data: e }]);
    } else if (e.month === monthStr) {
      monthEvents.push(e);
    }
  }

  const cells: (number | null)[] = [...Array(lead).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];
  const moreItems = more ? byDay.get(more.iso) ?? [] : [];

  return (
    <>
      {monthEvents.length > 0 && (
        <Box sx={{ mb: 1.5, p: 1, bgcolor: "background.paper", borderRadius: 1, border: 1, borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mb: 0.5 }}>
            Events this month
          </Typography>
          <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
            {monthEvents.map((ev) => (
              <ButtonBase
                key={ev.id}
                onClick={() => onPickEvent?.(ev)}
                sx={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 0.5,
                  px: 1,
                  py: 0.375,
                  borderRadius: 1,
                  fontSize: 12,
                  bgcolor: "action.hover",
                  border: 1,
                  borderColor: "divider",
                  "&:hover": { bgcolor: "action.selected" }
                }}
              >
                <Typography sx={{ fontSize: 12, fontWeight: 500 }}>{ev.title}</Typography>
                {ev.reportPending && (
                  <StatusPill tone="inconclusive" size="small">Report pending</StatusPill>
                )}
              </ButtonBase>
            ))}
          </Box>
        </Box>
      )}

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
          const isEmpty = items.length === 0;

          return (
            <Box
              key={iso}
              role="group"
              aria-label={`${day} ${MONTH_NAMES[month]}${isToday ? ", today" : ""}, ${items.length} item(s)`}
              onClick={() => {
                if (isEmpty && onAddEvent) {
                  onAddEvent(iso);
                }
              }}
              sx={{
                minHeight: { xs: 64, sm: 104 }, p: 0.5, border: isToday ? 2 : 1, borderRadius: 1,
                borderColor: isToday ? "primary.main" : "divider",
                bgcolor: isWeekend(i % 7) ? "action.hover" : "transparent",
                cursor: isEmpty && onAddEvent ? "pointer" : "default"
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
              {items.slice(0, VISIBLE_PER_DAY).map((item) => {
                if (item.kind === "occurrence") {
                  return <OccurrenceChip key={`occ-${item.data.id}`} o={item.data} onPick={onPick} />;
                }
                if (item.kind === "task") {
                  return <EquipmentTaskChip key={`task-${item.data.id}`} t={item.data} onPick={onPickTask} />;
                }
                return <EventChip key={`evt-${item.data.id}`} e={item.data} onPick={onPickEvent} />;
              })}
              {items.length > VISIBLE_PER_DAY && (
                <ButtonBase
                  aria-haspopup="dialog"
                  onClick={(e) => {
                    e.stopPropagation();
                    setMore({ anchor: e.currentTarget, iso });
                  }}
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
            {more ? `${Number(more.iso.slice(8))} ${MONTH_NAMES[month]} · ${moreItems.length} items` : ""}
          </Typography>
          {moreItems.map((item) => {
            if (item.kind === "occurrence") {
              const o = item.data;
              return (
                <ButtonBase
                  key={`occ-${o.id}`}
                  onClick={() => { setMore(null); onPick(o); }}
                  sx={{ display: "flex", width: "100%", justifyContent: "space-between", gap: 1, py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                >
                  <Typography sx={{ fontSize: 13 }}>{o.target}</Typography>
                  <OccurrenceStatusChip status={o.status} late={o.late} />
                </ButtonBase>
              );
            }
            if (item.kind === "task") {
              const t = item.data;
              const tone = EQUIPMENT_STATUS_TOKEN[t.status];
              const symbol = EQUIPMENT_STATUS_SYMBOL[t.status];
              return (
                <ButtonBase
                  key={`task-${t.id}`}
                  onClick={() => { setMore(null); onPickTask?.(t); }}
                  sx={{ display: "flex", width: "100%", justifyContent: "space-between", gap: 1, py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                >
                  <Typography sx={{ fontSize: 13 }}>{t.equipmentCode}</Typography>
                  <StatusPill tone={tone} symbol={symbol} size="small">{t.status}</StatusPill>
                </ButtonBase>
              );
            }
            const ev = item.data;
            return (
              <ButtonBase
                key={`evt-${ev.id}`}
                onClick={() => { setMore(null); onPickEvent?.(ev); }}
                sx={{ display: "flex", width: "100%", justifyContent: "space-between", gap: 1, py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
              >
                <Typography sx={{ fontSize: 13 }}>{ev.title}</Typography>
                {ev.reportPending ? (
                  <StatusPill tone="inconclusive" size="small">Report pending</StatusPill>
                ) : (
                  <StatusPill tone="info" size="small">Event</StatusPill>
                )}
              </ButtonBase>
            );
          })}
        </Box>
      </Popover>
    </>
  );
}
