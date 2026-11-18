import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../../i18n/testUtils";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { getMySections, laboratorySectionService } from "../../../../services/laboratorySectionService";
import { RegisterEquipmentDialog } from "./RegisterEquipmentDialog";

vi.mock("../../../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: vi.fn(), getMySections: vi.fn() },
  getMySections: vi.fn()
}));

describe("RegisterEquipmentDialog in Arabic", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([]);
    vi.mocked(getMySections).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows the dialog title, section headings, and field labels in Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <RegisterEquipmentDialog
          open
          editingItem={null}
          onClose={() => {}}
          onSuccess={() => {}}
        />
      </AuthProvider>,
      ["common", "inventory"]
    );
    expect(screen.getByRole("heading", { name: "تسجيل معدات" })).toBeTruthy();
    expect(screen.getByText("1. معلومات الجهاز")).toBeTruthy();
    expect(screen.getByText("2. المعايرة والحالة التشغيلية")).toBeTruthy();
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();
  });
});
