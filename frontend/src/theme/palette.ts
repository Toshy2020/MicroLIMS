import { ThemeOptions } from "@mui/material/styles";
import { statusTonesByMode, countdownTokensByMode } from "./statusTokens";
import { chromeTokensByMode } from "./chromeTokens";

// Single-hue purple ramp for charts - kept per-mode so the darkest steps
// (which read fine on the light #f4f6f8 background) don't disappear
// against a near-black dark background.
const chartPaletteByMode = {
  light: ["#4a1a55", "#7b2d8e", "#9b3fa8", "#c084c8", "#e9d5ff"],
  dark: ["#e9d5ff", "#c084c8", "#9b3fa8", "#7b2d8e", "#4a1a55"]
};

// Shared across both modes - typography/shape/spacing/component shape
// don't change with theme, only color does.
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
        containedPrimary: { "&:hover": { backgroundColor: "#631f74" } }
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
        containedPrimary: { "&:hover": { backgroundColor: "#9b3fa8" } }
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
    chrome: chromeTokensByMode.dark
  }
};
