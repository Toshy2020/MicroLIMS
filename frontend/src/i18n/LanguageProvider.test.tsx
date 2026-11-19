import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Box, useTheme } from "@mui/material";
import { useTranslation } from "react-i18next";
import { AuthProvider } from "../contexts/AuthContext";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { resetUiStateCache } from "../hooks/useUiState";
import { i18n } from "./index";
import { LanguageProvider, useLanguage } from "./LanguageProvider";

const service = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../services/uiStateService", () => ({ uiStateService: service }));

function Probe() {
  const { t } = useTranslation("common");
  const theme = useTheme();
  const { language, setLanguage } = useLanguage();
  return (
    <div>
      <output aria-label="language">{language}</output>
      <output aria-label="theme direction">{theme.direction}</output>
      <p aria-label="save label">{t("actions.save")}</p>
      <p aria-label="english only">{t("test.onlyInEnglish")}</p>
      <Box aria-label="padded" sx={{ marginLeft: "32px" }} />
      <button type="button" onClick={() => void setLanguage("ar")}>choose arabic</button>
      <button type="button" onClick={() => void setLanguage("en")}>choose english</button>
    </div>
  );
}

function renderApp() {
  return render(
    <BenchModeProvider>
      <ThemeModeProvider>
        <AuthProvider>
          <LanguageProvider>
            <Probe />
          </LanguageProvider>
        </AuthProvider>
      </ThemeModeProvider>
    </BenchModeProvider>
  );
}

function signIn() {
  localStorage.setItem("microlims_token", "header.payload.signature");
  localStorage.setItem("microlims_user_id", "7");
}

function browserLanguage(languages: string[]) {
  vi.spyOn(window.navigator, "languages", "get").mockReturnValue(languages);
  vi.spyOn(window.navigator, "language", "get").mockReturnValue(languages[0]);
}

const html = () => document.documentElement;

beforeEach(async () => {
  localStorage.clear();
  resetUiStateCache();
  service.getAll.mockResolvedValue({});
  service.set.mockResolvedValue(undefined);
  i18n.addResource("en", "common", "test.onlyInEnglish", "Only in English");
  await i18n.changeLanguage("en");
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.clearAllMocks();
  localStorage.clear();
  resetUiStateCache();
  html().removeAttribute("lang");
  html().removeAttribute("dir");
});

describe("LanguageProvider", () => {
  it("follows the browser language before sign-in and never asks the server", async () => {
    browserLanguage(["ar-EG", "en"]);
    renderApp();

    await waitFor(() => expect(screen.getByLabelText("save label").textContent).toBe("حفظ"));
    expect(html().getAttribute("lang")).toBe("ar");
    expect(html().getAttribute("dir")).toBe("rtl");
    expect(screen.getByLabelText("theme direction").textContent).toBe("rtl");
    expect(service.getAll).not.toHaveBeenCalled();
  });

  it("uses English for any other browser language and leaves English rendering as it was", () => {
    browserLanguage(["fr-FR"]);
    renderApp();

    expect(screen.getByLabelText("save label").textContent).toBe("Save");
    expect(html().getAttribute("lang")).toBe("en");
    expect(html().getAttribute("dir")).toBe("ltr");
    expect(screen.getByLabelText("theme direction").textContent).toBe("ltr");
  });

  it("takes the signed-in user's stored preference over the browser language", async () => {
    browserLanguage(["en-GB"]);
    signIn();
    service.getAll.mockResolvedValue({ language: "ar" });
    renderApp();

    await waitFor(() => expect(html().getAttribute("dir")).toBe("rtl"));
    expect(html().getAttribute("lang")).toBe("ar");
    expect(screen.getByLabelText("theme direction").textContent).toBe("rtl");
    await waitFor(() => expect(screen.getByLabelText("save label").textContent).toBe("حفظ"));
  });

  it("falls back to English for an unknown stored language", async () => {
    browserLanguage(["ar"]);
    signIn();
    service.getAll.mockResolvedValue({ language: "fr" });
    renderApp();

    await waitFor(() => expect(service.getAll).toHaveBeenCalled());
    await waitFor(() => expect(html().getAttribute("lang")).toBe("en"));
    expect(html().getAttribute("dir")).toBe("ltr");
  });

  it("switches language and direction on a click without reloading, and saves the choice", async () => {
    browserLanguage(["en"]);
    signIn();
    const user = userEvent.setup();
    renderApp();
    await waitFor(() => expect(service.getAll).toHaveBeenCalled());

    const marker = screen.getByLabelText("language");
    await user.click(screen.getByRole("button", { name: "choose arabic" }));

    await waitFor(() => expect(screen.getByLabelText("save label").textContent).toBe("حفظ"));
    expect(html().getAttribute("dir")).toBe("rtl");
    expect(screen.getByLabelText("theme direction").textContent).toBe("rtl");
    expect(screen.getByLabelText("language")).toBe(marker); // same element: nothing remounted
    expect(service.set).toHaveBeenCalledWith("language", "ar");

    await user.click(screen.getByRole("button", { name: "choose english" }));
    await waitFor(() => expect(screen.getByLabelText("save label").textContent).toBe("Save"));
    expect(html().getAttribute("dir")).toBe("ltr");
    expect(service.set).toHaveBeenLastCalledWith("language", "en");
  });

  it("switches before sign-in too, without calling the server", async () => {
    browserLanguage(["en"]);
    const user = userEvent.setup();
    renderApp();

    await user.click(screen.getByRole("button", { name: "choose arabic" }));
    await waitFor(() => expect(html().getAttribute("dir")).toBe("rtl"));
    expect(service.set).not.toHaveBeenCalled();
  });

  it("shows the English text for a key missing in Arabic", async () => {
    browserLanguage(["ar"]);
    renderApp();

    await waitFor(() => expect(screen.getByLabelText("save label").textContent).toBe("حفظ"));
    expect(screen.getByLabelText("english only").textContent).toBe("Only in English");
  });

  it("mirrors physical CSS in Arabic through the RTL style cache", async () => {
    browserLanguage(["en"]);
    const user = userEvent.setup();
    renderApp();
    expect(getComputedStyle(screen.getByLabelText("padded")).marginLeft).toBe("32px");

    await user.click(screen.getByRole("button", { name: "choose arabic" }));
    await waitFor(() => expect(getComputedStyle(screen.getByLabelText("padded")).marginRight).toBe("32px"));
    expect(getComputedStyle(screen.getByLabelText("padded")).marginLeft).not.toBe("32px");
  });
});
