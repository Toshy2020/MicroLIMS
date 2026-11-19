import { Alert, Box, MenuItem, TextField } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";

type LimitType = "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative";

export interface LimitValues {
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
  limitType?: "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative" | null;
  lowerLimit?: number | null;
  upperLimit?: number | null;
  lowerInclusive?: boolean;
  upperInclusive?: boolean;
  expectedResultText?: string | null;
}

export const emptyLimits: LimitValues = {
  alertLimit: "",
  actionLimit: "",
  specLimit: "",
  unit: "",
  limitType: "Range",
  lowerLimit: null,
  upperLimit: null,
  lowerInclusive: true,
  upperInclusive: true,
  expectedResultText: ""
};

// Advisory only: limits are free text (e.g. "<1"), and the ordering rule
// belongs to the backend, so this never blocks a save - it just points
// out an order that is probably a typo when all three are plain numbers.
export function limitOrderWarning(
  { alertLimit, actionLimit, specLimit }: LimitValues,
  t: TFunction = i18n.getFixedT("en", "common")
): string | null {
  const nums = [alertLimit, actionLimit, specLimit].map((s) => (s.trim() === "" ? null : Number(s.trim())));
  if (nums.some((n) => n !== null && Number.isNaN(n))) return null;
  const [alert, action, spec] = nums;
  if (alert != null && action != null && alert > action) return t("components.limitFields.alertGtAction");
  if (action != null && spec != null && action > spec) return t("components.limitFields.actionGtSpec");
  if (alert != null && spec != null && alert > spec) return t("components.limitFields.alertGtSpec");
  return null;
}

interface Props {
  idPrefix: string;
  value: LimitValues;
  onChange: (value: LimitValues) => void;
  unitRequired?: boolean;
  unitPlaceholder?: string;
  workflowType?: string;
}

export interface ChemistryLimitFieldsProps {
  idPrefix: string;
  value: LimitValues;
  onChange: (value: LimitValues) => void;
  unitPlaceholder?: string;
}

// Limit type select (Range / Not more than / Not less than / Qualitative),
// Lower, Upper, Expected result text, Unit for chemistry and other non-count tests.
export function ChemistryLimitFields({ idPrefix, value, onChange, unitPlaceholder }: ChemistryLimitFieldsProps) {
  const { t } = useTranslation();
  const limitType = value.limitType ?? "Range";

  const setLimitType = (type: LimitType) => {
    onChange({ ...value, limitType: type });
  };

  const setNum = (key: "lowerLimit" | "upperLimit") => (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value;
    onChange({ ...value, [key]: raw === "" ? null : Number(raw) });
  };

  const setText = (key: "expectedResultText" | "unit") => (e: React.ChangeEvent<HTMLInputElement>) => {
    onChange({ ...value, [key]: e.target.value });
  };

  const rangeOrderWarning =
    limitType === "Range" &&
    value.lowerLimit != null &&
    value.upperLimit != null &&
    value.lowerLimit > value.upperLimit
      ? t("components.limitFields.lowerGtUpper")
      : null;

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 1.25 }}>
      <TextField
        id={`${idPrefix}-limit-type`}
        select
        size="small"
        label={t("components.limitFields.limitType")}
        value={limitType}
        onChange={(e) => setLimitType(e.target.value as LimitType)}
      >
        <MenuItem value="Range">{t("components.limitFields.range")}</MenuItem>
        <MenuItem value="NotMoreThan">{t("components.limitFields.notMoreThan")}</MenuItem>
        <MenuItem value="NotLessThan">{t("components.limitFields.notLessThan")}</MenuItem>
        <MenuItem value="Qualitative">{t("components.limitFields.qualitative")}</MenuItem>
      </TextField>

      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 1.25 }}>
        {limitType === "Range" && (
          <>
            <TextField
              id={`${idPrefix}-lower`}
              size="small"
              type="number"
              label={t("components.limitFields.lower")}
              value={value.lowerLimit ?? ""}
              onChange={setNum("lowerLimit")}
              slotProps={{ htmlInput: { step: "any" } }}
            />
            <TextField
              id={`${idPrefix}-upper`}
              size="small"
              type="number"
              label={t("components.limitFields.upper")}
              value={value.upperLimit ?? ""}
              onChange={setNum("upperLimit")}
              slotProps={{ htmlInput: { step: "any" } }}
            />
          </>
        )}

        {limitType === "NotMoreThan" && (
          <TextField
            id={`${idPrefix}-upper`}
            size="small"
            type="number"
            label={t("components.limitFields.upper")}
            value={value.upperLimit ?? ""}
            onChange={setNum("upperLimit")}
            slotProps={{ htmlInput: { step: "any" } }}
          />
        )}

        {limitType === "NotLessThan" && (
          <TextField
            id={`${idPrefix}-lower`}
            size="small"
            type="number"
            label={t("components.limitFields.lower")}
            value={value.lowerLimit ?? ""}
            onChange={setNum("lowerLimit")}
            slotProps={{ htmlInput: { step: "any" } }}
          />
        )}

        {limitType === "Qualitative" && (
          <TextField
            id={`${idPrefix}-expected-result-text`}
            size="small"
            label={t("components.limitFields.expectedResultText")}
            value={value.expectedResultText ?? ""}
            onChange={setText("expectedResultText")}
            sx={{ gridColumn: "span 2" }}
          />
        )}

        <TextField
          id={`${idPrefix}-unit`}
          size="small"
          label={t("components.limitFields.unit")}
          placeholder={unitPlaceholder ?? t("components.limitFields.unitPlaceholder")}
          value={value.unit ?? ""}
          onChange={setText("unit")}
        />
      </Box>

      {rangeOrderWarning && (
        <Alert severity="warning" sx={{ mt: 0.5, py: 0 }}>
          {rangeOrderWarning}
        </Alert>
      )}
    </Box>
  );
}

// Labelled Alert / Action / Specification / Unit inputs for CountTest workflows,
// or switches to ChemistryLimitFields when workflowType is not CountTest.
export function LimitFields(props: Props) {
  const { idPrefix, value, onChange, unitRequired, unitPlaceholder, workflowType } = props;
  const { t } = useTranslation();

  if (workflowType && workflowType !== "CountTest") {
    return (
      <ChemistryLimitFields
        idPrefix={idPrefix}
        value={value}
        onChange={onChange}
        unitPlaceholder={unitPlaceholder}
      />
    );
  }

  const set = (k: keyof LimitValues) => (e: React.ChangeEvent<HTMLInputElement>) => onChange({ ...value, [k]: e.target.value });
  const warning = limitOrderWarning(value, t);

  return (
    <Box>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 1.25 }}>
        <TextField id={`${idPrefix}-alert`} size="small" label={t("components.limitFields.alert")} value={value.alertLimit} onChange={set("alertLimit")} />
        <TextField id={`${idPrefix}-action`} size="small" label={t("components.limitFields.action")} value={value.actionLimit} onChange={set("actionLimit")} />
        <TextField id={`${idPrefix}-spec`} size="small" label={t("components.limitFields.specification")} value={value.specLimit} onChange={set("specLimit")} />
        <TextField
          id={`${idPrefix}-unit`}
          size="small"
          label={t("components.limitFields.unit")}
          required={unitRequired}
          placeholder={unitPlaceholder}
          value={value.unit}
          onChange={set("unit")}
        />
      </Box>
      {warning && (
        <Alert severity="warning" sx={{ mt: 1, py: 0 }}>
          {warning} {t("components.limitFields.checkValues")}
        </Alert>
      )}
    </Box>
  );
}
