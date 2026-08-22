export interface ChromeTokens {
  topbarBg: string;
  brandAccent: string;
  sidebarBg: string;
  sidebarText: string;
  sidebarActiveBg: string;
  sidebarActiveText: string;
  sidebarActiveBorder: string;
}

// Topbar/sidebar brand chrome - previously mode-invariant (same bright
// purple in both themes), now darkens in dark mode instead of sitting as
// a bright band above/beside a dark page. Light values are the untouched
// originals (mirrors brandColors.topbarGradient/subnavBg/subnavText in
// ../index.ts - kept here as plain literals rather than importing that
// module, which would create a circular import since index.ts imports
// this file's consumer, palette.ts).
//
// Dark values aren't a straight inversion: topbarBg/sidebarBg/sidebarActiveBg
// are lifted from the approved darkmode_mockup_v2.html (already hand-picked
// deep-purple/near-black tones for this brand), while brandAccent/
// sidebarActiveText/sidebarActiveBorder instead reuse the app's own
// established dark-theme primary ramp (primary.main/light/dark from
// darkThemeOptions) so the wordmark and active-nav accent tie directly to
// colors already proven legible elsewhere, rather than introducing new
// one-off hex values.
export const chromeTokensByMode: Record<"light" | "dark", ChromeTokens> = {
  light: {
    topbarBg: "linear-gradient(90deg, #7b2d8e, #9b3fa8)",
    brandAccent: "rgba(255,255,255,0.85)",
    sidebarBg: "#6a1f78",
    sidebarText: "#f1d9f5",
    sidebarActiveBg: "rgba(255,255,255,0.15)",
    sidebarActiveText: "#ffffff",
    sidebarActiveBorder: "#ffffff"
  },
  dark: {
    topbarBg: "linear-gradient(90deg, #1E1229, #2A1740)",
    brandAccent: "#c084c8",
    sidebarBg: "#181021",
    sidebarText: "#9E8FB5",
    sidebarActiveBg: "#2C1E42",
    sidebarActiveText: "#e9d5ff",
    sidebarActiveBorder: "#9b3fa8"
  }
};
