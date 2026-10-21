import { MenuItem, Stack, TextField, Typography } from "@mui/material";

export type FrequencyKind = "" | "Daily" | "Weekly" | "Monthly";

export interface FrequencyValue {
  type: FrequencyKind;
  interval: number;
  weekday: string;
  anchorMonth: number | "";
  // Monthly only: pin to the Nth lab week of the month.
  weekOfMonth: number | "";
}

// The flat columns the master-data endpoints return.
export interface FrequencyColumns {
  frequencyType?: "Daily" | "Weekly" | "Monthly" | null;
  frequencyInterval?: number | null;
  samplingWeekday?: string | null;
  frequencyAnchorMonth?: number | null;
  frequencyWeekOfMonth?: number | null;
}

export const emptyFrequency: FrequencyValue = { type: "", interval: 1, weekday: "", anchorMonth: "", weekOfMonth: "" };

export const frequencyFrom = (e?: FrequencyColumns | null): FrequencyValue =>
  e?.frequencyType
    ? { type: e.frequencyType, interval: e.frequencyInterval ?? 1, weekday: e.samplingWeekday ?? "", anchorMonth: e.frequencyAnchorMonth ?? "", weekOfMonth: e.frequencyWeekOfMonth ?? "" }
    : emptyFrequency;

// Body for the `frequency` request field. An empty type clears the rule.
export const frequencyRule = (v: FrequencyValue) =>
  v.type === ""
    ? { type: null }
    : v.type === "Daily"
      ? { type: v.type, interval: 1, samplingWeekday: null, anchorMonth: null, weekOfMonth: null }
      : {
          type: v.type,
          interval: v.interval,
          samplingWeekday: v.weekday || null,
          anchorMonth: v.type === "Monthly" && v.interval > 1 && v.anchorMonth !== "" ? v.anchorMonth : null,
          weekOfMonth: v.type === "Monthly" && v.weekOfMonth !== "" ? v.weekOfMonth : null
        };

const WEEKDAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

export function frequencyLabel(v: FrequencyValue | FrequencyColumns): string {
  const f = "type" in v ? v : frequencyFrom(v);
  if (f.type === "") return "";
  if (f.type === "Daily") return "Daily";
  const unit = f.type === "Weekly" ? "week" : "month";
  const base = f.interval === 1 ? (f.type === "Weekly" ? "Weekly" : "Monthly") : `Every ${f.interval} ${unit}s`;
  const withWeek = f.type === "Monthly" && f.weekOfMonth !== "" ? `${base}, week ${f.weekOfMonth}` : base;
  return f.weekday ? `${withWeek} on ${f.weekday}` : withWeek;
}

interface Props {
  idPrefix: string;
  value: FrequencyValue;
  onChange: (v: FrequencyValue) => void;
  weekdayRequired?: boolean;
  // Shown when the field is empty, e.g. "Inherited from the department: Monthly".
  inheritedHint?: string;
  noneLabel?: string;
}

// Structured sampling frequency: daily (every working day), weekly or
// monthly, every N, an optional weekday, a week of the month for monthly
// rules and, for every-N-months rules, the month the cycle starts in.
export function StructuredFrequencyField({ idPrefix, value, onChange, weekdayRequired, inheritedHint, noneLabel = "No frequency" }: Props) {
  const set = (patch: Partial<FrequencyValue>) => onChange({ ...value, ...patch });
  const unit = value.type === "Weekly" ? "week(s)" : "month(s)";

  return (
    <Stack spacing={1.5}>
      <TextField
        select
        size="small"
        id={`${idPrefix}-type`}
        label="Frequency"
        value={value.type}
        onChange={(e) => set({ type: e.target.value as FrequencyKind, anchorMonth: "", weekOfMonth: "", ...(e.target.value === "Daily" ? { interval: 1, weekday: "" } : {}) })}
        slotProps={{ select: { displayEmpty: true } }}
      >
        <MenuItem value="">{noneLabel}</MenuItem>
        <MenuItem value="Daily">Daily (working days)</MenuItem>
        <MenuItem value="Weekly">Weekly</MenuItem>
        <MenuItem value="Monthly">Monthly</MenuItem>
      </TextField>
      {value.type === "" && inheritedHint && (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{inheritedHint}</Typography>
      )}
      {value.type === "Daily" && (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Due every working day of the lab week; days off in the lab calendar are skipped.
        </Typography>
      )}
      {(value.type === "Weekly" || value.type === "Monthly") && (
        <Stack direction="row" spacing={1.5}>
          <TextField
            size="small"
            type="number"
            id={`${idPrefix}-interval`}
            label={`Every N ${unit}`}
            value={value.interval}
            onChange={(e) => set({ interval: Math.max(1, Math.floor(Number(e.target.value) || 1)) })}
            slotProps={{ htmlInput: { min: 1, max: value.type === "Weekly" ? 52 : 12 } }}
            sx={{ flex: 1 }}
          />
          <TextField
            select
            size="small"
            id={`${idPrefix}-weekday`}
            label={weekdayRequired && value.type === "Weekly" ? "Sampling weekday" : "Sampling weekday (optional)"}
            required={weekdayRequired && value.type === "Weekly"}
            value={value.weekday}
            onChange={(e) => set({ weekday: e.target.value })}
            sx={{ flex: 1 }}
          >
            <MenuItem value="">Any working day</MenuItem>
            {WEEKDAYS.map((d) => (
              <MenuItem key={d} value={d}>
                {d}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      )}
      {value.type === "Monthly" && (
        <TextField
          select
          size="small"
          id={`${idPrefix}-week-of-month`}
          label="Week of the month"
          value={value.weekOfMonth}
          onChange={(e) => set({ weekOfMonth: e.target.value === "" ? "" : Number(e.target.value) })}
          helperText="Due any day in that lab week (Sunday to Saturday)."
        >
          <MenuItem value="">Any time in the month</MenuItem>
          {[1, 2, 3, 4].map((w) => (
            <MenuItem key={w} value={w}>
              Week {w}
            </MenuItem>
          ))}
        </TextField>
      )}
      {value.type === "Monthly" && value.interval > 1 && (
        <TextField
          select
          size="small"
          id={`${idPrefix}-anchor`}
          label="First month of the cycle"
          value={value.anchorMonth}
          onChange={(e) => set({ anchorMonth: e.target.value === "" ? "" : Number(e.target.value) })}
        >
          <MenuItem value="">January</MenuItem>
          {MONTHS.map((m, i) => (
            <MenuItem key={m} value={i + 1}>
              {m}
            </MenuItem>
          ))}
        </TextField>
      )}
    </Stack>
  );
}
