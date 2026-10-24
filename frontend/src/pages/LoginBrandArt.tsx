import { Box, SxProps, Theme } from "@mui/material";

// Line drawings along the bottom of the sign-in brand panel, one set per
// laboratory the system serves: physicochemical (chromatogram, titration,
// HPLC) and microbiology (culture plates, microscope). Drawn in currentColor
// so the panel sets their tint; decorative only, hidden from screen readers.
export function LoginBrandArt({ sx }: { sx?: SxProps<Theme> }) {
  return (
    <Box
      component="svg"
      viewBox="0 0 546 290"
      preserveAspectRatio="xMidYMax meet"
      aria-hidden="true"
      focusable="false"
      sx={sx}
    >
      <g fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        {/* Chromatogram */}
        <g transform="translate(30 70) scale(0.85)">
          <path d="M0 0 h130" />
          <path d="M0 -2 h14 c4 0 5 -34 9 -34 s5 34 9 34 h18 c3 0 4 -20 7 -20 s4 20 7 20 h10 c4 0 6 -50 10 -50 s6 50 10 50 h22 c3 0 4 -12 7 -12 s4 12 7 12 h10" />
        </g>
        {/* Streaked culture plate */}
        <g transform="translate(96 152)">
          <circle r="46" />
          <circle r="40" />
          <path d="M-27 -15 q9 -10 18 0 t18 0 t18 0" />
          <path d="M-22 5 q7 -9 15 0 t15 0 t15 0" />
          <circle cx="-10" cy="21" r="2.5" />
          <circle cx="5" cy="24" r="2" />
          <circle cx="17" cy="18" r="2.5" />
        </g>
        {/* Titration: burette on a stand over a conical flask */}
        <g transform="translate(176 30) scale(0.95)">
          <path d="M0 0 v230" />
          <path d="M-24 230 h120" />
          <path d="M0 26 h46" />
          <circle cx="46" cy="26" r="5" />
          <path d="M52 -6 h12 v150 l-3 10 h-6 l-3 -10 z" />
          <path d="M52 20 h6 M52 40 h6 M52 60 h6 M52 80 h6 M52 100 h6 M52 120 h6" />
          <path d="M50 140 h16" />
          <path d="M58 166 v8" strokeDasharray="2 5" />
          <path d="M50 182 h16 M54 182 v14 l-22 30 a5 5 0 0 0 4 8 h44 a5 5 0 0 0 4 -8 l-22 -30 v-14" />
          <path d="M38 216 h40" />
        </g>
        {/* HPLC: solvent bottles on a stack of modules */}
        <g transform="translate(270 78) scale(0.85)">
          <path d="M8 34 v-18 a6 6 0 0 1 6 -6 h8 a6 6 0 0 1 6 6 v18" />
          <path d="M14 10 v-8 h8 v8" />
          <path d="M40 34 v-18 a6 6 0 0 1 6 -6 h8 a6 6 0 0 1 6 6 v18" />
          <path d="M46 10 v-8 h8 v8" />
          <path d="M18 2 q0 -10 30 -10 q40 0 52 30" />
          <rect x="0" y="34" width="120" height="34" rx="3" />
          <path d="M10 51 h40" />
          <circle cx="104" cy="51" r="5" />
          <rect x="0" y="72" width="120" height="34" rx="3" />
          <path d="M10 89 h24" />
          <rect x="74" y="80" width="36" height="18" rx="2" />
          <rect x="0" y="110" width="120" height="34" rx="3" />
          <path d="M10 127 h56" />
          <circle cx="96" cy="127" r="3" />
          <circle cx="108" cy="127" r="3" />
          <rect x="0" y="148" width="120" height="34" rx="3" />
          <path d="M14 165 h92" strokeDasharray="4 6" />
        </g>
        {/* Microscope */}
        <g transform="translate(386 90) scale(0.8)">
          <path d="M40 0 l18 10 l-34 62 l-18 -10 z" />
          <path d="M14 66 l-8 14 l18 10 l8 -14" />
          <path d="M58 10 c40 20 50 70 22 110" />
          <path d="M0 132 h110" />
          <path d="M30 132 v-22 h60 v22" />
          <path d="M-6 100 h70" />
          <circle cx="92" cy="58" r="10" />
        </g>
        {/* Culture plate with colonies, cropped by the corner */}
        <g transform="translate(528 302)">
          <circle r="110" />
          <circle r="98" />
          <circle cx="-34" cy="-26" r="8" />
          <circle cx="-10" cy="-50" r="5" />
          <circle cx="26" cy="-34" r="10" />
          <circle cx="47" cy="-4" r="6" />
          <circle cx="-50" cy="12" r="5" />
          <circle cx="64" cy="-46" r="4" />
        </g>
      </g>
    </Box>
  );
}
