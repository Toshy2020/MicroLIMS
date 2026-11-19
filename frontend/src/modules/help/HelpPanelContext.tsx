import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode, RefObject } from "react";
import type { ReleaseNote } from "./whatsNew/releaseNotes";

// Replaceable data sources of the help panel and page. Production uses the
// guide modules; tests inject deterministic ones instead of mocking project
// code. Types are structural so this file pulls none of the guide content
// into the main bundle.
export interface HelpTarget {
  chapterId: string;
  heading?: string;
}

export interface HelpSearchResult {
  chapterId: string;
  title: string;
  heading: string;
  excerpt: string;
}

export interface HelpPanelServices {
  loadChapter?: (chapterId: string) => Promise<string>;
  search?: (query: string) => Promise<HelpSearchResult[]>;
  resolveTarget?: (pathname: string, search: string) => HelpTarget;
}

// Shown on every help surface (panel, field hints); the wording is fixed.
export const HELP_GUIDANCE = "Guidance only. Your approved SOPs take precedence.";

interface HelpPanelContextValue {
  open: boolean;
  /**
   * Opens the panel. With a target it shows that chapter and heading; without
   * one it shows the chapter for the current page, as the Help menu does.
   */
  openPanel(target?: HelpTarget): void;
  /** The target requested by the last openPanel(target) call, until it closes. */
  request: { target: HelpTarget; seq: number } | null;
  /** Closes the panel and returns focus to the Help button. */
  closePanel(): void;
  helpButtonRef: RefObject<HTMLButtonElement | null>;
  /** Incremented each time Replay tour is requested from the Help menu. */
  tourReplaySeq: number;
  startTour(): void;
  /** Optional release notes override supplied by tests; production uses RELEASE_NOTES. */
  whatsNewNotes?: readonly ReleaseNote[];
}

const NOOP_REF: RefObject<HTMLButtonElement | null> = { current: null };

const HelpPanelContext = createContext<HelpPanelContextValue>({
  open: false,
  openPanel: () => {},
  request: null,
  closePanel: () => {},
  helpButtonRef: NOOP_REF,
  tourReplaySeq: 0,
  startTour: () => {}
});

export function useHelpPanel(): HelpPanelContextValue {
  return useContext(HelpPanelContext);
}

const MODIFIERS = ["ctrlKey", "altKey", "metaKey", "shiftKey"] as const;

function isTextEntry(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  if (target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) return true;
  if (target instanceof HTMLInputElement) {
    return !["button", "checkbox", "radio", "submit", "reset", "range", "file", "image", "color"].includes(target.type);
  }
  return false;
}

export function HelpPanelProvider({
  children,
  whatsNewNotes
}: {
  children: ReactNode;
  whatsNewNotes?: readonly ReleaseNote[];
}) {
  const helpButtonRef = useRef<HTMLButtonElement | null>(null);
  const [open, setOpen] = useState(false);
  const [request, setRequest] = useState<{ target: HelpTarget; seq: number } | null>(null);
  const [tourReplaySeq, setTourReplaySeq] = useState(0);

  const openPanel = useCallback((target?: HelpTarget) => {
    if (target) setRequest((prev) => ({ target, seq: (prev?.seq ?? 0) + 1 }));
    setOpen(true);
  }, []);
  const closePanel = useCallback(() => {
    setOpen(false);
    setRequest(null);
    helpButtonRef.current?.focus();
  }, []);
  const startTour = useCallback(() => {
    setTourReplaySeq((prev) => prev + 1);
  }, []);

  // F1 opens help. In a text field a modified F1 is left to the browser.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "F1" || event.defaultPrevented) return;
      if (isTextEntry(event.target) && MODIFIERS.some((m) => event[m])) return;
      event.preventDefault();
      setOpen(true);
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  const value = useMemo(
    () => ({ open, openPanel, request, closePanel, helpButtonRef, tourReplaySeq, startTour, whatsNewNotes }),
    [open, openPanel, request, closePanel, tourReplaySeq, startTour, whatsNewNotes]
  );
  return <HelpPanelContext.Provider value={value}>{children}</HelpPanelContext.Provider>;
}
