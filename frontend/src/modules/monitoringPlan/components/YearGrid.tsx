import { Box, ButtonBase, Typography, useTheme } from "@mui/material";
import { MONTH_NAMES } from "../statusDisplay";
import type { YearSummary } from "../types";
import { StatusPill } from "./StatusPill";

// Twelve month tiles; picking one drills into that month's days. The bar
// shows performed, in progress and overdue as shares of what is planned.
export function YearGrid({ summary, today, onPick }: { summary: YearSummary | null; today: string; onPick: (month: number) => void }) {
  const palette = useTheme().palette;
  const currentMonth = summary && today.startsWith(String(summary.year)) ? Number(today.slice(5, 7)) : null;
  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5 }}>
      {MONTH_NAMES.map((name, i) => {
        const m = summary?.months.find((x) => x.month === i + 1);
        const planned = m !== undefined && m.total > 0;
        const share = (n: number) => `${planned ? (n / m.total) * 100 : 0}%`;
        const current = currentMonth === i + 1;
        return (
          <ButtonBase
            key={name}
            onClick={() => onPick(i)}
            aria-label={planned ? `${name}: ${m.performed} of ${m.total} performed${m.overdue > 0 ? `, ${m.overdue} overdue` : ""}` : `${name}: nothing planned`}
            sx={{
              p: 1.5, textAlign: "left", display: "block", border: 1, borderColor: current ? "primary.main" : "divider", borderRadius: 2,
              boxShadow: current ? (t) => `inset 0 0 0 1px ${t.palette.primary.main}` : "none",
              "&:hover": { bgcolor: "action.hover" }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{name}</Typography>
              {current && <StatusPill tone="info" size="small">Now</StatusPill>}
            </Box>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {planned ? `${m.performed} of ${m.total} performed` : "Nothing planned"}
            </Typography>
            {planned && (
              <>
                <Box aria-hidden="true" sx={{ display: "flex", height: 8, borderRadius: 1, overflow: "hidden", bgcolor: "action.selected", my: 1 }}>
                  <Box sx={{ width: share(m.performed), bgcolor: palette.success.main }} />
                  <Box sx={{ width: share(m.inProgress), bgcolor: palette.info.main }} />
                  <Box sx={{ width: share(m.overdue), bgcolor: palette.error.main }} />
                </Box>
                <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
                  {m.overdue > 0 && <StatusPill tone="detected" symbol="▲" size="small">{m.overdue} overdue</StatusPill>}
                  {m.inProgress > 0 && <StatusPill tone="info" symbol="◐" size="small">{m.inProgress} in progress</StatusPill>}
                  {m.upcoming > 0 && <StatusPill tone="pending" symbol="○" size="small">{m.upcoming} upcoming</StatusPill>}
                  {m.skipped > 0 && <StatusPill tone="pending" symbol="–" size="small">{m.skipped} skipped</StatusPill>}
                  {m.performed === m.total && <StatusPill tone="notDetected" symbol="✓" size="small">Complete</StatusPill>}
                </Box>
              </>
            )}
          </ButtonBase>
        );
      })}
    </Box>
  );
}
