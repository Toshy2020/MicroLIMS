import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { HplcSampleEntryPage, buildReplicatePayload } from "./HplcSampleEntryPage";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { TestWorkflowService } from "../../testingWorkspace/services/TestWorkflowService";
import type { HplcReplicateDto, HplcSampleEntryDto } from "../types";

afterEach(cleanup);

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: ["Hplc.Operate"], role: "Analyst" })
}));

vi.mock("../useTechnique", () => ({
  useTechnique: () => ({ basePath: "/hplc", label: "HPLC" })
}));

vi.mock("../services/HplcWorkspaceService", () => ({
  HplcWorkspaceService: {
    getSampleEntry: vi.fn(),
    getRun: vi.fn().mockResolvedValue({ id: 10, hplcMethodId: 1 }),
    saveReplicates: vi.fn()
  }
}));

vi.mock("../../testingWorkspace/services/TestWorkflowService", () => ({
  TestWorkflowService: {
    getMoisture: vi.fn()
  }
}));

const baseSampleEntry: HplcSampleEntryDto = {
  runSampleId: 100,
  hplcRunId: 10,
  runCode: "RUN-001",
  sstCode: "SST-001",
  sstStatus: "Passed",
  testOrderId: 7,
  sampleNumber: "SMP-001",
  testCode: "TEST-01",
  status: "Assigned",
  basis: "As Is",
  moistureRole: null,
  methodWeights: [],
  replicates: [],
  preview: [],
  official: [],
  evidence: [],
  editable: true,
  submitted: false,
  canSubmit: true
};

describe("HplcSampleEntryPage - buildReplicatePayload", () => {
  it("builds the full HplcReplicateInput for RS with principal ID, peaks, actualWeightMg 0, and drops zero-area unspecified rows", () => {
    const rsReplicates: HplcReplicateDto[] = [
      {
        replicateNo: 1,
        actualWeightMg: 50.5, // should be forced to 0 in RS mode
        responses: [{ hplcMethodAnalyteId: 10, response: 12000 }],
        internalStandardResponse: 999,
        peaks: [
          { hplcMethodImpurityId: 101, relativeRetention: 0.8, area: 150 }, // named peak, keep
          { hplcMethodImpurityId: 102, relativeRetention: 1.2, area: 0 }, // named peak with 0 area, keep
          { clientKey: "unsp-1", hplcMethodImpurityId: null, relativeRetention: 1.4, area: 250 }, // unspecified with area > 0, keep
          { clientKey: "unsp-2", hplcMethodImpurityId: null, relativeRetention: 1.6, area: 0 } // unspecified with area 0, drop!
        ]
      }
    ];

    // Case 1: RS mode, no internal standard
    const payloadNoIS = buildReplicatePayload(rsReplicates, true, false);
    expect(payloadNoIS).toEqual([
      {
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 12000 }],
        peaks: [
          { hplcMethodImpurityId: 101, relativeRetention: 0.8, area: 150 },
          { hplcMethodImpurityId: 102, relativeRetention: 1.2, area: 0 },
          { hplcMethodImpurityId: null, relativeRetention: 1.4, area: 250 }
        ]
      }
    ]);
    expect(payloadNoIS[0].internalStandardResponse).toBeUndefined();

    // Case 2: RS mode, with internal standard
    const payloadWithIS = buildReplicatePayload(rsReplicates, true, true);
    expect(payloadWithIS[0].internalStandardResponse).toBe(999);
  });
});

const renderPage = () =>
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/hplc/1/run/10/sample/100"]}>
        <Routes>
          <Route path="/hplc/:instrumentId/run/:runId/sample/:runSampleId" element={<HplcSampleEntryPage />} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );

