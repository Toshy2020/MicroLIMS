import { useEffect, useRef } from "react";
import {
  Box,
  Button,
  CircularProgress,
  Step,
  StepLabel,
  Stepper,
  Typography,
} from "@mui/material";
import { FloatingDialog } from "../FloatingDialog";
import { ConfirmationDialog } from "../ConfirmationDialog";
import { DraftRestoreNotice } from "../bench/DraftRestoreNotice";
import { ChangeReasonDialog } from "./ChangeReasonDialog";
import { WizardReview } from "./WizardReview";
import {
  REVIEW_STEP_KEY,
  useWizard,
  type ConfigWizardProps,
  type WizardStep,
} from "./useWizard";

export type { ConfigWizardProps, WizardStep };

function findFocusableForElement(el: HTMLElement): HTMLElement | null {
  if (el instanceof HTMLInputElement && el.classList.contains("MuiSelect-nativeInput")) {
    return el.parentElement?.querySelector<HTMLElement>("[role='combobox']") ?? el;
  }
  if (
    el instanceof HTMLInputElement ||
    el instanceof HTMLTextAreaElement ||
    el instanceof HTMLSelectElement ||
    el.getAttribute("role") === "combobox"
  ) {
    return el;
  }
  return el.querySelector<HTMLElement>(
    "input:not([type='hidden']):not([disabled]), textarea:not([disabled]), select:not([disabled]), [role='combobox']"
  );
}

function focusFieldInContainer(container: HTMLElement | null, fieldName?: string): void {
  if (!container) return;
  if (fieldName) {
    const escaped = CSS.escape(fieldName);
    const matched =
      container.querySelector<HTMLElement>(`[name="${escaped}"]`) ??
      container.querySelector<HTMLElement>(`[data-field="${escaped}"]`) ??
      container.querySelector<HTMLElement>(`#${escaped}`);
    const target = matched ? findFocusableForElement(matched) : null;
    if (target) {
      target.focus();
      return;
    }
  }
  const firstInvalid = container.querySelector<HTMLElement>("[aria-invalid='true']");
  if (firstInvalid) {
    findFocusableForElement(firstInvalid)?.focus();
    return;
  }
  const firstInput = container.querySelector<HTMLElement>(
    "input:not([type='hidden']):not([disabled]), textarea:not([disabled]), select:not([disabled]), [role='combobox']"
  );
  firstInput?.focus();
}

/**
 * Shared step-by-step configuration wizard frame with per-step validation,
 * non-linear stepper once every step is visited, device draft persistence,
 * review/diff/where-used, audit change reason prompt, and 409 conflict recovery.
 */
