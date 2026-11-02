import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SystemSuitabilityPanel } from "./SystemSuitabilityPanel";
import type { HplcRunDto } from "../types";

vi.mock("../../inventory/materials/services/MaterialService", () => ({
  MaterialService: { getUsableReferenceStandards: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../services/HplcWorkspaceService", () => ({ HplcWorkspaceService: {} }));
vi.mock("../useTechnique", () => ({ useTechnique: () => ({ label: "HPLC" }) }));
vi.mock("../../../components/SignatureDialog", () => ({ SignatureDialog: () => null }));

const runOf = (patch: Partial<HplcRunDto>) =>
  ({
    id: 1,
    status: "Open",
    evidence: [],
    samples: [],
    mobilePhases: [],
    canConfirmSst: false,
    canAssignSamples: false,
    resultMode: "Assay",
    sst: {
      id: 1,
      status: "Pending",
      analytes: [{ id: 1, hplcMethodAnalyteId: 10, analyteName: "Principal", injections: [] }]
    },
    ...patch
  }) as unknown as HplcRunDto;

const renderPanel = (run: HplcRunDto) =>
  render(
    <PinnedLightTheme>
      <SystemSuitabilityPanel run={run} method={null} canOperate onRunUpdated={vi.fn()} />
    </PinnedLightTheme>
  );

describe("SystemSuitabilityPanel reads the mode from the run", () => {
  afterEach(cleanup);

  it("does not demand a weight on a related-substances run while the method is not loaded", async () => {
    renderPanel(runOf({ resultMode: "RelatedSubstances" }));
    expect(await screen.findByText("Not required for this method (optional)")).toBeTruthy();
  });

  it("demands a weight on an assay run", async () => {
    renderPanel(runOf({ resultMode: "Assay" }));
    expect(await screen.findByText("Weighed on analytical balance")).toBeTruthy();
  });

  it("shows the internal standard name from the run", async () => {
    renderPanel(runOf({ internalStandardName: "Benzoic acid" }));
    expect((await screen.findAllByText(/Benzoic acid/)).length).toBeGreaterThan(0);
  });
});
