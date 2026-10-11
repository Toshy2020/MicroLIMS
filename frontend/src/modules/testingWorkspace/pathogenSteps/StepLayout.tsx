import { ReactNode } from "react";
import { Alert, Box, FormControlLabel, MenuItem, Radio, RadioGroup, TextField, Typography } from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";

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

// A medium fixed by the step template, with a lock showing it comes from
// Test Master and can't be changed here.
export function FixedMediumRow({ mediaName, label = "Medium" }: { mediaName: string; label?: string }) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, bgcolor: "action.hover", border: "1px solid", borderColor: "divider", borderRadius: 2, px: 1.5, py: 1 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="caption" sx={{ display: "block", color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.06em" }}>{label}</Typography>
        <Typography variant="body2" sx={{ fontWeight: 500 }}>{mediaName}</Typography>
      </Box>
      <Typography variant="caption" title="Assigned in Test Master" sx={{ ml: "auto", color: "text.secondary", display: "flex", alignItems: "center", gap: 0.5, whiteSpace: "nowrap" }}>
        <LockOutlinedIcon sx={{ fontSize: 14 }} /> Test Master
      </Typography>
    </Box>
  );
}

interface LotOption { id: number; lotNumber: string; expiryDate: string }
interface IncubatorOption { id: number; name: string; code: string; setTemperature: number }

// Labelled media lot + incubator pickers with helper text, shared by every
// step that starts an incubation.
export function MediaLotIncubatorFields({
  idPrefix, lots, incubators, tempMin, tempMax, mediaLotId, equipmentId, onLotChange, onIncubatorChange, disabled = false
}: {
  idPrefix: string;
  lots: LotOption[];
  incubators: IncubatorOption[];
  tempMin: number;
  tempMax: number;
  mediaLotId: number | "";
  equipmentId: number | "";
  onLotChange: (id: number) => void;
  onIncubatorChange: (id: number) => void;
  disabled?: boolean;
}) {
  const lotCount = lots.length;
  return (
    <>
      <TextField
        select fullWidth required disabled={disabled}
        id={`${idPrefix}-media-lot`}
        label="Media lot"
        value={mediaLotId}
        onChange={(e) => onLotChange(Number(e.target.value))}
        helperText={mediaLotId ? "Released and within expiry" : lotCount === 0 ? "No released lots of this medium are available" : `${lotCount} released lot${lotCount === 1 ? "" : "s"} available`}
        error={!disabled && lotCount === 0}
        slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }}
      >
        <MenuItem value="" disabled><Box component="span" sx={{ color: "text.secondary" }}>Select a released lot</Box></MenuItem>
        {lots.map((l) => (
          <MenuItem key={l.id} value={l.id}>{l.lotNumber} · expires {new Date(l.expiryDate).toLocaleDateString()}</MenuItem>
        ))}
      </TextField>
      <TextField
        select fullWidth required disabled={disabled}
        id={`${idPrefix}-incubator`}
        label="Incubator"
        value={equipmentId}
        onChange={(e) => onIncubatorChange(Number(e.target.value))}
        helperText={incubators.length === 0 ? `No incubators are set within ${tempMin}–${tempMax} °C` : `Only incubators set within ${tempMin}–${tempMax} °C are listed`}
        error={!disabled && incubators.length === 0}
        slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }}
      >
        <MenuItem value="" disabled><Box component="span" sx={{ color: "text.secondary" }}>Select an incubator</Box></MenuItem>
        {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.code} · {i.name} · set {i.setTemperature} °C</MenuItem>)}
      </TextField>
    </>
  );
}

// What a target-positive colony looks like on this medium, or a warning
// when Test Master has no expected appearance configured.
export function ExpectedAppearanceNote({ mediaName, expectedAppearance }: { mediaName: string; expectedAppearance: string | null }) {
  return expectedAppearance === null ? (
    <Alert severity="warning">
      Expected appearance not configured in Test Master for {mediaName}. Growth vs. no-growth can still be recorded,
      but a conforming/non-conforming judgment is unreliable until Test Master is fixed.
    </Alert>
  ) : (
    <Alert severity="info" icon={false} sx={{ py: 0.25 }}>
      <Typography variant="caption" sx={{ display: "block", textTransform: "uppercase", letterSpacing: "0.06em" }}>Expected target-positive colony</Typography>
      <strong>{expectedAppearance}</strong>
    </Alert>
  );
}

// Observation choices as full-width cards with a large tap area.
export function ObservationOptions<T extends string>({
  name, value, onChange, options, disabled = false
}: {
  name: string;
  value: T | "";
  onChange: (value: T) => void;
  options: { value: T; title: string; detail?: string }[];
  disabled?: boolean;
}) {
  return (
    <RadioGroup name={name} value={value} onChange={(e) => onChange(e.target.value as T)} sx={{ gap: 1 }}>
      {options.map((o) => {
        const selected = value === o.value;
        return (
          <FormControlLabel
            key={o.value}
            value={o.value}
            disabled={disabled}
            control={<Radio />}
            label={
              <Box sx={{ py: 0.75 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>{o.title}</Typography>
                {o.detail && <Typography variant="caption" sx={{ color: "text.secondary" }}>{o.detail}</Typography>}
              </Box>
            }
            sx={{
              m: 0, pr: 1.5, alignItems: "flex-start", minHeight: 48,
              border: "1px solid", borderRadius: 2,
              borderColor: selected ? "primary.main" : "divider",
              bgcolor: selected ? "action.selected" : "transparent",
              "& .MuiRadio-root": { pt: 1.25 }
            }}
          />
        );
      })}
    </RadioGroup>
  );
}
