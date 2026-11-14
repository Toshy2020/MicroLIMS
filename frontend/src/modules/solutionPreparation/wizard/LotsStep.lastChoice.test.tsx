import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { entryDefaultsService } from "../../../services/entryDefaultsService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { LAST_CHOICE_CAPTION } from "../../../components/entry/LastChoiceField";
import { EquipmentConfigurationService } from "../../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import type { SolutionPreparationResponse } from "../types";
import { LotsStep } from "./LotsStep";

vi.mock("../../../services/entryDefaultsService", () => ({ entryDefaultsService: { get: vi.fn() } }));
vi.mock("../../../services/masterDataOptions", () => ({ masterDataOptions: { getEquipment: vi.fn() } }));
vi.mock("../../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: { getAutoclavePrograms: vi.fn() }
}));

const recipe = {
  id: 3, name: "Buffer", type: "Buffer", shelfLifeValue: 1, shelfLifeUnit: "Month", storageCondition: "RT",
  finalVolumeMl: 1000, instructions: "", sterilizationRequired: true, components: []
};

function preparation(over: Partial<SolutionPreparationResponse> = {}): SolutionPreparationResponse {
  return {
    id: 1, version: 1, sectionId: 1, code: null, solutionMasterId: 3, solutionMasterName: "Buffer", type: "Buffer",
    hplcMethodId: null, hplcMethodAbbreviation: null, status: "InProgress", effectiveStatus: "InProgress",
    startedByUserId: 7, startedByUserName: "Alice", startedAt: "2026-11-14T08:00:00Z",
    preparedByUserId: null, preparedByUserName: null, preparedAt: null, expiresAt: null,
    finalVolumeMl: null, measuredPh: null, recipeSnapshotJson: JSON.stringify(recipe), components: [], statusHistory: [],
    ...over
  } as unknown as SolutionPreparationResponse;
}

function renderStep(prep = preparation()) {
  return renderWithTheme(
    <MemoryRouter>
      <LotsStep preparation={prep} onSave={vi.fn()} onNext={vi.fn()} />
    </MemoryRouter>,
    { bench: false }
  );
}

beforeEach(() => {
  vi.mocked(masterDataOptions.getEquipment).mockResolvedValue([{ id: 41, code: "AC-01", name: "Autoclave Alpha" }] as never);
  vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);
  vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [] });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("LotsStep autoclave last choice", () => {
  it("pre-selects the autoclave the server offers for this solution, labelled", async () => {
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [{ field: "autoclaveEquipmentId", id: 41, text: null, label: "AC-01", lastUsedAtUtc: "2026-11-13T08:00:00Z" }]
    });
    renderStep();
    expect(await screen.findByText(LAST_CHOICE_CAPTION)).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("AC-01");
    expect(entryDefaultsService.get).toHaveBeenCalledWith(
      { context: "solutionPreparation", solutionMasterId: 3 },
      expect.anything()
    );
    await waitFor(() => expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true));
  });

  it("offers nothing and asks nothing when the preparation already has a saved autoclave", async () => {
    renderStep(preparation({ autoclaveEquipmentId: 41 }));
    await screen.findByRole("combobox", { name: "Autoclave" });
    expect(entryDefaultsService.get).not.toHaveBeenCalled();
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
  });

  it("leaves the autoclave empty when the server offers nothing", async () => {
    renderStep();
    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).not.toContain("AC-");
  });
});
