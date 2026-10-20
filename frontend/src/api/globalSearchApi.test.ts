import { describe, expect, it, vi, beforeEach } from "vitest";
import { globalSearchApi } from "./globalSearchApi";
import { apiClient } from "../services/apiClient";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn()
  }
}));

describe("globalSearchApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls /api/search/global with encoded query parameters", async () => {
    const mockResponse = {
      data: {
        success: true,
        data: {
          query: "SMP-2026",
          totalMatches: 1,
          executionTimeMs: 12,
          categories: [
            {
              categoryName: "Samples",
              count: 1,
              items: [
                {
                  id: "1",
                  title: "SMP-2026-001",
                  subtitle: "Paracetamol 500mg",
                  category: "Samples",
                  badge: "InTesting",
                  route: "/receiving-testing?sampleId=1",
                  matchedField: "ReferenceNumber"
                }
              ]
            }
          ]
        }
      }
    };

    vi.mocked(apiClient.get).mockResolvedValueOnce(mockResponse);

    const result = await globalSearchApi.search("SMP-2026", "Samples", 10);

    expect(apiClient.get).toHaveBeenCalledWith("/search/global", {
      params: { q: "SMP-2026", category: "Samples", limit: 10 },
      signal: undefined
    });
    expect(result.totalMatches).toBe(1);
    expect(result.categories[0].categoryName).toBe("Samples");
  });

  it("passes abort signal to apiClient", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        success: true,
        data: { query: "", totalMatches: 0, executionTimeMs: 0, categories: [] }
      }
    });
    const controller = new AbortController();

    await globalSearchApi.search("test", undefined, 5, controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/search/global", {
      params: { q: "test", category: undefined, limit: 5 },
      signal: controller.signal
    });
  });
});
