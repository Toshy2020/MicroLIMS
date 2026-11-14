import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../../services/apiClient";
import { tableViewService, type TableViewDto } from "./tableViewService";

vi.mock("../../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockedGet = vi.mocked(apiClient.get);
const mockedPost = vi.mocked(apiClient.post);
const mockedPut = vi.mocked(apiClient.put);
const mockedDelete = vi.mocked(apiClient.delete);

const sampleView: TableViewDto = {
  id: 10,
  tableKey: "inventory.equipment",
  name: "Active equipment",
  scope: "Personal",
  sectionId: null,
  sectionName: null,
  stateJson: JSON.stringify({
    v: 1,
    filters: { status: "Active" },
    search: "",
    sort: null,
    pageSize: 25,
    columns: ["code", "status"],
    density: "comfortable",
  }),
  isMine: true,
  canEdit: true,
  isDefault: false,
  ownerName: "Ada Analyst",
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("tableViewService", () => {
  it("list fetches views for tableKey and forwards AbortSignal", async () => {
    mockedGet.mockResolvedValue({ data: { success: true, data: [sampleView] } });
    const controller = new AbortController();

    const result = await tableViewService.list("inventory.equipment", controller.signal);

    expect(mockedGet).toHaveBeenCalledWith("/table-views", {
      params: { tableKey: "inventory.equipment" },
      signal: controller.signal,
    });
    expect(result).toEqual([sampleView]);
  });

  it("create posts CreateTableViewRequest", async () => {
    mockedPost.mockResolvedValue({ data: { success: true, data: sampleView } });
    const req = {
      tableKey: "inventory.equipment",
      name: "Active equipment",
      stateJson: sampleView.stateJson,
      scope: "Personal" as const,
      sectionId: null,
    };

    const result = await tableViewService.create(req);

    expect(mockedPost).toHaveBeenCalledWith("/table-views", req, { signal: undefined });
    expect(result).toEqual(sampleView);
  });

  it("update puts UpdateTableViewRequest to /table-views/:id", async () => {
    mockedPut.mockResolvedValue({ data: { success: true, data: sampleView } });

    const result = await tableViewService.update(10, { name: "Renamed" });

    expect(mockedPut).toHaveBeenCalledWith(
      "/table-views/10",
      { name: "Renamed" },
      { signal: undefined }
    );
    expect(result).toEqual(sampleView);
  });

  it("remove deletes /table-views/:id", async () => {
    mockedDelete.mockResolvedValue({ data: { success: true, data: null } });

    await tableViewService.remove(10);

    expect(mockedDelete).toHaveBeenCalledWith("/table-views/10", { signal: undefined });
  });

  it("setDefault puts tableKey and viewId to /table-views/default", async () => {
    mockedPut.mockResolvedValue({ data: { success: true, data: null } });

    await tableViewService.setDefault("inventory.equipment", 10);

    expect(mockedPut).toHaveBeenCalledWith(
      "/table-views/default",
      { tableKey: "inventory.equipment", viewId: 10 },
      { signal: undefined }
    );
  });
});
