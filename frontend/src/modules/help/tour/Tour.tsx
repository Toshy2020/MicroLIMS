import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { Box, Button, Paper, Popper, Stack, Typography, useMediaQuery, useTheme } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { useHelpPanel } from "../HelpPanelContext";
import type { TourStep } from "./tourSteps";

export interface TourProps {
  steps: TourStep[];
  onFinish(result: "done" | "skipped"): void;
}

function escapeSelectorValue(value: string): string {
  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
    return CSS.escape(value);
  }
  return value.replace(/"/g, '\\"');
}

export function isTargetVisible(el: HTMLElement): boolean {
  if (!el.isConnected) return false;
  let current: HTMLElement | null = el;
  while (current) {
    if (current.hidden || current.getAttribute("aria-hidden") === "true") {
      return false;
    }
    if (current.style.display === "none" || current.style.visibility === "hidden") {
      return false;
    }
    const computed = window.getComputedStyle(current);
    if (computed.display === "none" || computed.visibility === "hidden") {
      return false;
    }
    current = current.parentElement;
  }
  return true;
}

export function findTourTarget(target: string): HTMLElement | null {
  if (!target) return null;
  const selector = `[data-tour="${escapeSelectorValue(target)}"]`;
  const candidates = document.querySelectorAll<HTMLElement>(selector);
  for (const el of candidates) {
    if (isTargetVisible(el)) {
      return el;
    }
  }
  return null;
}

