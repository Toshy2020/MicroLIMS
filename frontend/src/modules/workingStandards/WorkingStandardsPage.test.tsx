import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { PERMISSIONS } from "../../routes/routes";
import { WorkingStandardsPage } from "./WorkingStandardsPage";
import { WorkingStandardService } from "./services/WorkingStandardService";

vi.mock("./services/WorkingStandardService", () => ({
  WorkingStandardService: {
    getLots: vi.fn(),
    getQualifications: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

describe("WorkingStandardsPage in Arabic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.WORKING_STANDARDS_QUALIFY]);

    vi.mocked(WorkingStandardService.getLots).mockResolvedValue([
      {
        materialId: 1,
        code: "WS-2026-001",
        materialName: "Paracetamol RS",
        batchNumber: "B-104",
        masterEntryCode: "RM-PCM",
        potencyPercent: 99.8,
        moisturePercent: 0.25,
        quantityRemaining: 25,
        expiryDate: "2027-05-01T00:00:00Z",
        status: "Valid",
        openQualificationId: null
      }
    ]);
    vi.mocked(WorkingStandardService.getQualifications).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("renders Arabic headings and keeps lot codes left to right", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <WorkingStandardsPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "workingStandards"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "المواد القياسية العاملة" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "تأهيل جديد" })).toBeTruthy();

    const lotCode = await screen.findByText("WS-2026-001");
    expect(lotCode.closest("bdi")?.getAttribute("dir")).toBe("ltr");
  });
});
