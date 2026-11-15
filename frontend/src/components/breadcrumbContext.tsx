import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

// The last crumb of the trail is the record the user is looking at: an item
// code, a document code, a run reference. Only the page knows it (and only once
// it has loaded), so the page supplies it and the layout shows it.
//   undefined = the page supplies no label (list pages and other menu pages)
//   null      = the page supplies one but is still loading it (shows a skeleton)
//   string    = the record's human identifier
export type PageCrumb = string | null | undefined;

const BreadcrumbLabelContext = createContext<PageCrumb>(undefined);
const BreadcrumbSetterContext = createContext<((label: PageCrumb) => void) | null>(null);

export function BreadcrumbProvider({ children }: { children: ReactNode }) {
  const [label, setLabel] = useState<PageCrumb>(undefined);
  return (
    <BreadcrumbSetterContext.Provider value={setLabel}>
      <BreadcrumbLabelContext.Provider value={label}>{children}</BreadcrumbLabelContext.Provider>
    </BreadcrumbSetterContext.Provider>
  );
}

export function usePageBreadcrumb(): PageCrumb {
  return useContext(BreadcrumbLabelContext);
}

// Sets the last crumb for the lifetime of the calling page. Pass null while the
// record loads (never the raw id) and the human identifier once it has. The
// crumb is removed when the page unmounts. Outside a layout (isolated tests)
// it does nothing.
export function useBreadcrumb(label: string | null): void {
  const setLabel = useContext(BreadcrumbSetterContext);
  useEffect(() => {
    if (!setLabel) return undefined;
    setLabel(label);
    return () => setLabel(undefined);
  }, [setLabel, label]);
}

// Memo helper for pages that build a label from several fields.
export function useBreadcrumbLabel(parts: ReadonlyArray<string | null | undefined>, ready: boolean): string | null {
  const text = parts.filter((p): p is string => !!p && p.trim().length > 0).join(" ");
  return useMemo(() => (ready && text ? text : null), [ready, text]);
}

// For pages that supply a crumb only in some states (a list at /help, a chapter
// at /help/:chapterId): render it where hooks cannot be called conditionally.
export function BreadcrumbLabel({ label }: { label: string | null }) {
  useBreadcrumb(label);
  return null;
}
