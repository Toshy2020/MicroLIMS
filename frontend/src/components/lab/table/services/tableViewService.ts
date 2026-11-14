import { apiClient } from "../../../../services/apiClient";
import type { ApiResponse } from "../../../../services/apiTypes";

export type TableViewScope = "Personal" | "Section";

export interface TableViewDto {
  id: number;
  tableKey: string;
  name: string;
  scope: TableViewScope;
  sectionId: number | null;
  sectionName: string | null;
  stateJson: string;
  isMine: boolean;
  canEdit: boolean;
  isDefault: boolean;
  ownerName: string;
}

export interface CreateTableViewRequest {
  tableKey: string;
  name: string;
  stateJson: string;
  scope?: TableViewScope;
  sectionId?: number | null;
}

export interface UpdateTableViewRequest {
  name?: string | null;
  stateJson?: string | null;
}

export interface SetDefaultTableViewRequest {
  tableKey: string;
  viewId: number | null;
}

export const tableViewService = {
  async list(tableKey: string, signal?: AbortSignal): Promise<TableViewDto[]> {
    const res = await apiClient.get<ApiResponse<TableViewDto[]>>("/table-views", {
      params: { tableKey },
      signal,
    });
    return res.data.data ?? [];
  },

  async create(
    request: CreateTableViewRequest,
    signal?: AbortSignal
  ): Promise<TableViewDto> {
    const res = await apiClient.post<ApiResponse<TableViewDto>>(
      "/table-views",
      request,
      { signal }
    );
    if (!res.data.data) {
      throw new Error(res.data.message ?? "Failed to create view.");
    }
    return res.data.data;
  },

  async update(
    id: number,
    request: UpdateTableViewRequest,
    signal?: AbortSignal
  ): Promise<TableViewDto> {
    const res = await apiClient.put<ApiResponse<TableViewDto>>(
      `/table-views/${id}`,
      request,
      { signal }
    );
    if (!res.data.data) {
      throw new Error(res.data.message ?? "Failed to update view.");
    }
    return res.data.data;
  },

  async remove(id: number, signal?: AbortSignal): Promise<void> {
    await apiClient.delete<ApiResponse<null>>(`/table-views/${id}`, { signal });
  },

  async setDefault(
    tableKey: string,
    viewId: number | null,
    signal?: AbortSignal
  ): Promise<void> {
    const body: SetDefaultTableViewRequest = { tableKey, viewId };
    await apiClient.put<ApiResponse<null>>("/table-views/default", body, {
      signal,
    });
  },
};
