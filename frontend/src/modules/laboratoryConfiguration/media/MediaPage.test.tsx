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
import { apiClient } from "../../../services/apiClient";
import { labelService } from "../../../services/labelService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";
import type { MaterialItem } from "../../inventory/materials/types/materialTypes";
import { SamplePreparationService } from "../../testPreparation/services/SamplePreparationService";
import { MediaEvaluationService } from "../mediaEvaluation/services/MediaEvaluationService";
import type { MediaLot } from "../mediaEvaluation/types/mediaEvaluationTypes";
import { EquipmentConfigurationService } from "../masterDataSimple/services/EquipmentConfigurationService";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { MediaPage } from "./MediaPage";

// Network boundary: the server's answer about releasing a lot.
vi.mock("../../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  },
  ifMatch: vi.fn((version: number) => ({ headers: { "If-Match": String(version) } }))
}));

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

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getMaterials: vi.fn(),
    getEquipment: vi.fn()
  }
}));

vi.mock("../../inventory/materials/services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn()
  }
}));

vi.mock("../masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getAutoclavePrograms: vi.fn()
  }
}));

vi.mock("../mediaEvaluation/services/MediaEvaluationService", () => ({
  MediaEvaluationService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../testPreparation/services/SamplePreparationService", () => ({
  SamplePreparationService: {
    getWhereUsed: vi.fn()
  }
}));

vi.mock("./services/MediaPreparationService", () => ({
  MediaPreparationService: {
    getAll: vi.fn(),
    getAwaitingApproval: vi.fn(),
    getUsableSolutions: vi.fn(),
    prepare: vi.fn(),
    decideRelease: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const dehydratedMaterial: MaterialItem = {
  id: 301,
  sectionId: 1,
  materialType: "DehydratedMedia",
  customType: null,
  materialName: "Tryptic Soy Agar Powder",
  manufacturerName: "Oxoid",
  batchNumber: "OX-2601",
  receivingDate: "2026-01-10",
  expiryDate: "2028-01-10",
  code: "DM-001",
  location: "Shelf A",
  mediaProductId: 9,
  organismId: null,
  atccNumber: null,
  quantityReceived: 500,
  quantityRemaining: 400,
  unit: "Gram",
  minimumStockLevel: 50,
  status: "InStock"
};

const preparedLot: MediaLot = {
  id: 99,
  materialId: 301,
  material: dehydratedMaterial,
  lotNumber: "ML-2026-0099",
  manufacturerLot: "OX-2601",
  manufacturerName: "Oxoid",
  totalWeight: 40,
  totalVolume: "1000 mL",
  autoclaveEquipmentId: 41,
  autoclaveProgram: "STD-121",
  loadType: "Liquids",
  temperature: 121,
  cycleTime: 15,
  cycleNumber: 3,
  ph: 7.3,
  expiryDate: "2027-06-30",
  preparedAt: "2026-11-13T08:00:00Z",
  preparedByUserId: 1,
  status: "Prepared",
  approvalStatus: "PendingReview",
  approvedByUserId: null,
  approvedAt: null,
  isReleasedForUse: false
};

function renderPage(url = "/") {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter initialEntries={[url]}>
          <MediaPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

async function prepareMediaLot(user: ReturnType<typeof userEvent.setup>) {
  const openBtn = await screen.findByRole("button", { name: "Prepare New Media Lot" });
  await user.click(openBtn);

  const stockSelect = await screen.findByLabelText("Dehydrated Media Stock (Inventory)");
  await user.click(stockSelect);
  await user.click(await screen.findByRole("option", { name: /Tryptic Soy Agar Powder/i }));

  await user.type(screen.getByLabelText(/Total Weight \(g\)/i), "40");
  await user.type(screen.getByLabelText(/Total Volume/i), "1000 mL");

  await user.click(screen.getByLabelText("Autoclave"));
  await user.click(await screen.findByRole("option", { name: /AC-01: Autoclave Alpha/i }));

  const progSelect = screen.getByLabelText("Autoclave program");
  await waitFor(() => {
    expect(progSelect.getAttribute("aria-disabled")).not.toBe("true");
  });
  await user.click(progSelect);
  await user.click(await screen.findByRole("option", { name: /STD-121/i }));

  await user.type(screen.getByLabelText(/Cycle Number/i), "3");
  await user.type(screen.getByLabelText(/pH \(at 25°C\)/i), "7.30");

  const expiryGroup = screen.getByRole("group", { name: /Expiry Date/i });
  const daySpin = within(expiryGroup).getByRole("spinbutton", { name: "Day" });
  await user.type(daySpin, "30062027");

  await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));
}

// The remembered-tab store is answered for every test in this file.
beforeEach(() => {
  resetUiStateCache();
  vi.mocked(uiStateService.getAll).mockResolvedValue({});
  vi.mocked(uiStateService.set).mockResolvedValue(undefined);
});

describe("MediaPage label printing offer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MEDIA_PREPARE, PERMISSIONS.MEDIA_RELEASE]);

    let lots: MediaLot[] = [];
    vi.mocked(MediaPreparationService.getAll).mockImplementation(async () => lots);
    vi.mocked(MediaPreparationService.prepare).mockImplementation(async () => {
      lots = [preparedLot];
      return preparedLot;
    });
    vi.mocked(MediaPreparationService.getAwaitingApproval).mockResolvedValue([]);
    vi.mocked(MediaPreparationService.getUsableSolutions).mockResolvedValue([]);
    vi.mocked(MediaEvaluationService.getAll).mockResolvedValue([]);
    vi.mocked(SamplePreparationService.getWhereUsed).mockResolvedValue([]);
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: null } });

    vi.mocked(masterDataOptions.getMaterials).mockResolvedValue([dehydratedMaterial]);
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue([
      {
        id: 41,
        name: "Autoclave Alpha",
        code: "AC-01",
        type: "Autoclave",
        location: null,
        setPointTemperature: null,
        calibrationDueDate: null,
        vendor: null,
        cdsSoftware: null,
        connectionSettings: null,
        sectionId: 1
      }
    ]);
    vi.mocked(MaterialService.getAll).mockResolvedValue([dehydratedMaterial]);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([
      {
        id: 501,
        equipmentId: 41,
        autoclaveCode: "AC-01",
        autoclaveName: "Autoclave Alpha",
        programCode: "STD-121",
        programName: "Standard 121",
        loadType: "Liquids",
        temperature: 121,
        cycleTimeMinutes: 15,
        isActive: true,
        createdByUserId: 1,
        createdAt: "2026-01-01T00:00:00Z",
        lastModifiedByUserId: 1,
        lastModifiedAt: "2026-01-01T00:00:00Z"
      }
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("offers label printing after media lot is created", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MediaLot",
        entityId: 99,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await prepareMediaLot(user);

    const dialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(dialog).findByText("ML-2026-0099")).toBeTruthy();
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MediaLot",
        entityId: 99,
        canPrint: false,
        blockedReason: "Label printing is disabled.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await prepareMediaLot(user);

    expect(await screen.findByText("Media lot ML-2026-0099 successfully prepared.")).toBeTruthy();
    await waitFor(() => {
      expect(vi.mocked(MediaPreparationService.getAll).mock.calls.length).toBeGreaterThanOrEqual(2);
    });
    expect(screen.queryByRole("dialog", { name: "Print labels" })).toBeNull();
  }, 15000);
});

