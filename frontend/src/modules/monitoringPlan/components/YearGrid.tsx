import { Box, ButtonBase, Typography, useTheme } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { MONTH_INDICES } from "../statusDisplay";
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
  const { t } = useTranslation("monitoring");
  const palette = useTheme().palette;
  const currentYear = year ?? summary?.year ?? Number(today.slice(0, 4));
  const currentMonth = today.startsWith(String(currentYear)) ? Number(today.slice(5, 7)) : null;

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5 }}>
      {MONTH_INDICES.map((i) => {
        const name = t(`months.${i}`);
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
            key={i}
            onClick={() => onPick(i)}
            aria-label={
              hasPlanned
                ? overdue > 0
                  ? t("yearGrid.tileAriaPlannedOverdue", { month: name, performed, total: totalPlanned, overdue })
                  : t("yearGrid.tileAriaPlanned", { month: name, performed, total: totalPlanned })
                : t("yearGrid.tileAriaEmpty", { month: name })
            }
            sx={{
              p: 1.5,
              textAlign: "left",
              display: "block",
              border: 1,
              borderColor: current ? "primary.main" : "divider",
              borderRadius: 2,
              boxShadow: current ? (th) => `inset 0 0 0 1px ${th.palette.primary.main}` : "none",
              "&:hover": { bgcolor: "action.hover" }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{name}</Typography>
              {current && <StatusPill tone="info" size="small">{t("yearGrid.now")}</StatusPill>}
            </Box>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {hasPlanned ? (
                <Trans
                  i18nKey="yearGrid.performedOfTotal"
                  ns="monitoring"
                  values={{ performed, total: totalPlanned }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              ) : eventsCount > 0 ? (
                <Trans
                  i18nKey="yearGrid.eventsCount"
                  ns="monitoring"
                  count={eventsCount}
                  values={{ count: eventsCount }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              ) : (
                t("yearGrid.nothingPlanned")
              )}
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
                  {overdue > 0 && (
                    <StatusPill tone="detected" symbol="▲" size="small">
                      <Trans i18nKey="yearGrid.overdueCount" ns="monitoring" values={{ count: overdue }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </StatusPill>
                  )}
                  {inProgress > 0 && (
                    <StatusPill tone="info" symbol="◐" size="small">
                      <Trans i18nKey="yearGrid.inProgressCount" ns="monitoring" values={{ count: inProgress }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </StatusPill>
                  )}
                  {upcoming > 0 && (
                    <StatusPill tone="pending" symbol="○" size="small">
                      <Trans i18nKey="yearGrid.upcomingCount" ns="monitoring" values={{ count: upcoming }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </StatusPill>
                  )}
                  {skipped > 0 && (
                    <StatusPill tone="pending" symbol="–" size="small">
                      <Trans i18nKey="yearGrid.skippedCount" ns="monitoring" values={{ count: skipped }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </StatusPill>
                  )}
                  {eventsCount > 0 && (
                    <StatusPill tone="info" symbol="★" size="small">
                      <Trans i18nKey="yearGrid.eventsCount" ns="monitoring" count={eventsCount} values={{ count: eventsCount }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                    </StatusPill>
                  )}
                  {performed === totalPlanned && <StatusPill tone="notDetected" symbol="✓" size="small">{t("yearGrid.complete")}</StatusPill>}
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
                  {t("yearGrid.addMonthEvent")}
                </ButtonBase>
              </Box>
            )}
          </ButtonBase>
        );
      })}
    </Box>
  );
}
