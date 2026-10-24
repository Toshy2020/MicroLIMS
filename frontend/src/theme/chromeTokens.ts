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
  // sign-in page's brand panel) rather than as the whole application chrome.
  // brandBandBg layers several shades (a lighter corner, a deeper corner, a
  // diagonal ramp) so the panel has depth rather than one flat colour.
  brandBandBg: string;
  brandBandAccent: string;
  brandBandMuted: string;
  // Line drawings of laboratory equipment on the band - a few shades off the
  // background, texture rather than something to read.
  brandBandArt: string;
  brandBandChipBg: string;
  brandBandChipBorder: string;
  brandBandGlow: string;
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
    brandBandBg: [
      "radial-gradient(120% 80% at 0% 0%, #a64bb5 0%, rgba(166,75,181,0) 55%)",
      "radial-gradient(90% 70% at 100% 100%, #4a1a55 0%, rgba(74,26,85,0) 60%)",
      "linear-gradient(155deg, #8a35a0 0%, #7b2d8e 40%, #62206f 75%, #4f1a5c 100%)"
    ].join(", "),
    brandBandAccent: "rgba(255,255,255,0.85)",
    brandBandMuted: "rgba(255,255,255,0.82)",
    brandBandArt: "rgba(255,255,255,0.13)",
    brandBandChipBg: "rgba(255,255,255,0.14)",
    brandBandChipBorder: "rgba(255,255,255,0.28)",
    brandBandGlow: "rgba(233,213,255,0.22)",
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
    brandBandBg: [
      "radial-gradient(120% 80% at 0% 0%, #3b1f52 0%, rgba(59,31,82,0) 55%)",
      "radial-gradient(90% 70% at 100% 100%, #0f0816 0%, rgba(15,8,22,0) 60%)",
      "linear-gradient(155deg, #2c1840 0%, #23132f 45%, #1a0f24 80%, #140b1c 100%)"
    ].join(", "),
    brandBandAccent: "#c084c8",
    brandBandMuted: "#B9A8CC",
    brandBandArt: "rgba(192,132,200,0.16)",
    brandBandChipBg: "rgba(192,132,200,0.12)",
    brandBandChipBorder: "rgba(192,132,200,0.35)",
    brandBandGlow: "rgba(192,132,200,0.14)",
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
