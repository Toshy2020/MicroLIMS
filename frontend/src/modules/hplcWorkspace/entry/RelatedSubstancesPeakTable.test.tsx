import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { RelatedSubstancesPeakTable, rsReplicatesComplete } from "./RelatedSubstancesPeakTable";
import type { HplcMethodSnapshot, HplcMethodWeightDto, HplcReplicateDto } from "../types";

afterEach(cleanup);

const mockMethodWeights: HplcMethodWeightDto[] = [
  {
    hplcMethodAnalyteId: 10,
    analyteName: "API Principal",
    theoreticalWeightStdMg: 0,
    theoreticalWeightTestMg: 0
  }
];

const mockMethod: HplcMethodSnapshot = {
  id: 1,
  name: "Related Substances by HPLC",
  abbreviation: "RS-HPLC",
  version: 1,
  sectionId: 1,
  effectiveDate: "2026-01-01",
  isActive: true,
  columnDesignation: "L1",
  columnLengthMm: 150,
  columnInternalDiameterMm: 4.6,
  flowRateMlPerMin: 1.0,
  elutionMode: "Isocratic",
  detectorType: "UV",
  injectionVolumeUl: 10,
  runTimeMin: 20,
  diluentSolutionId: 1,
  technique: "Hplc",
  resultMode: "RelatedSubstances",
  referenceSolutionPercent: 0.1,
  disregardLimitPercent: 0.05,
  impurities: [
    {
      id: 101,
      displayOrder: 1,
      name: "Impurity A",
      relativeRetention: 0.8,
      correctionFactor: 1.0
    },
    {
      id: 102,
      displayOrder: 2,
      name: "Impurity B",
      relativeRetention: 1.2,
      correctionFactor: 0.7
    }
  ],
  analytes: [
    {
      id: 10,
      displayOrder: 1,
      name: "API Principal",
      standardEntryId: 1,
      standardEntryCode: "RS-API",
      standardInjections: 5,
      theoreticalWeightStdMg: 0,
      theoreticalWeightTestMg: 0
    }
  ],
  mobilePhases: [],
  gradientSteps: []
};

const mockReplicate: HplcReplicateDto = {
  replicateNo: 1,
  actualWeightMg: 0,
  responses: [
    {
      hplcMethodAnalyteId: 10,
      response: 10000
    }
  ],
  peaks: [
    {
      hplcMethodImpurityId: 101,
      relativeRetention: 0.8,
      area: 1200
    }
  ]
};

