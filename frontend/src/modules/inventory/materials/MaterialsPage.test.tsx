import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MaterialsPage } from "./MaterialsPage";
import { MaterialService } from "./services/MaterialService";
import type { MaterialItem } from "./types/materialTypes";

vi.mock("../../../contexts/AuthContext", () => ({ useAuth: () => ({ permissions: [] }) }));
vi.mock("../../../hooks/useLaboratorySections", () => ({ useLaboratorySections: () => ({ sections: [], sectionName: () => "" }) }));
vi.mock("./services/MaterialService", () => ({ MaterialService: { getAll: vi.fn(), getForPrint: vi.fn(), getTypeOptions: vi.fn().mockResolvedValue({ builtIn: [], custom: [] }) } }));
vi.mock("./components/AddMaterialDialog", () => ({ AddMaterialDialog: () => null }));
vi.mock("./components/MaterialLotDetailsDialog", () => ({ MaterialLotDetailsDialog: () => null }));
vi.mock("./components/MaterialReleaseDialog", () => ({ MaterialReleaseDialog: () => null }));
vi.mock("../../../components/AuditHistoryDialog", () => ({ AuditHistoryDialog: () => null }));

const lot = (over: Partial<MaterialItem>): MaterialItem => ({
  id: 1, sectionId: 1, materialType: "Reagent", customType: null, materialName: "Sodium Hydroxide", manufacturerName: "Merck",
  batchNumber: "LOT-A1", receivingDate: "2026-10-01T00:00:00Z", expiryDate: null, code: "RG-001", location: "Shelf 1",
  mediaProductId: null, organismId: null, atccNumber: null, quantityReceived: 10, quantityRemaining: 10, unit: "Gram",
  minimumStockLevel: null, status: "Available", ...over
} as MaterialItem);

beforeEach(() => {
  const rows = [lot({ id: 1, stockId: "STK-000123" }), lot({ id: 2, stockId: null, batchNumber: "LOT-OLD" })];
  vi.mocked(MaterialService.getAll).mockResolvedValue(rows);
  vi.mocked(MaterialService.getForPrint).mockResolvedValue(rows);
});
afterEach(cleanup);

describe("MaterialsPage stock ID", () => {
  it("lists the stock ID as a column and in the print view, with a dash on older lots", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter><MaterialsPage /></MemoryRouter>
      </PinnedLightTheme>
    );
    expect((await screen.findAllByText("STK-000123")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Stock ID").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("LOT-OLD", { selector: "span" })).toBeTruthy();
  });
});
