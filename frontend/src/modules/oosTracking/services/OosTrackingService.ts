import { apiClient } from "../../../services/apiClient";

// Mirrors backend Application.DTOs.OosTrackingEntryDto.
export interface OosTrackingEntry {
  newSampleId: number;
  newReferenceNumber: string;
  newSampleStatus: string;
  originSampleId: number;
  originReferenceNumber: string;
  originSampleStatus: string;
  displayName: string;
  category: string;
  batchNumber: string | null;
  retestType: string;
  testCodes: string[];
  analystNames: string[];
  openedAt: string;
}

// Mirrors backend Application.DTOs.OosGroupDto.
export interface OosGroup {
  oosGroupCode: string;
  originSampleId: number;
  originReferenceNumber: string;
  originSampleStatus: string;
  displayName: string;
  category: string;
  batchNumber: string | null;
  openedAt: string;
  hasInvestigationDocument: boolean;
  retestSamples: OosTrackingEntry[];
}

export interface OosInvestigationDocument {
  id: number;
  oosGroupCode: string;
  originalFileName: string;
  fileExtension: string;
  contentType: string;
  fileSizeBytes: number;
  contentSha256: string;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedAt: string;
  status: "Current" | "Superseded" | "Voided" | number | string;
  supersededByDocumentId: number | null;
  supersededAt: string | null;
  supersededByUserId: number | null;
  supersessionReason: string | null;
  voidedAt: string | null;
  voidedByUserId: number | null;
  voidReason: string | null;
}

export const OosTrackingService = {
  async getOosGroups(): Promise<OosGroup[]> {
    return (await apiClient.get("/oos-tracking")).data.data;
  },

  async getDocuments(oosGroupCode: string): Promise<OosInvestigationDocument[]> {
    return (await apiClient.get(`/oos-tracking/${encodeURIComponent(oosGroupCode)}/investigation-documents`)).data.data;
  },

  async uploadDocument(oosGroupCode: string, file: File): Promise<OosInvestigationDocument> {
    const fd = new FormData();
    fd.append("file", file);
    return (await apiClient.post(`/oos-tracking/${encodeURIComponent(oosGroupCode)}/investigation-documents`, fd)).data.data;
  },

  async downloadDocument(documentId: number, oosGroupCode: string): Promise<Blob> {
    const res = await apiClient.get(`/oos-investigation-documents/${documentId}/content`, {
      params: { oosGroupCode },
      responseType: "blob"
    });
    return res.data;
  },

  async supersedeDocument(
    documentId: number,
    oosGroupCode: string,
    file: File,
    reason: string
  ): Promise<OosInvestigationDocument> {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("reason", reason);
    return (await apiClient.post(`/oos-investigation-documents/${documentId}/supersede`, fd, {
      params: { oosGroupCode }
    })).data.data;
  },

  async voidDocument(documentId: number, oosGroupCode: string, reason: string): Promise<OosInvestigationDocument> {
    return (await apiClient.post(`/oos-investigation-documents/${documentId}/void`, { reason }, {
      params: { oosGroupCode }
    })).data.data;
  }
};

