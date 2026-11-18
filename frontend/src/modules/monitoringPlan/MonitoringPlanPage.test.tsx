import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { PERMISSIONS } from "../../routes/routes";
import { laboratorySectionService } from "../../services/laboratorySectionService";
import { MonitoringPlanPage } from "./MonitoringPlanPage";
import { EquipmentPlanService } from "./services/EquipmentPlanService";
import { MonitoringPlanService } from "./services/MonitoringPlanService";

vi.mock("./services/MonitoringPlanService", () => ({
  MonitoringPlanService: {
    getYear: vi.fn(),
    getDue: vi.fn(),
    getCalendar: vi.fn(),
    getCoverage: vi.fn(),
    getSpacing: vi.fn()
  }
}));

vi.mock("./services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    getYear: vi.fn(),
    getCalendar: vi.fn(),
    getEvents: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getSections: vi.fn()
  }
}));

vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getTestDefinitions: vi.fn().mockResolvedValue([])
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

describe("MonitoringPlanPage in Arabic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MONITORING_PLAN_VIEW, PERMISSIONS.MONITORING_PLAN_MANAGE]);

    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" } as never
    ]);
    vi.mocked(MonitoringPlanService.getYear).mockResolvedValue({
      year: 2026,
      months: Array.from({ length: 12 }, (_, i) => ({
        month: i + 1,
        total: i === 0 ? 4 : 0,
        performed: i === 0 ? 2 : 0,
        inProgress: i === 0 ? 1 : 0,
        overdue: i === 0 ? 1 : 0,
        upcoming: 0,
        skipped: 0
      }))
    });
    vi.mocked(MonitoringPlanService.getDue).mockResolvedValue({
      today: "2026-11-18",
      warningDayOfMonth: 20,
      showWarning: false,
      items: [
        {
          id: 101,
          version: 1,
          category: "Water",
          target: "SP-206",
          group: "Water Loop",
          periodKey: "2026-W46",
          targetDate: "2026-11-18",
          windowStart: "2026-11-15",
          windowEnd: "2026-11-22",
          originalWindowEnd: "2026-11-22",
          status: "DueToday",
          late: false,
          itemsTotal: 2,
          itemsPerformed: 0,
          manual: false,
          labSectionId: 1,
          labSectionCode: "MICRO"
        }
      ]
    });
    vi.mocked(EquipmentPlanService.getYear).mockResolvedValue([]);
    vi.mocked(EquipmentPlanService.getCalendar).mockResolvedValue({ tasks: [], events: [] });
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("lays the page out right to left and keeps sampling point identifiers left to right", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <MonitoringPlanPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "monitoring"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "خطة المراقبة" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "التقويم" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "إضافة ما بعد التنظيف" })).toBeTruthy();

    const identifier = await screen.findByText("SP-206");
    expect(identifier.closest("bdi")?.getAttribute("dir")).toBe("ltr");
  });
});
