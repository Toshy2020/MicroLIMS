import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import axios from "axios";
import { i18n } from "../../i18n";
import { useBenchDraft } from "../../hooks/useBenchDraft";
import { noteOwnSave } from "../../hooks/useRecordChanged";
import { isReasonRequiredError } from "../../services/apiClient";
import type { FieldError } from "../../services/apiTypes";
import type { UsageSummary } from "../../services/masterDataUsageService";
import { getErrorMessage } from "../../utils/errorMessage";
import { getFieldErrors } from "../../utils/fieldErrors";

export const REVIEW_STEP_KEY = "__review__";

export interface WizardStep<M> {
  key: string;
  title: string;
  applies?(model: M): boolean;
  validate(model: M): Record<string, string>;
  render(ctx: {
    model: M;
    set(patch: Partial<M>): void;
    errors: Record<string, string>;
    mode: "create" | "edit";
  }): ReactNode;
  summary(model: M): { label: string; value: string; field: string }[];
}

export interface WizardSaveAndNextAction {
  label: string;
  onSaved(): void;
}

export interface ConfigWizardProps<M> {
  open?: boolean;
  title: string;
  mode: "create" | "edit";
  steps: WizardStep<M>[];
  initial: M;
  original?: M;
  draftKey: string;
  draftFields: (keyof M)[];
  validateOnServer?(model: M): Promise<FieldError[]>;
  usage?(): Promise<UsageSummary>;
  reasonRequired?(usage: UsageSummary | null, model: M): boolean;
  reloadOriginal?(): Promise<M>;
  // The saved record, so the wizard can warn when someone else changes it while it is open.
  liveRecord?: { type: string; id: number | null };
  onSave(model: M, reason: string | null): Promise<void>;
  onClose(): void;
  saveAndNext?:
    | WizardSaveAndNextAction
    | ((model: M) => WizardSaveAndNextAction | null);
  extraReview?(model: M): ReactNode;
}

export interface UsageLoadState {
  status: "idle" | "loading" | "loaded" | "error";
  summary: UsageSummary | null;
  error: string | null;
}

export interface ConflictState {
  active: boolean;
  reloading: boolean;
  reloadError: string | null;
}

function isConflictError(err: unknown): boolean {
  if (axios.isAxiosError(err)) return err.response?.status === 409;
  if (typeof err === "object" && err !== null && "response" in err) {
    const resp = (err as { response?: { status?: unknown } }).response;
    return resp?.status === 409;
  }
  return false;
}

