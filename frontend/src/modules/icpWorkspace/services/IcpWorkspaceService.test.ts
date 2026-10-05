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

  it("calls GET /icp-workspace/method-options", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: [] } });
    await IcpWorkspaceService.getMethodOptions();
    expect(apiClient.get).toHaveBeenCalledWith("/icp-workspace/method-options");
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
    await IcpWorkspaceService.startRun({ equipmentId: 5, icpMethodId: 3 });
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs", {
      equipmentId: 5,
      icpMethodId: 3
    });
  });

  it("calls PUT /icp-workspace/runs/:id/calibration for saveCalibration", async () => {
    vi.mocked(apiClient.put).mockResolvedValueOnce({ data: { success: true, data: {} } });
    const payload = {
      calibrationStandardMaterialId: 101,
      icvStandardMaterialId: 102,
      elements: []
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

  it("calls POST /icp-workspace/runs/:id/ccv for addCcv", async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ data: { success: true, data: {} } });
    await IcpWorkspaceService.addCcv(8, { icpMethodElementId: 3, measuredMgPerL: 5.01 });
    expect(apiClient.post).toHaveBeenCalledWith("/icp-workspace/runs/8/ccv", {
      icpMethodElementId: 3,
      measuredMgPerL: 5.01
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
