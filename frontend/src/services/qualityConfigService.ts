import { apiClient, ifMatch } from "./apiClient";
import type { QualityClassificationDto, QualityRecordType } from "./qualityService";

export interface QualityChecklistItemDto {
  key: string;
  order: number;
  text: string;
  isActive: boolean;
}

export interface SaveQualityTemplateRequest {
  items: QualityChecklistItemDto[];
}

export interface QualityChecklistTemplateDto {
  id: number;
  type: QualityRecordType;
  sectionId: number | null;
  version: number;
  items: QualityChecklistItemDto[];
}

export interface SaveQualityClassificationRequest {
  name: string;
  order: number;
  isActive: boolean;
}

export interface SaveQualityDueRuleRequest {
  classificationId: number | null;
  workingDays: number | null;
}

export interface QualityDueRuleDto {
  id: number;
  type: QualityRecordType;
  classificationId: number | null;
  workingDays: number | null;
  version: number;
}

export interface QualitySectionSettingDto {
  sectionId: number;
  changeControlRequired: boolean;
  version: number;
}

export interface UpdateQualitySectionSettingRequest {
  changeControlRequired: boolean;
  reason: string;
  password: string;
}

export const qualityConfigService = {
  async getTemplate(
    type: QualityRecordType,
    sectionId?: number | null,
    signal?: AbortSignal
  ): Promise<QualityChecklistTemplateDto> {
    return (
      await apiClient.get(`/quality/config/templates/${type}`, {
        params: sectionId != null ? { sectionId } : undefined,
        signal
      })
    ).data.data;
  },

  async saveTemplate(
    type: QualityRecordType,
    sectionId: number | null,
    request: SaveQualityTemplateRequest,
    version: number
  ): Promise<QualityChecklistTemplateDto> {
    return (
      await apiClient.put(`/quality/config/templates/${type}`, request, {
        params: sectionId != null ? { sectionId } : undefined,
        ...ifMatch(version)
      })
    ).data.data;
  },

  async listClassifications(
    page = 1,
    pageSize = 100,
    signal?: AbortSignal
  ): Promise<QualityClassificationDto[]> {
    return (
      await apiClient.get("/quality/config/classifications", {
        params: { page, pageSize },
        signal
      })
    ).data.data;
  },

  async createClassification(
    request: SaveQualityClassificationRequest,
    version: number
  ): Promise<QualityClassificationDto> {
    return (
      await apiClient.post("/quality/config/classifications", request, ifMatch(version))
    ).data.data;
  },

  async saveClassification(
    id: number,
    request: SaveQualityClassificationRequest,
    version: number
  ): Promise<QualityClassificationDto> {
    return (
      await apiClient.put(`/quality/config/classifications/${id}`, request, ifMatch(version))
    ).data.data;
  },

  async listDueRules(
    page = 1,
    pageSize = 100,
    signal?: AbortSignal
  ): Promise<QualityDueRuleDto[]> {
    return (
      await apiClient.get("/quality/config/due-rules", {
        params: { page, pageSize },
        signal
      })
    ).data.data;
  },

  async saveDueRule(
    type: QualityRecordType,
    request: SaveQualityDueRuleRequest,
    version: number
  ): Promise<QualityDueRuleDto> {
    return (
      await apiClient.put(`/quality/config/due-rules/${type}`, request, ifMatch(version))
    ).data.data;
  },

  async getSectionSetting(
    sectionId: number,
    signal?: AbortSignal
  ): Promise<QualitySectionSettingDto> {
    return (await apiClient.get(`/quality/section-settings/${sectionId}`, { signal })).data.data;
  },

  async setSectionSetting(
    sectionId: number,
    request: UpdateQualitySectionSettingRequest,
    version: number
  ): Promise<QualitySectionSettingDto> {
    return (
      await apiClient.put(`/quality/section-settings/${sectionId}`, request, ifMatch(version))
    ).data.data;
  }
};
