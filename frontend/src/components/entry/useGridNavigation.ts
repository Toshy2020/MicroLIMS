import { useCallback, useRef } from "react";
import type React from "react";

export interface GridNavigationOptions {
  rows: number;
  cols: number;
  isEditable?(r: number, c: number): boolean;
  onCopyDown?(r: number, c: number): void;
  onSave?(): void;
  /** When true, focusCell also calls `.select()` on focused input/textarea elements. */
  selectOnFocus?: boolean;
  /**
   * When true, attaches the keyboard handler in the capture phase (`onKeyDownCapture`)
   * and stops propagation for handled keys so child MUI Select / combobox controls
   * do not open on Enter/ArrowUp/ArrowDown while closed.
   */
  capture?: boolean;
}

export interface GridCellProps {
  ref: (el: HTMLElement | null) => void;
  onKeyDown: (e: React.KeyboardEvent) => void;
  onKeyDownCapture?: (e: React.KeyboardEvent) => void;
  "data-grid-cell": string;
}

const noopKeyDown = () => {};

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
    const applyFocus = (node: HTMLElement) => {
      node.focus();
      if (
        optsRef.current.selectOnFocus &&
        (node instanceof HTMLInputElement || node instanceof HTMLTextAreaElement)
      ) {
        node.select();
      }
    };
    if (el.matches("input, textarea, select, button, [tabindex]")) {
      applyFocus(el);
      return;
    }
    const focusable = el.querySelector<HTMLElement>(
      "input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex='-1'])"
    );
    applyFocus(focusable ?? el);
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
      const capture = Boolean(optsRef.current.capture);
      const handleKeyDown = (e: React.KeyboardEvent) => {
        const target = e.target as HTMLElement | null;
        if (
          target?.getAttribute("aria-expanded") === "true" ||
          (e.currentTarget instanceof HTMLElement && e.currentTarget.getAttribute("aria-expanded") === "true")
        ) {
          return;
        }

        if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
          e.preventDefault();
          if (capture) e.stopPropagation();
          optsRef.current.onSave?.();
          return;
        }

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "d") {
          e.preventDefault();
          if (capture) e.stopPropagation();
          const { isEditable, onCopyDown } = optsRef.current;
          if (r > 0 && (!isEditable || isEditable(r, c))) {
            onCopyDown?.(r, c);
          }
          return;
        }

        if (e.ctrlKey || e.metaKey || e.altKey) {
          return;
        }

        const isMultiRadioCell =
          capture &&
          e.currentTarget instanceof HTMLElement &&
          e.currentTarget.querySelectorAll('input[type="radio"]').length > 1;

        switch (e.key) {
          case "Enter":
            if (e.shiftKey) return;
            e.preventDefault();
            if (capture) e.stopPropagation();
            moveFrom(r, c, 1, 0);
            break;
          case "ArrowDown":
            if (isMultiRadioCell) return;
            e.preventDefault();
            if (capture) e.stopPropagation();
            moveFrom(r, c, 1, 0);
            break;
          case "ArrowUp":
            if (isMultiRadioCell) return;
            e.preventDefault();
            if (capture) e.stopPropagation();
            moveFrom(r, c, -1, 0);
            break;
          case "ArrowLeft":
            if (isTextInputAtEdge(target, "left")) {
              e.preventDefault();
              if (capture) e.stopPropagation();
              moveFrom(r, c, 0, -1);
            }
            break;
          case "ArrowRight":
            if (isTextInputAtEdge(target, "right")) {
              e.preventDefault();
              if (capture) e.stopPropagation();
              moveFrom(r, c, 0, 1);
            }
            break;
          default:
            break;
        }
      };

      return {
        ref: (el: HTMLElement | null) => {
          if (el) {
            cellRefs.current.set(key, el);
          } else {
            cellRefs.current.delete(key);
          }
        },
        "data-grid-cell": key,
        onKeyDown: capture ? noopKeyDown : handleKeyDown,
        ...(capture ? { onKeyDownCapture: handleKeyDown } : {})
      };
    },
    [moveFrom]
  );

  return { cellProps, focusCell };
}
