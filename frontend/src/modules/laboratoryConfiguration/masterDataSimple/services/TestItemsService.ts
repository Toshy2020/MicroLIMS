import { apiClient } from "../../../../services/apiClient";

export type TestItemSopStatus = "Linked" | "NotFound" | "NoNumber";

export interface TestItemRow {
  itemId: number;
  itemCode: string;
  itemName: string;
  sopNumber: string | null;
  sopStatus: TestItemSopStatus;
  documentMasterId: number | null;
  effectiveRevisionNumber: string | null;
  uploadedDocumentCount: number;
  autoAssignedOnReceipt: boolean;
  hasSpecification: boolean;
}

export interface TestItemsPage {
  items: TestItemRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export const TestItemsService = {
  async getItems(
    testId: number,
    params: { search?: string; page: number; pageSize: number },
    signal?: AbortSignal
  ): Promise<TestItemsPage> {
    const res = await apiClient.get<{ data: TestItemsPage }>(`/masterdata/test-definitions/${testId}/items`, {
      params: { search: params.search || undefined, page: params.page, pageSize: params.pageSize },
      signal
    });
    return res.data.data;
  }
};
