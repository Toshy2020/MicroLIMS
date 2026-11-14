import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode, RefObject } from "react";

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

interface HelpPanelContextValue {
  open: boolean;
  openPanel(): void;
  /** Closes the panel and returns focus to the Help button. */
  closePanel(): void;
  helpButtonRef: RefObject<HTMLButtonElement | null>;
}

const NOOP_REF: RefObject<HTMLButtonElement | null> = { current: null };

const HelpPanelContext = createContext<HelpPanelContextValue>({
  open: false,
  openPanel: () => {},
  closePanel: () => {},
  helpButtonRef: NOOP_REF
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

export function HelpPanelProvider({ children }: { children: ReactNode }) {
  const helpButtonRef = useRef<HTMLButtonElement | null>(null);
  const [open, setOpen] = useState(false);

  const openPanel = useCallback(() => setOpen(true), []);
  const closePanel = useCallback(() => {
    setOpen(false);
    helpButtonRef.current?.focus();
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

  const value = useMemo(() => ({ open, openPanel, closePanel, helpButtonRef }), [open, openPanel, closePanel]);
  return <HelpPanelContext.Provider value={value}>{children}</HelpPanelContext.Provider>;
}
