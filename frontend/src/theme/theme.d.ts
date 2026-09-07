import "@mui/material/styles";
import { StatusTone, StatusToneTokens, CountdownTokens } from "./statusTokens";
import { ChromeTokens } from "./chromeTokens";

export interface CustomThemeTokens {
  status: Record<StatusTone, StatusToneTokens>;
  countdown: CountdownTokens;
  // Identity (categorical) vs order (sequential) - two different jobs, so
  // two different scales. Never use chartPalette for ordered data or
  // chartSequential for unrelated categories.
  chartPalette: string[];
  chartSequential: string[];
  chrome: ChromeTokens;
}

declare module "@mui/material/styles" {
  interface Theme {
    custom: CustomThemeTokens;
  }
  interface ThemeOptions {
    custom?: CustomThemeTokens;
  }
}
