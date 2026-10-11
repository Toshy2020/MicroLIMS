import { memo, useEffect, useState } from "react";
import { Box, Typography } from "@mui/material";

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
  const size = 168;
  const stroke = 12;
  const r = (size - stroke) / 2;
  const circumference = 2 * Math.PI * r;

  return (
    <Box sx={{ display: "grid", justifyItems: "center", gap: 0.5, py: 1 }}>
      <Box sx={{ position: "relative", width: size, height: size }}>
        <Box component="svg" viewBox={`0 0 ${size} ${size}`} sx={{ width: size, height: size, transform: "rotate(-90deg)" }} aria-hidden="true">
          <Box component="circle" cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} sx={{ stroke: (t) => t.palette.action.hover }} />
          <Box
            component="circle" cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} strokeLinecap="round"
            strokeDasharray={circumference} strokeDashoffset={circumference * (1 - elapsedFraction)}
            sx={{ stroke: (t) => (reached ? t.custom.status.notDetected.text : t.palette.primary.main) }}
          />
        </Box>
        <Box
          role="timer"
          aria-live="off"
          aria-label={reached ? `${nextLabel} is available` : `${formatRemaining(remaining)} until ${nextLabel} is available`}
          sx={{ position: "absolute", inset: 0, display: "grid", placeContent: "center", textAlign: "center", lineHeight: 1.15 }}
        >
          {reached ? (
            <>
              <Typography sx={{ fontSize: 22, fontWeight: 700, color: (t) => t.custom.status.notDetected.text }}>Ready</Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>00:00:00</Typography>
            </>
          ) : (
            <>
              <Typography sx={{ fontSize: 26, fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{formatRemaining(remaining)}</Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>hh:mm:ss</Typography>
            </>
          )}
        </Box>
      </Box>
      <Typography variant="body2" sx={{ color: reached ? (t) => t.custom.status.notDetected.text : "text.secondary", fontWeight: reached ? 600 : 400, textAlign: "center" }}>
        {reached ? `${nextLabel} is available now` : `until ${nextLabel} is available (${totalHours} h minimum incubation)`}
      </Typography>
    </Box>
  );
});
