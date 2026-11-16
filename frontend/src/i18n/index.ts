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
