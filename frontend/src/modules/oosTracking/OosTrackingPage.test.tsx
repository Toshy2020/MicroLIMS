import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { OosTrackingPage } from "./OosTrackingPage";
import { OosTrackingService } from "./services/OosTrackingService";
import {
  oosInvestigationService,
  type OosInvestigationListItemDto
} from "../../services/oosInvestigationService";
import { laboratorySectionService } from "../../services/laboratorySectionService";

vi.mock("./services/OosTrackingService", () => ({
  OosTrackingService: {
    getOosGroups: vi.fn(),
    getDocuments: vi.fn(),
    uploadDocument: vi.fn(),
    downloadDocument: vi.fn(),
    supersedeDocument: vi.fn(),
    voidDocument: vi.fn()
  }
}));

vi.mock("../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    list: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn()
  }
}));

const sampleItem = (
  overrides: Partial<OosInvestigationListItemDto> = {}
): OosInvestigationListItemDto => ({
  id: 101,
  number: "OOS1126001",
  kind: "Oos",
  status: "PhaseOne",
  sampleId: 10,
  sampleReferenceNumber: "S-2026-0010",
  sampleDisplayName: "Amoxicillin 500mg",
  batchNumber: "B-001",
  sectionId: 1,
  sectionName: "Microbiology",
  openedAtUtc: "2026-11-20T08:00:00Z",
  openedByName: "Rita Reviewer",
  phaseOneDueDate: "2026-11-23",
  dueDate: "2026-12-01",
  isOverdue: false,
  conclusion: null,
  concludedAtUtc: null,
  rootCauseCategoryName: null,
  testCodes: ["TAMC"],
  version: 1,
  ...overrides
});

function setAuth(permissions: string[], role = "SectionHead") {
  localStorage.setItem("microlims_username", "user1");
  localStorage.setItem("microlims_role", role);
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", "1");
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
}

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <OosTrackingPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("OosTrackingPage register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setAuth(["Oos.View", "Oos.Manage"]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      {
        sectionId: 1,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
    ]);
    vi.mocked(oosInvestigationService.list).mockResolvedValue({
      items: [sampleItem()],
      totalCount: 1,
      page: 1,
      pageSize: 25
    });
    vi.mocked(OosTrackingService.getOosGroups).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("links to the trends report", async () => {
    renderPage();

    expect(await screen.findByText("OOS1126001")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Trends" }).getAttribute("href")).toBe("/oos-tracking/trends");
  });

  it("renders register tabs and queries the selected tab and section filter", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText("OOS1126001")).toBeTruthy();
    expect(oosInvestigationService.list).toHaveBeenCalledWith(
      expect.objectContaining({ kind: "Oos", openOnly: true }),
      expect.any(AbortSignal)
    );

    await user.click(screen.getByRole("tab", { name: "Overdue" }));
    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({ overdue: true }),
        expect.any(AbortSignal)
      );
    });

    await user.click(screen.getByRole("tab", { name: "Concluded" }));
    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({ status: "Concluded" }),
        expect.any(AbortSignal)
      );
    });

    await user.click(screen.getByRole("tab", { name: "Excursions" }));
    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({ kind: "Excursion" }),
        expect.any(AbortSignal)
      );
    });

    await user.click(screen.getByLabelText("Section"));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));
    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({ kind: "Excursion", sectionId: 1 }),
        expect.any(AbortSignal)
      );
    });
  });

  it("renders Legacy tab in Arabic and keeps OOS group codes left to right", async () => {
    const user = userEvent.setup();
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
        retestSamples: []
      }
    ]);

    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <OosTrackingPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "oosTracking"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "تتبع خارج المواصفات" })).toBeTruthy();

    await user.click(screen.getByRole("tab", { name: "القديمة" }));
    expect(await screen.findByText("مجموعات تحقيق OOS")).toBeTruthy();
    expect(screen.getByText("تقرير تحقيق المختبر مرفق")).toBeTruthy();
    expect(screen.getByText("OOS-2026-014")).toBeTruthy();
  });

  it("Open tab uses openOnly and shows the server totalCount", async () => {
    vi.mocked(oosInvestigationService.list).mockResolvedValue({
      items: [sampleItem()],
      totalCount: 42,
      page: 1,
      pageSize: 25
    });

    renderPage();

    expect(await screen.findByText("OOS1126001")).toBeTruthy();
    expect(oosInvestigationService.list).toHaveBeenCalledWith(
      expect.objectContaining({ kind: "Oos", openOnly: true }),
      expect.any(AbortSignal)
    );
    expect(screen.getByText(/of 42/i)).toBeTruthy();
  });

  it("applies status, from/to date filters and clears all filters", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText("OOS1126001")).toBeTruthy();

    await user.click(screen.getByLabelText("Status"));
    await user.click(await screen.findByRole("option", { name: "Phase II" }));
    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({ kind: "Oos", openOnly: true, status: "PhaseTwo" }),
        expect.any(AbortSignal)
      );
    });

    const fromInput = screen.getByLabelText("Opened from");
    const toInput = screen.getByLabelText("Opened to");
    await user.type(fromInput, "2026-11-01");
    await user.type(toInput, "2026-11-25");

    await waitFor(() => {
      expect(oosInvestigationService.list).toHaveBeenCalledWith(
        expect.objectContaining({
          kind: "Oos",
          openOnly: true,
          status: "PhaseTwo",
          from: "2026-11-01",
          to: "2026-11-25"
        }),
        expect.any(AbortSignal)
      );
    });

    await user.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() => {
      const calls = vi.mocked(oosInvestigationService.list).mock.calls;
      const lastCall = calls[calls.length - 1]?.[0];
      expect(lastCall).toEqual({
        kind: "Oos",
        openOnly: true,
        page: 1,
        pageSize: 25
      });
    });
  });

  it("hides Legacy tab for a user without Oos.Manage", async () => {
    setAuth(["Oos.View"], "Reviewer");
    renderPage();

    expect(await screen.findByText("OOS1126001")).toBeTruthy();
    expect(screen.queryByRole("tab", { name: "Legacy" })).toBeNull();
  });
});
