import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { recentRecordsService } from "./recentRecordsService";

vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: { get: vi.fn(), post: vi.fn(), delete: vi.fn() } };
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("recentRecordsService.list", () => {
  it("returns the list the server sent", async () => {
    const row = { recordType: "sample", recordId: 1, identifier: "S-1", title: "Water", route: "/receiving-testing?sampleId=1", openedAtUtc: "2026-11-15T10:00:00Z" };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: [row] } });
    await expect(recentRecordsService.list()).resolves.toEqual([row]);
  });

  it("an empty answer is an empty list", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: null } });
    await expect(recentRecordsService.list()).resolves.toEqual([]);
  });

  it("an answer that is not a list is a failed load, so the section hides instead of breaking the layout", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: { unexpected: true } } });
    await expect(recentRecordsService.list()).rejects.toThrow();
  });
});
