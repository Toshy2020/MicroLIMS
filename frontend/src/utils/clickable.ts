import type { KeyboardEvent } from "react";

// Makes a clickable Box/Paper/card reachable and operable from the keyboard:
// spread onto the element that already has the onClick.
//
//   <Paper onClick={open} {...clickable(open, { label: "Open TSA lot 42" })}>
//
// `containsControls`: the element holds its own buttons (edit/delete icons).
// role="button" would flatten them away from screen readers, so the element
// only becomes focusable and Enter/Space-operable, keeping the inner controls
// reachable on their own.
export function clickable(
  onActivate: () => void,
  opts: { label?: string; containsControls?: boolean; pressed?: boolean; expanded?: boolean } = {}
) {
  return {
    role: opts.containsControls ? undefined : "button",
    tabIndex: 0,
    "aria-label": opts.label,
    "aria-pressed": opts.pressed,
    "aria-expanded": opts.expanded,
    onKeyDown: (e: KeyboardEvent<HTMLElement>) => {
      // Keys pressed inside an inner input or button are theirs, not ours.
      if (e.target !== e.currentTarget) return;
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        onActivate();
      }
    }
  };
}
