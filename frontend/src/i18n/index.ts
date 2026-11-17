import i18next, { type BackendModule } from "i18next";
import { initReactI18next } from "react-i18next";
import enCommon from "../locales/en/common.json";

// Interface language support (F4). Texts live in namespace files,
// frontend/src/locales/{en,ar}/<namespace>.json. English "common" is bundled
// so English renders on the first paint exactly as before; every other file
// is loaded the first time a screen asks for its namespace.

export const SUPPORTED_LANGUAGES = ["en", "ar"] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export type Direction = "ltr" | "rtl";

export const DEFAULT_LANGUAGE: Language = "en";
export const DEFAULT_NAMESPACE = "common";

export function isLanguage(value: unknown): value is Language {
  return typeof value === "string" && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

export function directionOf(language: Language): Direction {
  return language === "ar" ? "rtl" : "ltr";
}

// The browser's first preferred language that we support; English otherwise.
export function browserLanguage(): Language {
  if (typeof navigator === "undefined") return DEFAULT_LANGUAGE;
  const preferred = navigator.languages?.length ? navigator.languages : [navigator.language];
  for (const tag of preferred) {
    const base = tag?.toLowerCase().split("-")[0];
    if (isLanguage(base)) return base;
  }
  return DEFAULT_LANGUAGE;
}

const loaders = import.meta.glob<{ default: Record<string, unknown> }>([
  "../locales/*/*.json",
  "!../locales/en/common.json" // bundled above; a second, dynamic import would not split
]);

// A namespace with no file in a language simply has no keys there, so every
// key falls back to English.
const lazyFiles: BackendModule = {
  type: "backend",
  init() {},
  read(language, namespace, callback) {
    if (language === "en" && namespace === DEFAULT_NAMESPACE) {
      callback(null, enCommon);
      return;
    }
    const load = loaders[`../locales/${language}/${namespace}.json`];
    if (!load) {
      callback(null, {});
      return;
    }
    load().then(
      (module) => callback(null, module.default),
      (error: unknown) => callback(error instanceof Error ? error : new Error(String(error)), null)
    );
  }
};

export const i18n = i18next.createInstance();

// Printed and previewed records are English whatever the user's interface language
// (the English statement is the recorded one). While one is on screen the
// language is forced here, so code that formats outside React (dates) follows it.
let forcedLanguage: Language | null = null;
let forcedCount = 0;

export function activeLanguage(): string {
  return forcedLanguage ?? i18n.language;
}

// Returns the function that ends the forcing; nested uses are counted. Ending it tells
// screens that follow the language to draw again, because the page that replaces a
// printable one has already rendered once while English was forced.
export function forceLanguage(language: Language): () => void {
  forcedLanguage = language;
  forcedCount += 1;
  let released = false;
  return () => {
    if (released) return;
    released = true;
    forcedCount -= 1;
    if (forcedCount <= 0) {
      forcedCount = 0;
      forcedLanguage = null;
      // After this commit has subscribed its screens (they subscribe in an effect).
      setTimeout(() => {
        if (forcedLanguage === null) i18n.emit("languageChanged", i18n.language);
      }, 0);
    }
  };
}

// Import the hook from here (not straight from react-i18next) so the setup above
// has run wherever a screen uses translation keys, tests included.
export { useTranslation } from "react-i18next";

export const i18nReady = i18n
  .use(lazyFiles)
  .use(initReactI18next)
  .init({
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: [...SUPPORTED_LANGUAGES],
    ns: [DEFAULT_NAMESPACE],
    defaultNS: DEFAULT_NAMESPACE,
    resources: { en: { common: enCommon } },
    partialBundledLanguages: true,
    initAsync: false,
    interpolation: { escapeValue: false }, // React escapes
    returnNull: false,
    react: { useSuspense: false }
  });
