import { describe, it, expect, vi, beforeEach } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { IcpWorkspaceService } from "./IcpWorkspaceService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn()
  }
}));

describe("IcpWorkspaceService endpoint URL building", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls GET /icp-workspace/instruments", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: [] } });
    await IcpWorkspaceService.getInstruments();
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/instruments");
  });

  it("calls GET /icp-workspace/instruments/:equipmentId/runs", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: [] } });
    await IcpWorkspaceService.getRunHistory(42);
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/instruments/42/runs");
  });

  it("calls GET /icp-workspace/runs/:id", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: { id: 7 } } });
    await IcpWorkspaceService.getRun(7);
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/runs/7");
  });

  it("calls GET /icp-workspace/runs/:id/eligible-tests with search param", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: [] } });
    await IcpWorkspaceService.getEligibleTests(7, "lot123");
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/runs/7/eligible-tests", {
      params: { search: "lot123" }
    });
  });

  it("calls POST /icp-workspace/runs for startRun", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: { id: 10 } } });
    await IcpWorkspaceService.startRun({ equipmentId: 5 });
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs", { equipmentId: 5 });
  });

  it("calls PUT /icp-workspace/runs/:id/calibration for saveCalibration", async () => {
    vi.mocked(apiClient.put).mockResolvedValueOnce({ data: { success: true, data: {} } });
    const payload = {
      calibrationStandardMaterialId: 101,
      elements: [{ symbol: "Zn", correlationR: 0.9995 }]
    };
    await IcpWorkspaceService.saveCalibration(8, payload);
    expect(apiClient.put).toHaveBeenCalledWith("/icp-workspace/runs/8/calibration", payload);
  });

  it("calls POST /icp-workspace/runs/:id/calibration/confirm", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.confirmCalibration(8, { password: "secretPassword!", comment: "Approved" });
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs/8/calibration/confirm", {
      password: "secretPassword!",
      comment: "Approved"
    });
  });

  it("calls PUT /icp-workspace/samples/:runSampleId/entry for saveEntry", async () => {
    vi.mocked(apiClient.put).mockResolvedValueOnce({ data: { success: true, data: {} } });
    const payload = { actualWeightG: 0.5, concentrations: [{ icpTestElementId: 3, resultPpm: 9.5 }] };
    await IcpWorkspaceService.saveEntry(15, payload);
    expect(apiClient.put).toHaveBeenCalledWith("/icp-workspace/samples/15/entry", payload);
  });

  it("calls GET report-values with and without a chosen solution", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: {} } });
    await IcpWorkspaceService.getReportValues(15);
    expect(apiClient.get).toHaveBeenLastCalledWith("/icp-workspace/samples/15/report-values", { params: undefined });
    await IcpWorkspaceService.getReportValues(15, 4);
    expect(apiClient.get).toHaveBeenLastCalledWith("/icp-workspace/samples/15/report-values", { params: { solutionIndex: 4 } });
  });

  it("posts the assignment with the signature password and comment", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.assignSamples(8, { testOrderIds: [1, 2], password: "pw", comment: "two samples" });
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs/8/samples", {
      testOrderIds: [1, 2],
      password: "pw",
      comment: "two samples"
    });
  });

  it("calls POST /icp-workspace/runs/:id/abandon with reason", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.abandonRun(8, "Plasma unstable");
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs/8/abandon", {
      reason: "Plasma unstable"
    });
  });

  it("calls POST /icp-workspace/runs/:id/complete", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.completeRun(8);
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs/8/complete");
  });

  it("calls GET /icp-workspace/samples/:runSampleId", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.getSampleEntry(15);
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/samples/15");
  });

  it("calls GET /icp-workspace/test-orders/:testOrderId/evidence", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: [] } });
    await IcpWorkspaceService.getTestOrderEvidence(99);
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/test-orders/99/evidence");
  });
});
