import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { useTheme } from "@mui/material";
import { useTranslation } from "react-i18next";
import { AuthProvider } from "../contexts/AuthContext";
import { AppRoutes } from "../routes/AppRoutes";
import { formatLabDate } from "../utils/formatDate";
import { EnglishOnly } from "./EnglishOnly";
import { renderInArabic, resetLanguage } from "./testUtils";

const summaryService = vi.hoisted(() => ({ getSummary: vi.fn() }));
vi.mock("../modules/testingWorkspace/services/SampleSummaryService", () => ({ SampleSummaryService: summaryService }));

// A screen that follows the interface language, as every converted screen does.
function DateProbe({ label }: { label: string }) {
  useTranslation();
  return <p aria-label={label}>{formatLabDate("2026-12-05T14:30:00Z")}</p>;
}

function Probe() {
  const { t } = useTranslation();
  const theme = useTheme();
  return (
    <div>
      <p aria-label="cancel">{t("actions.cancel")}</p>
      <p aria-label="date">{formatLabDate("2026-12-05T14:30:00Z")}</p>
      <p aria-label="theme direction">{theme.direction}</p>
    </div>
  );
}

afterEach(async () => {
  localStorage.clear();
  vi.clearAllMocks();
  await resetLanguage();
});

describe("printable pages", () => {
  it("Print_pages_render_english_for_arabic_user", async () => {
    await renderInArabic(
      <EnglishOnly>
        <Probe />
      </EnglishOnly>
    );

    // The interface itself is Arabic and right to left...
    expect(document.documentElement.dir).toBe("rtl");

    // ...the record is English, left to right, with an English date.
    expect(screen.getByLabelText("cancel").textContent).toBe("Cancel");
    expect(screen.getByLabelText("date").textContent).toBe("05 Dec 2026");
    expect(screen.getByLabelText("theme direction").textContent).toBe("ltr");
    const wrapper = screen.getByLabelText("cancel").parentElement?.parentElement as HTMLElement;
    expect(wrapper.getAttribute("lang")).toBe("en");
    expect(wrapper.getAttribute("dir")).toBe("ltr");
  });

  it("gives the interface language back when the printable page is left", async () => {
    const { rerender } = await renderInArabic(
      <EnglishOnly>
        <Probe />
      </EnglishOnly>
    );
    expect(screen.getByLabelText("date").textContent).toBe("05 Dec 2026");
    // The page that replaces it already rendered once while English was forced; it draws again.
    rerender(<DateProbe label="after" />);
    await waitFor(() => expect(screen.getByLabelText("after").textContent).toContain("ديسمبر"));
  });

  it("wraps every printable route of the app in English-only", async () => {
    summaryService.getSummary.mockReturnValue(new Promise(() => {}));
    const payload = btoa(JSON.stringify({ "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead", permission: "Samples.View" }));
    localStorage.setItem("microlims_token", `h.${payload}.s`);
    localStorage.setItem("microlims_username", "head");

    for (const path of ["/samples/5/coa", "/samples/5/report"]) {
      const { unmount } = await renderInArabic(
        <AuthProvider>
          <MemoryRouter initialEntries={[path]}>
            <AppRoutes />
          </MemoryRouter>
        </AuthProvider>
      );
      await waitFor(() => expect(document.querySelector('div[lang="en"][dir="ltr"]')).not.toBeNull());
      expect(document.documentElement.dir).toBe("rtl");
      unmount();
    }
  });
});

// Rendered once so a plain English render of the wrapper is also covered.
describe("EnglishOnly outside Arabic", () => {
  it("renders its children in English as usual", () => {
    render(
      <EnglishOnly>
        <Probe />
      </EnglishOnly>
    );
    expect(screen.getByLabelText("cancel").textContent).toBe("Cancel");
  });
});
