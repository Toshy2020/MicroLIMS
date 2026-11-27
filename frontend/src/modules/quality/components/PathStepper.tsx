import { Box, Step, StepLabel, Stepper, Typography, useMediaQuery, useTheme } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { QualityPathStepDto } from "../../../services/qualityService";

interface PathStepperProps {
  steps: QualityPathStepDto[];
}

// The fixed path of a quality record. The server decides which step is current (the path is its state
// machine); this only draws it. Every step has a text label and a written state, so nothing is told by
// colour alone, and the current step is announced in words and with aria-current.
export function PathStepper({ steps }: PathStepperProps) {
  const { t } = useTranslation("quality");
  const theme = useTheme();
  const narrow = useMediaQuery(theme.breakpoints.down("sm"));

  if (steps.length === 0) return null;

  const currentIndex = steps.findIndex((s) => s.isCurrent);
  const announcement =
    currentIndex >= 0
      ? t("path.currentStep", { label: steps[currentIndex].label, index: currentIndex + 1, count: steps.length })
      : t("path.allDone");

  return (
    <Box role="group" aria-label={t("path.title")} sx={{ width: "100%" }}>
      <Stepper
        activeStep={currentIndex >= 0 ? currentIndex : steps.length}
        orientation={narrow ? "vertical" : "horizontal"}
        alternativeLabel={!narrow}
      >
        {steps.map((step) => {
          const state = step.isCurrent
            ? t("path.stateCurrent")
            : step.isCompleted
              ? t("path.stateCompleted")
              : t("path.stateNotStarted");
          return (
            <Step
              key={step.stepKey}
              completed={step.isCompleted}
              active={step.isCurrent}
              aria-current={step.isCurrent ? "step" : undefined}
            >
              <StepLabel
                optional={
                  <>
                    <Typography variant="caption" component="span" sx={{ display: "block" }}>
                      {state}
                    </Typography>
                    {step.isCompleted && step.completedByName && step.completedAtUtc && (
                      <Typography
                        variant="caption"
                        component="span"
                        sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}
                      >
                        {t("path.completedBy", {
                          name: step.completedByName,
                          date: formatDateTimeWithTimeZone(step.completedAtUtc)
                        })}
                      </Typography>
                    )}
                  </>
                }
              >
                {step.label}
              </StepLabel>
            </Step>
          );
        })}
      </Stepper>
      <Typography role="status" variant="body2" color="text.secondary" sx={{ mt: 1.5, textAlign: "center" }}>
        {announcement}
      </Typography>
    </Box>
  );
}
