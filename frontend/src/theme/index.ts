import { createTheme } from "@mui/material/styles";

// Design system extracted from the provided Sample Receiving mockup:
// purple-gradient brand header, dark-purple subnav, card-based content,
// light-green toolbars, pill badges. Applied consistently across every
// page via MainLayout + shared components below.
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

// Single-hue purple ramp for charts - "keep the color homogeneous"
// instead of mixed red/green/blue like a generic dashboard template.
export const chartPalette = ["#4a1a55", "#7b2d8e", "#9b3fa8", "#c084c8", "#e9d5ff"];

export const theme = createTheme({
  palette: {
    primary: { main: "#7b2d8e", dark: "#631f74", light: "#9b3fa8" },
    secondary: { main: "#16a34a" },
    error: { main: "#dc2626" },
    background: { default: "#f4f6f8", paper: "#ffffff" },
    text: { primary: "#333333", secondary: "#6b7280" }
  },
  typography: {
    fontFamily: "'Segoe UI', Roboto, Arial, sans-serif",
    h5: { color: "#4a1a55", fontWeight: 700 }
  },
  shape: { borderRadius: 8 },
  components: {
    MuiButton: {
      defaultProps: { size: "large" },
      styleOverrides: {
        root: { textTransform: "none", borderRadius: 8, fontWeight: 600 },
        containedPrimary: {
          "&:hover": { backgroundColor: "#631f74" }
        }
      }
    },
    MuiPaper: {
      styleOverrides: {
        root: { borderRadius: 10, boxShadow: "0 1px 3px rgba(0,0,0,0.08)" }
      }
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 700, fontSize: 11 }
      }
    }
  }
});
