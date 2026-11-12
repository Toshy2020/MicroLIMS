import { describe, it, expect, vi, afterEach } from "vitest";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return { ...actual, apiClient: http };
});

import { masterDataOptions } from "./masterDataOptions";

afterEach(() => { vi.clearAllMocks(); });

const ifMatchHeader = (version: number) => ({ headers: { "If-Match": `"${version}"` } });

describe("masterDataOptions sends the record version as If-Match", () => {
  it("freeze and unfreeze send the loaded test version", async () => {
    http.put.mockResolvedValue({ data: { data: {} } });
    await masterDataOptions.freezeTestDefinition(20, 7);
    await masterDataOptions.unfreezeTestDefinition(20, 8);

    expect(http.put).toHaveBeenNthCalledWith(1, "/masterdata/test-definitions/20/freeze", undefined, ifMatchHeader(7));
    expect(http.put).toHaveBeenNthCalledWith(2, "/masterdata/test-definitions/20/unfreeze", undefined, ifMatchHeader(8));
  });

  it("workflow type sends the test version", async () => {
    http.put.mockResolvedValue({ data: { data: {} } });
    await masterDataOptions.updateWorkflowType(20, "Observation", 7);

    expect(http.put).toHaveBeenCalledWith("/masterdata/test-definitions/20/workflow-type", { workflowType: "Observation" }, ifMatchHeader(7));
  });

  it("move sends the step version", async () => {
    http.put.mockResolvedValue({ data: { data: {} } });
    await masterDataOptions.moveTestWorkflowStep(5, "up", 3);

    expect(http.put).toHaveBeenCalledWith("/masterdata/test-definitions/steps/5/move", { direction: "up" }, ifMatchHeader(3));
  });

  it("delete sends the step version", async () => {
    http.delete.mockResolvedValue({ data: { data: {} } });
    await masterDataOptions.deleteTestWorkflowStep(5, 3);

    expect(http.delete).toHaveBeenCalledWith("/masterdata/test-definitions/steps/5", ifMatchHeader(3));
  });
});
