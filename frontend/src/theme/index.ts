import { createTheme } from "@mui/material/styles";
import { baseThemeOptions, lightThemeOptions, darkThemeOptions } from "./palette";

// Design system extracted from the provided Sample Receiving mockup:
// purple-gradient brand header, dark-purple subnav, card-based content,
// light-green toolbars, pill badges. These are brand "chrome" colors
// (topbar/sidebar/toolbar) that intentionally stay the same in both
// light and dark mode - they're already saturated/dark accents, not
// surfaces that need to flip with theme.palette.mode. Applied via
// `import { brandColors } from "../theme"` across the app.
export const brandColors = {
  topbarGradient: "linear-gradient(90deg, #7b2d8e, #9b3fa8)",
  subnavBg: "#6a1f78",
  subnavText: "#f1d9f5",
  pageTitle: "#4a1a55",
  sectionTitle: "#7b2d8e",
  toolbarBg: "#e4edc4",
  toolbarText: "#4a5a1f",
  badgeRM: "#2563eb",
  badgeProduct: "#16a34a",
  badgePM: "#d97706",
  causeBadgeBg: "#ede9fe",
  causeBadgeText: "#6d28d9",
  ok: "#16a34a",
  err: "#dc2626"
};

export const lightTheme = createTheme(baseThemeOptions, lightThemeOptions);
export const darkTheme = createTheme(baseThemeOptions, darkThemeOptions);

export function getTheme(mode: "light" | "dark") {
  return mode === "dark" ? darkTheme : lightTheme;
}

// Back-compat aliases - `theme`/`chartPalette` used to be the only
// (light-only) export; kept pointing at the light theme for anything
// not yet updated to consume mode-aware values via useTheme().
export const theme = lightTheme;
export const chartPalette = lightTheme.custom.chartPalette;