export function useWizard<M extends object>(props: ConfigWizardProps<M>) {
  const {
    open = true,
    title,
    steps,
    initial,
    original,
    draftKey,
    draftFields,
    validateOnServer,
    usage,
    reasonRequired,
    reloadOriginal,
    liveRecord,
    onSave,
    onClose,
    saveAndNext,
  } = props;

  const [model, setModel] = useState<M>(initial);
  const [baselineInitial, setBaselineInitial] = useState<M>(initial);
  const [activeOriginal, setActiveOriginal] = useState<M | undefined>(original);
  const [activeStepIndex, setActiveStepIndex] = useState(0);
  const [visitedKeys, setVisitedKeys] = useState<Set<string>>(() => {
    const first = steps.find((s) => (s.applies ? s.applies(initial) : true));
    return new Set([first ? first.key : REVIEW_STEP_KEY]);
  });
  const [stepErrors, setStepErrors] = useState<Record<string, Record<string, string>>>({});
  const [serverErrors, setServerErrors] = useState<FieldError[]>([]);
  const [validatingServer, setValidatingServer] = useState(false);
  const [serverValidationError, setServerValidationError] = useState<string | null>(null);
  const [serverValidationTick, setServerValidationTick] = useState(0);

  const [usageState, setUsageState] = useState<UsageLoadState>({
    status: "idle",
    summary: null,
    error: null,
  });
  const usageStatusRef = useRef(usageState.status);
  usageStatusRef.current = usageState.status;
  const [usageTick, setUsageTick] = useState(0);

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [reasonDialogOpen, setReasonDialogOpen] = useState(false);
  const [conflictState, setConflictState] = useState<ConflictState>({
    active: false,
    reloading: false,
    reloadError: null,
  });
  const [confirmDiscardOpen, setConfirmDiscardOpen] = useState(false);
  const [focusRequest, setFocusRequest] = useState<{ field?: string; seq: number }>({ seq: 0 });
  const pendingThenNextRef = useRef(false);

  const prevOpenRef = useRef(open);
  const prevDraftKeyRef = useRef(draftKey);

  useEffect(() => {
    const openedNow = open && !prevOpenRef.current;
    const keyChanged = draftKey !== prevDraftKeyRef.current;
    prevOpenRef.current = open;
    prevDraftKeyRef.current = draftKey;
    if (!openedNow && !keyChanged) return;

    const first = steps.find((s) => (s.applies ? s.applies(initial) : true));
    setModel(initial);
    setBaselineInitial(initial);
    setActiveOriginal(original);
    setActiveStepIndex(0);
    setVisitedKeys(new Set([first ? first.key : REVIEW_STEP_KEY]));
    setStepErrors({});
    setServerErrors([]);
    setServerValidationError(null);
    setValidatingServer(false);
    setUsageState({ status: "idle", summary: null, error: null });
    setSaving(false);
    setSaveError(null);
    setReasonDialogOpen(false);
    pendingThenNextRef.current = false;
    setConflictState({ active: false, reloading: false, reloadError: null });
    setConfirmDiscardOpen(false);
  }, [open, draftKey, initial, original, steps]);

  const applicableSteps = useMemo(
    () => steps.filter((s) => (s.applies ? s.applies(model) : true)),
    [steps, model]
  );

  const clampedStepIndex = Math.min(activeStepIndex, applicableSteps.length);
  const isReview = clampedStepIndex === applicableSteps.length;
  const currentStep = isReview ? null : applicableSteps[clampedStepIndex];

  useEffect(() => {
    if (!open) return;
    const currentKey = currentStep ? currentStep.key : REVIEW_STEP_KEY;
    setVisitedKeys((prev) => {
      if (prev.has(currentKey)) return prev;
      const next = new Set(prev);
      next.add(currentKey);
      return next;
    });
  }, [open, currentStep]);

  const allVisited = useMemo(
    () =>
      applicableSteps.every((s) => visitedKeys.has(s.key)) &&
      visitedKeys.has(REVIEW_STEP_KEY),
    [applicableSteps, visitedKeys]
  );

  const pickDraft = useCallback(
    (values: M): Partial<M> => {
      const picked: Partial<M> = {};
      for (const key of draftFields) {
        if (key in values) {
          picked[key] = values[key];
        }
      }
      return picked;
    },
    [draftFields]
  );

  const draft = useBenchDraft<M>({
    formKey: "config-wizard",
    recordKey: draftKey,
    recordVersion: null,
    title,
    values: model,
    pick: pickDraft,
    enabled: open,
  });

  const restoreDraft = useCallback(() => {
    const restored = draft.acceptRestore();
    if (!restored) return;
    setModel((prev) => {
      const next = { ...prev };
      for (const key of draftFields) {
        if (key in restored) {
          next[key] = restored[key] as M[keyof M];
        }
      }
      return next;
    });
    setStepErrors({});
    setSaveError(null);
  }, [draft, draftFields]);

  const set = useCallback(
    (patch: Partial<M>) => {
      setModel((prev) => ({ ...prev, ...patch }));
      const patchedKeys = new Set(Object.keys(patch).map((k) => k.toLowerCase()));
      if (currentStep) {
        setStepErrors((prev) => {
          const existing = prev[currentStep.key];
          if (!existing) return prev;
          const nextStepErrs: Record<string, string> = {};
          for (const [k, msg] of Object.entries(existing)) {
            if (!patchedKeys.has(k.toLowerCase())) nextStepErrs[k] = msg;
          }
          return { ...prev, [currentStep.key]: nextStepErrs };
        });
      }
      setServerErrors((prev) =>
        prev.filter((fe) => !patchedKeys.has(fe.field.trim().toLowerCase()))
      );
      setSaveError(null);
    },
    [currentStep]
  );

  // Run server validation whenever the user reaches Review or retries it.
  useEffect(() => {
    if (!open || !isReview) return;
    if (!validateOnServer) {
      setServerErrors([]);
      setServerValidationError(null);
      setValidatingServer(false);
      return;
    }
    let cancelled = false;
    setValidatingServer(true);
    setServerValidationError(null);
    validateOnServer(model)
      .then((errs) => {
        if (cancelled) return;
        setServerErrors(errs);
        setValidatingServer(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setServerValidationError(
          getErrorMessage(err, i18n.t("components.configWizard.serverValidationFailed", { ns: "common" }))
        );
        setValidatingServer(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, isReview, validateOnServer, model, serverValidationTick]);

  // Load usage when entering Review (and keep loaded value unless retried).
  useEffect(() => {
    if (!open || !isReview || !usage) return;
    if (usageStatusRef.current === "loaded" && usageTick === 0) return;
    let cancelled = false;
    setUsageState({ status: "loading", summary: null, error: null });
    usage()
      .then((summary) => {
        if (cancelled) return;
        setUsageState({ status: "loaded", summary, error: null });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setUsageState({
          status: "error",
          summary: null,
          error: getErrorMessage(err, i18n.t("components.configWizard.usageCheckFailed", { ns: "common" })),
        });
      });
    return () => {
      cancelled = true;
    };
  }, [open, isReview, usage, usageTick]);

  const { serverErrorsByStep, unmappedServerErrors, errorStepKeys } = useMemo(() => {
    const fieldToStep = new Map<string, { stepKey: string; field: string }>();
    for (const step of applicableSteps) {
      for (const row of step.summary(model)) {
        const lower = row.field.trim().toLowerCase();
        if (lower && !fieldToStep.has(lower)) {
          fieldToStep.set(lower, { stepKey: step.key, field: row.field });
        }
      }
    }

    const byStep: Record<string, Record<string, string>> = {};
    const unmapped: FieldError[] = [];
    const erroredSteps = new Set<string>();

    for (const [stepKey, errs] of Object.entries(stepErrors)) {
      if (Object.keys(errs).length > 0) erroredSteps.add(stepKey);
    }

    for (const fe of serverErrors) {
      const match = fieldToStep.get(fe.field.trim().toLowerCase());
      if (match) {
        const map = byStep[match.stepKey] ?? (byStep[match.stepKey] = {});
        if (!(match.field in map)) map[match.field] = fe.message;
        erroredSteps.add(match.stepKey);
      } else {
        unmapped.push(fe);
      }
    }

    return {
      serverErrorsByStep: byStep,
      unmappedServerErrors: unmapped,
      errorStepKeys: erroredSteps,
    };
  }, [applicableSteps, model, stepErrors, serverErrors]);

  const currentMergedErrors = useMemo(() => {
    if (!currentStep) return {};
    return {
      ...(serverErrorsByStep[currentStep.key] ?? {}),
      ...(stepErrors[currentStep.key] ?? {}),
    };
  }, [currentStep, serverErrorsByStep, stepErrors]);

  const goNext = useCallback(() => {
    if (!currentStep) return;
    const errs = currentStep.validate(model);
    const invalidFields = Object.keys(errs);
    if (invalidFields.length > 0) {
      setStepErrors((prev) => ({ ...prev, [currentStep.key]: errs }));
      setFocusRequest((prev) => ({ field: invalidFields[0], seq: prev.seq + 1 }));
      return;
    }
    setStepErrors((prev) => ({ ...prev, [currentStep.key]: {} }));
    setActiveStepIndex((i) => Math.min(i + 1, applicableSteps.length));
  }, [currentStep, model, applicableSteps.length]);

  const goBack = useCallback(() => {
    setActiveStepIndex((i) => Math.max(0, i - 1));
  }, []);

  const jumpToStep = useCallback(
    (stepKey: string, focusField?: string) => {
      if (stepKey === REVIEW_STEP_KEY) {
        setActiveStepIndex(applicableSteps.length);
        return;
      }
      const idx = applicableSteps.findIndex((s) => s.key === stepKey);
      if (idx === -1) return;
      const step = applicableSteps[idx];
      const erroredField =
        Object.keys(stepErrors[stepKey] ?? {})[0] ??
        Object.keys(serverErrorsByStep[stepKey] ?? {})[0];
      const targetField = focusField ?? erroredField ?? step.summary(model)[0]?.field;
      setActiveStepIndex(idx);
      setFocusRequest((prev) => ({ field: targetField, seq: prev.seq + 1 }));
    },
    [applicableSteps, stepErrors, serverErrorsByStep, model]
  );

  const resolvedSaveAndNext = useMemo(
    () => (typeof saveAndNext === "function" ? saveAndNext(model) : (saveAndNext ?? null)),
    [saveAndNext, model]
  );

  const executeSave = useCallback(
    async (reason: string | null) => {
      setSaving(true);
      setSaveError(null);
      try {
        await onSave(model, reason);
        if (liveRecord) noteOwnSave(liveRecord.type, liveRecord.id);
        await draft.onSubmitted();
        setReasonDialogOpen(false);
        const goNextAfterSave = pendingThenNextRef.current;
        pendingThenNextRef.current = false;
        if (goNextAfterSave && resolvedSaveAndNext) {
          resolvedSaveAndNext.onSaved();
        } else {
          onClose();
        }
      } catch (err: unknown) {
        if (reason === null && isReasonRequiredError(err)) {
          setReasonDialogOpen(true);
          return;
        }
        pendingThenNextRef.current = false;
        setReasonDialogOpen(false);
        if (isConflictError(err)) {
          await draft.onRejected(i18n.t("components.configWizard.recordModifiedByAnother", { ns: "common" }));
          setConflictState({
            active: true,
            reloading: Boolean(reloadOriginal),
            reloadError: null,
          });
          if (reloadOriginal) {
            try {
              const fresh = await reloadOriginal();
              setActiveOriginal(fresh);
              setConflictState({ active: true, reloading: false, reloadError: null });
            } catch (reloadErr: unknown) {
              setConflictState({
                active: true,
                reloading: false,
                reloadError: getErrorMessage(reloadErr, i18n.t("components.configWizard.reloadLatestFailed", { ns: "common" })),
              });
            }
          }
          return;
        }
        const fieldErrs = getFieldErrors(err);
        if (fieldErrs.length > 0) {
          setServerErrors(fieldErrs);
          return;
        }
        setSaveError(getErrorMessage(err, i18n.t("components.configWizard.saveChangesFailed", { ns: "common" })));
      } finally {
        setSaving(false);
      }
    },
    [model, onSave, draft, resolvedSaveAndNext, onClose, reloadOriginal, liveRecord]
  );

  const handleSaveClick = useCallback(
    (thenNext?: boolean) => {
      pendingThenNextRef.current = thenNext === true;
      const needsReason = reasonRequired ? reasonRequired(usageState.summary, model) : false;
      if (needsReason) {
        setReasonDialogOpen(true);
        return;
      }
      void executeSave(null);
    },
    [reasonRequired, usageState.summary, model, executeSave]
  );

  const isDirty = useMemo(
    () => JSON.stringify(model) !== JSON.stringify(baselineInitial),
    [model, baselineInitial]
  );

  // Replaces the form with the latest saved record. Only runs when the user asked for it.
  const [reloadLatestError, setReloadLatestError] = useState<string | null>(null);
  const reloadLatest = useCallback(async () => {
    if (!reloadOriginal) return;
    setReloadLatestError(null);
    try {
      const fresh = await reloadOriginal();
      setModel(fresh);
      setBaselineInitial(fresh);
      setActiveOriginal(fresh);
      setServerErrors([]);
      setSaveError(null);
      setConflictState({ active: false, reloading: false, reloadError: null });
    } catch (err: unknown) {
      setReloadLatestError(getErrorMessage(err, i18n.t("components.configWizard.reloadLatestFailed", { ns: "common" })));
    }
  }, [reloadOriginal]);

  const requestClose = useCallback(() => {
    if (saving) return;
    if (isDirty) {
      setConfirmDiscardOpen(true);
      return;
    }
    onClose();
  }, [saving, isDirty, onClose]);

  const confirmDiscard = useCallback(async () => {
    setConfirmDiscardOpen(false);
    await draft.discard();
    onClose();
  }, [draft, onClose]);

  const usageBlocked =
    usage !== undefined &&
    reasonRequired !== undefined &&
    (usageState.status === "loading" || usageState.status === "error");

  const saveDisabled =
    saving ||
    validatingServer ||
    serverErrors.length > 0 ||
    serverValidationError !== null ||
    usageBlocked ||
    conflictState.active;

  return {
    model,
    activeOriginal,
    applicableSteps,
    activeStepIndex: clampedStepIndex,
    isReview,
    currentStep,
    allVisited,
    errorStepKeys,
    currentMergedErrors,
    serverErrors,
    serverErrorsByStep,
    unmappedServerErrors,
    validatingServer,
    serverValidationError,
    retryServerValidation: () => setServerValidationTick((n) => n + 1),
    usageState,
    usageBlocked,
    retryUsage: () => {
      usageStatusRef.current = "idle";
      setUsageTick((n) => n + 1);
    },
    saving,
    saveError,
    saveDisabled,
    resolvedSaveAndNext,
    reasonDialogOpen,
    closeReasonDialog: () => {
      pendingThenNextRef.current = false;
      setReasonDialogOpen(false);
    },
    conflictState,
    dismissConflict: () => setConflictState({ active: false, reloading: false, reloadError: null }),
    confirmDiscardOpen,
    cancelDiscard: () => setConfirmDiscardOpen(false),
    confirmDiscard,
    requestClose,
    isDirty,
    reloadLatest,
    reloadLatestError,
    focusRequest,
    draftRestoredStartedAt: draft.restoredStartedAt,
    restoreDraft,
    discardDraft: () => void draft.discard(),
    set,
    goNext,
    goBack,
    jumpToStep,
    handleSaveClick,
    executeSave,
  };
}
