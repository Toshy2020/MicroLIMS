import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { EquipmentInventoryService } from "./services/EquipmentInventoryService";
import { EquipmentInventoryPage } from "./EquipmentInventoryPage";

// Only the network boundary is replaced; the page, KPI cards, filter bar, and table render for real.
vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: vi.fn(), getMySections: vi.fn() },
  getMySections: vi.fn()
}));
vi.mock("./services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn(),
    getForPrint: vi.fn()
  }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));

describe("EquipmentInventoryPage in Arabic", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([]);
    vi.mocked(getMySections).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getForPrint).mockResolvedValue([]);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows the heading, tabs, filter labels, and empty state in Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <EquipmentInventoryPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "inventory"]
    );
    expect(screen.getByRole("heading", { name: "المعدات" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "سجل المعدات" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "المعدات النشطة" })).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "نوع الجهاز" })).toBeTruthy();
    await waitFor(() => expect(screen.getByText("لا توجد معدات مسجلة")).toBeTruthy());
  });
});
