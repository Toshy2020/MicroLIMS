import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { workflowProfileService } from "./workflowProfileService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
  ifMatch: (version: number) => ({ headers: { "If-Match": `"${version}"` } })
}));

afterEach(() => vi.clearAllMocks());

describe("workflowProfileService", () => {
  it("lists with the filters as query params and unwraps the envelope", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: [{ id: 1 }] } } as never);

    const rows = await workflowProfileService.list({ workflowType: "CountTest", defaultOnly: true });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workflow-profiles", {
      params: { workflowType: "CountTest", defaultOnly: true }
    });
    expect(rows).toEqual([{ id: 1 }]);
  });

  it("reads the overview and coverage", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: { types: [] } } } as never);

    await workflowProfileService.overview();
    await workflowProfileService.coverage();

    expect(apiClient.get).toHaveBeenNthCalledWith(1, "/kpi/workflow-profiles/overview");
    expect(apiClient.get).toHaveBeenNthCalledWith(2, "/kpi/workflow-profiles/coverage");
  });

  it("posts a new version with exactly the wire field names", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true, data: { id: 9 } } } as never);
    const request = {
      workflowType: "CountTest" as const,
      testCode: null,
      workUnits: 1.5,
      targetAnalystDays: 7,
      effectiveFrom: "2026-10-11",
      reasonForChange: "SOP time study",
      password: "pw"
    };

    const created = await workflowProfileService.create(request);

    expect(apiClient.post).toHaveBeenCalledWith("/kpi/workflow-profiles", request);
    expect(created).toEqual({ id: 9 });
  });

  it("signs a draft with the version it was loaded at as If-Match", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true, data: { id: 7 } } } as never);
    const request = {
      workUnits: 2,
      targetAnalystDays: null,
      effectiveFrom: "2026-10-12",
      reasonForChange: "Reviewed",
      password: "pw"
    };

    await workflowProfileService.signDraft(7, 3, request);

    expect(apiClient.post).toHaveBeenCalledWith("/kpi/workflow-profiles/7/sign", request, {
      headers: { "If-Match": "\"3\"" }
    });
  });
});
