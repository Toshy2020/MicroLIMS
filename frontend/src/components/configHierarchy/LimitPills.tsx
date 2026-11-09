import { Stack, Typography } from "@mui/material";
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

export function formatChemistryLimit(props: {
  limitType?: string | null;
  lowerLimit?: number | null;
  upperLimit?: number | null;
  expectedResultText?: string | null;
  unit?: string | null;
  specLimit?: string | null;
}): string {
  const { limitType, lowerLimit, upperLimit, expectedResultText, unit, specLimit } = props;
  const unitSuffix = unit ? ` ${unit}` : "";

  switch (limitType) {
    case "Range":
      if (lowerLimit != null && upperLimit != null) {
        return `${lowerLimit} \u2013 ${upperLimit}${unitSuffix}`;
      }
      if (lowerLimit != null) return `NLT ${lowerLimit}${unitSuffix}`;
      if (upperLimit != null) return `NMT ${upperLimit}${unitSuffix}`;
      return specLimit ? `${specLimit}${unitSuffix}` : "not set";

    case "NotMoreThan":
      return upperLimit != null
        ? `NMT ${upperLimit}${unitSuffix}`
        : specLimit
          ? `NMT ${specLimit}${unitSuffix}`
          : "not set";

    case "NotLessThan":
      return lowerLimit != null
        ? `NLT ${lowerLimit}${unitSuffix}`
        : specLimit
          ? `NLT ${specLimit}${unitSuffix}`
          : "not set";

    case "Qualitative":
      return expectedResultText || specLimit || "not set";

    default:
      return specLimit ? `${specLimit}${unitSuffix}` : "not set";
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
  if (limitType) {
    const label = formatChemistryLimit({
      limitType,
      lowerLimit,
      upperLimit,
      expectedResultText,
      unit,
      specLimit
    });
    return (
      <Stack useFlexGap direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
        <ToneChip label={label} tone="detected" />
      </Stack>
    );
  }

  const v = (x?: string | null) => (x && x.trim() !== "" ? x : "not set");
  return (
    <Stack useFlexGap direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
      <ToneChip label={`Alert ${v(alertLimit)}`} tone={alertLimit ? "inconclusive" : "pending"} />
      <ToneChip label={`Action ${v(actionLimit)}`} tone={actionLimit ? "action" : "pending"} />
      <ToneChip label={`Spec ${v(specLimit)}`} tone={specLimit ? "detected" : "pending"} />
      {unit && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{unit}</Typography>}
    </Stack>
  );
}
