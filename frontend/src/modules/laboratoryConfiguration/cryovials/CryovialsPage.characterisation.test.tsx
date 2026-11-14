import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import { CryovialService } from "./services/CryovialService";
import { CryovialsPage } from "./CryovialsPage";
import type { CryovialItem } from "./types/cryovialTypes";

// Pins the Cryovials register on CryovialsPage: KPIs, columns, search, all dropdowns,
// row actions, permission gating, tabs and the exact service calls.

vi.mock("../../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: vi.fn(), getMySections: vi.fn() },
  getMySections: vi.fn()
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
vi.mock("../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: vi.fn() }
}));
vi.mock("./services/CryovialService", () => ({
  CryovialService: {
    getAll: vi.fn(),
    prepare: vi.fn(),
    approve: vi.fn(),
    destroy: vi.fn(),
    moveStorage: vi.fn(),
    thawVial: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();

const cryovial = (over: Partial<CryovialItem> & { id: number }): CryovialItem => ({
  code: `CV-${String(over.id).padStart(3, "0")}`,
  materialId: 10,
  material: { id: 10, materialName: "E. coli lyophilised", batchNumber: "B-100", manufacturerName: "ATCC Direct" },
  organismId: 20,
  organism: { id: 20, scientificName: "Escherichia coli", atccNumber: "8739", strainNumber: "S-1" },
  organismNameSnapshot: "Escherichia coli",
  referenceOrganismCode: "RO-01",
  cultureCollection: "ATCC",
  strainNumber: "8739",
  manufacturerName: "ATCC Direct",
  expiryDate: day(200),
  numberOfVialsPrepared: 10,
  vialsRemaining: 6,
  storageCondition: "Deep Freezer · DF-01 · Cold Room",
  physicalCheckConfirmed: true,
  physicalCheckText: "Intact",
  preparedAt: "2026-10-01T08:00:00Z",
  preparedByUserId: 5,
  preparedByName: "Jane Doe",
  approvalStatus: "Approved",
  isDestroyed: false,
  ...over
});

// 1: Approved, unexpired, 6 vials (counts towards Vials Available = 6)
// 2: PendingReview, 10 vials, Bacillus subtilis, expiring in 15 days
// 3: Approved, expiring in 10 days, 2 vials (counts towards Vials Available = 2, Expiring Soon, Low Stock)
// 4: Approved, expired (-5 days), 4 vials (does not count towards Vials Available or Expiring Soon)
// 5: Approved, depleted (0 vials), unexpired (counts towards Low Stock = 2 total with id 3)
// 6: Rejected, 5 vials
// 7: Destroyed, 3 vials
const ROWS: CryovialItem[] = [
  cryovial({ id: 1 }),
  cryovial({
    id: 2,
    code: "CV-002",
    organism: { id: 21, scientificName: "Bacillus subtilis", atccNumber: "6633" },
    organismNameSnapshot: "Bacillus subtilis",
    material: { id: 11, materialName: "B. subtilis discs", batchNumber: "B-200", manufacturerName: "Microbiologics" },
    manufacturerName: "Microbiologics",
    approvalStatus: "PendingReview",
    vialsRemaining: 10,
    expiryDate: day(15)
  }),
  cryovial({
    id: 3,
    code: "CV-003",
    organism: { id: 22, scientificName: "Candida albicans", atccNumber: "10231" },
    organismNameSnapshot: "Candida albicans",
    material: { id: 12, materialName: "C. albicans discs", batchNumber: "B-300", manufacturerName: "CultiLoops" },
    manufacturerName: "CultiLoops",
    approvalStatus: "Approved",
    vialsRemaining: 2,
    expiryDate: day(10)
  }),
  cryovial({
    id: 4,
    code: "CV-004",
    approvalStatus: "Approved",
    vialsRemaining: 4,
    expiryDate: day(-5)
  }),
  cryovial({
    id: 5,
    code: "CV-005",
    approvalStatus: "Approved",
    vialsRemaining: 0,
    expiryDate: day(120)
  }),
  cryovial({
    id: 6,
    code: "CV-006",
    approvalStatus: "Rejected",
    vialsRemaining: 5,
    expiryDate: day(90)
  }),
  cryovial({
    id: 7,
    code: "CV-007",
    approvalStatus: "Approved",
    isDestroyed: true,
    vialsRemaining: 3,
    expiryDate: day(90)
  })
];

function renderPage(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <CryovialsPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const registerRows = () => screen.getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasCode = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(screen.getByText("CV-001")).toBeTruthy());
const SEARCH = "Search by code, organism, material...";

async function pickSelect(user: ReturnType<typeof userEvent.setup>, label: string, option: string) {
  await user.click(screen.getByLabelText(label));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("CryovialsPage register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.CRYOVIALS_MANAGE, PERMISSIONS.CRYOVIALS_APPROVE]);
    vi.mocked(CryovialService.getAll).mockResolvedValue(ROWS);
    vi.mocked(CryovialService.approve).mockResolvedValue({});
    vi.mocked(CryovialService.thawVial).mockResolvedValue({});
    vi.mocked(CryovialService.destroy).mockResolvedValue({});
    vi.mocked(CryovialService.moveStorage).mockResolvedValue({});
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
    const sections = [
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
    ];
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue(sections);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue(sections);
    vi.mocked(getMySections).mockResolvedValue(sections);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([
      {
        id: 50,
        code: "DF-02",
        instrumentType: "Deep Freezer",
        manufacturerName: "Thermo",
        serialNumber: null,
        firmwareVersion: null,
        location: "Cold Room",
        calibrationDueDate: null,
        status: "InService",
        isCalibrationOverdue: false,
        sectionId: 1
      }
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("loads cryovials once with no args, shows KPIs and all columns", async () => {
    renderPage();
    await ready();

    expect(CryovialService.getAll).toHaveBeenCalledTimes(1);
    expect(CryovialService.getAll).toHaveBeenCalledWith();
    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Code", "Organism & Source", "Status", "Vials Stock", "Prepared", "Expiry", "Actions"
    ]);
    expect(registerRows()).toHaveLength(7);

    expect(registerRows()[0]).toContain("CV-001");
    expect(registerRows()[0]).toContain("Escherichia coli");
    expect(registerRows()[0]).toContain("(ATCC 8739)");
    expect(registerRows()[0]).toContain("6 of 10 vials");
    expect(registerRows()[0]).toContain("Jane Doe");
    expect(registerRows()[4]).toContain("Depleted");
    expect(registerRows()[6]).toContain("Destroyed");

    // KPI tiles reflect total batches (7), usable vials (6 + 2 = 8), expiring soon (1), low stock (<=2 vials: id 3 and id 5 = 2)
    expect(screen.getByText("Total Batches").parentElement?.parentElement?.textContent).toContain("7");
    expect(screen.getByText("Vials Available").parentElement?.parentElement?.textContent).toContain("8");
    expect(screen.getByText("Expiring Soon (30 d)").parentElement?.parentElement?.textContent).toContain("1");
    expect(screen.getByText("Low Stock (≤ 2 vials)").parentElement?.parentElement?.textContent).toContain("2");
  });

  it("search narrows by code, organism, material, batch and manufacturer, and Reset restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    const search = screen.getByRole("textbox", { name: SEARCH });
    await user.type(search, "cv-003");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasCode("CV-003")).toBe(true);
    });

    await user.clear(search);
    await user.type(search, "bacillus");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasCode("CV-002")).toBe(true);
    });

    await user.clear(search);
    await user.type(search, "b-200");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasCode("CV-002")).toBe(true);
    });

    await user.clear(search);
    await user.type(search, "cultiloops");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasCode("CV-003")).toBe(true);
    });

    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(7));
    expect(screen.queryByRole("button", { name: "Reset" })).toBeNull();
  });

  it("organism, status and expiry dropdowns each filter the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pickSelect(user, "Organism", "Bacillus subtilis");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(7));

    await pickSelect(user, "Status", "Approved");
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    expect(hasCode("CV-007")).toBe(false);

    await pickSelect(user, "Status", "Pending Review");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);

    await pickSelect(user, "Status", "Rejected");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-006")).toBe(true);

    await pickSelect(user, "Status", "Destroyed");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-007")).toBe(true);

    await pickSelect(user, "Status", "Depleted (0 Vials)");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-005")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(7));

    await pickSelect(user, "Expiry", "Expiring in 30 Days");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasCode("CV-002")).toBe(true);
    expect(hasCode("CV-003")).toBe(true);

    await pickSelect(user, "Expiry", "Expired");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-004")).toBe(true);

    await pickSelect(user, "Expiry", "Valid / Unexpired");
    await waitFor(() => expect(registerRows()).toHaveLength(6));
    expect(hasCode("CV-004")).toBe(false);
  });

  it("an empty result says no cryovials match the filter criteria", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await user.type(screen.getByRole("textbox", { name: SEARCH }), "no-such-cryovial");
    expect(await screen.findByText("No cryovials matching the filter criteria")).toBeTruthy();
  });

  it("row actions: approve/reject pending batch, thaw, move storage, destroy and open report", async () => {
    const user = userEvent.setup();
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    renderPage();
    await ready();

    // Pending batch (id 2): Approve batch, Reject batch, View laboratory report record
    await user.click(screen.getByRole("button", { name: "Row actions for 2" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Approve batch", "Reject batch", "View laboratory report record"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "Approve batch" }));
    const approveDialog = await screen.findByRole("dialog");
    expect(within(approveDialog).getByText("I am approving Cryovial Batch CV-002 for laboratory testing and media evaluation.")).toBeTruthy();
    await user.type(within(approveDialog).getByLabelText(/Comment/i), "Looks good");
    await user.type(within(approveDialog).getByLabelText(/Password/), "pw-123");
    await user.click(within(approveDialog).getByRole("button", { name: /Sign|Confirm/ }));
    await waitFor(() => expect(CryovialService.approve).toHaveBeenCalledWith(2, true, "pw-123", "Looks good"));
    expect(await screen.findByText("Cryovial batch CV-002 has been Approved.")).toBeTruthy();

    // Approved unexpired batch with vials (id 1): Thaw, Change storage, Destroy, View report
    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Thaw vial", "Change storage location", "Destroy (decommission batch)", "View laboratory report record"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "View laboratory report record" }));
    expect(open).toHaveBeenCalledWith("/cryovials/1/report", "_blank", "noopener");

    // Thaw vial
    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));
    await user.click(screen.getByRole("menuitem", { name: "Thaw vial" }));
    const thawDialog = await screen.findByRole("dialog");
    await user.type(within(thawDialog).getByLabelText("Enter reason for thawing this cryovial"), "GPT media check");
    await user.click(within(thawDialog).getByRole("button", { name: "Confirm Thaw" }));
    await waitFor(() => expect(CryovialService.thawVial).toHaveBeenCalledWith(1, "GPT media check"));
    expect(await screen.findByText("Vial from CV-001 successfully thawed and logged.")).toBeTruthy();

    // Destroy batch
    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));
    await user.click(screen.getByRole("menuitem", { name: "Destroy (decommission batch)" }));
    const destroyDialog = await screen.findByRole("dialog");
    await user.click(within(destroyDialog).getByRole("button", { name: "Confirm Destruction" }));
    await waitFor(() => expect(CryovialService.destroy).toHaveBeenCalledWith(1));
    expect(await screen.findByText("Batch CV-001 has been decommissioned and destroyed.")).toBeTruthy();

    // Change storage location
    await user.click(screen.getByRole("button", { name: "Row actions for 3" }));
    await user.click(screen.getByRole("menuitem", { name: "Change storage location" }));
    const moveDialog = await screen.findByRole("dialog");
    await waitFor(() => expect(EquipmentInventoryService.getAll).toHaveBeenCalled());
    await user.click(within(moveDialog).getByRole("button", { name: "Other area" }));
    await user.type(within(moveDialog).getByLabelText(/Storage area/i), "Cryo Room B");
    await user.type(within(moveDialog).getByLabelText(/Reason for move/i), "Freezer maintenance");
    await user.click(within(moveDialog).getByRole("button", { name: "Move" }));
    await waitFor(() =>
      expect(CryovialService.moveStorage).toHaveBeenCalledWith(3, {
        equipmentId: null,
        locationText: "Cryo Room B",
        reason: "Freezer maintenance"
      })
    );
    expect(await screen.findByText("Storage location of cryovial batch CV-003 changed.")).toBeTruthy();

    // Depleted (id 5), expired (id 4), destroyed (id 7) availability rules
    await user.click(screen.getByRole("button", { name: "Row actions for 5" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Change storage location", "Destroy (decommission batch)", "View laboratory report record"
    ]);
    await user.keyboard("{Escape}");

    await user.click(screen.getByRole("button", { name: "Row actions for 4" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Change storage location", "Destroy (decommission batch)", "View laboratory report record"
    ]);
    await user.keyboard("{Escape}");

    await user.click(screen.getByRole("button", { name: "Row actions for 7" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "View laboratory report record"
    ]);
  });

  it("without CRYOVIALS_APPROVE permission the Change storage location action is absent", async () => {
    seedPermissions([PERMISSIONS.CRYOVIALS_MANAGE]);
    const user = userEvent.setup();
    renderPage();
    await ready();

    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Thaw vial", "Destroy (decommission batch)", "View laboratory report record"
    ]);
  });

  it("?tab=approved shows the Approved Cryovials list instead", async () => {
    renderPage("/?tab=approved");
    await waitFor(() => expect(CryovialService.getAll).toHaveBeenCalledTimes(2));
    expect(screen.getByRole("tab", { name: "Approved Cryovials" }).getAttribute("aria-selected")).toBe("true");
  });

  it("a failed load shows the error alert", async () => {
    vi.mocked(CryovialService.getAll).mockRejectedValue(new Error("Network down"));
    renderPage();
    expect(await screen.findByText("Failed to load cryovials register.")).toBeTruthy();
  });
});
