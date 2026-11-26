import { apiClient } from "./apiClient";

// The two reads the change-control prompt needs before and while a master-data change is saved
// (CONF-227). Quality records themselves are the quality register's business, not this file's.

// One approved change control, as the prompt lists it. Mirrors QualityRecordListItemDto.
export interface ApprovedChangeControl {
  id: number;
  number: string;
  type: string;
  title: string;
  status: string;
  sectionId: number | null;
  sectionName: string | null;
  version: number;
}

// Mirrors QualitySectionSettingDto.
export interface ChangeControlSectionSetting {
  sectionId: number;
  changeControlRequired: boolean;
  version: number;
}

interface PagedBody<T> {
  items?: T[];
}

export const changeControlLookupService = {
  // Change controls "Approved for implementation" for one laboratory. The server allows 100 per page at most.
  listApproved: (sectionId: number | null, signal?: AbortSignal): Promise<ApprovedChangeControl[]> =>
    apiClient
      .get<{ data?: PagedBody<ApprovedChangeControl> }>("/quality/records", {
        params: {
          type: "ChangeControl",
          status: "ApprovedForImplementation",
          ...(sectionId != null ? { sectionId } : {}),
          pageSize: 100
        },
        signal
      })
      .then((r) => r.data.data?.items ?? []),

  // Whether the laboratory requires an approved change control for master-data changes.
  getSectionSetting: (sectionId: number, signal?: AbortSignal): Promise<ChangeControlSectionSetting> =>
    apiClient
      .get<{ data: ChangeControlSectionSetting }>(`/quality/section-settings/${sectionId}`, { signal })
      .then((r) => r.data.data)
};
