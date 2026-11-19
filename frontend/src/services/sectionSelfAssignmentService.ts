import { apiClient } from "./apiClient";
import type { ApiResponse } from "./apiTypes";

export interface SectionSelfAssignmentDto {
  sectionId: number;
  sectionName: string;
  enabled: boolean;
  changedAt: string | null;
  changedByName: string | null;
  version: number;
}

export interface UpdateSectionSelfAssignmentRequest {
  enabled: boolean;
  reason: string;
  version: number;
}

export const sectionSelfAssignmentService = {
  async getAll(signal?: AbortSignal): Promise<SectionSelfAssignmentDto[]> {
    const res = await apiClient.get<ApiResponse<SectionSelfAssignmentDto[]>>(
      "/sections/self-assignment",
      { signal }
    );
    return res.data.data ?? [];
  },

  async update(
    sectionId: number,
    payload: UpdateSectionSelfAssignmentRequest
  ): Promise<SectionSelfAssignmentDto> {
    const res = await apiClient.put<ApiResponse<SectionSelfAssignmentDto>>(
      `/sections/${sectionId}/self-assignment`,
      payload
    );
    if (!res.data.data) {
      throw new Error("Section self-assignment response was empty.");
    }
    return res.data.data;
  }
};