describe("RelatedSubstancesPeakTable", () => {
  it("renders reference solution area and named impurities without a sample weight column", () => {
    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getAllByText(/Reference solution area/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Impurity A/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Impurity B/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/Sample weight/i)).toBeNull();
    expect(screen.queryByText(/Actual Weight/i)).toBeNull();
  });

  it("shows error alert and disables Add Replicate when method snapshot is missing principal analyte", () => {
    const invalidMethod: HplcMethodSnapshot = {
      ...mockMethod,
      analytes: []
    };

    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={invalidMethod}
          methodWeights={[]}
          replicates={[]}
          onChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Method snapshot is missing the principal analyte.")).toBeTruthy();
    const addBtn = screen.getByRole("button", { name: /add replicate/i });
    expect(addBtn.hasAttribute("disabled")).toBe(true);
  });

  it("edits a named peak and triggers onChange with updated area", () => {
    const handleChange = vi.fn();
    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[mockReplicate]}
          onChange={handleChange}
        />
      </PinnedLightTheme>
    );

    const namedInput = screen.getByLabelText(/impurity a \(rrt 0.8\) area/i);
    fireEvent.change(namedInput, { target: { value: "3500" } });

    expect(handleChange).toHaveBeenCalled();
    const updated = handleChange.mock.calls[handleChange.mock.calls.length - 1][0];
    const impPeak = updated[0].peaks?.find((p: { hplcMethodImpurityId?: number | null }) => p.hplcMethodImpurityId === 101);
    expect(impPeak?.area).toBe(3500);
  });

  it("adds and removes unspecified peak rows with stable keys", () => {
    const handleChange = vi.fn();
    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[mockReplicate]}
          onChange={handleChange}
        />
      </PinnedLightTheme>
    );

    const addButton = screen.getByRole("button", { name: /add unspecified peak/i });
    fireEvent.click(addButton);

    expect(handleChange).toHaveBeenCalled();
    const updatedReplicates: HplcReplicateDto[] = handleChange.mock.calls[0][0];
    const peaks = updatedReplicates[0].peaks ?? [];
    expect(peaks.length).toBe(2);
    expect(peaks[1].hplcMethodImpurityId).toBeNull();
    expect(peaks[1].clientKey).toBeDefined();

    // Now test removal
    const repWithUnspecified: HplcReplicateDto = {
      ...mockReplicate,
      peaks: [
        { hplcMethodImpurityId: 101, relativeRetention: 0.8, area: 1200 },
        { clientKey: "test-client-key-1", hplcMethodImpurityId: null, relativeRetention: 1.5, area: 600 }
      ]
    };

    cleanup();
    const handleRemoveChange = vi.fn();
    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[repWithUnspecified]}
          onChange={handleRemoveChange}
        />
      </PinnedLightTheme>
    );

    const deleteButton = screen.getByLabelText(/remove unspecified peak 1/i);
    fireEvent.click(deleteButton);

    expect(handleRemoveChange).toHaveBeenCalled();
    const remainingPeaks: HplcReplicateDto[] = handleRemoveChange.mock.calls[0][0];
    expect(remainingPeaks[0].peaks?.length).toBe(1);
    expect(remainingPeaks[0].peaks?.[0].hplcMethodImpurityId).toBe(101);
  });

  it("shows 'Peak areas cannot be negative.' when a negative area is entered", () => {
    const repWithNegative: HplcReplicateDto = {
      ...mockReplicate,
      peaks: [
        {
          hplcMethodImpurityId: 101,
          relativeRetention: 0.8,
          area: -50
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[repWithNegative]}
          onChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    const errorElements = screen.getAllByText("Peak areas cannot be negative.");
    expect(errorElements.length).toBeGreaterThan(0);
  });

  it("shows error when unspecified peak RRT is negative or zero", () => {
    const repWithInvalidRrt: HplcReplicateDto = {
      ...mockReplicate,
      peaks: [
        { clientKey: "unsp-1", hplcMethodImpurityId: null, relativeRetention: -0.5, area: 100 }
      ]
    };

    render(
      <PinnedLightTheme>
        <RelatedSubstancesPeakTable
          method={mockMethod}
          methodWeights={mockMethodWeights}
          replicates={[repWithInvalidRrt]}
          onChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Must be > 0")).toBeTruthy();
  });

  describe("rsReplicatesComplete", () => {
    it("requires reference area > 0 for the principal analyte and peak areas >= 0", () => {
      const repZeroRef: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }],
        peaks: [{ hplcMethodImpurityId: 101, relativeRetention: 0.8, area: 100 }]
      };
      expect(rsReplicatesComplete([repZeroRef], 10)).toBe(false);

      const repNegativePeak: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 10000 }],
        peaks: [{ hplcMethodImpurityId: 101, relativeRetention: 0.8, area: -10 }]
      };
      expect(rsReplicatesComplete([repNegativePeak], 10)).toBe(false);

      const repValid: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 10000 }],
        peaks: [
          { hplcMethodImpurityId: 101, relativeRetention: 0.8, area: 100 },
          { hplcMethodImpurityId: null, relativeRetention: null, area: 0 }
        ]
      };
      expect(rsReplicatesComplete([repValid], 10)).toBe(true);
    });

    it("returns false if principalAnalyteId is missing or wrong analyte responded", () => {
      const rep: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 99, response: 10000 }],
        peaks: []
      };
      expect(rsReplicatesComplete([rep], null)).toBe(false);
      expect(rsReplicatesComplete([rep], 10)).toBe(false);
    });

    it("requires unspecified RRT to be > 0 when given", () => {
      const repZeroRrt: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 10000 }],
        peaks: [{ hplcMethodImpurityId: null, relativeRetention: 0, area: 50 }]
      };
      expect(rsReplicatesComplete([repZeroRrt], 10)).toBe(false);

      const repNegativeRrt: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 10000 }],
        peaks: [{ hplcMethodImpurityId: null, relativeRetention: -1.2, area: 50 }]
      };
      expect(rsReplicatesComplete([repNegativeRrt], 10)).toBe(false);

      const repPositiveRrt: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 10000 }],
        peaks: [{ hplcMethodImpurityId: null, relativeRetention: 1.15, area: 50 }]
      };
      expect(rsReplicatesComplete([repPositiveRrt], 10)).toBe(true);
    });
  });
});
