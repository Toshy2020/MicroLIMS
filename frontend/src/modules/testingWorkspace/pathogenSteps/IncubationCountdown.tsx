import { memo, useEffect, useState } from "react";
import { Box, Typography, useTheme } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { BENCH_COUNTDOWN_RING_SIZE, BENCH_COUNTDOWN_STATE_FONT_SIZE, BENCH_COUNTDOWN_TIME_FONT_SIZE } from "../../../theme/benchTheme";

interface Props {
  // Client timestamp (ms) at which the minimum incubation ends. Built from
  // the server's remainingMinimumSeconds when the step was loaded, so a
  // wrong clock on the analyst's device does not shift the countdown.
  deadlineMs: number;
  // Minimum incubation in hours - the countdown's starting point (e.g. 18).
  totalHours: number;
  nextLabel: string;
  onReached: () => void;
}

const pad = (n: number) => String(n).padStart(2, "0");
const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

function formatRemaining(ms: number) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return `${pad(h)}:${pad(m)}:${pad(s)}`;
}

// Countdown from the minimum incubation time (e.g. 18 h) to zero. Kept in
// its own component so the once-a-second tick re-renders only this ring,
// not the rest of the dialog. The timer stops at zero and calls onReached
// once; the server still enforces the window when the step is submitted.
export const IncubationCountdown = memo(function IncubationCountdown({ deadlineMs, totalHours, nextLabel, onReached }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const bench = Boolean(useTheme().custom?.bench);
  const [now, setNow] = useState(() => Date.now());
  const remaining = deadlineMs - now;
  const reached = remaining <= 0;

  useEffect(() => {
    if (reached) {
      onReached();
      return;
    }
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [reached, onReached]);

  const totalMs = totalHours * 3600 * 1000;
  const elapsedFraction = totalMs > 0 ? Math.min(1, Math.max(0, 1 - remaining / totalMs)) : 1;
  const size = bench ? BENCH_COUNTDOWN_RING_SIZE : 168;
  const stroke = 12;
  const r = (size - stroke) / 2;
  const circumference = 2 * Math.PI * r;

  return (
    <Box sx={{ display: "grid", justifyItems: "center", gap: 0.5, py: 1 }}>
      <Box sx={{ position: "relative", width: size, height: size }}>
        <Box component="svg" viewBox={`0 0 ${size} ${size}`} sx={{ width: size, height: size, transform: "rotate(-90deg)" }} aria-hidden="true">
          <Box component="circle" cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} sx={{ stroke: (th) => th.palette.action.hover }} />
          <Box
            component="circle" cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} strokeLinecap="round"
            strokeDasharray={circumference} strokeDashoffset={circumference * (1 - elapsedFraction)}
            sx={{ stroke: (th) => (reached ? th.custom.status.notDetected.text : th.palette.primary.main) }}
          />
        </Box>
        <Box
          role="timer"
          aria-live="off"
          aria-label={reached ? t("pathogenSteps.countdown.ariaAvailable", { nextLabel }) : t("pathogenSteps.countdown.ariaRemaining", { remaining: formatRemaining(remaining), nextLabel })}
          sx={{ position: "absolute", inset: 0, display: "grid", placeContent: "center", textAlign: "center", lineHeight: 1.15 }}
        >
          {reached ? (
            <>
              <Typography sx={{ fontSize: bench ? BENCH_COUNTDOWN_STATE_FONT_SIZE : 22, fontWeight: 700, fontVariantNumeric: "tabular-nums", color: (th) => th.custom.status.notDetected.text }}>
                {t("pathogenSteps.countdown.ready")}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}><Ltr>00:00:00</Ltr></Typography>
            </>
          ) : (
            <>
              <Typography sx={{ fontSize: bench ? BENCH_COUNTDOWN_TIME_FONT_SIZE : 26, fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                <Ltr>{formatRemaining(remaining)}</Ltr>
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("pathogenSteps.countdown.hhmmss")}</Typography>
            </>
          )}
        </Box>
      </Box>
      <Typography variant="body2" sx={{ color: reached ? (th) => th.custom.status.notDetected.text : "text.secondary", fontWeight: reached ? 600 : 400, textAlign: "center" }}>
        {reached ? (
          t("pathogenSteps.countdown.availableNow", { nextLabel: capitalize(nextLabel) })
        ) : (
          <Trans
            i18nKey="pathogenSteps.countdown.untilAvailable"
            ns="testingWorkspace"
            values={{ nextLabel, hours: totalHours }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        )}
      </Typography>
    </Box>
  );
});
