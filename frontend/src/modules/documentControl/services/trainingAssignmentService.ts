import { apiClient } from "../../../services/apiClient";
import type {
  DocumentTrainingAssignmentDto,
  DocumentTrainingAssignmentFilter,
  TrainingAssignmentStatus
} from "../types/trainingAssignmentTypes";

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export const trainingAssignmentService = {
  getAssignments: async (
    filter: DocumentTrainingAssignmentFilter = {}
  ): Promise<PagedResult<DocumentTrainingAssignmentDto>> => {
    const res = await apiClient.get("/document-control/training-assignments", {
      params: filter
    });
    return res.data.data;
  },

  getAssignmentById: async (
    assignmentId: number
  ): Promise<DocumentTrainingAssignmentDto> => {
    const res = await apiClient.get(
      `/document-control/training-assignments/${assignmentId}`
    );
    return res.data.data;
  },

  getMyAssignments: async (
    status?: TrainingAssignmentStatus
  ): Promise<DocumentTrainingAssignmentDto[]> => {
    const res = await apiClient.get(
      "/document-control/training-assignments/my-assignments",
      {
        params: status ? { status } : undefined
      }
    );
    return res.data.data;
  },

  getUserAssignments: async (
    userId: number,
    status?: TrainingAssignmentStatus
  ): Promise<DocumentTrainingAssignmentDto[]> => {
    const res = await apiClient.get(
      `/document-control/training-assignments/users/${userId}`,
      {
        params: status ? { status } : undefined
      }
    );
    return res.data.data;
  },

  calculateDueDate: async (
    masterId: number,
    roleId?: number
  ): Promise<{ dueDateUtc: string; gracePeriodDays: number }> => {
    const res = await apiClient.get(
      "/document-control/training-assignments/calculate-due-date",
      {
        params: { masterId, roleId }
      }
    );
    return res.data.data;
  },

  checkSupersededGap: async (
    userId: number,
    currentRevisionId: number
  ): Promise<{ isTrainedOnSupersededOnly: boolean }> => {
    const res = await apiClient.get(
      "/document-control/training-assignments/superseded-gap-check",
      {
        params: { userId, currentRevisionId }
      }
    );
    return res.data.data;
  }
};
