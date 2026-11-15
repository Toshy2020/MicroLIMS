import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../theme";
import { resetUiStateCache } from "../../../hooks/useUiState";
import { uiStateService } from "../../../services/uiStateService";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import { CryovialService } from "./services/CryovialService";
import { CryovialsPage } from "./CryovialsPage";
import type { CryovialItem } from "./types/cryovialTypes";
import type { MaterialItem } from "../../inventory/materials/types/materialTypes";

vi.mock("../../../services/uiStateService", () => ({
  uiStateService: { getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }
}));

vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
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

vi.mock("../../inventory/materials/services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getReleasedMedia: vi.fn(),
    getEquipment: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const lyophilizedMaterial: MaterialItem = {
  id: 21,
  sectionId: 1,
  materialType: "LyophilizedMicroorganism",
  customType: null,
  materialName: "Escherichia coli ATCC 8739",
  manufacturerName: "Microbiologics",
  batchNumber: "LYO-01",
  receivingDate: "2026-01-01",
  expiryDate: "2028-01-01",
  code: "ORG-001",
  location: "Deep Freezer DF-01",
  mediaProductId: null,
  organismId: 4,
  organism: {
    id: 4,
    scientificName: "Escherichia coli",
    atccNumber: "8739",
    description: "Gram-negative rod"
  },
  atccNumber: "8739",
  quantityReceived: 10,
  quantityRemaining: 8,
  unit: "Disc",
  minimumStockLevel: 1,
  status: "InStock"
};

const preparedCryovial: CryovialItem = {
  id: 42,
  code: "CRY-2026-0042",
  materialId: 21,
  organismId: 4,
  organismNameSnapshot: "Escherichia coli",
  manufacturerName: "Microbiologics",
  expiryDate: "2027-06-30",
  numberOfVialsPrepared: 15,
  vialsRemaining: 15,
  storageCondition: "Deep Freezer, Thermo (DF-01)",
  physicalCheckConfirmed: true,
  physicalCheckText: "",
  preparedAt: "2026-11-13T08:00:00Z",
  preparedByUserId: 1,
  approvalStatus: "PendingReview",
  isDestroyed: false
};

function renderPage(url = "/") {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter initialEntries={[url]}>
          <CryovialsPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

async function prepareCryovialBatch(user: ReturnType<typeof userEvent.setup>) {
  const openBtn = await screen.findByRole("button", { name: "Prepare Cryovial Batch" });
  await user.click(openBtn);

  const dialog = await screen.findByRole("dialog");

  const sourceSelect = within(dialog).getByLabelText(/Lyophilized Microorganism Stock/i);
  await user.click(sourceSelect);
  await user.click(await screen.findByRole("option", { name: /Escherichia coli ATCC 8739/i }));

  await user.type(within(dialog).getByLabelText(/Vials Prepared/i), "15");
  await user.type(within(dialog).getByLabelText(/Discs Used/i), "1");

  const expiryGroup = within(dialog).getByRole("group", { name: /Expiry Date/i });
  await user.type(within(expiryGroup).getByRole("spinbutton", { name: "Day" }), "30062027");

  await user.click(within(dialog).getByLabelText(/Storage Equipment/i));
  await user.click(await screen.findByRole("option", { name: /DF-01/i }));

  await user.click(within(dialog).getByRole("checkbox"));

  const comboboxes = within(dialog).getAllByRole("combobox");
  const panelMediaSelect = comboboxes[comboboxes.length - 2];
  const panelIncubatorSelect = comboboxes[comboboxes.length - 1];

  await user.click(panelMediaSelect);
  await user.click(await screen.findByRole("option", { name: /ML-100/i }));

  await user.click(panelIncubatorSelect);
  await user.click(await screen.findByRole("option", { name: /INC-01/i }));

  const startGroup = within(dialog).getByRole("group", { name: /Row 1 incubation start/i });
  await user.type(within(startGroup).getByRole("spinbutton", { name: "Day" }), "01112026");

  const endGroup = within(dialog).getByRole("group", { name: /Row 1 incubation end/i });
  await user.type(within(endGroup).getByRole("spinbutton", { name: "Day" }), "03112026");

  await user.click(within(dialog).getByRole("button", { name: "Save Batch" }));
}

// The remembered-tab store is answered for every test in this file.
beforeEach(() => {
  resetUiStateCache();
  vi.mocked(uiStateService.getAll).mockResolvedValue({});
  vi.mocked(uiStateService.set).mockResolvedValue(undefined);
});

describe("CryovialsPage label printing offer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.CRYOVIALS_MANAGE, PERMISSIONS.LABELS_REPRINT]);

    let rows: CryovialItem[] = [];
    vi.mocked(CryovialService.getAll).mockImplementation(async () => rows);
    vi.mocked(CryovialService.prepare).mockImplementation(async () => {
      rows = [preparedCryovial];
      return preparedCryovial;
    });
    vi.mocked(MaterialService.getAll).mockResolvedValue([lyophilizedMaterial]);
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue([
      { id: 10, lotNumber: "ML-100", material: { materialName: "Tryptic Soy Broth" } }
    ]);
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue([
      {
        id: 30,
        name: "Incubator 1",
        code: "INC-01",
        type: "Incubator",
        location: null,
        setPointTemperature: null,
        calibrationDueDate: null,
        vendor: null,
        cdsSoftware: null,
        connectionSettings: null,
        sectionId: 1
      }
    ]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([
      {
        id: 50,
        code: "DF-01",
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
  });

  it("offers label printing after cryovial batch is created", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Cryovial",
        entityId: 42,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await prepareCryovialBatch(user);

    const printDialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(printDialog).findByText("CRY-2026-0042")).toBeTruthy();
    expect(labelService.getStatus).toHaveBeenCalledWith(
      [{ entityType: "Cryovial", entityId: 42 }],
      expect.any(AbortSignal)
    );
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Cryovial",
        entityId: 42,
        canPrint: false,
        blockedReason: "Batch is pending approval.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await prepareCryovialBatch(user);

    expect(
      await screen.findByText("Cryovial batch prepared successfully. Awaiting Section Head approval.")
    ).toBeTruthy();
    await waitFor(() => {
      expect(vi.mocked(CryovialService.getAll).mock.calls.length).toBeGreaterThanOrEqual(2);
    });
    expect(screen.queryByRole("dialog", { name: "Print labels" })).toBeNull();
  }, 15000);
});

describe("CryovialsPage tabs", () => {
  const selectedTab = () => screen.getByRole("tab", { selected: true }).textContent;

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    resetUiStateCache();
    seedPermissions([]);
    vi.mocked(uiStateService.getAll).mockResolvedValue({});
    vi.mocked(uiStateService.set).mockResolvedValue(undefined);
    vi.mocked(CryovialService.getAll).mockResolvedValue([]);
    vi.mocked(MaterialService.getAll).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    resetUiStateCache();
    localStorage.clear();
  });

  it("switches between its tabs and remembers the choice", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(selectedTab()).toBe("Cryovials");
    await user.click(screen.getByRole("tab", { name: "Approved Cryovials" }));
    expect(selectedTab()).toBe("Approved Cryovials");
    await waitFor(() => expect(uiStateService.set).toHaveBeenCalledWith("nav.tab.cryovials", "approved"), { timeout: 2000 });
  });

  it("a deep link with ?tab=approved still opens the approved tab", () => {
    renderPage("/?tab=approved");

    expect(selectedTab()).toBe("Approved Cryovials");
  });

  it("opens the last tab used when the address names none", async () => {
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.cryovials": "approved" });
    renderPage();

    await waitFor(() => expect(selectedTab()).toBe("Approved Cryovials"));
  });
});
