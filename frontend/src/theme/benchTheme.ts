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

const target = { minHeight: BENCH_MIN_TARGET, minWidth: BENCH_MIN_TARGET };

export const benchThemeOptions: ThemeOptions = {
  components: {
    MuiButton: {
      defaultProps: { size: "large" },
      styleOverrides: { root: { ...target } }
    },
    MuiIconButton: {
      defaultProps: { size: "large" },
      styleOverrides: { root: { ...target } }
    },
    MuiTextField: {
      defaultProps: { size: "medium" }
    },
    // The input box of every TextField / Select / Autocomplete.
    MuiOutlinedInput: {
      styleOverrides: {
        root: { minHeight: BENCH_MIN_TARGET, fontSize: BENCH_INPUT_FONT_SIZE },
        input: { fontSize: BENCH_INPUT_FONT_SIZE, minHeight: "1.4375em" }
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
