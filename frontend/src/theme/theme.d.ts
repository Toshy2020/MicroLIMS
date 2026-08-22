import "@mui/material/styles";
import { StatusTone, StatusToneTokens, CountdownTokens } from "./statusTokens";
import { ChromeTokens } from "./chromeTokens";

export interface CustomThemeTokens {
  status: Record<StatusTone, StatusToneTokens>;
  countdown: CountdownTokens;
  chartPalette: string[];
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
