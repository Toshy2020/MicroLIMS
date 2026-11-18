import { useMemo, useState } from "react";
import { Alert, Box, ButtonBase, Chip, Paper, ToggleButton, ToggleButtonGroup, Typography, useTheme } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import {
  CATEGORY_KEY,
  EQUIPMENT_STATUS_KEY,
  EQUIPMENT_STATUS_SYMBOL,
  EQUIPMENT_STATUS_TOKEN,
  EQUIPMENT_TASK_KEY,
  formatShortDate
} from "../statusDisplay";
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

// One entry per sampling point, so a point missed every week reads as one
// line with its dates instead of one row per week. Most-missed first.
function groupByPoint(items: OccurrenceSummary[]): MissedPoint[] {
  const points = new Map<string, MissedPoint>();
  for (const o of items) {
    const key = `${o.category}|${o.group ?? ""}|${o.target}${o.category === "Water" && o.labSectionId ? `|${o.labSectionId}` : ""}`;
    const point = points.get(key) ?? { key, target: o.target, group: o.group, category: o.category, occurrences: [] };
    point.occurrences.push(o);
    points.set(key, point);
  }
  for (const p of points.values()) p.occurrences.sort((a, b) => occurrenceDate(a).localeCompare(occurrenceDate(b)));
  return [...points.values()].sort((a, b) => b.occurrences.length - a.occurrences.length || a.target.localeCompare(b.target));
}

