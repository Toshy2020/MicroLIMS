import { createContext, ReactNode, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState } from "react";
import { ThemeProvider, type Theme } from "@mui/material/styles";
import { CacheProvider } from "@emotion/react";
import createCache from "@emotion/cache";
import { prefixer } from "stylis";
import rtlPlugin from "stylis-plugin-rtl";
import { I18nextProvider } from "react-i18next";
import { useOptionalAuth } from "../contexts/AuthContext";
import { useUiState } from "../hooks/useUiState";
import { browserLanguage, DEFAULT_LANGUAGE, directionOf, i18n, isLanguage, type Direction, type Language } from "./index";

// Where the user's language is kept in the per-user interface state (C6).
export const LANGUAGE_STATE_KEY = "language";

// One style cache per direction. The right-to-left one rewrites physical CSS
// (margin-left, left, text-align...) so shared components mirror without edits.
const ltrCache = createCache({ key: "mui" });
const rtlCache = createCache({ key: "muirtl", stylisPlugins: [prefixer, rtlPlugin] });

interface LanguageContextValue {
  language: Language;
  direction: Direction;
  // Signed in: saved to the user's interface state. Before sign-in: this tab only.
  setLanguage: (language: Language) => Promise<void>;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

interface StoredLanguage {
  value: string | null;
  loaded: boolean;
  save: (language: Language) => Promise<void>;
}

// Mounted only while signed in, so nothing calls the server before sign-in.
function StoredLanguageSource({ onChange }: { onChange: (stored: StoredLanguage | null) => void }) {
  const { value, loaded, set } = useUiState(LANGUAGE_STATE_KEY);
  useEffect(() => {
    onChange({ value, loaded, save: (language) => set(language) });
  }, [value, loaded, set, onChange]);
  useEffect(() => () => onChange(null), [onChange]);
  return null;
}

function resolveLanguage(signedIn: boolean, stored: StoredLanguage | null, chosen: Language | null): Language {
  if (signedIn && stored?.loaded && stored.value !== null) {
    // A stored value we do not support (an older or newer build) is English.
    return isLanguage(stored.value) ? stored.value : DEFAULT_LANGUAGE;
  }
  return chosen ?? browserLanguage();
}

export function LanguageProvider({ children }: { children: ReactNode }) {
  const signedIn = Boolean(useOptionalAuth()?.token);
  const [stored, setStored] = useState<StoredLanguage | null>(null);
  const [chosen, setChosen] = useState<Language | null>(null);

  const language = resolveLanguage(signedIn, signedIn ? stored : null, chosen);
  const direction = directionOf(language);

  // The page's own language and direction, so screen readers pick the right
  // voice and the browser lays out bidirectional text correctly.
  useLayoutEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = direction;
  }, [language, direction]);

  useEffect(() => {
    void i18n.changeLanguage(language);
  }, [language]);

  const setLanguage = useCallback(
    async (next: Language) => {
      if (signedIn && stored) await stored.save(next);
      else setChosen(next);
    },
    [signedIn, stored]
  );

  const withDirection = useCallback((outer: Theme): Theme => ({ ...outer, direction }), [direction]);
  const value = useMemo<LanguageContextValue>(() => ({ language, direction, setLanguage }), [language, direction, setLanguage]);

  return (
    <LanguageContext.Provider value={value}>
      {signedIn && <StoredLanguageSource onChange={setStored} />}
      <I18nextProvider i18n={i18n}>
        <CacheProvider value={direction === "rtl" ? rtlCache : ltrCache}>
          <ThemeProvider theme={withDirection}>{children}</ThemeProvider>
        </CacheProvider>
      </I18nextProvider>
    </LanguageContext.Provider>
  );
}

export function useLanguage(): LanguageContextValue {
  const ctx = useContext(LanguageContext);
  if (!ctx) throw new Error("useLanguage must be used within a LanguageProvider");
  return ctx;
}
