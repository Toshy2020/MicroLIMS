import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { SolutionPreparationService } from "../../solutionPreparation/services/SolutionPreparationService";
import { SamplePreparationService } from "../../testPreparation/services/SamplePreparationService";
import { MaterialsPage } from "./MaterialsPage";
import { MaterialService } from "./services/MaterialService";
import type { MaterialItem } from "./types/materialTypes";

// Materials Stock on the shared table state (smarter-tables Task 10): filters in the address, saved views.

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

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7, tableKey: "inventory.materials", name: "Expired lots", scope: "Personal", sectionId: null, sectionName: null,
    isMine: true, canEdit: true, isDefault: false, ownerName: "Analyst", stateJson
  };
}

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

const hasHeader = (label: string) =>
  registerRoot().getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("MaterialsPage on the shared table state", () => {
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

  it("URL round trip: search, dropdown and KPI filters are in the address and restore on reload; ?lab= is kept", async () => {
    const user = userEvent.setup();
    const first = renderPage("/?lab=MICRO");
    await ready();
    await user.type(screen.getByRole("textbox", { name: "Search by name, lot no., code..." }), "merck");
    await pick(user, "Status", "In Stock");
    await user.click(screen.getByRole("button", { name: /^In Stock/ }));
    await waitFor(() => expect(address().get("f.kpi")).toBe("in_stock"));
    expect(address().get("q")).toBe("merck");
    expect(address().get("f.status")).toBe("InStock");
    expect(address().get("lab")).toBe("MICRO");
    const url = `/${screen.getByLabelText("Address").textContent}`;
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    const before = registerRows();
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(registerRoot().getByText("Sodium Hydroxide")).toBeTruthy());
    expect(registerRows()).toEqual(before);
    expect((screen.getByRole("textbox", { name: "Search by name, lot no., code..." }) as HTMLInputElement).value).toBe("merck");
    expect(screen.getByRole("button", { name: /^In Stock \(filtered\)/ })).toBeTruthy();
  });

  it("Reset clears every filter from the address but keeps ?lab=", async () => {
    const user = userEvent.setup();
    renderPage("/?lab=MICRO&f.manufacturer=Merck&f.kpi=low_stock&q=x");
    await waitFor(() => expect(screen.getByText("No materials matching the selected filter criteria")).toBeTruthy());
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows().length).toBeGreaterThan(0));
    expect(address().has("f.manufacturer")).toBe(false);
    expect(address().has("f.kpi")).toBe(false);
    expect(address().has("q")).toBe(false);
    expect(address().get("lab")).toBe("MICRO");
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(JSON.stringify({
        v: 1, filters: { materialType: "DehydratedMedia", kpi: "out_of_stock" }, search: "", sort: null, pageSize: 25,
        columns: ["materialType", "materialName", "batchNumber"], density: "compact"
      }))
    ]);
    renderPage("/?view=7");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("Peptone Water")).toBe(true);
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim()).filter(Boolean)).toEqual([
      "Type", "Material Name", "Batch/Lot No.", "Actions"
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
