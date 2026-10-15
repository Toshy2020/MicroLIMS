import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { RecordDestructionDialog } from "./RecordDestructionDialog";
import { BiologicalWasteService } from "../services/BiologicalWasteService";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Ana Lyst", username: "analyst", role: "Analyst", permissions: [] })
}));
vi.mock("../services/BiologicalWasteService", () => ({
  BiologicalWasteService: { getEligible: vi.fn(), record: vi.fn() }
}));
vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: { getEquipment: vi.fn().mockResolvedValue([{ id: 5, name: "Autoclave 2", code: "AUT-02" }]) }
}));
vi.mock("../../masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getAutoclavePrograms: vi.fn().mockResolvedValue([
      { id: 9, equipmentId: 5, programCode: "P09", programName: "Waste decontamination", loadType: "Biohazard Waste", temperature: 121, cycleTimeMinutes: 30 }
    ])
  }
}));

const service = vi.mocked(BiologicalWasteService);

const vial = (thawEventId: number, code: string) => ({
  thawEventId, cryovialId: 11, cryovialCode: code, organismName: "E. coli", thawedAt: "2026-10-13T08:00:00Z",
  thawedByName: "Ana Lyst", eligibleBecause: "Thawed over 24 h ago", usedInMediaLots: ["TSA-0001"]
});

async function choose(label: string, option: RegExp) {
  fireEvent.mouseDown(within((await screen.findByLabelText(label)).parentElement!).getByRole("combobox"));
  fireEvent.click(await screen.findByRole("option", { name: option }));
}

describe("RecordDestructionDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    service.getEligible.mockResolvedValue({ thawedVials: [vial(21, "ECO/01/26"), vial(22, "SAU/02/26")], withdrawnBatches: [] });
  });
  afterEach(cleanup);

  it("destroys every eligible thawed vial in bulk with other waste, under an electronic signature", async () => {
    service.record.mockResolvedValue({ reference: "BWD-00001" } as never);
    const onRecorded = vi.fn();
    render(<PinnedLightTheme><RecordDestructionDialog open onClose={vi.fn()} onRecorded={onRecorded} /></PinnedLightTheme>);

    await choose("Autoclave", /Autoclave 2/);
    await choose("Autoclave program", /P09: Waste decontamination/);
    fireEvent.change(screen.getByLabelText("Cycle number"), { target: { value: "118" } });
    fireEvent.click(await screen.findByLabelText("Select all thawed vials"));
    fireEvent.click(screen.getByRole("button", { name: "Add other waste" }));
    fireEvent.change(screen.getByLabelText("Description"), { target: { value: "Used GPT plates" } });
    fireEvent.change(screen.getByLabelText("Quantity"), { target: { value: "12" } });
    fireEvent.change(screen.getByLabelText("Unit"), { target: { value: "plates" } });

    fireEvent.click(screen.getByRole("button", { name: /Sign & record \(3 items\)/ }));
    fireEvent.change(await screen.findByLabelText(/password/i), { target: { value: "Secret1!" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(service.record).toHaveBeenCalled());
    expect(service.record.mock.calls[0][0]).toMatchObject({
      autoclaveEquipmentId: 5,
      autoclaveProgramId: 9,
      cycleNumber: "118",
      thawEventIds: [21, 22],
      withdrawnCryovialIds: [],
      otherItems: [{ description: "Used GPT plates", quantity: 12, unit: "plates" }],
      password: "Secret1!"
    });
    expect(onRecorded).toHaveBeenCalledWith("BWD-00001");
  });

  it("asks for the cycle details before signing", async () => {
    render(<PinnedLightTheme><RecordDestructionDialog open onClose={vi.fn()} onRecorded={vi.fn()} /></PinnedLightTheme>);
    await screen.findByText("ECO/01/26");

    fireEvent.click(screen.getByRole("button", { name: /Sign & record/ }));

    expect(await screen.findByText("Select the autoclave.")).toBeTruthy();
    expect(screen.queryByLabelText(/password/i)).toBeNull();
  });
});
