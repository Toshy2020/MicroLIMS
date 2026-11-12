import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, waitFor, act, cleanup } from "@testing-library/react";
import { useTestDefinitions, type TestDefinitionOption } from "./useTestDefinitions";

const api = vi.hoisted(() => ({
  getTestDefinitions: vi.fn(),
  freezeTestDefinition: vi.fn(),
  unfreezeTestDefinition: vi.fn()
}));
vi.mock("../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: { ...actual.masterDataOptions, ...api } };
});

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const testRow = (version: number): TestDefinitionOption => ({
  id: 20, code: "TAMC-01", displayName: "Total Aerobic Count", isActive: true, version, workflowType: "CountTest"
} as unknown as TestDefinitionOption);

describe("useTestDefinitions setActive (freeze / unfreeze)", () => {
  it("freeze sends If-Match with the loaded version", async () => {
    api.getTestDefinitions.mockResolvedValue([testRow(7)]);
    api.freezeTestDefinition.mockResolvedValue({ id: 20, version: 8 });
    const { result } = renderHook(() => useTestDefinitions());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await act(async () => { await result.current.setActive(20, false); });

    expect(api.freezeTestDefinition).toHaveBeenCalledWith(20, 7);
  });

  it("uses the version returned by the server for the next action", async () => {
    // After the freeze the reloaded row carries the version the server bumped to.
    api.getTestDefinitions
      .mockResolvedValueOnce([testRow(7)])
      .mockResolvedValue([{ ...testRow(8), isActive: false }]);
    api.freezeTestDefinition.mockResolvedValue({ id: 20, version: 8 });
    api.unfreezeTestDefinition.mockResolvedValue({ id: 20, version: 9 });
    const { result } = renderHook(() => useTestDefinitions());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await act(async () => { await result.current.setActive(20, false); });
    await act(async () => { await result.current.setActive(20, true); });

    expect(api.freezeTestDefinition).toHaveBeenCalledWith(20, 7);
    expect(api.unfreezeTestDefinition).toHaveBeenCalledWith(20, 8);
  });
});
