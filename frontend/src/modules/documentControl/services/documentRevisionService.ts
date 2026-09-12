import { apiClient } from "../../../services/apiClient";
import type {
  CreateRevisionRequest,
  ProposeNextRevisionResponse,
  RevisionChangeItemDto,
  AddChangeItemRequest,
  UpdateChangeItemRequest,
  ConvertFindingRequest,
  RevisionImpactAssessmentDto,
  SaveImpactAssessmentRequest,
  DocumentRevisionDto,
  RevisionType
} from "../types/documentControlTypes";

export const documentRevisionService = {
  proposeNextRevision: async (
    masterId: number,
    revisionType: RevisionType
  ): Promise<ProposeNextRevisionResponse> => {
    const res = await apiClient.post(
      `/document-control/documents/${masterId}/propose-revision`,
      { revisionType }
    );
    return res.data.data;
  },

  createRevisionFromEffective: async (
    masterId: number,
    request: CreateRevisionRequest
  ): Promise<DocumentRevisionDto> => {
    const res = await apiClient.post(
      `/document-control/documents/${masterId}/revisions`,
      request
    );
    return res.data.data;
  },

  getRevisionDetails: async (revisionId: number): Promise<DocumentRevisionDto> => {
    const res = await apiClient.get(`/document-control/revisions/${revisionId}`);
    return res.data.data;
  },

  getChangeItems: async (
    revisionId: number
  ): Promise<RevisionChangeItemDto[]> => {
    const res = await apiClient.get(
      `/document-control/revisions/${revisionId}/change-items`
    );
    return res.data.data;
  },

  addChangeItem: async (
    revisionId: number,
    request: AddChangeItemRequest
  ): Promise<RevisionChangeItemDto> => {
    const res = await apiClient.post(
      `/document-control/revisions/${revisionId}/change-items`,
      request
    );
    return res.data.data;
  },

  updateChangeItem: async (
    changeItemId: number,
    request: UpdateChangeItemRequest
  ): Promise<RevisionChangeItemDto> => {
    const res = await apiClient.put(
      `/document-control/change-items/${changeItemId}`,
      request
    );
    return res.data.data;
  },

  // Deactivates the change item and retains the record. It used to be
  // physically deleted, which DC-URS-184 and BR-015 prohibit.
  deactivateChangeItem: async (changeItemId: number): Promise<void> => {
    await apiClient.post(`/document-control/change-items/${changeItemId}/deactivate`);
  },

  convertFindingToChangeItem: async (
    revisionId: number,
    findingId: number,
    request: ConvertFindingRequest
  ): Promise<RevisionChangeItemDto> => {
    const res = await apiClient.post(
      `/document-control/revisions/${revisionId}/findings/${findingId}/convert`,
      request
    );
    return res.data.data;
  },

  getImpactAssessment: async (
    revisionId: number
  ): Promise<RevisionImpactAssessmentDto | null> => {
    const res = await apiClient.get(
      `/document-control/revisions/${revisionId}/impact-assessment`
    );
    return res.data.data;
  },

  saveImpactAssessment: async (
    revisionId: number,
    request: SaveImpactAssessmentRequest
  ): Promise<RevisionImpactAssessmentDto> => {
    const res = await apiClient.post(
      `/document-control/revisions/${revisionId}/impact-assessment`,
      request
    );
    return res.data.data;
  }
};
