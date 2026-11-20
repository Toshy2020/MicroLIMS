import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { EquipmentInventoryService } from "./services/EquipmentInventoryService";
import { EquipmentPlanService } from "./services/EquipmentPlanService";
import { EquipmentInventoryPage } from "./EquipmentInventoryPage";
import type { EquipmentItem } from "./types/equipmentTypes";

// The Equipment register on the shared table state (smarter-tables Task 10): filters in the address, saved views.

vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: vi.fn(), getMySections: vi.fn() },
  getMySections: vi.fn()
}));
vi.mock("./services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn(),
    getForPrint: vi.fn(),
    getStatusHistory: vi.fn(),
    getDocuments: vi.fn(),
    create: vi.fn(),
    update: vi.fn()
  }
}));
vi.mock("./services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    getMaintenanceLog: vi.fn(),
    getActive: vi.fn(),
    getActivities: vi.fn(),
    saveSchedule: vi.fn(),
    voidLogEntry: vi.fn()
  }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
vi.mock("../../auditSearch/services/AuditSearchService", () => ({
  AuditSearchService: { getForEntity: vi.fn() }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();

const eq = (over: Partial<EquipmentItem>): EquipmentItem => ({
  id: 1,
  instrumentType: "Autoclave",
  manufacturerName: "Tuttnauer",
  serialNumber: "SN-100",
  firmwareVersion: "1.0",
  code: "EQ-AC-01",
  location: "Prep room",
  calibrationDueDate: day(200),
  status: "InService",
  isCalibrationOverdue: false,
  sectionId: 1,
  ...over
});

const ROWS: EquipmentItem[] = [
  eq({ id: 1 }),
  eq({
    id: 2, instrumentType: "Balance", manufacturerName: "Mettler", serialNumber: "SN-200", code: "EQ-BL-02",
    location: "Weighing room", calibrationDueDate: day(-10), isCalibrationOverdue: true, sectionId: 2
  }),
  eq({
    id: 3, instrumentType: "Incubator", manufacturerName: "Memmert", serialNumber: "SN-300", code: "EQ-IN-03",
    location: "Prep room", calibrationDueDate: day(10), sectionId: 1
  }),
  eq({
    id: 4, instrumentType: "pH Meter", manufacturerName: "Hanna", serialNumber: "SN-400", code: "EQ-PH-04",
    location: "Weighing room", status: "Retired", calibrationDueDate: null, sectionId: null
  })
];

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7, tableKey: "inventory.equipment", name: "Overdue", scope: "Personal", sectionId: null, sectionName: null,
    isMine: true, canEdit: true, isDefault: false, ownerName: "Analyst", stateJson
  };
}

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Address">{location.search}</output>;
}

function renderPage(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <EquipmentInventoryPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

// The register only: the page also renders a print-only copy of the list.
const registerRoot = () => within(document.querySelector(".no-print") as HTMLElement);
const registerRows = () => registerRoot().getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasCode = (code: string) => registerRows().some((r) => r.includes(code));
const SEARCH = "Search by type, manufacturer, serial no., code, location...";

const hasHeader = (label: string) =>
  registerRoot().getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("EquipmentInventoryPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.AUDIT_VIEW]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue(ROWS);
    vi.mocked(EquipmentInventoryService.getForPrint).mockResolvedValue(ROWS);
    vi.mocked(EquipmentInventoryService.getStatusHistory).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([]);
    vi.mocked(EquipmentPlanService.getMaintenanceLog).mockResolvedValue([] as never);
    vi.mocked(AuditSearchService.getForEntity).mockResolvedValue([]);
    const sections = [
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Micro", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
      { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
    ];
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue(sections);
    vi.mocked(getMySections).mockResolvedValue(sections);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue(sections);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("URL round trip: search, dropdown and KPI filters are in the address and restore on reload; ?lab= is kept", async () => {
    const user = userEvent.setup();
    const first = renderPage("/?lab=MICRO");
    await waitFor(() => expect(registerRoot().getByText("EQ-IN-03")).toBeTruthy());
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "prep");
    await pick(user, "Status", "In Service");
    await user.click(screen.getByRole("button", { name: /^Calibration Due Soon/ }));
    await waitFor(() => expect(address().get("f.kpi")).toBe("calibration_due_soon"));
    expect(address().get("q")).toBe("prep");
    expect(address().get("f.status")).toBe("InService");
    expect(address().get("lab")).toBe("MICRO");
    await waitFor(() => expect(registerRows()).toHaveLength(1), { timeout: 5000 });
    const url = `/${screen.getByLabelText("Address").textContent}`;
    const before = registerRows();
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(registerRoot().getByText("EQ-IN-03")).toBeTruthy());
    expect(registerRows()).toEqual(before);
    expect((screen.getByRole("textbox", { name: SEARCH }) as HTMLInputElement).value).toBe("prep");
    expect(screen.getByRole("button", { name: /^Calibration Due Soon \(filtered\)/ })).toBeTruthy();
  });

  it("Reset clears every filter from the address but keeps ?lab=", async () => {
    const user = userEvent.setup();
    renderPage("/?lab=MICRO&f.location=Nowhere&f.kpi=in_service&q=x");
    await waitFor(() => expect(screen.getByText("No equipment matching the selected filter criteria")).toBeTruthy());
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows().length).toBeGreaterThan(0));
    expect(address().has("f.location")).toBe(false);
    expect(address().has("f.kpi")).toBe(false);
    expect(address().has("q")).toBe(false);
    expect(address().get("lab")).toBe("MICRO");
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(JSON.stringify({
        v: 1, filters: { kpi: "calibration_overdue", instrumentType: "Balance" }, search: "", sort: null, pageSize: 25,
        columns: ["instrumentType", "code"], density: "compact"
      }))
    ]);
    renderPage("/?view=7");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasCode("EQ-BL-02")).toBe(true);
    }, { timeout: 5000 });
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim()).filter(Boolean)).toEqual([
      "Type", "Code", "Actions"
    ]);
  });

  it("the toolbar offers saved views and a column chooser that hides and shows a column", async () => {
    const user = userEvent.setup();
    renderPage();
    await waitFor(() => expect(registerRows().length).toBeGreaterThan(0));
    expect(screen.getByRole("button", { name: /Views/ })).toBeTruthy();
    expect(hasHeader("Manufacturer")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Manufacturer" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Manufacturer")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Manufacturer" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Manufacturer")).toBe(true));
  });
});
