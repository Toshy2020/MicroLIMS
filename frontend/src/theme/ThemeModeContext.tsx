import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import { ThemeProvider, CssBaseline } from "@mui/material";
import { getTheme } from "./index";

export type ThemeMode = "light" | "dark";

const STORAGE_KEY = "microlims-theme-mode";

interface ThemeModeContextValue {
  mode: ThemeMode;
  toggleMode: () => void;
}

const ThemeModeContext = createContext<ThemeModeContextValue | null>(null);

function readStoredMode(): ThemeMode | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === "light" || stored === "dark" ? stored : null;
  } catch {
    // localStorage can throw in privacy modes / disabled storage - fall
    // back to no stored preference rather than crash the app.
    return null;
  }
}

function initialMode(): ThemeMode {
  const stored = readStoredMode();
  if (stored) return stored;
  // No explicit user choice yet - honor OS preference as the first-load
  // default, but never overwrite localStorage with it so a later OS
  // preference change doesn't silently flip the app.
  if (typeof window !== "undefined" && window.matchMedia?.("(prefers-color-scheme: dark)").matches) {
    return "dark";
  }
  return "light";
}

// Wraps the app in the actual MUI ThemeProvider + CssBaseline, so callers
// only need to mount this once at the root instead of also wiring
// ThemeProvider themselves.
export function ThemeModeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(initialMode);

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // Ignore - persistence is a nice-to-have, not a hard requirement.
    }
  }, [mode]);

  const value = useMemo<ThemeModeContextValue>(
    () => ({ mode, toggleMode: () => setMode((m) => (m === "light" ? "dark" : "light")) }),
    [mode]
  );

  const theme = useMemo(() => getTheme(mode), [mode]);

  return (
    <ThemeModeContext.Provider value={value}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeModeContext.Provider>
  );
}

export function useThemeMode(): ThemeModeContextValue {
  const ctx = useContext(ThemeModeContext);
  if (!ctx) throw new Error("useThemeMode must be used within a ThemeModeProvider");
  return ctx;
}
