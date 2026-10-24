export interface ChromeTokens {
  topbarBg: string;
  topbarText: string;
  topbarMutedText: string;
  topbarBorder: string;
  // Fill for controls sitting on the topbar (search box, theme switch track).
  topbarControlBg: string;
  topbarControlHoverBg: string;
  // The theme switch's track - needs more weight than a fill to read as a
  // control on a neutral bar.
  topbarSwitchTrack: string;
  brandAccent: string;
  // The purple brand band, now used only where the brand is the point (the
  // sign-in card header) rather than as the whole application chrome.
  brandBandBg: string;
  brandBandAccent: string;
  sidebarBg: string;
  sidebarText: string;
  sidebarActiveBg: string;
  sidebarActiveText: string;
  sidebarActiveBorder: string;
  tableHeaderBg: string;
  tableHeaderText: string;
}

// Application chrome is neutral: a light topbar with a hairline border and a
// dark slate sidebar. Saturated purple across the topbar, the whole sidebar,
// buttons, links and selection at once left nothing for an alert or a
// primary action to stand out against, so the brand hue is now kept for the
// things that are interactive or selected - primary buttons, the active nav
// marker, the wordmark accent - and for the sign-in brand band.
//
// The sidebar stays dark in both modes so every white-on-dark nav style in
// Sidebar.tsx keeps its contrast. Measured: sidebarText on sidebarBg 9.85:1
// light / 7.34:1 dark; topbarMutedText on topbarBg 6.00:1 / 6.48:1, and on
// topbarControlBg (search box) 5.48:1 in both; brandAccent wordmark 8.05:1 /
// 5.81:1; the active-nav border clears 3:1 against the sidebar (5.14 / 6.59).
export const chromeTokensByMode: Record<"light" | "dark", ChromeTokens> = {
  light: {
    topbarBg: "#FFFFFF",
    topbarText: "#1A2027",
    topbarMutedText: "#5A6472",
    topbarBorder: "#E2E8F0",
    topbarControlBg: "#F1F5F9",
    topbarControlHoverBg: "#E2E8F0",
    topbarSwitchTrack: "#94A3B8",
    brandAccent: "#7b2d8e",
    brandBandBg: "linear-gradient(90deg, #7b2d8e, #9b3fa8)",
    brandBandAccent: "rgba(255,255,255,0.85)",
    sidebarBg: "#1E293B",
    sidebarText: "#CBD5E1",
    sidebarActiveBg: "rgba(255,255,255,0.08)",
    sidebarActiveText: "#ffffff",
    sidebarActiveBorder: "#c084c8",
    // Light mode is unchanged from what the tables already did
    // (background.default over a white card, secondary ink).
    tableHeaderBg: "#F4F6F8",
    tableHeaderText: "#5A6472"
  },
  dark: {
    topbarBg: "#1A1F27",
    topbarText: "#E7EBF0",
    topbarMutedText: "#99A3B0",
    topbarBorder: "#2C333E",
    topbarControlBg: "rgba(255,255,255,0.06)",
    topbarControlHoverBg: "rgba(255,255,255,0.1)",
    topbarSwitchTrack: "#4B5563",
    brandAccent: "#c084c8",
    brandBandBg: "linear-gradient(90deg, #1E1229, #2A1740)",
    brandBandAccent: "#c084c8",
    sidebarBg: "#0F1217",
    sidebarText: "#99A3B0",
    sidebarActiveBg: "#232A35",
    sidebarActiveText: "#ffffff",
    sidebarActiveBorder: "#c084c8",
    // Dark mode is the correction. Table heads used to take
    // background.default (#12161C), which is DARKER than the card they sit
    // on (#1A1F27) - so the header sank into the surface and separated from
    // it by only 1.1:1. This band sits above the card instead, at 1.65:1,
    // which is what makes it read as a header at a glance.
    //
    // The text has to travel with it: the secondary ink most tables used for
    // header cells lands at 3.93:1 on this band, under the 4.5:1 it needs, so
    // the token pairs the band with primary ink at 8.39:1.
    tableHeaderBg: "#424242",
    tableHeaderText: "#E7EBF0"
  }
};
