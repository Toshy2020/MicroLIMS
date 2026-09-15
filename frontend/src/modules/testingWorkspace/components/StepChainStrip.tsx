import { Box, Stack, useTheme } from "@mui/material";
import { CurrentStepResponse } from "../types/testWorkflowTypes";

// A completed step whose outcome contains this is shown as failed. A
// confirmatory plating outcome is ConfirmatoryResult.ToString() -
// "AllConforming" or "Inconclusive" - and other outcomes can mention it
// after other words.
const INCONCLUSIVE_OUTCOME_MARKER = "Inconclusive";

// Read-only progress strip above a step dialog's content - one chip per
// step in the template, sourced from current-step's allSteps/completedSteps/
// step fields. No click actions.
export function StepChainStrip({ current }: { current: CurrentStepResponse }) {
  const theme = useTheme();
  const completedByOrder = new Map((current.completedSteps ?? []).map((s) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;

  return (
    <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
      {(current.allSteps ?? []).map((s) => {
        const done = completedByOrder.get(s.stepOrder);
        const isCurrent = s.stepOrder === currentOrder;
        const isInconclusive = done?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);

        let tone = theme.custom.status.pending;
        let label = s.stepName;
        if (done) {
          label = `${s.stepName}: ${done.outcome}`;
          tone = isInconclusive ? theme.custom.status.detected : theme.custom.status.notDetected;
        } else if (isCurrent) {
          label = `${s.stepName}: In progress`;
          tone = theme.custom.status.info;
        }

        return (
          <Box key={s.stepOrder} sx={{ px: 1.25, py: 0.5, borderRadius: 999, fontSize: 12, fontWeight: 600, bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}` }}>
            {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{label}
          </Box>
        );
      })}
    </Stack>
  );
}
