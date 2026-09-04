/**
 * Determines whether an event target is an interactive control or contained within one,
 * or if it originated from an external overlay/portal outside currentTarget (e.g. Popover, Dialog, Menu, Backdrop).
 *
 * This ensures that clicking action buttons, icon buttons, dropdowns, checkboxes, links,
 * clickable badges/chips, or closing popovers does NOT trigger the parent row/card click handler.
 */
export function isInteractiveElement(
  target: EventTarget | null,
  currentTarget?: EventTarget | null
): boolean {
  if (!target || !(target instanceof Element)) {
    return false;
  }

  // If currentTarget is provided, check whether target is physically inside currentTarget in the DOM.
  // In React, events dispatched inside Portals (e.g. Popover, Menu, Dialog, Backdrop) bubble up the React tree.
  // If target is NOT a DOM descendant of currentTarget, it originated from a portal/overlay.
  if (currentTarget instanceof Element && !currentTarget.contains(target)) {
    return true;
  }

  const interactiveSelector = [
    "button",
    "a[href]",
    "input",
    "select",
    "textarea",
    "[role='button']",
    "[role='link']",
    "[role='checkbox']",
    "[role='radio']",
    "[role='menuitem']",
    "[role='option']",
    "[role='tab']",
    "[role='switch']",
    "[role='combobox']",
    "[role='dialog']",
    "[aria-haspopup]",
    ".MuiButtonBase-root",
    ".MuiChip-clickable",
    ".MuiIconButton-root",
    ".MuiButton-root",
    ".MuiSelect-root",
    ".MuiInputBase-root",
    ".MuiCheckbox-root",
    ".MuiRadio-root",
    ".MuiSwitch-root",
    ".MuiMenuItem-root",
    ".MuiListItemButton-root",
    "[data-no-row-click='true']"
  ].join(", ");

  const closestInteractive = target.closest(interactiveSelector);
  if (!closestInteractive) {
    return false;
  }

  // If the interactive element matched is the currentTarget itself (e.g. if the row itself has role="button"),
  // it is not a child interactive element.
  if (currentTarget && closestInteractive === currentTarget) {
    return false;
  }

  return true;
}
