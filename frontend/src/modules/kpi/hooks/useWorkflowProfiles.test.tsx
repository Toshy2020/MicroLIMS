import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useWorkflowProfiles } from "./useWorkflowProfiles";
import { workflowProfileService } from "../services/workflowProfileService";

vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { overview: vi.fn(), coverage: vi.fn() }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("useWorkflowProfiles", () => {
  it("loads the overview and coverage together", async () => {
    vi.mocked(workflowProfileService.overview).mockResolvedValue({ types: [] });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: ["Gravimetric"],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });

    const { result } = renderHook(() => useWorkflowProfiles());
    expect(result.current.loading).toBe(true);

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBeNull();
    expect(result.current.overview).toEqual({ types: [] });
    expect(result.current.coverage?.typesWithoutUnits).toEqual(["Gravimetric"]);
  });

  it("surfaces the server message instead of swallowing the failure", async () => {
    vi.mocked(workflowProfileService.overview).mockRejectedValue({ response: { data: { message: "Boom" } } });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: [],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });

    const { result } = renderHook(() => useWorkflowProfiles());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe("Boom");
    expect(result.current.overview).toBeNull();
  });

  it("reload fetches again", async () => {
    vi.mocked(workflowProfileService.overview).mockResolvedValue({ types: [] });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: [],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });
    const { result } = renderHook(() => useWorkflowProfiles());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await act(async () => {
      await result.current.reload();
    });

    expect(workflowProfileService.overview).toHaveBeenCalledTimes(2);
  });
});
