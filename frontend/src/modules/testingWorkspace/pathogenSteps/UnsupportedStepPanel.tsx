import { Alert } from "@mui/material";
import { useTranslation } from "../../../i18n";

// The explicit fallback every step-type switch in this module must have.
// Rendering a growth-observation form here by accident is the exact bug
// class this whole refactor exists to eliminate - this panel is what
// stands in its place instead of a silent fallthrough.
export function UnsupportedStepPanel({ stepType }: { stepType: string }) {
  const { t } = useTranslation("testingWorkspace");
  return (
    <Alert severity="error">
      {t("pathogenSteps.unsupported", { stepType })}
    </Alert>
  );
}
