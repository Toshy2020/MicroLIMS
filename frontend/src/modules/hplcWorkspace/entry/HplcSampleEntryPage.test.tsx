import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { HplcSampleEntryPage, buildReplicatePayload } from "./HplcSampleEntryPage";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { TestWorkflowService } from "../../testingWorkspace/services/TestWorkflowService";
import { HplcMethodService } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
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

vi.mock("../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService", () => ({
  HplcMethodService: {
    getById: vi.fn()
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

describe("HplcSampleEntryPage - moisture line display", () => {
  it("shows the moisture line when moistureRole is set and not RS/residual", async () => {
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      moistureRole: "LossOnDrying"
    });
    vi.mocked(HplcMethodService.getById).mockResolvedValue({
      id: 1,
      name: "Assay Method",
      abbreviation: "ASSAY",
      resultMode: "Assay",
      analytes: [{ id: 10, name: "Analyte 1" }]
    } as never);
    vi.mocked(TestWorkflowService.getMoisture).mockResolvedValue({
      role: "LossOnDrying",
      percent: 0.5,
      testCode: "LOD-101"
    });

    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/hplc/1/run/10/sample/100"]}>
          <Routes>
            <Route path="/hplc/:instrumentId/run/:runId/sample/:runSampleId" element={<HplcSampleEntryPage />} />
          </Routes>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(TestWorkflowService.getMoisture).toHaveBeenCalledWith(7);
      expect(screen.getByText("Loss on drying 0.5 % (from LOD-101)")).toBeTruthy();
    });
  });

  it("does not fetch or show the moisture line when moistureRole is null", async () => {
    vi.mocked(TestWorkflowService.getMoisture).mockClear();
    vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue({
      ...baseSampleEntry,
      moistureRole: null
    });
    vi.mocked(HplcMethodService.getById).mockResolvedValue({
      id: 1,
      name: "Assay Method",
      abbreviation: "ASSAY",
      resultMode: "Assay",
      analytes: [{ id: 10, name: "Analyte 1" }]
    } as never);

    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/hplc/1/run/10/sample/100"]}>
          <Routes>
            <Route path="/hplc/:instrumentId/run/:runId/sample/:runSampleId" element={<HplcSampleEntryPage />} />
          </Routes>
        </MemoryRouter>
      </PinnedLightTheme>
    );

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
      moistureRole: "LossOnDrying"
    });
    vi.mocked(HplcMethodService.getById).mockResolvedValue({
      id: 1,
      name: "RS Method",
      abbreviation: "RS",
      resultMode: "RelatedSubstances",
      analytes: [{ id: 10, name: "Principal Analyte" }]
    } as never);

    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/hplc/1/run/10/sample/100"]}>
          <Routes>
            <Route path="/hplc/:instrumentId/run/:runId/sample/:runSampleId" element={<HplcSampleEntryPage />} />
          </Routes>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByText("SMP-001")).toBeTruthy();
    });

    expect(TestWorkflowService.getMoisture).not.toHaveBeenCalled();
    expect(screen.queryByText(/Loss on drying/i)).toBeNull();
  });
});
