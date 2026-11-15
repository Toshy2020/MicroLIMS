import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { EquipmentInventoryService } from "./services/EquipmentInventoryService";
import { EquipmentPlanService } from "./services/EquipmentPlanService";
import { EquipmentInventoryPage } from "./EquipmentInventoryPage";
import type { EquipmentItem } from "./types/equipmentTypes";

// Pins the Equipment register: columns, every filter, row actions and the exact service
// calls. The same assertions hold before and after the move onto the shared table state.

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
const ready = () => waitFor(() => expect(registerRoot().getByText("EQ-BL-02")).toBeTruthy());
const SEARCH = "Search by type, manufacturer, serial no., code, location...";

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

async function openRowMenu(user: ReturnType<typeof userEvent.setup>, id: number) {
  await user.click(await screen.findByRole("button", { name: `Row actions for ${id}` }));
}

describe("EquipmentInventoryPage register", () => {
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

  it("loads the register and the print list once, with no arguments, and shows every column", async () => {
    renderPage();
    await ready();
    expect(EquipmentInventoryService.getAll).toHaveBeenCalledTimes(1);
    expect(EquipmentInventoryService.getAll).toHaveBeenCalledWith();
    expect(EquipmentInventoryService.getForPrint).toHaveBeenCalledTimes(1);
    const headers = registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim());
    expect(headers).toEqual(expect.arrayContaining([
      "Type", "Manufacturer", "Serial No.", "Firmware", "Code", "Location", "Calibration Due", "Laboratory", "Status"
    ]));
    expect(registerRows()).toHaveLength(4);
    // The laboratory column resolves the section name; a lot without one is flagged.
    expect(hasCode("Physicochemical")).toBe(true);
    expect(registerRoot().getByText("Unassigned")).toBeTruthy();
  });

  it("search narrows by type, manufacturer, serial, firmware, code and location, and Reset restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const search = screen.getByRole("textbox", { name: SEARCH });
    await user.type(search, "mettler");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("EQ-BL-02")).toBe(true);
    await user.clear(search);
    await user.type(search, "sn-300");
    await waitFor(() => expect(hasCode("EQ-IN-03")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    expect(screen.queryByRole("button", { name: "Reset" })).toBeNull();
  });

  it("instrument type, status, location and calibration dropdowns each filter the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pick(user, "Instrument Type", "Incubator");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("EQ-IN-03")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Status", "Retired");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("EQ-PH-04")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Location", "Weighing room");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasCode("EQ-AC-01")).toBe(false);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Calibration Due", "Calibration Overdue");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("EQ-BL-02")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Calibration Due", "Due in 30 Days");
    await waitFor(() => expect(hasCode("EQ-IN-03")).toBe(true));
    expect(registerRows()).toHaveLength(1);
  });

  it("a KPI tile filters the list, a second click clears it, and it combines with a dropdown", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(screen.getByRole("button", { name: /^Calibration Overdue/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("EQ-BL-02")).toBe(true);
    await user.click(screen.getByRole("button", { name: /^Calibration Overdue \(filtered\)/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    await user.click(screen.getByRole("button", { name: /^Out of Service/ }));
    await waitFor(() => expect(hasCode("EQ-PH-04")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await pick(user, "Location", "Prep room");
    await waitFor(() => expect(screen.getByText("No equipment matching the selected filter criteria")).toBeTruthy());
  });

  it("?lab= keeps only that laboratory's equipment", async () => {
    renderPage("/?lab=MICRO");
    await waitFor(() => expect(registerRoot().getByText("EQ-IN-03")).toBeTruthy());
    expect(registerRows()).toHaveLength(2);
    expect(hasCode("EQ-BL-02")).toBe(false);
    expect(new URLSearchParams(screen.getByLabelText("Address").textContent ?? "").get("lab")).toBe("MICRO");
  });

  it("row actions: details, edit and audit history", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await openRowMenu(user, 2);
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Details and documents", "Edit equipment", "Audit history"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "Details and documents" }));
    expect(await screen.findByRole("dialog")).toBeTruthy();
    await waitFor(() => expect(EquipmentInventoryService.getStatusHistory).toHaveBeenCalledWith(2));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await openRowMenu(user, 3);
    await user.click(screen.getByRole("menuitem", { name: "Audit history" }));
    await waitFor(() => expect(AuditSearchService.getForEntity).toHaveBeenCalledWith("EquipmentInventory", 3));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await openRowMenu(user, 1);
    await user.click(screen.getByRole("menuitem", { name: "Edit equipment" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByDisplayValue("EQ-AC-01")).toBeTruthy();
    expect(EquipmentInventoryService.getStatusHistory).not.toHaveBeenCalledWith(1);
  });

  it("clicking a row opens the equipment details", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(registerRoot().getByText("EQ-IN-03"));
    await screen.findByRole("dialog");
    await waitFor(() => expect(EquipmentInventoryService.getStatusHistory).toHaveBeenCalledWith(3));
  });

  it("without the audit permission that row action is absent", async () => {
    seedPermissions([]);
    const user = userEvent.setup();
    renderPage();
    await ready();
    await openRowMenu(user, 4);
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual(["Details and documents", "Edit equipment"]);
  });

  it("the Active Equipment tab replaces the register and hides the filters", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(screen.getByRole("tab", { name: "Active Equipment" }));
    await waitFor(() => expect(screen.queryByRole("textbox", { name: SEARCH })).toBeNull());
    expect(screen.queryByRole("button", { name: "Register Equipment" })).toBeNull();
  });
  it("?equipmentId= opens that equipment's details once the register has loaded", async () => {
    renderPage("/?equipmentId=3");
    expect(await screen.findByRole("dialog", {}, { timeout: 5000 })).toBeTruthy();
    await waitFor(() => expect(EquipmentInventoryService.getStatusHistory).toHaveBeenCalledWith(3));
  });

  it("?equipmentId= for equipment not in the register opens nothing", async () => {
    renderPage("/?equipmentId=99");
    await ready();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(EquipmentInventoryService.getStatusHistory).not.toHaveBeenCalled();
  });
});
