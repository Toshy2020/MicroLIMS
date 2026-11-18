import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { OosTrackingPage } from "./OosTrackingPage";
import { OosTrackingService } from "./services/OosTrackingService";

vi.mock("./services/OosTrackingService", () => ({
  OosTrackingService: {
    getOosGroups: vi.fn(),
    getDocuments: vi.fn(),
    uploadDocument: vi.fn(),
    downloadDocument: vi.fn(),
    supersedeDocument: vi.fn()
  }
}));

describe("OosTrackingPage in Arabic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(OosTrackingService.getOosGroups).mockResolvedValue([
      {
        oosGroupCode: "OOS-2026-014",
        originSampleId: 1,
        originReferenceNumber: "S-2026-0101",
        originSampleStatus: "Failed",
        displayName: "Paracetamol tablets",
        category: "Physicochemical",
        batchNumber: "B-104",
        openedAt: "2026-11-02T08:30:00Z",
        hasInvestigationDocument: true,
        retestSamples: [
          {
            newSampleId: 2,
            newReferenceNumber: "S-2026-0102",
            newSampleStatus: "Pending",
            originSampleId: 1,
            originReferenceNumber: "S-2026-0101",
            originSampleStatus: "Failed",
            displayName: "Paracetamol tablets",
            category: "Physicochemical",
            batchNumber: "B-104",
            retestType: "RetestRetainedSample",
            testCodes: ["ASSAY"],
            analystNames: [],
            openedAt: "2026-11-03T09:00:00Z"
          }
        ]
      }
    ]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("renders Arabic headings and keeps OOS group codes left to right", async () => {
    await renderInArabic(
      <MemoryRouter>
        <OosTrackingPage />
      </MemoryRouter>,
      ["common", "oosTracking"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "تتبع خارج المواصفات" })).toBeTruthy();
    expect(screen.getByText("مجموعات تحقيق OOS")).toBeTruthy();
    expect(screen.getByText("تقرير تحقيق المختبر مرفق")).toBeTruthy();
    expect(screen.getByRole("button", { name: "تصغير مجموعة OOS" })).toBeTruthy();
    expect(screen.getByText("OOS-2026-014")).toBeTruthy();
  });
});
