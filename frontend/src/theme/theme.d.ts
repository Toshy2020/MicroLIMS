import "@mui/material/styles";
import { StatusTone, StatusToneTokens, CountdownTokens } from "./statusTokens";

export interface CustomThemeTokens {
  status: Record<StatusTone, StatusToneTokens>;
  countdown: CountdownTokens;
  chartPalette: string[];
}

declare module "@mui/material/styles" {
  interface Theme {
    custom: CustomThemeTokens;
  }
  interface ThemeOptions {
    custom?: CustomThemeTokens;
  }
}
