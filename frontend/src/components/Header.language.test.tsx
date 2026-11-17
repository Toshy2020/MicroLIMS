import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { resetUiStateCache } from "../hooks/useUiState";
import { i18n } from "../i18n";
import { LanguageProvider } from "../i18n/LanguageProvider";
import { Header } from "./Header";

const service = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../services/uiStateService", () => ({ uiStateService: service }));
vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

const html = () => document.documentElement;
const stored = new Map<string, string>();

beforeEach(async () => {
  localStorage.clear();
  localStorage.setItem("microlims_token", "header.payload.signature");
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
  stored.clear();
  service.getAll.mockImplementation(async () => Object.fromEntries(stored));
  service.set.mockImplementation(async (key: string, value: string) => {
    stored.set(key, value);
  });
  vi.spyOn(window.navigator, "languages", "get").mockReturnValue(["en"]);
  vi.spyOn(window.navigator, "language", "get").mockReturnValue("en");
  await i18n.changeLanguage("en");
  await i18n.loadNamespaces(["common"]);
});

afterEach(async () => {
  cleanup();
  vi.restoreAllMocks();
  vi.clearAllMocks();
  localStorage.clear();
  resetUiStateCache();
  await i18n.changeLanguage("en");
  html().removeAttribute("lang");
  html().removeAttribute("dir");
});

async function openAccountMenu() {
  const user = userEvent.setup();
  render(
    <PinnedLightTheme>
      <BenchModeProvider><ThemeModeProvider><AuthProvider>
        <LanguageProvider>
          <MemoryRouter>
            <Header labCodes={[]} />
          </MemoryRouter>
        </LanguageProvider>
      </AuthProvider></ThemeModeProvider></BenchModeProvider>
    </PinnedLightTheme>
  );
  await user.click(await screen.findByRole("button", { name: /^Account menu for/ }));
  return user;
}

describe("Header language choice", () => {
  it("offers each language in its own language with a lang attribute", async () => {
    await openAccountMenu();
    const english = await screen.findByRole("menuitemradio", { name: "English" });
    const arabic = screen.getByRole("menuitemradio", { name: "العربية" });
    expect(english.getAttribute("lang")).toBe("en");
    expect(arabic.getAttribute("lang")).toBe("ar");
    expect(english.getAttribute("aria-checked")).toBe("true");
    expect(arabic.getAttribute("aria-checked")).toBe("false");
  });

  it("switches to Arabic at once and saves the choice, then back to English", async () => {
    const user = await openAccountMenu();
    await user.click(await screen.findByRole("menuitemradio", { name: "العربية" }));

    await waitFor(() => expect(html().getAttribute("dir")).toBe("rtl"));
    expect(html().getAttribute("lang")).toBe("ar");
    expect(service.set).toHaveBeenCalledWith("language", "ar");
    expect(stored.get("language")).toBe("ar");

    await user.click(await screen.findByRole("button", { name: /^(Account menu for|قائمة الحساب)/ }));
    await user.click(await screen.findByRole("menuitemradio", { name: "English" }));

    await waitFor(() => expect(html().getAttribute("dir")).toBe("ltr"));
    expect(html().getAttribute("lang")).toBe("en");
    expect(stored.get("language")).toBe("en");
  });
});
