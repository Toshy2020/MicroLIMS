import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../theme";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { getMySections, laboratorySectionService } from "../../../services/laboratorySectionService";
import { EquipmentInventoryService } from "../equipment/services/EquipmentInventoryService";
import { MaterialMasterService } from "../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import { MaterialCategoryService } from "../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import { ReferenceOrganismService } from "../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { MaterialsPage } from "./MaterialsPage";
import { MaterialService } from "./services/MaterialService";
import type { MaterialItem } from "./types/materialTypes";

vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getSections: vi.fn(),
    getMySections: vi.fn()
  },
  getMySections: vi.fn()
}));
vi.mock("./services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn(),
    getForPrint: vi.fn(),
    getTypeOptions: vi.fn(),
    getDefaultUnit: vi.fn(),
    create: vi.fn(),
    update: vi.fn()
  }
}));
vi.mock("../equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn()
  }
}));
vi.mock("../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: {
    getAll: vi.fn()
  }
}));
vi.mock("../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService", () => ({
  MaterialCategoryService: {
    getAll: vi.fn()
  }
}));
vi.mock("../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService", () => ({
  ReferenceOrganismService: {
    getAll: vi.fn()
  }
}));
vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getMediaProducts: vi.fn()
  }
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

const createdLot = lot({
  id: 91,
  materialType: "Chemical",
  materialName: "Acetone AR",
  batchNumber: "SUPPLIER-BATCH-77",
  stockId: "STK-2026-00091",
  code: "CHEM-001",
  location: "Microbiology Lab"
});

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  seedPermissions([PERMISSIONS.MATERIALS_MANAGE, PERMISSIONS.LABELS_REPRINT]);

  let rows = [lot({ id: 1, stockId: "STK-000123" }), lot({ id: 2, stockId: null, batchNumber: "LOT-OLD" })];
  vi.mocked(MaterialService.getAll).mockImplementation(async () => rows);
  vi.mocked(MaterialService.getForPrint).mockImplementation(async () => rows);
  vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Chemical"], custom: [] });
  vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
  vi.mocked(MaterialService.create).mockImplementation(async () => {
    rows = [...rows, createdLot];
    return createdLot;
  });
  vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
    { sectionId: 1, sectionCode: "MICRO", sectionName: "Micro", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
  ]);
  vi.mocked(getMySections).mockResolvedValue([
    { sectionId: 1, sectionCode: "MICRO", sectionName: "Micro", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
  ]);
  vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([]);
  vi.mocked(MaterialMasterService.getAll).mockResolvedValue([
    {
      id: 7,
      sectionId: 1,
      code: "CHEM-001",
      name: "Acetone AR",
      category: "Reagent",
      baseUnit: "Gram",
      isActive: true
    }
  ]);
  vi.mocked(MaterialCategoryService.getAll).mockResolvedValue([]);
  vi.mocked(ReferenceOrganismService.getAll).mockResolvedValue([]);
  vi.mocked(masterDataOptions.getMediaProducts).mockResolvedValue([]);
});
afterEach(() => {
  cleanup();
  localStorage.clear();
});

async function receiveChemicalLot(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: "Add to Stock" }));
  const dialog = await screen.findByRole("dialog");

  await waitFor(() => {
    expect(within(dialog).getByRole("combobox", { name: /Material Type/i }).textContent).toBe("Chemical");
  });

  await user.click(within(dialog).getByRole("combobox", { name: /Reagent Master Entry/i }));
  await user.click(await screen.findByRole("option", { name: /CHEM-001/i }));
  await user.click(within(dialog).getByRole("button", { name: /^Next$/ }));

  await user.type(within(dialog).getByLabelText(/Batch \/ Lot No\./i), "SUPPLIER-BATCH-77");
  await user.click(within(dialog).getByRole("combobox", { name: /Storage Location/i }));
  await user.click(await screen.findByRole("option", { name: "Microbiology Lab" }));
  await user.type(within(dialog).getByLabelText(/Quantity Received/i), "5");
  await user.click(within(dialog).getByRole("button", { name: /^Next$/ }));
  await user.click(within(dialog).getByRole("button", { name: /^Next$/ }));

  await user.click(await within(dialog).findByRole("button", { name: /^Save$/ }));
}

describe("MaterialsPage stock ID", () => {
  it("lists the stock ID as a column and in the print view, with a dash on older lots", async () => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter><MaterialsPage /></MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
    expect((await screen.findAllByText("STK-000123")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Stock ID").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("LOT-OLD", { selector: "span" })).toBeTruthy();
  });
});

describe("MaterialsPage label printing offer", () => {
  it("offers label printing after stock lot is created", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MaterialStockLot",
        entityId: 91,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter><MaterialsPage /></MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    await receiveChemicalLot(user);

    const printDialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(printDialog).findByText("STK-2026-00091")).toBeTruthy();
    expect(labelService.getStatus).toHaveBeenCalledWith(
      [{ entityType: "MaterialStockLot", entityId: 91 }],
      expect.any(AbortSignal)
    );
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MaterialStockLot",
        entityId: 91,
        canPrint: false,
        blockedReason: "Label printing is disabled.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter><MaterialsPage /></MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    await receiveChemicalLot(user);

    expect(await screen.findByText("Material added to stock as STK-2026-00091.")).toBeTruthy();
    await waitFor(() => {
      expect(vi.mocked(MaterialService.getAll).mock.calls.length).toBeGreaterThanOrEqual(2);
    });
    expect(screen.queryByRole("dialog", { name: "Print labels" })).toBeNull();
  }, 15000);
});
