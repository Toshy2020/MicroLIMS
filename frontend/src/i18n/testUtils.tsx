import { type ReactElement } from "react";
import { render, waitFor, type RenderResult } from "@testing-library/react";
import { expect, vi } from "vitest";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { i18n } from "./index";
import { LanguageProvider } from "./LanguageProvider";

// Test helpers for screens shown in Arabic. The default test language is English,
// so existing tests query by English text; Arabic tests call these.

// Makes the browser report Arabic so the real LanguageProvider picks it, and loads
// the Arabic files the screen asks for before it renders.
export async function renderInArabic(ui: ReactElement, namespaces: string[] = ["common"]): Promise<RenderResult> {
  vi.spyOn(window.navigator, "languages", "get").mockReturnValue(["ar"]);
  vi.spyOn(window.navigator, "language", "get").mockReturnValue("ar");
  await i18n.changeLanguage("ar");
  await i18n.loadNamespaces(namespaces);
  const result = render(
    <PinnedLightTheme>
      <LanguageProvider>{ui}</LanguageProvider>
    </PinnedLightTheme>
  );
  await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
  return result;
}

// Puts the page back to English so the next test starts clean.
export async function resetLanguage(): Promise<void> {
  vi.restoreAllMocks();
  await i18n.changeLanguage("en");
  document.documentElement.removeAttribute("lang");
  document.documentElement.removeAttribute("dir");
}
