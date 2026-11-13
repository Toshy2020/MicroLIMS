import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../../theme";
import {
  labelService,
  type ScanMatch,
  type ScanResolveResponse
} from "../../../../services/labelService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import {
  EquipmentConfigurationService,
  type EquipmentResponse
} from "../../masterDataSimple/services/EquipmentConfigurationService";
import { MediaPreparationService } from "../services/MediaPreparationService";
import { MediaPreparationDialog } from "./MediaPreparationDialog";

vi.mock("../../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getEquipment: vi.fn()
  }
}));

vi.mock("../../../inventory/materials/services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getAutoclavePrograms: vi.fn()
  }
}));

vi.mock("../services/MediaPreparationService", () => ({
  MediaPreparationService: {
    getUsableSolutions: vi.fn(),
    prepare: vi.fn()
  }
}));

const dehydratedMediaItems: MaterialItem[] = [
  {
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
  }
];

const autoclaveOptions: EquipmentResponse[] = [
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
];

function makeResolveResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "MaterialStockLot",
      id: 301,
      code: "MAT-DM-001",
      displayName: "Tryptic Soy Agar Powder",
      status: "InStock",
      usable: true,
      blockedReason: null,
      navigationTarget: "/inventory/materials?id=301",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

function renderDialog() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <MediaPreparationDialog open={true} onClose={vi.fn()} onSuccess={vi.fn()} />
    </ThemeProvider>
  );
}

describe("MediaPreparationDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue(autoclaveOptions);
    vi.mocked(MaterialService.getAll).mockImplementation(async (type?: string) =>
      type === "DehydratedMedia" ? dehydratedMediaItems : []
    );
    vi.mocked(MediaPreparationService.getUsableSolutions).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
  });

  it("scanning an offered dehydrated media stock lot selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 301, code: "MAT-DM-001" })
    );

    renderDialog();
    await screen.findByLabelText("Scan dehydrated media stock lot");
    await waitFor(() => {
      expect(MaterialService.getAll).toHaveBeenCalledWith("DehydratedMedia");
    });

    await user.type(screen.getByLabelText("Scan dehydrated media stock lot"), "MAT:MAT-DM-001{Enter}");

    await waitFor(() => {
      expect(
        screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }).textContent
      ).toContain("Tryptic Soy Agar Powder");
    });
    expect(screen.getByText(/Manufacturer: Oxoid/)).toBeTruthy();
    expect(screen.getByRole("status").textContent).toContain("Selected MAT-DM-001");
  });

  it("scanning a dehydrated media stock lot that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 999, code: "MAT-DM-999" })
    );

    renderDialog();
    await screen.findByLabelText("Scan dehydrated media stock lot");
    await waitFor(() => {
      expect(MaterialService.getAll).toHaveBeenCalledWith("DehydratedMedia");
    });

    await user.type(screen.getByLabelText("Scan dehydrated media stock lot"), "MAT:MAT-DM-999{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("MAT-DM-999 is not available for this step.");
    expect(
      screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }).textContent
    ).toContain("Dehydrated Media Stock (Inventory) *");
  });

  it("scanning an offered autoclave selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        entityType: "EquipmentAsset",
        id: 801,
        linkedEquipmentId: 41,
        code: "AC-01",
        displayName: "Autoclave Alpha"
      })
    );

    renderDialog();
    await screen.findByLabelText("Scan autoclave");
    await waitFor(() => {
      expect(masterDataOptions.getEquipment).toHaveBeenCalledWith("Autoclave");
    });

    await user.type(screen.getByLabelText("Scan autoclave"), "EQ:AC-01{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("AC-01");
      expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true);
    });
    expect(screen.getByRole("status").textContent).toContain("Selected AC-01");
  });

  it("scanning an autoclave that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        entityType: "EquipmentAsset",
        id: 802,
        linkedEquipmentId: 99,
        code: "AC-99",
        displayName: "Autoclave 99"
      })
    );

    renderDialog();
    await screen.findByLabelText("Scan autoclave");
    await waitFor(() => {
      expect(masterDataOptions.getEquipment).toHaveBeenCalledWith("Autoclave");
    });

    await user.type(screen.getByLabelText("Scan autoclave"), "EQ:AC-99{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("AC-99 is not available for this step.");
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("Select Autoclave *");
    expect(EquipmentConfigurationService.getAutoclavePrograms).not.toHaveBeenCalled();
  });
});
