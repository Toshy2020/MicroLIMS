import { ReactNode } from "react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "./index";

// Forces light mode regardless of the app-wide dark mode toggle - for
// documents/screens that may be printed, reviewed by auditors, or
// captured as evidence (COA/report PDFs, e-signature dialogs). An
// auditor's expectation of a "normal" printed appearance shouldn't
// depend on which UI theme a specific analyst happened to have enabled.
// A nested ThemeProvider (not a CSS override) is required so MUI
// components resolve their built-in styles against the actual light
// palette rather than inheriting the ambient dark one.
export function PinnedLightTheme({ children }: { children: ReactNode }) {
  return <ThemeProvider theme={lightTheme}>{children}</ThemeProvider>;
}
