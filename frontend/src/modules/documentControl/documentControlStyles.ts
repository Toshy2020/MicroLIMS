import { monospaceFontFamily } from "../../theme/palette";

/**
 * Shared sizing for the Document Control screens.
 *
 * These exist because the module had drifted into 38 ad-hoc `sx` blocks setting
 * `fontSize` to 9, 10 or 11 and chip heights to 16, 18, 20 or 22 - different
 * values on different screens for the same kind of label. Two consequences:
 *
 *  - 9px and 10px text is below any reasonable legibility floor, and this is
 *    software read all day under laboratory lighting, often on the far side of a
 *    bench. Revision numbers and document codes being unreadable is not a
 *    cosmetic problem in a GMP system.
 *  - Chips at 16-18px tall are hard to hit and made rows look ragged, because
 *    neighbouring chips on the same row were different heights.
 *
 * The floor is 12px (0.75rem, MUI's `caption`). Chips use MUI's own small-chip
 * height of 24px so the rhythm matches the rest of the application instead of
 * being specific to this module.
 *
 * Note on touch targets: the accessibility guidance asks for 44x44px. That is
 * not reachable for a row action inside a table this dense, and this is a
 * workstation application driven by a mouse rather than a phone. Row actions are
 * therefore raised to a 32px target with real spacing between them, and every
 * icon-only control carries an accessible name - a deliberate, documented
 * trade-off rather than an oversight.
 */

// Smallest type used anywhere in the module.
export const MIN_LABEL_FONT_SIZE = "0.75rem";

/** Status, category and count chips sitting inside table rows and headers. */
export const compactChipSx = {
  height: 24,
  fontSize: MIN_LABEL_FONT_SIZE
};

/** Same chip where the label is carrying emphasis (counts, alert states). */
export const compactChipStrongSx = {
  height: 24,
  fontSize: MIN_LABEL_FONT_SIZE,
  fontWeight: 700
};

/**
 * Controlled-document identifiers: company document code, MicroLIMS document id,
 * revision number, SHA-256 digest. Monospace so characters can be compared, with
 * slight negative tracking so a long digest still fits a table cell.
 */
export const documentCodeSx = {
  fontFamily: monospaceFontFamily,
  fontSize: MIN_LABEL_FONT_SIZE,
  letterSpacing: "-0.01em"
};

/** Document code at body size, for headers and dialog titles. */
export const documentCodeBodySx = {
  fontFamily: monospaceFontFamily,
  letterSpacing: "-0.01em"
};

/**
 * Icon-only row action. Raises the hit area and keeps actions far enough apart
 * that neighbouring ones are not mis-clicked - which matters when the actions on
 * a row include irreversible ones like voiding a master.
 */
export const rowActionSx = {
  minWidth: 32,
  minHeight: 32
};
