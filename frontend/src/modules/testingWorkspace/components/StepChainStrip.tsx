import { memo } from "react";
import { Box, Tooltip, Typography, useTheme } from "@mui/material";
import { CurrentStepResponse } from "../types/testWorkflowTypes";

// A completed step whose outcome contains this is shown as failed. A
// confirmatory plating outcome is ConfirmatoryResult.ToString() -
// "AllConforming" or "Inconclusive" - and other outcomes can mention it
// after other words.
const INCONCLUSIVE_OUTCOME_MARKER = "Inconclusive";

// Read-only progress above a step dialog's content: "Step N of M", one
// segment per step in the template and a short label under each, sourced
// from current-step's allSteps/completedSteps/step fields. Kept to a single
// row so it never wraps and pushes the step form down on a phone. A done
// step's outcome is in its tooltip and accessible label. No click actions.
export const StepChainStrip = memo(function StepChainStrip({ current }: { current: CurrentStepResponse }) {
  const theme = useTheme();
  const steps = current.allSteps ?? [];
  const completedByOrder = new Map((current.completedSteps ?? []).map((s) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;
  const currentIndex = steps.findIndex((s) => s.stepOrder === currentOrder);
  const status = theme.custom.status;

  if (steps.length === 0) return null;

  const headline = current.allStepsComplete
    ? `All ${steps.length} steps complete`
    : currentIndex >= 0
      ? `Step ${currentIndex + 1} of ${steps.length}`
      : `${completedByOrder.size} of ${steps.length} steps complete`;

  return (
    <Box component="nav" aria-label="Workflow progress" sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", gap: 1, mb: 0.75 }}>
        <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>{headline}</Typography>
        {currentIndex >= 0 && !current.allStepsComplete && (
          <Typography variant="caption" sx={{ color: "text.secondary", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {steps[currentIndex].stepName}
          </Typography>
        )}
      </Box>
      <Box
        component="ol"
        sx={{ listStyle: "none", m: 0, p: 0, display: "grid", gridTemplateColumns: `repeat(${steps.length}, minmax(0, 1fr))`, gap: 0.5 }}
      >
        {steps.map((s) => {
          const done = completedByOrder.get(s.stepOrder);
          const isCurrent = s.stepOrder === currentOrder && !current.allStepsComplete;
          const isInconclusive = done?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);

          let barColor = status.pending.border;
          let labelColor = theme.palette.text.secondary;
          let stateText = "Not started";
          if (done) {
            barColor = isInconclusive ? status.detected.text : status.notDetected.text;
            labelColor = barColor;
            stateText = done.outcome ? `Complete: ${done.outcome}` : "Complete";
          } else if (isCurrent) {
            barColor = theme.palette.primary.main;
            labelColor = theme.palette.primary.main;
            stateText = "In progress";
          }

          return (
            <Tooltip key={s.stepOrder} title={`${s.stepName}: ${stateText}`} disableInteractive>
              <Box
                component="li"
                aria-label={`${s.stepName}: ${stateText}`}
                aria-current={isCurrent ? "step" : undefined}
                sx={{ minWidth: 0 }}
              >
                <Box sx={{ height: 4, borderRadius: 2, bgcolor: barColor }} />
                <Typography
                  component="span"
                  sx={{
                    display: "block", mt: 0.5, fontSize: 11, lineHeight: 1.2, textAlign: "center",
                    color: labelColor, fontWeight: isCurrent ? 700 : 500,
                    overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap"
                  }}
                >
                  {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{s.stepName}
                </Typography>
              </Box>
            </Tooltip>
          );
        })}
      </Box>
    </Box>
  );
});
