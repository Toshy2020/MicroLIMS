import { ThemeOptions } from "@mui/material/styles";
import { statusTonesByMode, countdownTokensByMode } from "./statusTokens";
import { chromeTokensByMode } from "./chromeTokens";

// Single-hue purple ramp for charts - kept per-mode so the darkest steps
// (which read fine on the light #f4f6f8 background) don't disappear
// against a near-black dark background.
// CATEGORICAL - identity, not magnitude. One hue per entity, assigned in a
// fixed order and never cycled.
//
// This used to be a five-step ramp of the single brand purple, which is a
// *sequential* scale: it encodes magnitude, so using it for categories
// asserted a ranking that does not exist, and adjacent steps of one hue are
// the hardest pairs to tell apart. Measured, that palette failed the
// lightness band, the chroma floor and the normal-vision separation floor in
// both modes (worst adjacent pair dE 8.3, where 15 is the floor), and its
// lightest step sat at 1.36:1 on a white card - effectively invisible.
//
// These two sets are validated against the real card surfaces (#FFFFFF and
// #1A1F27): all checks pass, worst adjacent pair dE 19.6 light / 19.3 dark.
// Seven slots covers the widest consumer (the Reports location donut, capped
// at seven server-side) so nothing has to wrap around.
const chartPaletteByMode = {
  light: ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7"],
  dark:  ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9"]
};

// SEQUENTIAL - for genuinely ordered data (pipeline stages, ranked bands),
// which is the job the old purple ramp was actually shaped for. Kept on the
// brand hue and re-stepped so each mode's shallow end still clears its own
// surface: on a dark card the *dark* end is the one that disappears, so the
// dark ramp starts mid-tone rather than near-black.
// Both validated as ordinal ramps: monotone lightness, >=0.06 L between
// steps, shallow end 2.33:1 (light) / 2.79:1 (dark) against the surface.
const chartSequentialByMode = {
  light: ["#c79ad6", "#b070c5", "#9b3fa8", "#7b2d8e", "#5c2069"],
  dark:  ["#e9d5ff", "#d69ae4", "#c17dd2", "#a660b8", "#8a4a9c"]
};
// Shared across both modes - typography/shape/spacing/component shape
// don't change with theme, only color does.
// Controlled-document identifiers (SOP-QC-042, DOC-0000042, SHA-256 digests,
// revision numbers) are compared character by character by the people reading
// them, so they are set in a monospace face where 0/O and 1/l stay distinct and
// digits line up between table rows. Lived as a hardcoded "monospace" string at
// eleven call sites before this token existed, which meant no control over the
// fallback stack and no single place to change the treatment.
export const monospaceFontFamily =
  "'Cascadia Mono', Consolas, 'SF Mono', 'Roboto Mono', ui-monospace, monospace";

export const baseThemeOptions: ThemeOptions = {
  typography: {
    fontFamily: "'Segoe UI', Roboto, Arial, sans-serif"
  },
  shape: { borderRadius: 8 },
  components: {
    MuiButton: {
      defaultProps: { size: "large" },
      styleOverrides: {
        root: { textTransform: "none", borderRadius: 8, fontWeight: 600 }
      }
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 700, fontSize: 11 }
      }
    }
  }
};

export const lightThemeOptions: ThemeOptions = {
  palette: {
    mode: "light",
    primary: { main: "#7b2d8e", dark: "#631f74", light: "#9b3fa8" },
    secondary: { main: "#15803d" },
    error: { main: "#dc2626" },
    background: { default: "#F4F6F8", paper: "#FFFFFF" },
    text: { primary: "#1A2027", secondary: "#5A6472" },
    divider: "#E3E7EB"
  },
  typography: {
    h5: { color: "#4a1a55", fontWeight: 700 }
  },
  components: {
    // Without this, native form controls (the date-picker calendar icon
    // and "dd/mm/yyyy" placeholder text on <input type="date">, native
    // scrollbars, etc.) keep rendering with the browser's light-mode UA
    // styles even once the rest of the page has gone dark - MUI doesn't
    // set this on its own.
    MuiCssBaseline: {
      styleOverrides: {
        html: { colorScheme: "light" },
        "*:focus-visible": {
          outline: "2px solid #7b2d8e",
          outlineOffset: "2px"
        }
      }
    },
    MuiButton: {
      styleOverrides: {
        // MUI v9 removed the containedPrimary style slot; the same element is
        // targeted through its variant + color classes instead.
        root: { "&.MuiButton-contained.MuiButton-colorPrimary:hover": { backgroundColor: "#631f74" } }
      }
    },
    MuiPaper: {
      styleOverrides: {
        // colorScheme here (not just on MuiCssBaseline's <html>) matters for
        // anything MUI portals to document.body - Dialog/Menu/Popover/Select
        // all render their Paper there, outside the DOM subtree any wrapping
        // element in this theme's own render tree could reach via CSS
        // inheritance, even though React context (and so the palette/colors)
        // still flows correctly through the portal. Without this, a Paper
        // rendered under a nested light ThemeProvider (e.g. PinnedLightTheme)
        // while the app-wide CssBaseline has set <html> to color-scheme:dark
        // keeps native form-control chrome (password input backgrounds,
        // autofill tint, etc.) rendering with dark UA defaults regardless of
        // the correct light palette colors MUI itself is applying.
        root: { borderRadius: 10, boxShadow: "0 1px 3px rgba(20,30,45,0.08), 0 1px 2px rgba(20,30,45,0.06)", colorScheme: "light" }
      }
    }
  },
  custom: {
    status: statusTonesByMode.light,
    countdown: countdownTokensByMode.light,
    chartPalette: chartPaletteByMode.light,
    chartSequential: chartSequentialByMode.light,
    chrome: chromeTokensByMode.light
  }
};

export const darkThemeOptions: ThemeOptions = {
  palette: {
    mode: "dark",
    // Purple brand hue preserved (not swapped for a different accent) -
    // lightened for legibility against a near-black background rather
    // than reusing the light-mode shade as-is.
    primary: { main: "#c084c8", dark: "#9b3fa8", light: "#e9d5ff" },
    secondary: { main: "#4ade80" },
    error: { main: "#f0a8a8" },
    background: { default: "#12161C", paper: "#1A1F27" },
    text: { primary: "#E7EBF0", secondary: "#99A3B0" },
    divider: "#2C333E"
  },
  typography: {
    h5: { color: "#e9d5ff", fontWeight: 700 }
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        html: { colorScheme: "dark" },
        "*:focus-visible": {
          outline: "2px solid #c084c8",
          outlineOffset: "2px"
        }
      }
    },
    MuiButton: {
      styleOverrides: {
        // See the light theme: v9 removed containedPrimary, so target the
        // variant + color classes.
        root: { "&.MuiButton-contained.MuiButton-colorPrimary:hover": { backgroundColor: "#9b3fa8" } }
      }
    },
    MuiPaper: {
      styleOverrides: {
        // Mirrors the light theme's MuiPaper colorScheme (see that theme's
        // comment) - a no-op today since dark is already the ambient
        // <html> color-scheme, but keeps this theme self-consistent if a
        // future PinnedDarkTheme-style nesting is ever added.
        root: { borderRadius: 10, boxShadow: "0 1px 3px rgba(0,0,0,0.5), 0 1px 2px rgba(0,0,0,0.4)", colorScheme: "dark" }
      }
    }
  },
  custom: {
    status: statusTonesByMode.dark,
    countdown: countdownTokensByMode.dark,
    chartPalette: chartPaletteByMode.dark,
    chartSequential: chartSequentialByMode.dark,
    chrome: chromeTokensByMode.dark
  }
};
