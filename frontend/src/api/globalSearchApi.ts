import { apiClient } from "../services/apiClient";

export interface GlobalSearchItemDto {
  id: string;
  title: string;
  subtitle: string;
  category: string;
  badge?: string;
  route: string;
  matchedField: string;
  additionalInfo?: string;
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
