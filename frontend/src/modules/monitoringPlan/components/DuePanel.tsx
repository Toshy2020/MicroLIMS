import { useMemo, useState } from "react";
import { Alert, Box, ButtonBase, Paper, ToggleButton, ToggleButtonGroup, Typography, useTheme } from "@mui/material";
import { CATEGORY_LABEL, MONTH_NAMES } from "../statusDisplay";
import type { DueResult, EquipmentTaskItem, OccurrenceSummary } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";
import { StatusPill } from "./StatusPill";

type Filter = "month" | "earlier" | "all";

interface MissedPoint {
  key: string;
  target: string;
  group: string | null;
  category: OccurrenceSummary["category"];
  occurrences: OccurrenceSummary[];
}

const occurrenceDate = (o: OccurrenceSummary) => o.targetDate ?? o.windowEnd;

// "8 Jan", or "8 Jan 2025" when it is not in the current year.
const shortDate = (iso: string, thisYear: string) =>
  `${Number(iso.slice(8))} ${MONTH_NAMES[Number(iso.slice(5, 7)) - 1].slice(0, 3)}${iso.startsWith(thisYear) ? "" : ` ${iso.slice(0, 4)}`}`;

// One entry per sampling point, so a point missed every week reads as one
// line with its dates instead of one row per week. Most-missed first.
function groupByPoint(items: OccurrenceSummary[]): MissedPoint[] {
  const points = new Map<string, MissedPoint>();
  for (const o of items) {
    const key = `${o.category}|${o.group ?? ""}|${o.target}`;
    const point = points.get(key) ?? { key, target: o.target, group: o.group, category: o.category, occurrences: [] };
    point.occurrences.push(o);
    points.set(key, point);
  }
  for (const p of points.values()) p.occurrences.sort((a, b) => occurrenceDate(a).localeCompare(occurrenceDate(b)));
  return [...points.values()].sort((a, b) => b.occurrences.length - a.occurrences.length || a.target.localeCompare(b.target));
}

