import { apiClient } from "../../../../services/apiClient";

// String-valued to match the backend's JSON enum serialization
// (JsonStringEnumConverter is registered globally in Program.cs, so every
// enum - including these - arrives as its name, not its numeric value).
export enum ItemDocumentType {
  Sop = "Sop",
  VerificationReport = "VerificationReport",
}

export enum MaterialDocumentStatus {
  Current = "Current",
  Superseded = "Superseded",
  Voided = "Voided",
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
    const res = await apiClient.get<{ data: ItemDocumentDto[] }>(`/items/${itemId}/documents`);
    return res.data.data;
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

    const res = await apiClient.post<{ data: ItemDocumentDto }>(`/items/${itemId}/documents`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return res.data.data;
  },

  getContentUrl(documentId: number, download = false): string {
    const base = apiClient.defaults.baseURL || "";
    return `${base}/item-documents/${documentId}/content${download ? "?download=true" : ""}`;
  },
};
