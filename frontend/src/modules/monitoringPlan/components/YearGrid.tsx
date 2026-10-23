import { Box, ButtonBase, Typography, useTheme } from "@mui/material";
import { MONTH_NAMES } from "../statusDisplay";
import type { EquipmentMonthSummary, YearSummary } from "../types";
import { StatusPill } from "./StatusPill";

export interface YearGridProps {
  summary: YearSummary | null;
  equipmentSummary?: EquipmentMonthSummary[] | null;
  year?: number;
  today: string;
  onPick: (month: number) => void;
  onAddMonthEvent?: (monthStr: string) => void;
}

// Twelve month tiles; picking one drills into that month's days. The bar
// shows performed, in progress and overdue as shares of what is planned.
// Each tile also offers "Add event for this month".
export function YearGrid({
  summary,
  equipmentSummary,
  year,
  today,
  onPick,
  onAddMonthEvent
}: YearGridProps) {
  const palette = useTheme().palette;
  const currentYear = year ?? summary?.year ?? Number(today.slice(0, 4));
  const currentMonth = today.startsWith(String(currentYear)) ? Number(today.slice(5, 7)) : null;

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5 }}>
      {MONTH_NAMES.map((name, i) => {
        const m = summary?.months.find((x) => x.month === i + 1);
        const eq = equipmentSummary?.find((x) => x.month === i + 1);

        const totalPlanned = (m?.total ?? 0) + (eq?.planned ?? 0);
        const performed = (m?.performed ?? 0) + (eq?.done ?? 0);
        const overdue = (m?.overdue ?? 0) + (eq?.overdue ?? 0);
        const inProgress = m?.inProgress ?? 0;
        const upcoming = (m?.upcoming ?? 0) + (eq?.upcoming ?? 0);
        const skipped = m?.skipped ?? 0;
        const eventsCount = eq?.events ?? 0;

        const hasPlanned = totalPlanned > 0;
        const share = (n: number) => `${hasPlanned ? (n / totalPlanned) * 100 : 0}%`;
        const current = currentMonth === i + 1;

        return (
          <ButtonBase
            key={name}
            onClick={() => onPick(i)}
            aria-label={
              hasPlanned
                ? `${name}: ${performed} of ${totalPlanned} performed${overdue > 0 ? `, ${overdue} overdue` : ""}`
                : `${name}: nothing planned`
            }
            sx={{
              p: 1.5,
              textAlign: "left",
              display: "block",
              border: 1,
              borderColor: current ? "primary.main" : "divider",
              borderRadius: 2,
              boxShadow: current ? (t) => `inset 0 0 0 1px ${t.palette.primary.main}` : "none",
              "&:hover": { bgcolor: "action.hover" }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{name}</Typography>
              {current && <StatusPill tone="info" size="small">Now</StatusPill>}
            </Box>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {hasPlanned
                ? `${performed} of ${totalPlanned} performed`
                : eventsCount > 0
                ? `${eventsCount} event(s)`
                : "Nothing planned"}
            </Typography>
            {hasPlanned && (
              <>
                <Box
                  aria-hidden="true"
                  sx={{ display: "flex", height: 8, borderRadius: 1, overflow: "hidden", bgcolor: "action.selected", my: 1 }}
                >
                  <Box sx={{ width: share(performed), bgcolor: palette.success.main }} />
                  <Box sx={{ width: share(inProgress), bgcolor: palette.info.main }} />
                  <Box sx={{ width: share(overdue), bgcolor: palette.error.main }} />
                </Box>
                <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
                  {overdue > 0 && <StatusPill tone="detected" symbol="▲" size="small">{overdue} overdue</StatusPill>}
                  {inProgress > 0 && <StatusPill tone="info" symbol="◐" size="small">{inProgress} in progress</StatusPill>}
                  {upcoming > 0 && <StatusPill tone="pending" symbol="○" size="small">{upcoming} upcoming</StatusPill>}
                  {skipped > 0 && <StatusPill tone="pending" symbol="–" size="small">{skipped} skipped</StatusPill>}
                  {eventsCount > 0 && <StatusPill tone="info" symbol="★" size="small">{eventsCount} event(s)</StatusPill>}
                  {performed === totalPlanned && <StatusPill tone="notDetected" symbol="✓" size="small">Complete</StatusPill>}
                </Box>
              </>
            )}

            {onAddMonthEvent && (
              <Box sx={{ mt: 1, pt: 0.5, borderTop: 1, borderColor: "divider", display: "flex", justifyContent: "flex-end" }}>
                <ButtonBase
                  onClick={(e) => {
                    e.stopPropagation();
                    const monthStr = `${currentYear}-${String(i + 1).padStart(2, "0")}`;
                    onAddMonthEvent(monthStr);
                  }}
                  sx={{
                    fontSize: 11,
                    fontWeight: 600,
                    color: "primary.main",
                    px: 0.5,
                    py: 0.25,
                    borderRadius: 0.5,
                    "&:hover": { bgcolor: "action.hover", textDecoration: "underline" }
                  }}
                >
                  Add event for this month
                </ButtonBase>
              </Box>
            )}
          </ButtonBase>
        );
      })}
    </Box>
  );
}
