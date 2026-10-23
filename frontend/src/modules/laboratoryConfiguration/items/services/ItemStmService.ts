import { apiClient } from "../../../../services/apiClient";

export type ItemStmStatus = "NotLinked" | "NotFound" | "NoEffectiveRevision" | "Effective";

export interface ItemStmDto {
  status: ItemStmStatus;
  sopNumber: string | null;
  documentMasterId: number | null;
  companyDocumentCode: string | null;
  title: string | null;
  documentTypeCode: string | null;
  revisionNumber: string | null;
  effectiveDate: string | null;
  controlledPdfFileId: number | null;
  controlledPdfFileName: string | null;
  canDownload: boolean;
}

export const ItemStmService = {
  async getForItem(itemId: number): Promise<ItemStmDto> {
    const res = await apiClient.get<{ data: ItemStmDto }>(`/items/${itemId}/stm`);
    return res.data.data;
  },
};
