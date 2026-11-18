import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { ConfirmPreparationForm } from "./ConfirmPreparationForm";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";
import { SamplePreparationService } from "./services/SamplePreparationService";

vi.mock("./services/SamplePreparationService", () => ({
  SamplePreparationService: {
    getLotOptions: vi.fn(),
    confirm: vi.fn()
  }
}));

const config = {
  id: 1,
  itemId: 1,
  amount: 10,
  technique: "PourPlate",
  filtrationVolume: null,
  washingVolume: null,
  diluent: "Peptone water",
  neutralizer: "Tween 80",
  approvalStatus: "Approved",
  createdByUserId: 9,
  createdByName: "Config Author",
  createdAt: "2026-10-01T00:00:00Z",
  approvedByUserId: 9,
  approvedByName: "Config Author",
  approvedAt: "2026-10-02T00:00:00Z"
} as ItemPreparationConfiguration;

describe("ConfirmPreparationForm in Arabic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(SamplePreparationService.getLotOptions).mockResolvedValue({
      configurationId: 1,
      needsMaterialMapping: false,
      diluent: { source: "ContainedInMedium", name: "", lotRequired: false, options: [] },
      neutralizer: { source: "ContainedInMedium", name: "", lotRequired: false, options: [] }
    });
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("renders Arabic labels and steps right to left", async () => {
    await renderInArabic(
      <AuthProvider>
        <ConfirmPreparationForm
          sample={{ sampleId: 42, assignedAnalystIds: [] }}
          config={config}
          onSaved={vi.fn()}
        />
      </AuthProvider>,
      ["common", "testPreparation"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByText("خطوات التحضير المُعدّة")).toBeTruthy();
    expect(screen.getByRole("button", { name: "تأكيد وبدء الاختبار" })).toBeTruthy();
  });
});
