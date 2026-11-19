import { Stack, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";
import { ToneChip } from "./ToneChip";

export interface LimitPillsProps {
  alertLimit?: string | null;
  actionLimit?: string | null;
  specLimit?: string | null;
  unit?: string | null;
  limitType?: "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative" | string | null;
  lowerLimit?: number | null;
  upperLimit?: number | null;
  lowerInclusive?: boolean;
  upperInclusive?: boolean;
  expectedResultText?: string | null;
}

export function formatChemistryLimit(
  props: {
    limitType?: string | null;
    lowerLimit?: number | null;
    upperLimit?: number | null;
    expectedResultText?: string | null;
    unit?: string | null;
    specLimit?: string | null;
  },
  t: TFunction = i18n.getFixedT("en", "common")
): string {
  const notSet = t("components.limitPills.notSet");
  const nlt = (value: number | string) => t("components.limitPills.nlt", { value });
  const nmt = (value: number | string) => t("components.limitPills.nmt", { value });
  const { limitType, lowerLimit, upperLimit, expectedResultText, unit, specLimit } = props;
  const unitSuffix = unit ? ` ${unit}` : "";

  switch (limitType) {
    case "Range":
      if (lowerLimit != null && upperLimit != null) {
        return `${lowerLimit} \u2013 ${upperLimit}${unitSuffix}`;
      }
      if (lowerLimit != null) return `${nlt(lowerLimit)}${unitSuffix}`;
      if (upperLimit != null) return `${nmt(upperLimit)}${unitSuffix}`;
      return specLimit ? `${specLimit}${unitSuffix}` : notSet;

    case "NotMoreThan":
      return upperLimit != null
        ? `${nmt(upperLimit)}${unitSuffix}`
        : specLimit
          ? `${nmt(specLimit)}${unitSuffix}`
          : notSet;

    case "NotLessThan":
      return lowerLimit != null
        ? `${nlt(lowerLimit)}${unitSuffix}`
        : specLimit
          ? `${nlt(specLimit)}${unitSuffix}`
          : notSet;

    case "Qualitative":
      return expectedResultText || specLimit || notSet;

    default:
      return specLimit ? `${specLimit}${unitSuffix}` : notSet;
  }
}

// Alert / Action / Spec shown as coloured pills for micro, or a single formatted
// limit pill for chemistry ("6.5 – 8.5 pH", "NMT 1.3 µS/cm", "NLT 5", expected text).
export function LimitPills({
  alertLimit,
  actionLimit,
  specLimit,
  unit,
  limitType,
  lowerLimit,
  upperLimit,
  expectedResultText
}: LimitPillsProps) {
  const { t } = useTranslation();
  if (limitType) {
    const label = formatChemistryLimit(
      {
        limitType,
        lowerLimit,
        upperLimit,
        expectedResultText,
        unit,
        specLimit
      },
      t
    );
    return (
      <Stack useFlexGap direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
        <ToneChip label={label} tone="detected" />
      </Stack>
    );
  }

  const v = (x?: string | null) => (x && x.trim() !== "" ? x : t("components.limitPills.notSet"));
  return (
    <Stack useFlexGap direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
      <ToneChip label={t("components.limitPills.alert", { value: v(alertLimit) })} tone={alertLimit ? "inconclusive" : "pending"} />
      <ToneChip label={t("components.limitPills.action", { value: v(actionLimit) })} tone={actionLimit ? "action" : "pending"} />
      <ToneChip label={t("components.limitPills.spec", { value: v(specLimit) })} tone={specLimit ? "detected" : "pending"} />
      {unit && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{unit}</Typography>}
    </Stack>
  );
}
