import { useLayoutEffect, useState, type ReactNode } from "react";
import { ThemeProvider, type Theme } from "@mui/material/styles";
import { CacheProvider } from "@emotion/react";
import { I18nextProvider } from "react-i18next";
import { forceLanguage, i18n } from "./index";
import { ltrCache } from "./LanguageProvider";

// One English view of the same text store: it has its own current language, so
// English can be shown while the interface itself stays in Arabic.
const englishI18n = i18n.cloneInstance({ lng: "en", fallbackLng: "en" });

const asLtr = (outer: Theme): Theme => ({ ...outer, direction: "ltr" });

/**
 * @component EnglishScope
 * @purpose English, left-to-right view of a part of the page (texts, theme direction and style
 * direction) WITHOUT changing the language of the rest of the app. Used by in-page printed tables.
 * Dates inside it must be formatted with the `english` option of the date helpers, because the
 * date helpers follow the app-wide language.
 */
export function EnglishScope({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <I18nextProvider i18n={englishI18n}>
      <CacheProvider value={ltrCache}>
        <ThemeProvider theme={asLtr}>
          <div lang="en" dir="ltr" className={className}>
            {children}
          </div>
        </ThemeProvider>
      </CacheProvider>
    </I18nextProvider>
  );
}

/**
 * @component EnglishOnly
 * @purpose Printable records (reports, certificates of analysis, labels) are English and left-to-right
 * for every user: the printed record is what is audited, and it must not depend on who printed it.
 * Wraps a route so its texts, dates, theme direction and style direction are English/LTR
 * even when the user's interface language is Arabic.
 */
export function EnglishOnly({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);

  // Forced before the first paint of the children, undone when the route is left.
  useLayoutEffect(() => {
    const release = forceLanguage("en");
    setReady(true);
    return release;
  }, []);

  if (!ready) return null;
  return <EnglishScope>{children}</EnglishScope>;
}