export function ConfigWizard<M extends object>(props: ConfigWizardProps<M>) {
  const { open = true, title, mode, steps, usage, reasonRequired, extraReview } = props;
  const wizard = useWizard(props);
  const bodyRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (wizard.focusRequest.seq === 0) return;
    focusFieldInContainer(bodyRef.current, wizard.focusRequest.field);
  }, [wizard.focusRequest]);

  const stepperItems = [
    ...wizard.applicableSteps.map((s) => ({ key: s.key, title: s.title })),
    { key: REVIEW_STEP_KEY, title: "Review" },
  ];

  const reasonNeededOnSave = reasonRequired
    ? reasonRequired(wizard.usageState.summary)
    : false;

  return (
    <>
      <FloatingDialog
        open={open}
        title={
          <Typography variant="h6" component="span">
            {title}
          </Typography>
        }
        onClose={wizard.requestClose}
        maxWidth="md"
        subHeader={
          <Box sx={{ px: 3, py: 1.5, borderBottom: 1, borderColor: "divider" }}>
            <Stepper
              activeStep={wizard.activeStepIndex}
              nonLinear={wizard.allVisited}
              aria-label="Wizard steps"
            >
              {stepperItems.map((item, idx) => {
                const isActive = idx === wizard.activeStepIndex;
                const hasError = wizard.errorStepKeys.has(item.key);
                const labelNode = wizard.allVisited ? (
                  <Button
                    type="button"
                    variant="text"
                    size="small"
                    onClick={() => wizard.jumpToStep(item.key)}
                    sx={{
                      p: 0,
                      minWidth: 0,
                      textTransform: "none",
                      fontWeight: "inherit",
                      color: "inherit",
                      justifyContent: "flex-start",
                    }}
                  >
                    {item.title}
                  </Button>
                ) : (
                  item.title
                );
                return (
                  <Step
                    key={item.key}
                    aria-current={isActive ? "step" : undefined}
                    completed={!hasError && wizard.allVisited ? idx < wizard.activeStepIndex : undefined}
                  >
                    <StepLabel
                      error={hasError}
                      optional={
                        hasError ? (
                          <Typography variant="caption" color="error">
                            needs attention
                          </Typography>
                        ) : undefined
                      }
                    >
                      {labelNode}
                    </StepLabel>
                  </Step>
                );
              })}
            </Stepper>
          </Box>
        }
        actions={
          <>
            <Button
              type="button"
              onClick={wizard.requestClose}
              disabled={wizard.saving}
            >
              Close
            </Button>
            <Button
              type="button"
              onClick={wizard.goBack}
              disabled={wizard.activeStepIndex === 0 || wizard.saving}
            >
              Back
            </Button>
            {!wizard.isReview ? (
              <Button type="button" variant="contained" onClick={wizard.goNext}>
                Next
              </Button>
            ) : (
              <Button
                type="button"
                variant="contained"
                onClick={wizard.handleSaveClick}
                disabled={wizard.saveDisabled}
                startIcon={
                  wizard.saving ? <CircularProgress size={16} color="inherit" /> : undefined
                }
              >
                {wizard.saving ? "Saving..." : "Save"}
              </Button>
            )}
          </>
        }
      >
        <Box ref={bodyRef} sx={{ pt: 0.5 }}>
          <DraftRestoreNotice
            startedAt={wizard.draftRestoredStartedAt}
            onRestore={wizard.restoreDraft}
            onDiscard={wizard.discardDraft}
          />
          {wizard.currentStep ? (
            wizard.currentStep.render({
              model: wizard.model,
              set: wizard.set,
              errors: wizard.currentMergedErrors,
              mode,
            })
          ) : (
            <WizardReview
              mode={mode}
              steps={steps}
              applicableSteps={wizard.applicableSteps}
              model={wizard.model}
              original={wizard.activeOriginal}
              serverErrors={wizard.serverErrors}
              serverErrorsByStep={wizard.serverErrorsByStep}
              unmappedServerErrors={wizard.unmappedServerErrors}
              validatingServer={wizard.validatingServer}
              serverValidationError={wizard.serverValidationError}
              onRetryServerValidation={wizard.retryServerValidation}
              hasUsage={Boolean(usage)}
              usageState={wizard.usageState}
              usageBlocked={wizard.usageBlocked}
              onRetryUsage={wizard.retryUsage}
              reasonRequired={reasonNeededOnSave}
              conflictState={wizard.conflictState}
              onDismissConflict={wizard.dismissConflict}
              saveError={wizard.saveError}
              onEditStep={wizard.jumpToStep}
              extraReview={extraReview}
            />
          )}
        </Box>
      </FloatingDialog>

      <ChangeReasonDialog
        open={wizard.reasonDialogOpen}
        loading={wizard.saving}
        onCancel={wizard.closeReasonDialog}
        onConfirm={(reason) => wizard.executeSave(reason)}
      />

      <ConfirmationDialog
        open={wizard.confirmDiscardOpen}
        destructive
        title="Discard unsaved changes?"
        message="You have unsaved changes. Closing now discards them."
        confirmText="Discard"
        onCancel={wizard.cancelDiscard}
        onConfirm={() => void wizard.confirmDiscard()}
      />
    </>
  );
}
