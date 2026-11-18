import { useState } from "react";
import { Box, ButtonBase, IconButton, Popover, Typography, useTheme } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import {
  EQUIPMENT_STATUS_KEY,
  EQUIPMENT_STATUS_SYMBOL,
  EQUIPMENT_STATUS_TOKEN,
  STATUS_KEY,
  STATUS_SYMBOL,
  STATUS_TOKEN,
  WEEKDAY_INDICES,
  isWeekend,
  isoDate
} from "../statusDisplay";
import type { CalendarEventResponse, EquipmentTaskItem, OccurrenceSummary } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";
import { StatusPill } from "./StatusPill";

const VISIBLE_PER_DAY = 2;
const EVENT_SYMBOL = "★";

type GridItem =
  | { kind: "occurrence"; data: OccurrenceSummary }
  | { kind: "task"; data: EquipmentTaskItem }
  | { kind: "event"; data: CalendarEventResponse };

function OccurrenceChip({ o, onPick }: { o: OccurrenceSummary; onPick: (o: OccurrenceSummary) => void }) {
  const { t } = useTranslation("monitoring");
  const tokens = useTheme().custom.status[STATUS_TOKEN[o.status]];
  return (
    <ButtonBase
      onClick={(e) => {
        e.stopPropagation();
        onPick(o);
      }}
      aria-label={`${o.target}${o.category === "Water" && o.labSectionCode ? ` (${o.labSectionCode})` : ""}, ${t(STATUS_KEY[o.status])}`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>{STATUS_SYMBOL[o.status]}</Box>
      <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", flex: 1 }}>
        <Ltr>{o.target}</Ltr>
      </Box>
      {o.category === "Water" && o.labSectionCode && (
        <Box
          component="span"
          sx={{
            flex: "none",
            fontSize: 9,
            fontWeight: 700,
            px: 0.5,
            py: 0.1,
            borderRadius: 0.5,
            bgcolor: "action.selected",
            letterSpacing: "0.02em"
          }}
        >
          <Ltr>{o.labSectionCode}</Ltr>
        </Box>
      )}
    </ButtonBase>
  );
}

function EquipmentTaskChip({
  task,
  onPick
}: {
  task: EquipmentTaskItem;
  onPick?: (task: EquipmentTaskItem) => void;
}) {
  const { t } = useTranslation("monitoring");
  const tokens = useTheme().custom.status[EQUIPMENT_STATUS_TOKEN[task.status]];
  return (
    <ButtonBase
      onClick={(e) => {
        e.stopPropagation();
        onPick?.(task);
      }}
      aria-label={`${task.equipmentCode}, ${task.type}, ${t(EQUIPMENT_STATUS_KEY[task.status])}`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>
        {EQUIPMENT_STATUS_SYMBOL[task.status]}
      </Box>
      <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
        <Ltr>{task.equipmentCode}</Ltr> · {task.type === "Calibration" ? t("monthGrid.calShort") : t("monthGrid.pmShort")}
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
  const { t } = useTranslation("monitoring");
  const theme = useTheme();
  const tokens = e.reportPending ? theme.custom.status.inconclusive : theme.custom.status.info;
  return (
    <ButtonBase
      onClick={(ev) => {
        ev.stopPropagation();
        onPick?.(e);
      }}
      aria-label={`${e.title}, ${t("monthGrid.event")}`}
      sx={{
        display: "flex", alignItems: "center", gap: 0.5, width: "100%", textAlign: "left", fontSize: 12, lineHeight: 1.4,
        px: 0.5, py: 0.25, mt: 0.375, borderRadius: 0.5, overflow: "hidden",
        color: tokens.text, bgcolor: tokens.bg, borderLeft: 3, borderLeftColor: tokens.text, border: `1px solid ${tokens.border}`,
        "&:hover": { filter: "brightness(0.96)" }
      }}
    >
      <Box component="span" aria-hidden="true" sx={{ flex: "none", fontWeight: 700 }}>
        {EVENT_SYMBOL}
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
  const { t } = useTranslation("monitoring");
  const [more, setMore] = useState<{ anchor: HTMLElement; iso: string } | null>(null);
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const lead = first.getUTCDay(); // Sunday = 0

  const byDay = new Map<string, GridItem[]>();

  for (const o of occurrences) {
    const key = o.targetDate ?? o.windowEnd;
    byDay.set(key, [...(byDay.get(key) ?? []), { kind: "occurrence", data: o }]);
  }

  for (const task of tasks) {
    const key = task.dueDate;
    byDay.set(key, [...(byDay.get(key) ?? []), { kind: "task", data: task }]);
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
  const monthName = t(`months.${month}`);

  return (
    <>
      {monthEvents.length > 0 && (
        <Box sx={{ mb: 1.5, p: 1, bgcolor: "background.paper", borderRadius: 1, border: 1, borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mb: 0.5 }}>
            {t("monthGrid.eventsThisMonth")}
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
                  <StatusPill tone="inconclusive" size="small">{t("monthGrid.reportPending")}</StatusPill>
                )}
              </ButtonBase>
            ))}
          </Box>
        </Box>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(7, minmax(0, 1fr))", gap: 0.5 }}>
        {WEEKDAY_INDICES.map((i) => (
          <Typography key={i} sx={{ fontSize: 12, fontWeight: 600, textAlign: "center", color: isWeekend(i) ? "text.disabled" : "text.secondary" }}>
            {t(`weekdays.${i}`)}
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
              aria-label={t(isToday ? "monthGrid.dayAriaToday" : "monthGrid.dayAria", { day, month: monthName, count: items.length })}
              // Any day takes an event: click the free space of the cell, or its + button
              // (the keyboard path). Chips and "+N more" stop propagation.
              onClick={() => onAddEvent?.(iso)}
              sx={{
                minHeight: { xs: 64, sm: 104 }, p: 0.5, border: isToday ? 2 : 1, borderRadius: 1,
                borderColor: isToday ? "primary.main" : "divider",
                bgcolor: isWeekend(i % 7) ? "action.hover" : "transparent",
                cursor: onAddEvent ? "pointer" : "default",
                "&:hover .add-event, & .add-event:focus-visible": { opacity: 1 }
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <Box
                  component="span"
                  sx={{
                    display: "inline-flex", alignItems: "center", justifyContent: "center", minWidth: 22, height: 22, px: 0.5, borderRadius: 11,
                    fontSize: 12, fontWeight: isToday ? 700 : 400,
                    ...(isToday && { bgcolor: "primary.main", color: "primary.contrastText" })
                  }}
                >
                  <Ltr>{day}</Ltr>
                </Box>
                {onAddEvent && (
                  <IconButton
                    className="add-event"
                    size="small"
                    aria-label={t("monthGrid.addEventOn", { day, month: monthName })}
                    onClick={(e) => {
                      e.stopPropagation();
                      onAddEvent(iso);
                    }}
                    sx={{ p: 0.25, opacity: isEmpty ? 0.6 : 0, transition: "opacity 150ms" }}
                  >
                    <AddIcon sx={{ fontSize: 16 }} />
                  </IconButton>
                )}
              </Box>
              {items.slice(0, VISIBLE_PER_DAY).map((item) => {
                if (item.kind === "occurrence") {
                  return <OccurrenceChip key={`occ-${item.data.id}`} o={item.data} onPick={onPick} />;
                }
                if (item.kind === "task") {
                  return <EquipmentTaskChip key={`task-${item.data.id}`} task={item.data} onPick={onPickTask} />;
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
                  {t("monthGrid.more", { count: items.length - VISIBLE_PER_DAY })}
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
            {more ? (
              <Trans
                i18nKey="monthGrid.popoverHeader"
                ns="monitoring"
                values={{ day: Number(more.iso.slice(8)), month: monthName, count: moreItems.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            ) : ""}
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
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                    <Typography sx={{ fontSize: 13 }}><Ltr>{o.target}</Ltr></Typography>
                    {o.category === "Water" && o.labSectionCode && (
                      <Box
                        component="span"
                        sx={{
                          fontSize: 10,
                          fontWeight: 700,
                          px: 0.6,
                          py: 0.15,
                          borderRadius: 0.5,
                          bgcolor: "action.selected",
                          letterSpacing: "0.02em"
                        }}
                      >
                        <Ltr>{o.labSectionCode}</Ltr>
                      </Box>
                    )}
                  </Box>
                  <OccurrenceStatusChip status={o.status} late={o.late} />
                </ButtonBase>
              );
            }
            if (item.kind === "task") {
              const task = item.data;
              const tone = EQUIPMENT_STATUS_TOKEN[task.status];
              const symbol = EQUIPMENT_STATUS_SYMBOL[task.status];
              return (
                <ButtonBase
                  key={`task-${task.id}`}
                  onClick={() => { setMore(null); onPickTask?.(task); }}
                  sx={{ display: "flex", width: "100%", justifyContent: "space-between", gap: 1, py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                >
                  <Typography sx={{ fontSize: 13 }}><Ltr>{task.equipmentCode}</Ltr></Typography>
                  <StatusPill tone={tone} symbol={symbol} size="small">{t(EQUIPMENT_STATUS_KEY[task.status])}</StatusPill>
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
                  <StatusPill tone="inconclusive" size="small">{t("monthGrid.reportPending")}</StatusPill>
                ) : (
                  <StatusPill tone="info" size="small">{t("monthGrid.event")}</StatusPill>
                )}
              </ButtonBase>
            );
          })}
        </Box>
      </Popover>
    </>
  );
}
