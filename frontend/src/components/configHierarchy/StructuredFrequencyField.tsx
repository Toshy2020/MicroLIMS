import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";

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

export interface FrequencyRulePayload {
  type: "Daily" | "Weekly" | "Monthly" | null;
  interval?: number | null;
  samplingWeekday?: string | null;
  anchorMonth?: number | null;
  weekOfMonth?: number | null;
}

// Body for the `frequency` request field. An empty type clears the rule.
export const frequencyRule = (v: FrequencyValue): FrequencyRulePayload =>
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

// The stored weekday and month values stay English; only their display is translated.
const weekdayText = (t: TFunction, day: string) => t(`components.structuredFrequency.weekdays.${day}`, { defaultValue: day });

export function frequencyLabel(v: FrequencyValue | FrequencyColumns, t: TFunction = i18n.getFixedT("en", "common")): string {
  const f = "type" in v ? v : frequencyFrom(v);
  if (f.type === "") return "";
  if (f.type === "Daily") return t("components.structuredFrequency.daily");
  const base =
    f.interval === 1
      ? f.type === "Weekly"
        ? t("components.structuredFrequency.weekly")
        : t("components.structuredFrequency.monthly")
      : f.type === "Weekly"
        ? t("components.structuredFrequency.everyWeeks", { count: f.interval })
        : t("components.structuredFrequency.everyMonths", { count: f.interval });
  const withWeek =
    f.type === "Monthly" && f.weekOfMonth !== ""
      ? t("components.structuredFrequency.weekOfMonthSuffix", { base, week: f.weekOfMonth })
      : base;
  return f.weekday ? t("components.structuredFrequency.onWeekday", { base: withWeek, weekday: weekdayText(t, f.weekday) }) : withWeek;
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
export function StructuredFrequencyField({ idPrefix, value, onChange, weekdayRequired, inheritedHint, noneLabel }: Props) {
  const { t } = useTranslation();
  const set = (patch: Partial<FrequencyValue>) => onChange({ ...value, ...patch });

  return (
    <Stack spacing={1.5}>
      <TextField
        select
        size="small"
        id={`${idPrefix}-type`}
        label={t("components.structuredFrequency.frequency")}
        value={value.type}
        onChange={(e) => set({ type: e.target.value as FrequencyKind, anchorMonth: "", weekOfMonth: "", ...(e.target.value === "Daily" ? { interval: 1, weekday: "" } : {}) })}
        slotProps={{ select: { displayEmpty: true } }}
      >
        <MenuItem value="">{noneLabel ?? t("components.structuredFrequency.noFrequency")}</MenuItem>
        <MenuItem value="Daily">{t("components.structuredFrequency.dailyWorkingDays")}</MenuItem>
        <MenuItem value="Weekly">{t("components.structuredFrequency.weekly")}</MenuItem>
        <MenuItem value="Monthly">{t("components.structuredFrequency.monthly")}</MenuItem>
      </TextField>
      {value.type === "" && inheritedHint && (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{inheritedHint}</Typography>
      )}
      {value.type === "Daily" && (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {t("components.structuredFrequency.dailyHint")}
        </Typography>
      )}
      {(value.type === "Weekly" || value.type === "Monthly") && (
        <Stack direction="row" spacing={1.5}>
          <TextField
            size="small"
            type="number"
            id={`${idPrefix}-interval`}
            label={value.type === "Weekly" ? t("components.structuredFrequency.everyNWeeks") : t("components.structuredFrequency.everyNMonths")}
            value={value.interval}
            onChange={(e) => set({ interval: Math.max(1, Math.floor(Number(e.target.value) || 1)) })}
            slotProps={{ htmlInput: { min: 1, max: value.type === "Weekly" ? 52 : 12 } }}
            sx={{ flex: 1 }}
          />
          <TextField
            select
            size="small"
            id={`${idPrefix}-weekday`}
            label={weekdayRequired && value.type === "Weekly" ? t("components.structuredFrequency.samplingWeekday") : t("components.structuredFrequency.samplingWeekdayOptional")}
            required={weekdayRequired && value.type === "Weekly"}
            value={value.weekday}
            onChange={(e) => set({ weekday: e.target.value })}
            sx={{ flex: 1 }}
          >
            <MenuItem value="">{t("components.structuredFrequency.anyWorkingDay")}</MenuItem>
            {WEEKDAYS.map((d) => (
              <MenuItem key={d} value={d}>
                {weekdayText(t, d)}
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
          label={t("components.structuredFrequency.weekOfMonthLabel")}
          value={value.weekOfMonth}
          onChange={(e) => set({ weekOfMonth: e.target.value === "" ? "" : Number(e.target.value) })}
          helperText={t("components.structuredFrequency.weekOfMonthHelper")}
        >
          <MenuItem value="">{t("components.structuredFrequency.anyTimeInMonth")}</MenuItem>
          {[1, 2, 3, 4].map((w) => (
            <MenuItem key={w} value={w}>
              {t("components.structuredFrequency.week", { week: w })}
            </MenuItem>
          ))}
        </TextField>
      )}
      {value.type === "Monthly" && value.interval > 1 && (
        <TextField
          select
          size="small"
          id={`${idPrefix}-anchor`}
          label={t("components.structuredFrequency.firstMonth")}
          value={value.anchorMonth}
          onChange={(e) => set({ anchorMonth: e.target.value === "" ? "" : Number(e.target.value) })}
        >
          <MenuItem value="">{t("components.structuredFrequency.months.January")}</MenuItem>
          {MONTHS.map((m, i) => (
            <MenuItem key={m} value={i + 1}>
              {t(`components.structuredFrequency.months.${m}`)}
            </MenuItem>
          ))}
        </TextField>
      )}
    </Stack>
  );
}
