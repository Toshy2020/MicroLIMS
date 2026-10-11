import { ReactNode } from "react";
import { Box, Typography } from "@mui/material";

// Shared building blocks for the pathogen step panels, so every step reads
// the same way: label/value rows for recorded details and an action bar
// pinned to the bottom of the dialog's scrolling body.

// Action bar that stays at the bottom of the dialog body while the step
// content scrolls, so the primary action is always in thumb reach on a
// phone. The negative margins cancel DialogContent's dividers padding
// (16px 24px) so the bar spans the full width and sits flush on the edge.
export function StepActionBar({ children, note }: { children: ReactNode; note?: ReactNode }) {
  return (
    <Box
      sx={{
        position: "sticky", bottom: 0, zIndex: 1,
        mx: -3, mb: -2, mt: 1, px: 3, pt: 1.5, pb: 2,
        bgcolor: "background.paper", borderTop: "1px solid", borderColor: "divider",
        display: "grid", gap: 0.75
      }}
    >
      <Box sx={{ display: "flex", gap: 1, justifyContent: "flex-end", flexWrap: "wrap", "& > .MuiButton-contained": { flex: { xs: "1 1 100%", sm: "0 0 auto" }, minHeight: 44 } }}>
        {children}
      </Box>
      {note && (
        <Typography variant="caption" sx={{ color: "text.secondary", textAlign: { xs: "center", sm: "right" } }}>
          {note}
        </Typography>
      )}
    </Box>
  );
}

// Label/value rows for details already recorded (lot, incubator, times).
export function StepDetailList({ rows }: { rows: { label: string; value: ReactNode }[] }) {
  return (
    <Box
      component="dl"
      sx={{ m: 0, display: "grid", gridTemplateColumns: "auto minmax(0, 1fr)", columnGap: 2, rowGap: 0.75, fontSize: 14 }}
    >
      {rows.map((r) => (
        <Box key={r.label} sx={{ display: "contents" }}>
          <Box component="dt" sx={{ color: "text.secondary" }}>{r.label}</Box>
          <Box component="dd" sx={{ m: 0, fontWeight: 500, textAlign: "right", fontVariantNumeric: "tabular-nums", overflowWrap: "anywhere" }}>
            {r.value}
          </Box>
        </Box>
      ))}
    </Box>
  );
}

// Three-point incubation timeline: start, earliest read, latest read.
// Every value is passed in exactly as the server resolved it - this never
// computes hours or readiness itself.
export function IncubationTimeline({ points }: { points: { label: string; value: ReactNode; active?: boolean }[] }) {
  return (
    <Box sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, p: 1.5 }}>
      <Typography variant="caption" sx={{ color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.06em", fontWeight: 600 }}>
        Incubation window
      </Typography>
      <Box component="ol" sx={{ listStyle: "none", m: 0, mt: 1, p: 0, display: "grid", gridTemplateColumns: `repeat(${points.length}, minmax(0, 1fr))` }}>
        {points.map((p, i) => (
          <Box component="li" key={p.label} sx={{ minWidth: 0, textAlign: i === 0 ? "left" : i === points.length - 1 ? "right" : "center" }}>
            <Box sx={{ position: "relative", height: 12, mb: 0.75 }}>
              <Box sx={{ position: "absolute", top: 5, height: 2, left: i === 0 ? 6 : 0, right: i === points.length - 1 ? 6 : 0, bgcolor: "divider" }} />
              <Box
                sx={{
                  position: "absolute", top: 0, width: 12, height: 12, borderRadius: "50%",
                  left: i === 0 ? 0 : i === points.length - 1 ? "auto" : "calc(50% - 6px)",
                  right: i === points.length - 1 ? 0 : "auto",
                  bgcolor: p.active ? "primary.main" : "background.paper",
                  border: "2px solid", borderColor: p.active ? "primary.main" : "text.disabled"
                }}
              />
            </Box>
            <Typography variant="caption" sx={{ display: "block", color: "text.secondary", lineHeight: 1.3 }}>{p.label}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 500, fontVariantNumeric: "tabular-nums", lineHeight: 1.3 }}>{p.value}</Typography>
          </Box>
        ))}
      </Box>
    </Box>
  );
}
