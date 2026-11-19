import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { SolutionPreparationService } from "../../solutionPreparation/services/SolutionPreparationService";
import { SamplePreparationService } from "../../testPreparation/services/SamplePreparationService";
import { MaterialsPage } from "./MaterialsPage";
import { MaterialService } from "./services/MaterialService";
import type { MaterialItem } from "./types/materialTypes";

// Pins the Materials Stock page: columns, every filter, row actions and the exact
// service calls. The same assertions hold before and after the move onto the shared table state.

vi.mock("../../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: vi.fn(), getMySections: vi.fn() },
  getMySections: vi.fn()
}));
vi.mock("./services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn(),
    getForPrint: vi.fn(),
    getTypeOptions: vi.fn(),
    getDefaultUnit: vi.fn(),
    getCOAEligibility: vi.fn(),
    getDocuments: vi.fn(),
    create: vi.fn(),
    update: vi.fn()
  }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
vi.mock("../../auditSearch/services/AuditSearchService", () => ({
  AuditSearchService: { getForEntity: vi.fn() }
}));
vi.mock("../../solutionPreparation/services/SolutionPreparationService", () => ({
  SolutionPreparationService: { getByLot: vi.fn() }
}));
vi.mock("../../testPreparation/services/SamplePreparationService", () => ({
  SamplePreparationService: { getWhereUsed: vi.fn() }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const lot = (over: Partial<MaterialItem>): MaterialItem => ({
  id: 1,
  sectionId: 1,
  materialType: "Chemical",
  customType: null,
  materialName: "Sodium Hydroxide",
  manufacturerName: "Merck",
  batchNumber: "LOT-A1",
  receivingDate: "2026-10-01T00:00:00Z",
  expiryDate: null,
  code: "RG-001",
  location: "Shelf 1",
  mediaProductId: null,
  organismId: null,
  atccNumber: null,
  quantityReceived: 10,
  quantityRemaining: 10,
  unit: "Gram",
  minimumStockLevel: null,
  status: "InStock",
  ...over
});

const ROWS: MaterialItem[] = [
  lot({ id: 1, stockId: "STK-1", materialName: "Sodium Hydroxide", batchNumber: "LOT-A1" }),
  lot({
    id: 2, stockId: "STK-2", materialType: "DehydratedMedia", materialName: "Peptone Water", manufacturerName: "Oxoid",
    batchNumber: "LOT-B2", location: "Fridge", status: "Expired", mediaProductId: 4, code: "MD-002"
  }),
  lot({
    id: 3, stockId: "STK-3", materialName: "Acetone AR", manufacturerName: "ChemCorp", batchNumber: "LOT-C3",
    quantityRemaining: 2, minimumStockLevel: 5, sectionId: 2, code: "RG-003"
  }),
  lot({
    id: 4, stockId: "STK-4", materialName: "Glycerol", manufacturerName: "Merck", batchNumber: "LOT-D4",
    approvalStatus: "PendingReview", code: "RG-004"
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
          <MaterialsPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");
// The register only: the page also renders a print-only copy of the list.
const registerRoot = () => within(document.querySelector(".no-print") as HTMLElement);
const registerRows = () => registerRoot().getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const ready = () => waitFor(() => expect(registerRoot().getByText("Peptone Water")).toBeTruthy());
const hasLot = (name: string) => registerRows().some((r) => r.includes(name));

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

async function openRowMenu(user: ReturnType<typeof userEvent.setup>, id: number) {
  await user.click(await screen.findByRole("button", { name: `Row actions for ${id}` }));
}

describe("MaterialsPage register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MATERIALS_MANAGE, PERMISSIONS.MATERIALS_RELEASE, PERMISSIONS.AUDIT_VIEW]);
    vi.mocked(MaterialService.getAll).mockResolvedValue(ROWS);
    vi.mocked(MaterialService.getForPrint).mockResolvedValue(ROWS);
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Chemical", "DehydratedMedia"], custom: [] });
    vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
    vi.mocked(MaterialService.getCOAEligibility).mockResolvedValue({ eligible: true, reasons: [] } as never);
    vi.mocked(MaterialService.getDocuments).mockResolvedValue([]);
    vi.mocked(SolutionPreparationService.getByLot).mockResolvedValue([]);
    vi.mocked(SamplePreparationService.getWhereUsed).mockResolvedValue([] as never);
    vi.mocked(AuditSearchService.getForEntity).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    const sections = [
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Micro", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
      { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
    ];
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue(sections);
    vi.mocked(getMySections).mockResolvedValue(sections);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("loads the stock list and the print list once, with no arguments, and shows every column", async () => {
    renderPage();
    await ready();
    expect(MaterialService.getAll).toHaveBeenCalledTimes(1);
    expect(MaterialService.getAll).toHaveBeenCalledWith();
    expect(MaterialService.getForPrint).toHaveBeenCalledTimes(1);
    expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(undefined);
    const headers = screen.getAllByRole("columnheader").map((h) => h.textContent?.trim());
    expect(headers).toEqual(expect.arrayContaining([
      "Type", "Material Name", "Manufacturer", "Stock ID", "Batch/Lot No.", "Received", "Expiry", "Code",
      "Location", "Storage Condition", "Qty Received", "Qty Remaining", "Min Stock", "Status"
    ]));
    expect(registerRows()).toHaveLength(4);
  });

  it("search narrows by name, lot, code, manufacturer and stock ID, and Reset restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const search = screen.getByRole("textbox", { name: "Search by name, lot no., code..." });
    await user.type(search, "oxoid");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Peptone Water")).toBe(true);
    await user.clear(search);
    await user.type(search, "rg-003");
    await waitFor(() => expect(hasLot("Acetone AR")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    expect(screen.queryByRole("button", { name: "Reset" })).toBeNull();
  });

  it("type, manufacturer, location, status and expiry dropdowns each filter the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pick(user, "Type", "Dehydrated Media");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Peptone Water")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Manufacturer", "Merck");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasLot("Acetone AR")).toBe(false);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Location", "Fridge");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Status", "Low Stock");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Acetone AR")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));

    await pick(user, "Expiry", "Expired");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Peptone Water")).toBe(true);
  });

  it("a KPI tile filters the list, a second click clears it, and it combines with a dropdown", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(screen.getByRole("button", { name: /^Low Stock/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Acetone AR")).toBe(true);
    await user.click(screen.getByRole("button", { name: /^Low Stock \(filtered\)/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    await user.click(screen.getByRole("button", { name: /^Out of Stock/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Peptone Water")).toBe(true);
    await pick(user, "Manufacturer", "Merck");
    await waitFor(() => expect(screen.getByText("No materials matching the selected filter criteria")).toBeTruthy());
  });

  it("?lab= keeps only that laboratory's lots and asks for that section's type options", async () => {
    renderPage("/?lab=FP");
    await waitFor(() => expect(registerRoot().getByText("Acetone AR")).toBeTruthy());
    expect(registerRows()).toHaveLength(1);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenLastCalledWith(2));
    expect(address().get("lab")).toBe("FP");
  });

  it("row actions: lot details, edit, release (only for a lot awaiting release) and audit history", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await openRowMenu(user, 1);
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Lot details and documents", "Edit material", "Audit history"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "Lot details and documents" }));
    expect(await screen.findByRole("dialog", { name: /Lot Details/ })).toBeTruthy();
    await waitFor(() => expect(MaterialService.getDocuments).toHaveBeenCalledWith(1));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog", { name: /Lot Details/ })).toBeNull());

    await openRowMenu(user, 4);
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Lot details and documents", "Edit material", "Release", "Audit history"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "Release" }));
    expect(await screen.findByRole("dialog", { name: /Release Lot: Glycerol/ })).toBeTruthy();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog", { name: /Release Lot/ })).toBeNull());

    await openRowMenu(user, 2);
    await user.click(screen.getByRole("menuitem", { name: "Audit history" }));
    await waitFor(() => expect(AuditSearchService.getForEntity).toHaveBeenCalledWith("Material", 2));
    await user.keyboard("{Escape}");

    await openRowMenu(user, 1);
    await user.click(screen.getByRole("menuitem", { name: "Edit material" }));
    expect(await screen.findByRole("dialog")).toBeTruthy();
  });

  it("clicking a row opens the lot details", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(registerRoot().getByText("Acetone AR"));
    const dialog = await screen.findByRole("dialog", { name: /Lot Details/ });
    expect(within(dialog).getByText(/Acetone AR/)).toBeTruthy();
  });

  it("without release and audit permissions those row actions are absent", async () => {
    seedPermissions([PERMISSIONS.MATERIALS_MANAGE]);
    const user = userEvent.setup();
    renderPage();
    await ready();
    await openRowMenu(user, 4);
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual(["Lot details and documents", "Edit material"]);
  });
  it("?materialId= opens that lot's details once the register has loaded", async () => {
    renderPage("/?materialId=3");
    expect(await screen.findByRole("dialog", { name: /Lot Details/ }, { timeout: 5000 })).toBeTruthy();
    await waitFor(() => expect(MaterialService.getDocuments).toHaveBeenCalledWith(3));
  });

  it("?materialId= for a lot not in the register opens nothing", async () => {
    renderPage("/?materialId=99");
    await ready();
    expect(screen.queryByRole("dialog", { name: /Lot Details/ })).toBeNull();
    expect(MaterialService.getDocuments).not.toHaveBeenCalled();
  });
});
