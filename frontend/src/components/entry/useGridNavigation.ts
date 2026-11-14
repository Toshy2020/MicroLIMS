import { useCallback, useRef } from "react";
import type React from "react";

export interface GridNavigationOptions {
  rows: number;
  cols: number;
  isEditable?(r: number, c: number): boolean;
  onCopyDown?(r: number, c: number): void;
  onSave?(): void;
}

export interface GridCellProps {
  ref: (el: HTMLElement | null) => void;
  onKeyDown: (e: React.KeyboardEvent) => void;
  "data-grid-cell": string;
}

function isTextInputAtEdge(target: EventTarget | null, direction: "left" | "right"): boolean {
  if (!(target instanceof HTMLInputElement) && !(target instanceof HTMLTextAreaElement)) {
    return true;
  }
  const { selectionStart, selectionEnd } = target;
  if (selectionStart === null || selectionEnd === null) {
    return true;
  }
  if (direction === "left") {
    return selectionStart === 0 && selectionEnd === 0;
  }
  const len = target.value.length;
  return selectionStart === len && selectionEnd === len;
}

export function useGridNavigation(opts: GridNavigationOptions): {
  cellProps(r: number, c: number): GridCellProps;
  focusCell(r: number, c: number): void;
} {
  const cellRefs = useRef<Map<string, HTMLElement | null>>(new Map());
  const optsRef = useRef(opts);
  optsRef.current = opts;

  const focusCell = useCallback((r: number, c: number) => {
    const el = cellRefs.current.get(`${r}:${c}`);
    if (!el) return;
    if (el.matches("input, textarea, select, button, [tabindex]")) {
      el.focus();
      return;
    }
    const focusable = el.querySelector<HTMLElement>(
      "input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex='-1'])"
    );
    (focusable ?? el).focus();
  }, []);

  const moveFrom = useCallback(
    (r: number, c: number, dr: number, dc: number) => {
      const { rows, cols, isEditable } = optsRef.current;
      let nr = r + dr;
      let nc = c + dc;
      while (nr >= 0 && nr < rows && nc >= 0 && nc < cols) {
        if (!isEditable || isEditable(nr, nc)) {
          focusCell(nr, nc);
          return;
        }
        nr += dr;
        nc += dc;
      }
    },
    [focusCell]
  );

  const cellProps = useCallback(
    (r: number, c: number): GridCellProps => {
      const key = `${r}:${c}`;
      return {
        ref: (el: HTMLElement | null) => {
          if (el) {
            cellRefs.current.set(key, el);
          } else {
            cellRefs.current.delete(key);
          }
        },
        "data-grid-cell": key,
        onKeyDown: (e: React.KeyboardEvent) => {
          const target = e.target as HTMLElement | null;
          if (
            target?.getAttribute("aria-expanded") === "true" ||
            (e.currentTarget instanceof HTMLElement && e.currentTarget.getAttribute("aria-expanded") === "true")
          ) {
            return;
          }

          if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
            e.preventDefault();
            optsRef.current.onSave?.();
            return;
          }

          if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "d") {
            e.preventDefault();
            const { isEditable, onCopyDown } = optsRef.current;
            if (r > 0 && (!isEditable || isEditable(r, c))) {
              onCopyDown?.(r, c);
            }
            return;
          }

          if (e.ctrlKey || e.metaKey || e.altKey) {
            return;
          }

          switch (e.key) {
            case "Enter":
              if (e.shiftKey) return;
              e.preventDefault();
              moveFrom(r, c, 1, 0);
              break;
            case "ArrowDown":
              e.preventDefault();
              moveFrom(r, c, 1, 0);
              break;
            case "ArrowUp":
              e.preventDefault();
              moveFrom(r, c, -1, 0);
              break;
            case "ArrowLeft":
              if (isTextInputAtEdge(target, "left")) {
                e.preventDefault();
                moveFrom(r, c, 0, -1);
              }
              break;
            case "ArrowRight":
              if (isTextInputAtEdge(target, "right")) {
                e.preventDefault();
                moveFrom(r, c, 0, 1);
              }
              break;
            default:
              break;
          }
        }
      };
    },
    [moveFrom]
  );

  return { cellProps, focusCell };
}
