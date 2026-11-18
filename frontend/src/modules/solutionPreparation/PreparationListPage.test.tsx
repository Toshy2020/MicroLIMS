import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { PreparationListPage } from "./PreparationListPage";
import { SolutionPreparationService } from "./services/SolutionPreparationService";

vi.mock("./services/SolutionPreparationService", () => ({
  SolutionPreparationService: {
    getAll: vi.fn()
  }
}));

describe("PreparationListPage in Arabic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(SolutionPreparationService.getAll).mockResolvedValue([
      {
        id: 1,
        code: "MP-2026-0001",
        type: "MobilePhase",
        solutionMasterName: "Phosphate Buffer pH 6.8",
        hplcMethodAbbreviation: "AMX-01",
        effectiveStatus: "Prepared",
        preparedAt: "2026-11-18T08:00:00Z",
        expiresAt: "2026-11-25T08:00:00Z",
        preparedByUserName: "Analyst",
        approvalStatus: null
      }
    ]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("renders Arabic headings and keeps preparation codes left to right", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <PreparationListPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "solutionPreparation"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "تحضير المحاليل" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "بدء التحضير" })).toBeTruthy();

    const code = await screen.findByText("MP-2026-0001");
    expect(code.closest("bdi")?.getAttribute("dir")).toBe("ltr");
  });
});
