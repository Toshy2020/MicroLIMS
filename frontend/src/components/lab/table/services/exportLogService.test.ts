import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../../services/apiClient";
import { exportLogService } from "./exportLogService";

vi.mock("../../../../services/apiClient", () => ({
  apiClient: {
    post: vi.fn(),
  },
}));

const mockedPost = vi.mocked(apiClient.post);

beforeEach(() => {
  vi.clearAllMocks();
});

describe("exportLogService", () => {
  it("posts export log payload and returns server response", async () => {
    const payload = {
      tableKey: "inventory.equipment",
      filterJson: JSON.stringify({ status: "Active" }),
      columns: ["code", "status"],
      rowCount: 2,
      recordIds: ["EQ-001", "EQ-002"],
    };
    const response = {
      exportId: 42,
      serverTimeUtc: "2031-05-06T07:08:09Z",
      timeZoneId: "Africa/Cairo",
      exportedBy: "Ada Analyst",
    };
    mockedPost.mockResolvedValue({ data: { success: true, data: response } });

    const result = await exportLogService.log(payload);

    expect(mockedPost).toHaveBeenCalledWith("/exports/log", payload);
    expect(result).toEqual(response);
  });
});
