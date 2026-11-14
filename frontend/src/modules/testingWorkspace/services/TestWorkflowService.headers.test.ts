import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { TestWorkflowService } from "./TestWorkflowService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(() => Promise.resolve({ data: { data: { ok: true } } })) }
}));

afterEach(() => vi.clearAllMocks());

const countPayload = { stepName: "TAMC", rawPlateReadings: ["12"], dilutionFactor: 10 };
const qualitativePayload = {
  analysedAt: "2026-10-11T09:00:00.000Z",
  equipmentId: null,
  parameters: [{ specificationId: 101, conforms: true, observation: null }],
  password: "pw",
  comment: null
};

describe("TestWorkflowService request headers", () => {
  it("recordCountResult passes the headers through and leaves the body alone", async () => {
    await TestWorkflowService.recordCountResult(9, countPayload, { headers: { "X-Draft-Started-At": "2026-10-11T09:00:00.000Z" } });
    expect(apiClient.post).toHaveBeenCalledWith("/test-workflow/9/record-result", countPayload, {
      headers: { "X-Draft-Started-At": "2026-10-11T09:00:00.000Z" }
    });
  });

  it("recordQualitativeResult passes the headers through and leaves the body alone", async () => {
    await TestWorkflowService.recordQualitativeResult(42, qualitativePayload, { headers: { "X-Draft-Started-At": "2026-10-11T09:00:00.000Z" } });
    expect(apiClient.post).toHaveBeenCalledWith("/test-workflow/42/record-qualitative-result", qualitativePayload, {
      headers: { "X-Draft-Started-At": "2026-10-11T09:00:00.000Z" }
    });
  });

  it("without a config the requests are exactly as before", async () => {
    await TestWorkflowService.recordCountResult(9, countPayload);
    await TestWorkflowService.recordQualitativeResult(42, qualitativePayload);
    expect(vi.mocked(apiClient.post).mock.calls[0].slice(0, 2)).toEqual(["/test-workflow/9/record-result", countPayload]);
    expect(vi.mocked(apiClient.post).mock.calls[0][2]).toBeUndefined();
    expect(vi.mocked(apiClient.post).mock.calls[1].slice(0, 2)).toEqual(["/test-workflow/42/record-qualitative-result", qualitativePayload]);
    expect(vi.mocked(apiClient.post).mock.calls[1][2]).toBeUndefined();
  });
});