export function Tour({ steps, onFinish }: TourProps) {
  const { t } = useTranslation("help");
  const theme = useTheme();
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
  const { helpButtonRef } = useHelpPanel();
  const titleId = useId();
  const bodyId = useId();

  const previousFocusRef = useRef<HTMLElement | null>(null);
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const primaryButtonRef = useRef<HTMLButtonElement | null>(null);
  const finishedRef = useRef(false);
  const [mounted, setMounted] = useState(false);

  useLayoutEffect(() => {
    const active = document.activeElement;
    if (
      active instanceof HTMLElement &&
      active.closest("#header-help-menu") &&
      helpButtonRef.current
    ) {
      previousFocusRef.current = helpButtonRef.current;
    } else {
      previousFocusRef.current =
        active instanceof HTMLElement && active !== document.body ? active : null;
    }
    setMounted(true);
  }, [helpButtonRef]);

  const availableSteps = useMemo(
    () => (mounted ? steps.filter((step) => findTourTarget(step.target) !== null) : []),
    [mounted, steps]
  );

  const [stepIndex, setStepIndex] = useState(0);

  const restoreFocusToPage = useCallback(() => {
    const saved = previousFocusRef.current;
    if (saved && saved.isConnected && isTargetVisible(saved)) {
      saved.focus();
      return;
    }
    const main = document.getElementById("main-content");
    if (main) {
      main.focus();
      return;
    }
    const firstTarget = availableSteps[0] ? findTourTarget(availableSteps[0].target) : null;
    firstTarget?.focus();
  }, [availableSteps]);

  const finishTour = useCallback(
    (result: "done" | "skipped") => {
      if (finishedRef.current) return;
      finishedRef.current = true;
      restoreFocusToPage();
      onFinish(result);
    },
    [onFinish, restoreFocusToPage]
  );

  // If no target is available in the DOM, end silently as "skipped" and render nothing.
  useEffect(() => {
    if (mounted && availableSteps.length === 0) {
      finishTour("skipped");
    }
  }, [mounted, availableSteps.length, finishTour]);

  const clampedIndex = Math.min(stepIndex, Math.max(0, availableSteps.length - 1));
  const currentStep: TourStep | undefined = availableSteps[clampedIndex];
  const anchorEl = currentStep ? findTourTarget(currentStep.target) : null;

  // If the current step's target disappeared dynamically, advance or end.
  useEffect(() => {
    if (availableSteps.length > 0 && currentStep && !anchorEl) {
      const nextValid = availableSteps.findIndex(
        (s, idx) => idx > clampedIndex && findTourTarget(s.target) !== null
      );
      if (nextValid !== -1) {
        setStepIndex(nextValid);
      } else {
        finishTour("skipped");
      }
    }
  }, [anchorEl, availableSteps, clampedIndex, currentStep, finishTour]);

  // Outline highlight on the active target element using a theme token (no dimming overlay).
  useLayoutEffect(() => {
    if (!anchorEl) return;
    const prevOutline = anchorEl.style.outline;
    const prevOutlineOffset = anchorEl.style.outlineOffset;
    const prevBorderRadius = anchorEl.style.borderRadius;

    anchorEl.style.outline = `3px solid ${theme.palette.primary.main}`;
    anchorEl.style.outlineOffset = "2px";
    if (!prevBorderRadius) {
      anchorEl.style.borderRadius = "4px";
    }
    anchorEl.setAttribute("data-tour-active", "true");

    if (typeof anchorEl.scrollIntoView === "function") {
      anchorEl.scrollIntoView({
        block: "nearest",
        behavior: reducedMotion ? "auto" : "smooth"
      });
    }

    return () => {
      anchorEl.style.outline = prevOutline;
      anchorEl.style.outlineOffset = prevOutlineOffset;
      anchorEl.style.borderRadius = prevBorderRadius;
      anchorEl.removeAttribute("data-tour-active");
    };
  }, [anchorEl, reducedMotion, theme.palette.primary.main]);

  // Move focus into the dialog whenever the active step changes.
  useLayoutEffect(() => {
    if (!currentStep || !anchorEl) return;
    primaryButtonRef.current?.focus();
  }, [clampedIndex, currentStep, anchorEl]);

  // Global Escape listener so Escape dismisses the tour reliably.
  useEffect(() => {
    if (availableSteps.length === 0) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !event.defaultPrevented) {
        event.preventDefault();
        finishTour("skipped");
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [availableSteps.length, finishTour]);

  if (availableSteps.length === 0 || !currentStep || !anchorEl) {
    return null;
  }

  const isLastStep = clampedIndex >= availableSteps.length - 1;

  const handleNext = () => {
    if (isLastStep) {
      finishTour("done");
    } else {
      setStepIndex((prev) => Math.min(prev + 1, availableSteps.length - 1));
    }
  };

  const handleBack = () => {
    setStepIndex((prev) => Math.max(0, prev - 1));
  };

  const handleSkip = () => {
    finishTour("skipped");
  };

  const handleDialogKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      handleSkip();
      return;
    }
    if (event.key !== "Tab") return;
    const dialog = dialogRef.current;
    if (!dialog) return;
    const focusables = Array.from(
      dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      )
    );
    if (focusables.length === 0) return;
    event.preventDefault();
    const active = document.activeElement as HTMLElement | null;
    const currentIdx = active ? focusables.indexOf(active) : -1;
    const nextIdx = event.shiftKey
      ? currentIdx <= 0
        ? focusables.length - 1
        : currentIdx - 1
      : currentIdx === -1 || currentIdx >= focusables.length - 1
        ? 0
        : currentIdx + 1;
    focusables[nextIdx]?.focus();
  };

  return (
    <Popper
      open
      disablePortal
      anchorEl={anchorEl}
      placement="bottom-start"
      modifiers={[
        { name: "offset", options: { offset: [0, 10] } },
        // MUI Popper already registers preventOverflow with altBoundary: true and Popper.js merges
        // options by name, so altBoundary must be turned off explicitly: otherwise the overflow is
        // measured against the anchor and the popover is pushed off-screen in RTL.
        { name: "preventOverflow", options: { padding: 12, altBoundary: false, rootBoundary: "viewport" } }
      ]}
      sx={{ zIndex: theme.zIndex.modal - 1 }}
    >
      <Paper
        ref={dialogRef}
        role="dialog"
        aria-modal="false"
        aria-labelledby={titleId}
        aria-describedby={bodyId}
        onKeyDown={handleDialogKeyDown}
        sx={{
          width: { xs: "calc(100vw - 24px)", sm: 360 },
          maxWidth: 360,
          p: 2,
          border: `1px solid ${theme.palette.primary.main}`,
          bgcolor: "background.paper",
          color: "text.primary",
          boxShadow: 6
        }}
      >
        <Box sx={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 1 }}>
          <Typography id={titleId} component="span" variant="subtitle1" sx={{ fontWeight: 700 }}>
            {currentStep.title}
          </Typography>
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{ fontVariantNumeric: "tabular-nums", flexShrink: 0, fontWeight: 600 }}
          >
            {t("tour.step", { current: clampedIndex + 1, total: availableSteps.length })}
          </Typography>
        </Box>

        <Typography id={bodyId} variant="body2" sx={{ mt: 1 }}>
          {currentStep.body}
        </Typography>

        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
          {t("shared.guidance")}
        </Typography>

        <Stack
          direction="row"
          spacing={1}
          sx={{ mt: 2, alignItems: "center", justifyContent: "space-between" }}
        >
          <Button size="small" color="inherit" onClick={handleSkip}>
            {t("tour.skip")}
          </Button>
          <Stack direction="row" spacing={1}>
            <Button size="small" variant="outlined" onClick={handleBack} disabled={clampedIndex === 0}>
              {t("tour.back")}
            </Button>
            <Button
              ref={primaryButtonRef}
              size="small"
              variant="contained"
              onClick={handleNext}
            >
              {isLastStep ? t("tour.finish") : t("tour.next")}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Popper>
  );
}
