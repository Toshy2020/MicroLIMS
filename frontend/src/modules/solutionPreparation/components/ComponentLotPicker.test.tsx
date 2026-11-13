import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../theme";
import {
  labelService,
  type ScanMatch,
  type ScanResolveResponse
} from "../../../services/labelService";
import type { LotOption, SolutionPreparationComponentResponse } from "../types";
import { ComponentLotPicker } from "./ComponentLotPicker";

vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const component: SolutionPreparationComponentResponse = {
  id: 5,
  order: 1,
  kind: "Material",
  materialMasterEntryId: 12,
  entryCode: "NACL",
  entryName: "Sodium Chloride",
  recipeQuantity: 8.5,
  recipeUnit: "Gram",
  materialId: null,
  lotBatchNumber: null,
  lotUnit: null,
  quantityUsed: null
};

const lotOptions: LotOption[] = [
  {
    materialId: 101,
    batchNumber: "NACL-01",
    expiryDate: "2027-01-15",
    quantityRemaining: 500,
    unit: "Gram",
    usable: true,
    reason: null
  },
  {
    materialId: 102,
    batchNumber: "NACL-02",
    expiryDate: "2027-06-30",
    quantityRemaining: 250,
    unit: "Gram",
    usable: true,
    reason: null
  },
  {
    materialId: 103,
    batchNumber: "NACL-03",
    expiryDate: "2027-06-30",
    quantityRemaining: 0,
    unit: "Gram",
    usable: false,
    reason: "Depleted"
  }
];

function makeResolveResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "MaterialStockLot",
      id: 102,
      code: "MAT-NACL-02",
      displayName: "Sodium Chloride",
      status: "InStock",
      usable: true,
      blockedReason: null,
      navigationTarget: "/inventory/materials?id=102",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

function renderPicker(initialSelected: number | null = null) {
  const onSelectLotSpy = vi.fn();

  function Wrapper() {
    const [selectedMaterialId, setSelectedMaterialId] = useState<number | null>(initialSelected);
    const [quantityUsed, setQuantityUsed] = useState<number | null>(null);
    return (
      <ComponentLotPicker
        component={component}
        lotOptions={lotOptions}
        selectedMaterialId={selectedMaterialId}
        quantityUsed={quantityUsed}
        onSelectLot={(id) => {
          onSelectLotSpy(id);
          setSelectedMaterialId(id);
        }}
        onChangeQuantity={setQuantityUsed}
      />
    );
  }

  render(
    <ThemeProvider theme={lightTheme}>
      <Wrapper />
    </ThemeProvider>
  );

  return { onSelectLotSpy };
}

describe("ComponentLotPicker", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("scanning an offered stock lot selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ id: 102, code: "MAT-NACL-02" })
    );

    const { onSelectLotSpy } = renderPicker();
    await user.type(
      screen.getByLabelText("Scan stock lot for Sodium Chloride"),
      "MAT:MAT-NACL-02{Enter}"
    );

    await waitFor(() => {
      expect(onSelectLotSpy).toHaveBeenCalledWith(102);
    });
    expect(screen.getByRole("combobox", { name: /Select Lot/ }).textContent).toContain("NACL-02");
    expect((screen.getByLabelText("Quantity Used") as HTMLInputElement).disabled).toBe(false);
    expect(screen.getByRole("status").textContent).toContain("Selected MAT-NACL-02");
  });

  it("scanning a stock lot that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ id: 999, code: "MAT-OTHER-999" })
    );

    const { onSelectLotSpy } = renderPicker(101);
    await user.type(
      screen.getByLabelText("Scan stock lot for Sodium Chloride"),
      "MAT:MAT-OTHER-999{Enter}"
    );

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("MAT-OTHER-999 is not available for this step.");
    expect(onSelectLotSpy).not.toHaveBeenCalled();
    expect(screen.getByRole("combobox", { name: /Select Lot/ }).textContent).toContain("NACL-01");
  });

  it("scanning a lot the picker lists as blocked does not select it", async () => {
    const user = userEvent.setup();
    // The scan endpoint may still call the lot usable; the picker's own list blocks it.
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ id: 103, code: "MAT-NACL-03" })
    );

    const { onSelectLotSpy } = renderPicker(101);
    await user.type(
      screen.getByLabelText("Scan stock lot for Sodium Chloride"),
      "MAT:MAT-NACL-03{Enter}"
    );

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("MAT-NACL-03 is not available for this step.");
    expect(onSelectLotSpy).not.toHaveBeenCalled();
  });
});
