// True when a key event comes from inside an open dialog. Page-level, single-key shortcuts must not act
// there: a character typed in a dialog belongs to the dialog.
export function isInsideDialog(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest("[role='dialog'], [role='alertdialog']") !== null;
}
