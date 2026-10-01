import { Stack, Typography } from "@mui/material";
import { ToneChip } from "./ToneChip";

interface Props {
  alertLimit?: string | null;
  actionLimit?: string | null;
  specLimit?: string | null;
  unit?: string | null;
}

// Alert / Action / Spec shown as coloured pills, "not set" for blanks.
export function LimitPills({ alertLimit, actionLimit, specLimit, unit }: Props) {
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
