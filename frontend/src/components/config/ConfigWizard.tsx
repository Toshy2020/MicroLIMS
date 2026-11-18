import { useEffect, useRef } from "react";
import {
  Alert,
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
import { RecordChangedBanner } from "../RecordChangedBanner";
import { DraftRestoreNotice } from "../bench/DraftRestoreNotice";
import { ChangeReasonDialog } from "./ChangeReasonDialog";
import { WizardReview } from "./WizardReview";
import {
  REVIEW_STEP_KEY,
  useWizard,
  type ConfigWizardProps,
  type WizardSaveAndNextAction,
  type WizardStep,
} from "./useWizard";

export type { ConfigWizardProps, WizardSaveAndNextAction, WizardStep };

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
  const { open = true, title, mode, steps, usage, reasonRequired, extraReview, liveRecord, reloadOriginal } = props;
  const wizard = useWizard(props);
  const bodyRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (wizard.focusRequest.seq === 0) return;
    focusFieldInContainer(bodyRef.current, wizard.focusRequest.field);
  }, [wizard.focusRequest]);

  // Focus goes to the first field when the wizard has opened and whenever the step changes, so typing
  // lands in the form (not on the dialog container, where it would reach page-level shortcuts).
  const stepKey = wizard.currentStep?.key ?? REVIEW_STEP_KEY;
  const firstStepFocus = useRef(true);
  useEffect(() => {
    // The opening itself is handled in onEntered, once the dialog has finished its transition.
    if (firstStepFocus.current) {
      firstStepFocus.current = false;
      return;
    }
    focusFieldInContainer(bodyRef.current);
  }, [stepKey]);

  const stepperItems = [
    ...wizard.applicableSteps.map((s) => ({ key: s.key, title: s.title })),
    { key: REVIEW_STEP_KEY, title: "Review" },
  ];

  const reasonNeededOnSave = reasonRequired
    ? reasonRequired(wizard.usageState.summary, wizard.model)
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
        onEntered={() => focusFieldInContainer(bodyRef.current)}
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
              <>
                {wizard.resolvedSaveAndNext && (
                  <Button
                    type="button"
                    variant="outlined"
                    onClick={() => wizard.handleSaveClick(true)}
                    disabled={wizard.saveDisabled}
                  >
                    {wizard.resolvedSaveAndNext.label}
                  </Button>
                )}
                <Button
                  type="button"
                  variant="contained"
                  onClick={() => wizard.handleSaveClick(false)}
                  disabled={wizard.saveDisabled}
                  startIcon={
                    wizard.saving ? <CircularProgress size={16} color="inherit" /> : undefined
                  }
                >
                  {wizard.saving ? "Saving..." : "Save"}
                </Button>
              </>
            )}
          </>
        }
      >
        <Box ref={bodyRef} sx={{ pt: 0.5 }}>
          {liveRecord && reloadOriginal && (
            <RecordChangedBanner
              type={liveRecord.type}
              id={liveRecord.id}
              dirty={wizard.isDirty}
              onReload={() => void wizard.reloadLatest()}
            />
          )}
          {wizard.reloadLatestError && (
            <Alert severity="error" role="alert" sx={{ mb: 2 }}>{wizard.reloadLatestError}</Alert>
          )}
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