function MissedPointRow({ point, thisYear, onPick }: { point: MissedPoint; thisYear: string; onPick: (o: OccurrenceSummary) => void }) {
  const { t } = useTranslation("monitoring");
  const tokens = useTheme().custom.status.detected;
  const allMissed = point.occurrences.every((o) => o.status === "Missed");
  const waterLabCode = point.category === "Water" ? point.occurrences[0]?.labSectionCode : undefined;
  return (
    <Box sx={{ display: "flex", justifyContent: "space-between", gap: 1.5, py: 1, px: 0.75, borderBottom: 1, borderColor: "divider" }}>
      <Box sx={{ minWidth: 0 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 600 }}><Ltr>{point.target}</Ltr></Typography>
          {waterLabCode && (
            <Chip size="small" label={<Ltr>{waterLabCode}</Ltr>} sx={{ height: 18, fontSize: 10, fontWeight: 600 }} />
          )}
        </Box>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {point.group ? `${point.group} · ` : ""}{t(CATEGORY_KEY[point.category])}
        </Typography>
        <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.5, mt: 0.5 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mr: 0.5 }}>
            {allMissed ? t("due.missedOn") : t("due.notPerformedOn")}
          </Typography>
          {point.occurrences.map((o) => {
            const date = formatShortDate(occurrenceDate(o), thisYear, t);
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
                <Ltr>{date}</Ltr>
              </ButtonBase>
            );
          })}
        </Box>
      </Box>
      <Box sx={{ flex: "none" }}>
        <StatusPill tone="detected" symbol="▲">
          <Trans
            i18nKey={allMissed ? "due.missedCount" : "due.notPerformedCount"}
            ns="monitoring"
            values={{ count: point.occurrences.length }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </StatusPill>
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
  const { t } = useTranslation("monitoring");
  const [picked, setPicked] = useState<Filter | null>(null);

  const { current, earlier, points, earlierEq, currentEq, totalCount } = useMemo(() => {
    const thisMonth = due?.today.slice(0, 7) ?? "";
    const items = due?.items ?? [];
    const earlierItems = items.filter((o) => o.windowEnd.slice(0, 7) < thisMonth);

    // Active equipment tasks
    const activeEq = equipmentTasks.filter((item) => !item.completedOn && item.status !== "Done" && item.status !== "Retired");
    const eEq = activeEq.filter((item) => item.dueDate.slice(0, 7) < thisMonth && item.status === "Overdue");
    const cEq = activeEq.filter((item) => !eEq.includes(item));

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
      <Typography sx={{ fontWeight: 600, mb: 1 }}>{t("due.title")}</Typography>
      {earlier.length > 0 && (
        <Alert severity="error" sx={{ mb: 1 }}>
          <Trans
            i18nKey="due.earlierAlert"
            ns="monitoring"
            values={{ points: points.length, occurrences: earlier.length }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
      )}
      {earlierEq.length > 0 && (
        <Alert severity="error" sx={{ mb: 1 }}>
          <Trans
            i18nKey="due.earlierEquipmentAlert"
            ns="monitoring"
            values={{ count: earlierEq.length }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
      )}
      {due.showWarning && current.length > 0 && (
        <Alert severity="warning" sx={{ mb: 1 }}>
          <Trans
            i18nKey="due.monthWarning"
            ns="monitoring"
            values={{ day: Number(due.today.slice(8)), count: current.length }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
      )}
      <ToggleButtonGroup
        exclusive size="small" value={filter} aria-label={t("due.filterAria")} sx={{ mb: 1 }}
        onChange={(_, v: Filter | null) => { if (v) setPicked(v); }}
      >
        <ToggleButton value="month">
          <Trans i18nKey="due.filterMonth" ns="monitoring" values={{ count: currentTotal }} components={{ ltr: <Ltr>{null}</Ltr> }} />
        </ToggleButton>
        <ToggleButton value="earlier">
          <Trans i18nKey="due.filterEarlier" ns="monitoring" values={{ count: earlierTotal }} components={{ ltr: <Ltr>{null}</Ltr> }} />
        </ToggleButton>
        <ToggleButton value="all">
          <Trans i18nKey="due.filterAll" ns="monitoring" values={{ count: totalCount }} components={{ ltr: <Ltr>{null}</Ltr> }} />
        </ToggleButton>
      </ToggleButtonGroup>

      {!showEarlier && !showCurrent && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("due.nothingOutstanding")}</Typography>}
      <Box sx={{ maxHeight: 320, overflowY: "auto" }} tabIndex={0} aria-label={t("due.listAria")}>
        {showEarlier && (
          <Box>
            {points.length > 0 && (
              <>
                {sectionHeading(t("due.earlierPointsHeading", { count: points.length }))}
                {points.map((p) => <MissedPointRow key={p.key} point={p} thisYear={thisYear} onPick={onPick} />)}
              </>
            )}
            {earlierEq.length > 0 && (
              <>
                {sectionHeading(t("due.earlierEquipmentHeading", { count: earlierEq.length }))}
                {earlierEq.map((item) => (
                  <ButtonBase
                    key={`eq-${item.id}`}
                    onClick={() => onPickTask?.(item)}
                    sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                  >
                    <Box>
                      <Typography sx={{ fontSize: 14 }}><Ltr>{item.equipmentCode}</Ltr></Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {t(EQUIPMENT_TASK_KEY[item.type])} ·{" "}
                        <Trans i18nKey="due.dueDate" ns="monitoring" values={{ date: item.dueDate }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                      </Typography>
                    </Box>
                    <StatusPill tone="detected" symbol="▲" size="small">{t("statuses.Overdue")}</StatusPill>
                  </ButtonBase>
                ))}
              </>
            )}
          </Box>
        )}
        {showCurrent && (
          <Box>
            {sectionHeading(t("due.thisMonthHeading", { count: currentTotal }))}
            {current.map((o) => (
              <ButtonBase
                key={o.id}
                onClick={() => onPick(o)}
                sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
              >
                <Box>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                    <Typography sx={{ fontSize: 14 }}><Ltr>{o.target}</Ltr></Typography>
                    {o.category === "Water" && o.labSectionCode && (
                      <Chip size="small" label={<Ltr>{o.labSectionCode}</Ltr>} sx={{ height: 18, fontSize: 10, fontWeight: 600 }} />
                    )}
                  </Box>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {o.group ? `${o.group} · ` : ""}
                    <Trans
                      i18nKey={o.windowEnd < due.today ? "due.windowEnded" : "due.windowEnds"}
                      ns="monitoring"
                      values={{ date: o.windowEnd }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  </Typography>
                </Box>
                <OccurrenceStatusChip status={o.status} late={o.late} />
              </ButtonBase>
            ))}
            {currentEq.map((item) => {
              const tone = EQUIPMENT_STATUS_TOKEN[item.status];
              const symbol = EQUIPMENT_STATUS_SYMBOL[item.status];
              return (
                <ButtonBase
                  key={`eq-${item.id}`}
                  onClick={() => onPickTask?.(item)}
                  sx={{ display: "flex", width: "100%", justifyContent: "space-between", py: 0.5, px: 0.75, textAlign: "left", borderRadius: 1, "&:hover": { bgcolor: "action.hover" } }}
                >
                  <Box>
                    <Typography sx={{ fontSize: 14 }}><Ltr>{item.equipmentCode}</Ltr></Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {t(EQUIPMENT_TASK_KEY[item.type])} ·{" "}
                      <Trans i18nKey="due.dueDate" ns="monitoring" values={{ date: item.dueDate }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </Typography>
                  </Box>
                  <StatusPill tone={tone} symbol={symbol} size="small">{t(EQUIPMENT_STATUS_KEY[item.status])}</StatusPill>
                </ButtonBase>
              );
            })}
          </Box>
        )}
      </Box>
    </Paper>
  );
}
