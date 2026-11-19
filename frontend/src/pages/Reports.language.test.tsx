import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../contexts/AuthContext";
import { resetUiStateCache } from "../hooks/useUiState";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { ReportsPage } from "./Reports";

// Network boundary only: every request stays pending, so the real tabs render their loading state.
vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn(() => new Promise(() => {})) }
}));
vi.mock("../services/uiStateService", () => ({
  uiStateService: { getAll: vi.fn(async () => ({})), set: vi.fn(async () => undefined), remove: vi.fn() }
}));

beforeEach(() => {
  localStorage.clear();
  resetUiStateCache();
});

afterEach(async () => {
  cleanup();
  await resetLanguage();
});

describe("ReportsPage in Arabic", () => {
  it("shows the page title, subtitle and tab names in Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter initialEntries={["/reports"]}>
          <ReportsPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "reports"]
    );

    expect(screen.getByRole("heading", { name: "التقارير" })).toBeTruthy();
    expect(screen.getByText("ابحث في نتائج المختبر وتتبّع اتجاهاتها وصدّرها.")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "نظرة عامة" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "بحث السجلات" })).toBeTruthy();
    expect(screen.getByRole("tablist", { name: "أقسام التقارير" })).toBeTruthy();
  });
});
