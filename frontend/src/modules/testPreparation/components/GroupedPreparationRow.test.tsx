import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { GroupedPreparationRow } from "./GroupedPreparationRow";
import { PERMISSIONS } from "../../../routes/routes";
import type { GroupedPreparation } from "../services/SamplePreparationService";

const { authState } = vi.hoisted(() => ({ authState: { permissions: [] as string[] } }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: authState.permissions })
}));

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

  it("offers the confirm button to a role holding samples.prepare", () => {
    authState.permissions = [PERMISSIONS.SAMPLES_PREPARE];
    renderRow();
    expect(screen.getByText(/Confirm & Start Testing/)).toBeTruthy();
  });

  it("hides the confirm button from a role without it (QC Manager)", () => {
    authState.permissions = [];
    renderRow();
    expect(screen.queryByText(/Confirm & Start Testing/)).toBeNull();
  });
});
