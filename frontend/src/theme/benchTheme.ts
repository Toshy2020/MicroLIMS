import type { ThemeOptions } from "@mui/material/styles";

// Bench mode: the same application on a shared tablet, used standing up,
// sometimes with gloves. Every interactive control gets at least a 48 x 48 px
// touch target (WCAG 2.2 target size, with margin) and entry text is large
// enough to read at arm's length. Sizing only: colours, status tokens and
// every GMP behaviour come from the normal theme.
export const BENCH_MIN_TARGET = 48;
export const BENCH_INPUT_FONT_SIZE = 18;
// Gap kept between adjacent targets so a fingertip does not hit two.
export const BENCH_TARGET_GAP = 8;

// The incubation countdown is read from across the bench, so its figures are larger
// than the 26 px / 22 px used on a desk.
export const BENCH_COUNTDOWN_TIME_FONT_SIZE = 32;
export const BENCH_COUNTDOWN_STATE_FONT_SIZE = 28;
export const BENCH_COUNTDOWN_RING_SIZE = 220;

const target = { minHeight: BENCH_MIN_TARGET, minWidth: BENCH_MIN_TARGET };

// An inline-flex box so a text link or sort label can take a 48 px height
// without changing its width or the look of its text.
const centredTarget = { display: "inline-flex", alignItems: "center", ...target } as const;

export const benchThemeOptions: ThemeOptions = {
  components: {
    // Catch-all for every control built on ButtonBase (clickable chips, list
    // buttons, header and row action buttons, accordion summaries). Components
    // below that need more than a height set their own.
    MuiButtonBase: {
      styleOverrides: { root: { ...target } }
    },
    MuiButton: {
      defaultProps: { size: "large" },
      // Small and medium buttons too: an explicit size prop beats defaultProps.
      styleOverrides: { root: { ...target }, sizeSmall: { ...target }, sizeMedium: { ...target } }
    },
    MuiIconButton: {
      defaultProps: { size: "large" },
      styleOverrides: { root: { ...target } }
    },
    // Text links (for example "Take <test>") and breadcrumb links.
    MuiLink: {
      styleOverrides: { root: { ...centredTarget } }
    },
    MuiBreadcrumbs: {
      styleOverrides: { li: { display: "inline-flex", alignItems: "center", "& a, & button": { ...centredTarget } } }
    },
    // Column header sort buttons.
    MuiTableSortLabel: {
      styleOverrides: { root: { ...centredTarget } }
    },
    // Rows-per-page select.
    MuiTablePagination: {
      styleOverrides: {
        select: { display: "flex", alignItems: "center", minHeight: BENCH_MIN_TARGET, boxSizing: "border-box" }
      }
    },
    MuiToggleButton: {
      styleOverrides: { root: { ...target } }
    },
    MuiListItemButton: {
      styleOverrides: { root: { ...target } }
    },
    MuiChip: {
      styleOverrides: { clickable: { ...target }, deletable: { ...target } }
    },
    // Every input control, whatever its variant, including the inner input of
    // a small field.
    MuiInputBase: {
      styleOverrides: {
        root: { minHeight: BENCH_MIN_TARGET },
        input: { boxSizing: "border-box", minHeight: BENCH_MIN_TARGET }
      }
    },
    MuiSelect: {
      styleOverrides: { select: { display: "flex", alignItems: "center", minHeight: BENCH_MIN_TARGET } }
    },
    MuiTextField: {
      defaultProps: { size: "medium" }
    },
    // The input box of every TextField / Select / Autocomplete.
    MuiOutlinedInput: {
      styleOverrides: {
        root: { minHeight: BENCH_MIN_TARGET, fontSize: BENCH_INPUT_FONT_SIZE },
        input: { fontSize: BENCH_INPUT_FONT_SIZE, boxSizing: "border-box", minHeight: BENCH_MIN_TARGET }
      }
    },
    MuiCheckbox: {
      defaultProps: { size: "medium" },
      styleOverrides: { root: { ...target, padding: 12 } }
    },
    MuiRadio: {
      defaultProps: { size: "medium" },
      styleOverrides: { root: { ...target, padding: 12 } }
    },
    MuiSwitch: {
      defaultProps: { size: "medium" },
      styleOverrides: {
        // The default is 58 x 38; the touch target is the switch base.
        root: { ...target, height: BENCH_MIN_TARGET, padding: "17px 12px" },
        switchBase: { padding: 14 }
      }
    },
    MuiTab: {
      styleOverrides: { root: { ...target } }
    },
    MuiMenuItem: {
      styleOverrides: { root: { ...target, paddingTop: 12, paddingBottom: 12 } }
    },
    MuiTableCell: {
      styleOverrides: { root: { minHeight: BENCH_MIN_TARGET, padding: "14px 16px" } }
    }
  }
};