function MissedPointRow({ point, thisYear, onPick }: { point: MissedPoint; thisYear: string; onPick: (o: OccurrenceSummary) => void }) {
  const tokens = useTheme().custom.status.detected;
  const allMissed = point.occurrences.every((o) => o.status === "Missed");
  return (
    <Box sx={{ display: "flex", justifyContent: "space-between", gap: 1.5, py: 1, px: 0.75, borderBottom: 1, borderColor: "divider" }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{point.target}</Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {point.group ? `${point.group} · ` : ""}{CATEGORY_LABEL[point.category]}
        </Typography>
        <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.5, mt: 0.5 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mr: 0.5 }}>{allMissed ? "Missed on" : "Not performed on"}</Typography>
          {point.occurrences.map((o) => {
            const date = shortDate(occurrenceDate(o), thisYear);
            return (
              <ButtonBase
                key={o.id}
                onClick={() => onPick(o)}
                aria-label={`${point.target}, ${date}`}
                sx={{
                  px: 0.75, py: 0.125, borderRadius: 1, fontSize: 12, fontWeight: 500,
                  color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`, "&:hover": { filter: "brightness(0.96)" }
                }}
              >
                {date}
              </ButtonBase>
            );
          })}
        </Box>
      </Box>
      <Box sx={{ flex: "none" }}>
        <StatusPill tone="detected" symbol="▲">{allMissed ? "Missed" : "Not performed"} ×{point.occurrences.length}</StatusPill>
      </Box>
    </Box>
  );
}

export interface DuePanelProps {
  due: DueResult | null;
  equipmentTasks?: EquipmentTaskItem[];
  onPick: (o: OccurrenceSummary) => void;
  onPickTask?: (t: EquipmentTaskItem) => void;
}

// What is still to do this month, kept apart from what was left over from
// earlier months. Shows occurrences and equipment tasks.
export function DuePanel({
  due,
  equipmentTasks = [],
  onPick,
  onPickTask
}: DuePanelProps) {
  const [picked, setPicked] = useState<Filter | null>(null);

  const { current, earlier, points, earlierEq, currentEq, totalCount } = useMemo(() => {
    const thisMonth = due?.today.slice(0, 7) ?? "";
    const items = due?.items ?? [];
    const earlierItems = items.filter((o) => o.windowEnd.slice(0, 7) < thisMonth);

    // Active equipment tasks
    const activeEq = equipmentTasks.filter((t) => !t.completedOn && t.status !== "Done" && t.status !== "Retired");
    const eEq = activeEq.filter((t) => t.dueDate.slice(0, 7) < thisMonth && t.status === "Overdue");
    const cEq = activeEq.filter((t) => !eEq.includes(t));

    return {
      current: items.filter((o) => o.windowEnd.slice(0, 7) >= thisMonth),
      earlier: earlierItems,
      points: groupByPoint(earlierItems),
      earlierEq: eEq,
      currentEq: cEq,
      totalCount: items.length + activeEq.length
    };
  }, [due, equipmentTasks]);

  if (!due) return null;

  const currentTotal = current.length + currentEq.length;
  const earlierTotal = earlier.length + earlierEq.length;

  const filter: Filter = picked ?? (currentTotal > 0 || earlierTotal === 0 ? "month" : "earlier");
  const showEarlier = filter !== "month" && (points.length > 0 || earlierEq.length > 0);
  const showCurrent = filter !== "earlier" && currentTotal > 0;
  const thisYear = due.today.slice(0, 4);

  const sectionHeading = (text: string) => (
    <Typography
      sx={{
        position: "sticky", top: 0, zIndex: 1, bgcolor: "background.paper", px: 0.75, py: 0.5, fontSize: 12, fontWeight: 700,
        color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.04em", borderBottom: 1, borderColor: "divider"
      }}
    >
      {text}
    </Typography>
  );

  return (
    <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
      <Typography sx={{ fontWeight: 600, mb: 1 }}>Due this month</Typography>
      {earlier.length > 0 && (
        <Alert severity="error" sx={{ mb: 1 }}>
          {points.length} sampling point(s) have {earlier.length} occurrence(s) from earlier months that were not performed and need review. Track and close them on the Deviations tab.
        </Alert>
      )}
      {earlierEq.length > 0 && (
        <Alert severity="error" sx={{ mb: 1 }}>
          {earlierEq.length} equipment task(s) from earlier months are overdue and need attention.
        </Alert>
      )}
      {due.showWarning && current.length > 0 && (
        <Alert severity="warning" sx={{ mb: 1 }}>
          It is day {Number(due.today.slice(8))} of the month and {current.length} occurrence(s) this month are still not performed.
        </Alert>
      )}
      <ToggleButtonGroup
        exclusive size="small" value={filter} aria-label="Filter the due list" sx={{ mb: 1 }}
        onChange={(_, v: Filter | null) => { if (v) setPicked(v); }}
      >
        <ToggleButton value="month">This month ({currentTotal})</ToggleButton>
        <ToggleButton value="earlier">Earlier months ({earlierTotal})</ToggleButton>
        <ToggleButton value="all">All ({totalCount})</ToggleButton>
      </ToggleButtonGroup>

      {!showEarlier && !showCurrent && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Nothing outstanding.</Typography>}
      <Box sx={{ maxHeight: 320, overflowY: "auto" }} tabIndex={0} aria-label="Due occurrences">
        {showEarlier && (
          <Box>
            {points.length > 0 && (
              <>
                {sectionHeading(`Earlier months · ${points.length} sampling point(s)`)}
                {points.map((p) => <MissedPointRow key={p.key} point={p} thisYear={thisYear} onPick={onPick} />)}
              </>
            )}
            {earlierEq.length > 0 && (
              <>
                {sectionHeading(`Earlier months · ${earlierEq.length} equipment task(s)`)}
                {earlierEq.map((t) => (
                  <ButtonBase
                    key={`eq-${t.id}`}
                    onClick={() => onPickTask?.(t)}
                    sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                  >
                    <Box>
                      <Typography sx={{ fontSize: 14 }}>{t.equipmentCode}</Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {t.type === "Calibration" ? "Calibration" : "Preventive maintenance"} · due {t.dueDate}
                      </Typography>
                    </Box>
                    <StatusPill tone="detected" symbol="▲" size="small">Overdue</StatusPill>
                  </ButtonBase>
                ))}
              </>
            )}
          </Box>
        )}
        {showCurrent && (
          <Box>
            {sectionHeading(`This month · ${currentTotal}`)}
            {current.map((o) => (
              <ButtonBase
                key={o.id}
                onClick={() => onPick(o)}
                sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
              >
                <Box>
                  <Typography sx={{ fontSize: 14 }}>{o.target}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {o.group ? `${o.group} · ` : ""}window {o.windowEnd < due.today ? "ended" : "ends"} {o.windowEnd}
                  </Typography>
                </Box>
                <OccurrenceStatusChip status={o.status} late={o.late} />
              </ButtonBase>
            ))}
            {currentEq.map((t) => {
              const tone = t.status === "Overdue" ? "detected" : t.status === "Due" ? "inconclusive" : "pending";
              const symbol = t.status === "Overdue" ? "▲" : t.status === "Due" ? "●" : "○";
              return (
                <ButtonBase
                  key={`eq-${t.id}`}
                  onClick={() => onPickTask?.(t)}
                  sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                >
                  <Box>
                    <Typography sx={{ fontSize: 14 }}>{t.equipmentCode}</Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {t.type === "Calibration" ? "Calibration" : "Preventive maintenance"} · due {t.dueDate}
                    </Typography>
                  </Box>
                  <StatusPill tone={tone} symbol={symbol} size="small">{t.status}</StatusPill>
                </ButtonBase>
              );
            })}
          </Box>
        )}
      </Box>
    </Paper>
  );
}
