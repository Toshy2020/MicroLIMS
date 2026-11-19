import { Chip, useTheme } from "@mui/material";
import { useTranslation } from "../../../i18n";
import type { StatusTone } from "../../../theme/statusTokens";
import type { MyWorkUrgency } from "../myWorkTypes";

const URGENCY_TONE: Record<Exclude<MyWorkUrgency, "None">, StatusTone> = {
  Overdue: "detected",
  Now: "purple",
  Today: "action",
  Later: "info"
};

export function UrgencyLabel({ urgency }: { urgency: MyWorkUrgency }) {
  const theme = useTheme();
  const { t } = useTranslation("myWork");
  if (urgency === "None") return null;

  const tone = theme.custom.status[URGENCY_TONE[urgency]];

  return (
    <Chip
      size="small"
      label={t(`urgency.${urgency}`)}
      sx={{
        height: 22,
        fontSize: 11.5,
        fontWeight: 700,
        bgcolor: tone.bg,
        color: tone.text,
        border: `1px solid ${tone.border}`,
        borderRadius: 1.5
      }}
    />
  );
}