describe("HplcSampleEntryPage - moisture line display", () => {
  it("shows the moisture line when moistureRole is set and not RS/residual", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      moistureRole: "LossOnDrying",
      resultMode: "Assay"
    });
    vi.mocked(TestWorkflowService.getMoisture).mockResolvedValue({
      role: "LossOnDrying",
      percent: 0.5,
      testCode: "LOD-101"
    });

    renderPage();

    await waitFor(() => {
      expect(TestWorkflowService.getMoisture).toHaveBeenCalledWith(7);
      expect(screen.getByText("Loss on drying 0.5 % (from LOD-101)")).toBeTruthy();
    });
  });

  it("does not fetch or show the moisture line when moistureRole is null", async () => {
    vi.mocked(TestWorkflowService.getMoisture).mockClear();
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      moistureRole: null,
      resultMode: "Assay"
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("SMP-001")).toBeTruthy();
    });

    expect(TestWorkflowService.getMoisture).not.toHaveBeenCalled();
    expect(screen.queryByText(/Loss on drying/i)).toBeNull();
  });

  it("does not fetch or show the moisture line on RelatedSubstances runs even if moistureRole is present", async () => {
    vi.mocked(TestWorkflowService.getMoisture).mockClear();
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      moistureRole: "LossOnDrying",
      resultMode: "RelatedSubstances",
      principalAnalyte: { id: 10, name: "Principal Analyte" }
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("SMP-001")).toBeTruthy();
    });

    expect(TestWorkflowService.getMoisture).not.toHaveBeenCalled();
    expect(screen.queryByText(/Loss on drying/i)).toBeNull();
  });

  it("drives the RS layout by the entry's resultMode alone without relying on preview strings or zero weights", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      preview: [],
      resultMode: "RelatedSubstances",
      principalAnalyte: { id: 10, name: "Principal Analyte" },
      methodWeights: [{ hplcMethodAnalyteId: 10, analyteName: "API", theoreticalWeightStdMg: 50, theoreticalWeightTestMg: 50 }]
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Peak area entry")).toBeTruthy();
    });
    expect(screen.queryByText("Method & theoretical weights")).toBeNull();
  });

  it("builds the RS peak table from the run snapshot fields on the entry, not the live method", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      resultMode: "RelatedSubstances",
      principalAnalyte: { id: 10, name: "Principal Analyte" },
      impurities: [{ id: 101, displayOrder: 1, name: "Snapshot Impurity", relativeRetention: 0.8, correctionFactor: 1 }],
      referenceSolutionPercent: 0.1,
      disregardLimitPercent: 0.05,
      replicates: [{ replicateNo: 1, actualWeightMg: 0, responses: [{ hplcMethodAnalyteId: 10, response: 0 }], peaks: [] }]
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getAllByText(/Snapshot Impurity/).length).toBeGreaterThan(0);
    });
    expect(screen.getByText("Reference: 0.1%")).toBeTruthy();
    // The page no longer asks for the run or the live method.
    expect(HplcWorkspaceService.getRun).not.toHaveBeenCalled();
  });

  it("takes the internal standard from the entry", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      resultMode: "Assay",
      internalStandardName: "Benzoic acid",
      methodWeights: [{ hplcMethodAnalyteId: 10, analyteName: "API", theoreticalWeightStdMg: 50, theoreticalWeightTestMg: 50 }],
      replicates: [{ replicateNo: 1, actualWeightMg: 50, responses: [{ hplcMethodAnalyteId: 10, response: 100 }], internalStandardResponse: 50 }]
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Benzoic acid Area *")).toBeTruthy();
    });
  });

  it("shows the table's error instead of 'Enter a positive reference...' caption when principal analyte is missing", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      editable: true,
      resultMode: "RelatedSubstances",
      principalAnalyte: null // snapshot has no single analyte
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getAllByText("This related-substances method must have exactly one analyte.").length).toBeGreaterThan(0);
    });
    expect(screen.queryByText(/Enter a positive reference solution area/i)).toBeNull();
  });
});

describe("HplcSampleEntryPage - empty RS preview", () => {
  it("asks for peak areas, not weights, before an RS preview exists", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      resultMode: "RelatedSubstances",
      principalAnalyte: { id: 10, name: "Principal Analyte" },
      preview: []
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/Enter peak areas above and click Save Replicates/i)).toBeTruthy();
    });
  });
});
