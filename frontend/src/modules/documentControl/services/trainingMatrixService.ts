import { apiClient } from "../../../services/apiClient";
import type {
  TrainingMatrixGridDto,
  TrainingMatrixFilterDto,
  ComplianceKpiSummaryDto,
  UserComplianceDetailDto,
  DocumentComplianceDetailDto
} from "../types/trainingMatrixTypes";

export const trainingMatrixService = {
  getMatrixGrid: async (
    filter: TrainingMatrixFilterDto = {}
  ): Promise<TrainingMatrixGridDto> => {
    const res = await apiClient.get("/document-control/training-matrix/grid", {
      params: filter
    });
    return res.data.data;
  },

  getComplianceKpis: async (
    departmentId?: number
  ): Promise<ComplianceKpiSummaryDto> => {
    const res = await apiClient.get("/document-control/training-matrix/kpis", {
      params: departmentId ? { departmentId } : undefined
    });
    return res.data.data;
  },

  getUserCompliance: async (
    userId: number
  ): Promise<UserComplianceDetailDto> => {
    const res = await apiClient.get(
      `/document-control/training-matrix/users/${userId}`
    );
    return res.data.data;
  },

  getDocumentCompliance: async (
    documentMasterId: number
  ): Promise<DocumentComplianceDetailDto> => {
    const res = await apiClient.get(
      `/document-control/training-matrix/documents/${documentMasterId}`
    );
    return res.data.data;
  }
};
