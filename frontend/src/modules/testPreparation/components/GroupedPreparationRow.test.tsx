import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { GroupedPreparationRow } from "./GroupedPreparationRow";
import { PERMISSIONS } from "../../../routes/routes";
import type { GroupedPreparation } from "../services/SamplePreparationService";

const { authState } = vi.hoisted(() => ({ authState: { permissions: [] as string[] } }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: authState.permissions })
}));

const svc = vi.hoisted(() => ({ confirmBatch: vi.fn(), getLotOptions: vi.fn() }));
vi.mock("../services/SamplePreparationService", () => ({ SamplePreparationService: svc }));
vi.mock("../../../components/SignatureDialog", () => ({
  SignatureDialog: ({ open, onConfirm }: any) =>
    open ? <button onClick={() => onConfirm("pw")}>sign</button> : null
}));

const noLots = {
  configurationId: 1, needsMaterialMapping: false,
  diluent: { source: null, name: "", lotRequired: false, options: [] },
  neutralizer: { source: null, name: "", lotRequired: false, options: [] }
};
const withLots = {
  configurationId: 1, needsMaterialMapping: false,
  diluent: {
    source: "SolutionRecipe", name: "Buffer", lotRequired: true,
    options: [{ id: 101, code: "SP-101", expiresAt: null, usable: true, reason: null }]
  },
  neutralizer: {
    source: "MaterialEntry", name: "Tween", lotRequired: true,
    options: [{ id: 201, code: "RM-201", expiresAt: null, usable: true, reason: null }]
  }
};

beforeEach(() => {
  svc.confirmBatch.mockReset().mockResolvedValue({ totalRequested: 2, succeededCount: 2, skippedCount: 0, succeeded: [], skipped: [] });
  svc.getLotOptions.mockReset().mockResolvedValue(noLots);
});

const group: GroupedPreparation = {
  groupKey: "g1", configurationId: 1, itemId: 1, itemName: "Item A", approvalStatus: "Approved",
  amount: 10, technique: "PourPlate", filtrationVolume: null, washingVolume: null,
  diluent: "Buffer", neutralizer: "None", sampleCount: 1,
  samples: [{
    sampleId: 7, sampleReference: "S-7", displayName: "Item A", batchNumber: "B1",
    category: "RawMaterial", assignedAnalystId: null, assignedAnalystName: null
  }]
};

function renderRow() {
  render(
    <PinnedLightTheme>
      <GroupedPreparationRow group={group} onComplete={vi.fn()} />
    </PinnedLightTheme>
  );
  fireEvent.click(screen.getByLabelText("Expand group"));
}

describe("GroupedPreparationRow permission", () => {
  afterEach(() => cleanup());

  it("offers the confirm button to a role holding samples.prepare", async () => {
    authState.permissions = [PERMISSIONS.SAMPLES_PREPARE];
    renderRow();
    expect(await screen.findByText(/Confirm & Start Testing/)).toBeTruthy();
  });

  it("hides the confirm button from a role without it (QC Manager)", () => {
    authState.permissions = [];
    renderRow();
    expect(screen.queryByText(/Confirm & Start Testing/)).toBeNull();
  });
});

describe("GroupedPreparationRow lots", () => {
  afterEach(() => cleanup());

  it("loads lot options once for the group and sends one lot pair for every sample", async () => {
    authState.permissions = [PERMISSIONS.SAMPLES_PREPARE];
    svc.getLotOptions.mockResolvedValue(withLots);
    const onComplete = vi.fn();
    render(
      <PinnedLightTheme>
        <GroupedPreparationRow group={{ ...group, sampleCount: 2, samples: [...group.samples, { ...group.samples[0], sampleId: 8, sampleReference: "S-8" }] }} onComplete={onComplete} />
      </PinnedLightTheme>
    );
    fireEvent.click(screen.getByLabelText("Expand group"));
    await screen.findByRole("combobox", { name: /Diluent lot/ });
    expect(svc.getLotOptions).toHaveBeenCalledTimes(1);
    expect(svc.getLotOptions).toHaveBeenCalledWith({ configurationId: 1 });

    const btn = screen.getByRole("button", { name: /Confirm & Start Testing/ });
    expect((btn as HTMLButtonElement).disabled).toBe(true);

    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Diluent lot/ }));
    fireEvent.click(await screen.findByRole("option", { name: /SP-101/ }));
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Neutralizer lot/ }));
    fireEvent.click(await screen.findByRole("option", { name: /RM-201/ }));
    expect((btn as HTMLButtonElement).disabled).toBe(false);

    fireEvent.click(btn);
    fireEvent.click(await screen.findByText("sign"));
    await waitFor(() => expect(svc.confirmBatch).toHaveBeenCalledWith({
      sampleIds: [7, 8], configurationId: 1, password: "pw", diluentLotId: 101, neutralizerLotId: 201
    }));
    expect(svc.confirmBatch).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onComplete).toHaveBeenCalled());
  });
});
