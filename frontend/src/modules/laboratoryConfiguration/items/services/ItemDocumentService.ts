import { apiClient } from "../../../../services/apiClient";

export enum ItemDocumentType {
  Sop = 0,
  VerificationReport = 1,
}

export enum MaterialDocumentStatus {
  Current = 0,
  Superseded = 1,
  Voided = 2,
}

export interface ItemDocumentDto {
  id: number;
  itemId: number;
  documentType: ItemDocumentType;
  originalFileName: string;
  version: string;
  effectiveDate: string | null;
  fileSizeBytes: number;
  uploadedByUserId: number;
  uploadedByUserName: string;
  uploadedAt: string;
  status: MaterialDocumentStatus;
  supersededByDocumentId: number | null;
  supersededAt: string | null;
}

export const ItemDocumentService = {
  async getDocumentsForItem(itemId: number): Promise<ItemDocumentDto[]> {
    const res = await apiClient.get<ItemDocumentDto[]>(`/api/items/${itemId}/documents`);
    return res.data;
  },

  async uploadDocument(
    itemId: number,
    documentType: ItemDocumentType,
    version: string,
    effectiveDate: string | null,
    file: File
  ): Promise<ItemDocumentDto> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("documentType", documentType.toString());
    formData.append("version", version || "Rev 01");
    if (effectiveDate) {
      formData.append("effectiveDate", effectiveDate);
    }

    const res = await apiClient.post<ItemDocumentDto>(`/api/items/${itemId}/documents`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return res.data;
  },

  getContentUrl(documentId: number, download = false): string {
    const base = apiClient.defaults.baseURL || "";
    return `${base}/api/item-documents/${documentId}/content${download ? "?download=true" : ""}`;
  },
};
