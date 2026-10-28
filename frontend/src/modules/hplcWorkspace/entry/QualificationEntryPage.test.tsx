import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { QualificationEntryPage } from "./QualificationEntryPage";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcQualificationEntryDto } from "../types";

afterEach(cleanup);

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: ["Hplc.Operate"], role: "Analyst" })
}));

vi.mock("../useTechnique", () => ({
  useTechnique: () => ({ basePath: "/hplc", label: "HPLC" })
}));

vi.mock("../evidence/ReportUploadPanel", () => ({ ReportUploadPanel: () => null }));

vi.mock("../services/HplcWorkspaceService", () => ({
  HplcWorkspaceService: {
    getQualificationEntry: vi.fn(),
    saveQualificationReplicates: vi.fn()
  }
}));

const entry: HplcQualificationEntryDto = {
  runSampleId: 100,
  hplcRunId: 10,
  runCode: "RUN-001",
  sstCode: "SST-001",
  sstStatus: "Passed",
  qualificationId: 5,
  qualificationCode: "WSQ-01",
  qualificationStatus: "Draft",
  materialName: "Vitamin C",
  batchNumber: "B1",
  methodWeights: [{ hplcMethodAnalyteId: 10, analyteName: "Vitamin C", theoreticalWeightStdMg: 50, theoreticalWeightTestMg: 50 }],
  requiredReplicates: 1,
  replicates: [],
  evidence: [],
  editable: true,
  submitted: false,
  canSubmit: false,
  internalStandardName: "Benzoic acid"
};

describe("QualificationEntryPage - internal standard methods", () => {
  it("collects the IS area per replicate and sends it with the save", async () => {
    vi.mocked(HplcWorkspaceService.getQualificationEntry).mockResolvedValue(entry);
    vi.mocked(HplcWorkspaceService.saveQualificationReplicates).mockResolvedValue(entry);

    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/hplc/1/run/10/qualification/100"]}>
          <Routes>
            <Route path="/hplc/:instrumentId/run/:runId/qualification/:runSampleId" element={<QualificationEntryPage />} />
          </Routes>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const isArea = await screen.findByLabelText("Replicate 1 IS area");
    fireEvent.change(isArea, { target: { value: "500" } });
    fireEvent.change(screen.getByLabelText("Replicate 1 actual weight (mg)"), { target: { value: "50" } });
    fireEvent.change(screen.getByLabelText("Replicate 1 Analyte area"), { target: { value: "1000" } });
    fireEvent.click(screen.getByRole("button", { name: /Save Replicates/i }));

    await waitFor(() => expect(HplcWorkspaceService.saveQualificationReplicates).toHaveBeenCalled());
    const [, request] = vi.mocked(HplcWorkspaceService.saveQualificationReplicates).mock.calls[0];
    expect(request.replicates[0].internalStandardResponse).toBe(500);
  });
});
