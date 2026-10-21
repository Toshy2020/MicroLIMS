import { apiClient } from "../services/apiClient";

// Mirrors MicroLIMS.Application.DTOs.Search.GlobalSearchItemDto.
export interface GlobalSearchItemDto {
  id: number;
  entityType: string;
  title: string;
  subtitle: string;
  status: string;
  matchedField: string;
  matchedValue: string;
  badgeColor: string;
  navigationTarget: string;
  metadata?: Record<string, string> | null;
}

export interface GlobalSearchCategoryDto {
  categoryName: string;
  count: number;
  items: GlobalSearchItemDto[];
}

export interface GlobalSearchResultDto {
  query: string;
  totalMatches: number;
  categories: GlobalSearchCategoryDto[];
  executionTimeMs: number;
}

export const globalSearchApi = {
  search: async (
    query: string,
    category?: string,
    limit: number = 5,
    signal?: AbortSignal
  ): Promise<GlobalSearchResultDto> => {
    const res = await apiClient.get<{ success: boolean; data: GlobalSearchResultDto }>("/search/global", {
      params: { q: query, category, limit },
      signal
    });
    return res.data?.data ?? { query, totalMatches: 0, categories: [], executionTimeMs: 0 };
  }
};
