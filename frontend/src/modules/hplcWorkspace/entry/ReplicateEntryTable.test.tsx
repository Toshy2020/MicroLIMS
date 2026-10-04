import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReplicateEntryTable, replicatesComplete } from "./ReplicateEntryTable";
import type { HplcMethodWeightDto, HplcReplicateDto } from "../types";

afterEach(cleanup);

const mockWeights: HplcMethodWeightDto[] = [
  {
    hplcMethodAnalyteId: 10,
    analyteName: "Methanol",
    theoreticalWeightStdMg: 0,
    theoreticalWeightTestMg: 0
  }
];

const mockReplicate: HplcReplicateDto = {
  replicateNo: 1,
  actualWeightMg: 100.5,
  responses: [
    {
      hplcMethodAnalyteId: 10,
      response: 0
    }
  ]
};

describe("ReplicateEntryTable", () => {
  it("shows ResidualSolvents column headers: 'Sample weight (mg) *' and '{analyte} Area *'", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={true}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Sample weight (mg) *")).toBeTruthy();
    expect(screen.getByText("Methanol Area *")).toBeTruthy();
    expect(screen.queryByText("Actual Weight *")).toBeNull();
  });

  it("shows Assay mode headers when isResidualSolvents is false", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={false}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Actual Weight *")).toBeTruthy();
    expect(screen.getByText("Methanol Response *")).toBeTruthy();
    expect(screen.queryByText("Sample weight (mg) *")).toBeNull();
  });

  it("displays '0 = not detected' helper when response is 0 in residual solvents mode", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={true}
        />
      </PinnedLightTheme>
    );

    const matches = screen.getAllByText(/0 = not detected/);
    expect(matches.length).toBeGreaterThan(0);
  });

  describe("replicatesComplete", () => {
    it("permits response 0 in ResidualSolvents mode", () => {
      const repZero: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }]
      };
      expect(replicatesComplete(mockWeights, [repZero], true)).toBe(true);
    });

    it("requires response > 0 in Assay mode", () => {
      const repZero: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }]
      };
      expect(replicatesComplete(mockWeights, [repZero], false)).toBe(false);

      const repPositive: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }]
      };
      expect(replicatesComplete(mockWeights, [repPositive], false)).toBe(true);
    });

    it("requires actualWeightMg > 0 in both modes", () => {
      const repNoWeight: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }]
      };
      expect(replicatesComplete(mockWeights, [repNoWeight], true)).toBe(false);
      expect(replicatesComplete(mockWeights, [repNoWeight], false)).toBe(false);
    });
  });
});
