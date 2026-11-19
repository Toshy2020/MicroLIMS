import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./apiClient";
import { actionAvailabilityService } from "./actionAvailabilityService";

vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return { ...actual, apiClient: { get: vi.fn() } };
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("actionAvailabilityService.get", () => {
  it("returns the server's list and sends the section only for a sample", async () => {
    const row = { action: "review.complete", allowed: false, reason: "You cannot review a sample you tested." };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: [row] } });
    await expect(actionAvailabilityService.get("sample", 5, 3)).resolves.toEqual([row]);
    expect(vi.mocked(apiClient.get).mock.calls[0][0]).toBe("/actions/sample/5");
    expect(vi.mocked(apiClient.get).mock.calls[0][1]?.params).toEqual({ sectionId: 3 });

    await actionAvailabilityService.get("media", 9, 3);
    expect(vi.mocked(apiClient.get).mock.calls[1][1]?.params).toBeUndefined();
  });

  it("an answer that is not a list is a failed load, so the buttons stay usable", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: { unexpected: true } } });
    await expect(actionAvailabilityService.get("test-order", 1)).rejects.toThrow();
  });
});