describe("MediaPage tabs", () => {
  const selectedTab = () => screen.getByRole("tab", { selected: true }).textContent;

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    resetUiStateCache();
    seedPermissions([]);
    vi.mocked(uiStateService.getAll).mockResolvedValue({});
    vi.mocked(uiStateService.set).mockResolvedValue(undefined);
    vi.mocked(MediaPreparationService.getAll).mockResolvedValue([]);
    vi.mocked(MediaPreparationService.getAwaitingApproval).mockResolvedValue([]);
    vi.mocked(MediaEvaluationService.getAll).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMaterials).mockResolvedValue([]);
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: null } });
  });

  afterEach(() => {
    cleanup();
    resetUiStateCache();
    localStorage.clear();
  });

  it("switches between its tabs and remembers the choice", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(selectedTab()).toBe("Media Lots");
    await user.click(screen.getByRole("tab", { name: "Approved Media" }));
    expect(selectedTab()).toBe("Approved Media");
    await waitFor(() => expect(uiStateService.set).toHaveBeenCalledWith("nav.tab.media", "approved"), { timeout: 2000 });
  });

  it("a deep link with ?tab=approved still opens the approved tab", () => {
    renderPage("/?tab=approved");

    expect(selectedTab()).toBe("Approved Media");
  });

  it("opens the last tab used when the address names none", async () => {
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.media": "approved" });
    renderPage();

    await waitFor(() => expect(selectedTab()).toBe("Approved Media"));
  });
});
