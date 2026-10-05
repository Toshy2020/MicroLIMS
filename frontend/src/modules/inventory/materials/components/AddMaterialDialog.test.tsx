import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { AddMaterialDialog } from "./AddMaterialDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";

vi.mock("../services/MaterialService", () => ({
  MaterialService: {
    getTypeOptions: vi.fn(),
    getDefaultUnit: vi.fn()
  }
}));
vi.mock("../../equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: () => Promise.resolve([]) }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: () => Promise.resolve([]) }
}));
vi.mock("../../../../services/laboratorySectionService", () => ({
  getMySections: () => Promise.resolve([{ sectionId: 1, sectionCode: "MICRO", sectionName: "Micro" }])
}));
vi.mock("../../../../components/MediaProductPicker", () => ({ MediaProductPicker: () => null }));
vi.mock("../../../../components/OrganismPicker", () => ({ OrganismPicker: () => null }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});


describe("AddMaterialDialog initial type", () => {
  it("opens a new micro lot on the lab's first built-in type", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Supplement", "DehydratedMedia"], custom: [] });
    vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalled());
    await waitFor(() => expect((screen.getByRole("combobox", { name: /Material Type/ }) as HTMLInputElement).value).toBe("Supplement"), { timeout: 4000 });
  }, 20000);

  it("keeps the lot's own type when editing", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["DehydratedMedia", "Supplement"] as never[], custom: [] });
    vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
    const item = {
      id: 5, sectionId: 1, materialType: "Supplement", customType: null, materialName: "S", manufacturerName: "",
      batchNumber: "B1", receivingDate: "2026-01-01", expiryDate: null, code: null, location: "Room",
      mediaProductId: null, organismId: null, atccNumber: null, quantityReceived: 1, quantityRemaining: 1,
      unit: "Gram", minimumStockLevel: null, status: "InStock"
    } as MaterialItem;
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={item} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(1), { timeout: 4000 });
    await new Promise((r) => setTimeout(r, 50));
    expect((screen.getByRole("combobox", { name: /Material Type/ }) as HTMLInputElement).value).toBe("Supplement");
    expect(MaterialService.getDefaultUnit).not.toHaveBeenCalled();
  }, 20000);
});
