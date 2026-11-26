import { apiClient, ifMatch } from "./apiClient";
import type {
  QualityAttachmentDto,
  QualityDeviationStatus,
  QualityLinkRequest,
  QualityLinkTargetType,
  QualityRecordType
} from "./qualityService";

export interface QualityRecordDto {
  id: number;
  number: string;
  type: QualityRecordType;
  title: string;
  description: string;
  sectionId: number | null;
  raisedByUserId: number | null;
  raisedAtUtc: string;
  ownerUserId: number | null;
  version: number;
  attachments: QualityAttachmentDto[];
}

export interface QualityRecordRefDto {
  id: number;
  number: string;
  type: QualityRecordType;
  title: string;
  isRestricted?: boolean;
}

export interface RaiseDeviationRequest {
  title: string;
  description: string;
  occurredAtUtc: string;
  location: string | null;
  immediateAction: string;
  sectionId: number | null;
  links: QualityLinkRequest[] | null;
}

export interface RaiseDeviationFromSourceRequest {
  title: string;
  occurredAtUtc: string;
  immediateAction: string;
}

export interface DeviationDto {
  id: number;
  version: number;
  record: QualityRecordDto;
  status: QualityDeviationStatus;
  occurredAtUtc: string;
  location: string | null;
  immediateAction: string;
  classificationId: number | null;
  dueDate: string | null;
  impactAssessment: string | null;
  rootCauseCategoryId: number | null;
  rootCauseDescription: string | null;
  proposedActions: string | null;
  capaRequired: boolean | null;
  rejectionReason: string | null;
  cancelReason: string | null;
}

export const qualityEntryService = {
  async forTarget(
    targetType: QualityLinkTargetType,
    targetId: number,
    offset = 0,
    pageSize = 25,
    signal?: AbortSignal
  ): Promise<QualityRecordRefDto[]> {
    return (
      await apiClient.get(`/quality/for-target/${targetType}/${targetId}`, {
        params: { offset, pageSize },
        signal
      })
    ).data.data;
  },

  async raiseDeviation(request: RaiseDeviationRequest): Promise<DeviationDto> {
    return (await apiClient.post("/quality/deviations", request)).data.data;
  },

  async raiseFromOosRequest(
    requestId: number,
    request: RaiseDeviationFromSourceRequest,
    version: number
  ): Promise<DeviationDto> {
    return (
      await apiClient.post(
        `/quality/deviations/from-oos-request/${requestId}`,
        request,
        ifMatch(version)
      )
    ).data.data;
  },

  async escalateMonitoringDeviation(
    monitoringDeviationId: number,
    request: RaiseDeviationFromSourceRequest,
    version: number
  ): Promise<DeviationDto> {
    return (
      await apiClient.post(
        `/quality/deviations/from-monitoring-deviation/${monitoringDeviationId}`,
        request,
        ifMatch(version)
      )
    ).data.data;
  },

  async raiseFromAuditFinding(
    findingId: number,
    request: RaiseDeviationFromSourceRequest,
    version: number
  ): Promise<DeviationDto> {
    return (
      await apiClient.post(
        `/quality/deviations/from-audit-finding/${findingId}`,
        request,
        ifMatch(version)
      )
    ).data.data;
  }
};
