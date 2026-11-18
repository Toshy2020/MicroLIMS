import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../../theme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { labelService } from "../../../../services/labelService";
import { SolutionPreparationService } from "../../../solutionPreparation/services/SolutionPreparationService";
import { SamplePreparationService } from "../../../testPreparation/services/SamplePreparationService";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";
import { MaterialLotDetailsDialog } from "./MaterialLotDetailsDialog";

vi.mock("../../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../services/MaterialService", () => ({
  MaterialService: {
    getDocuments: vi.fn(),
    getCOAEligibility: vi.fn()
  }
}));

vi.mock("../../../solutionPreparation/services/SolutionPreparationService", () => ({
  SolutionPreparationService: {
    getByLot: vi.fn()
  }
}));

vi.mock("../../../testPreparation/services/SamplePreparationService", () => ({
  SamplePreparationService: {
    getWhereUsed: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const sampleMaterial: MaterialItem = {
  id: 45,
  sectionId: 1,
  materialType: "Chemical",
  customType: null,
  materialName: "Acetone AR",
  manufacturerName: "Merck",
  stockId: "STK-2026-00045",
  batchNumber: "SUPPLIER-LOT-99",
  receivingDate: "2026-10-01",
  expiryDate: "2028-10-01",
  code: "CHEM-001",
  location: "Shelf 2",
  mediaProductId: null,
  organismId: null,
  atccNumber: null,
  quantityReceived: 1000,
  quantityRemaining: 750,
  unit: "Milliliter",
  minimumStockLevel: 100,
  status: "InStock"
};

describe("MaterialLotDetailsDialog label printing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MATERIALS_MANAGE, PERMISSIONS.LABELS_REPRINT]);

    vi.mocked(MaterialService.getDocuments).mockResolvedValue([]);
    vi.mocked(SolutionPreparationService.getByLot).mockResolvedValue([]);
    vi.mocked(SamplePreparationService.getWhereUsed).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MaterialStockLot",
        entityId: 45,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("shows the print label button with the record's code", async () => {
    const user = userEvent.setup();
    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <MaterialLotDetailsDialog open={true} material={sampleMaterial} onClose={vi.fn()} />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    const printBtn = await screen.findByRole("button", { name: "Print label" });
    await waitFor(() => {
      expect(printBtn.hasAttribute("disabled")).toBe(false);
    });

    await user.click(printBtn);

    const dialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(dialog).findByText("STK-2026-00045")).toBeTruthy();
    expect(labelService.getStatus).toHaveBeenCalledWith(
      [{ entityType: "MaterialStockLot", entityId: 45 }],
      expect.any(AbortSignal)
    );
  });
});

describe("MaterialLotDetailsDialog calendar dates", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MATERIALS_MANAGE]);
    vi.mocked(MaterialService.getDocuments).mockResolvedValue([]);
    vi.mocked(SolutionPreparationService.getByLot).mockResolvedValue([]);
    vi.mocked(SamplePreparationService.getWhereUsed).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.unstubAllEnvs();
  });

  it.each(["America/Los_Angeles", "Pacific/Honolulu"])("shows the expiry as the same calendar day in %s", async (zone) => {
    vi.stubEnv("TZ", zone);
    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <MaterialLotDetailsDialog open={true} material={{ ...sampleMaterial, expiryDate: "2028-10-01T00:00:00Z" }} onClose={vi.fn()} />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );
    const expiry = (await screen.findByText("Expiry")).parentElement as HTMLElement;
    expect(expiry.textContent).toContain("01 Oct 2028");
    expect(expiry.textContent).not.toMatch(/\d{2}:\d{2}/);
  });
});
