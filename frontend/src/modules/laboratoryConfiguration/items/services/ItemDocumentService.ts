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

// Tables that show a per-item document indicator render one badge per row,
// so a single page asks for the same items over and over - once per row, and
// again on every remount (page change, filter change, refresh). Only the
// count is cached, and concurrent rows asking for the same item share one
// request. The dialogs that manage documents keep calling
// getDocumentsForItem directly, so they always read live data.
const documentCountCache = new Map<number, { count: number; timestamp: number }>();
const documentCountInFlight = new Map<number, Promise<number>>();
const COUNT_CACHE_TTL_MS = 3 * 60 * 1000;

function forgetDocumentCount(itemId: number) {
  documentCountCache.delete(itemId);
  documentCountInFlight.delete(itemId);
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
    forgetDocumentCount(itemId);
    return res.data.data;
  },

  // Count only - for row badges. Cached and de-duplicated; see the note above.
  async getDocumentCountForItem(itemId: number): Promise<number> {
    const cached = documentCountCache.get(itemId);
    if (cached && Date.now() - cached.timestamp < COUNT_CACHE_TTL_MS) {
      return cached.count;
    }

    const pending = documentCountInFlight.get(itemId);
    if (pending) return pending;

    // Named rather than `this` so a destructured reference still works.
    const request = ItemDocumentService.getDocumentsForItem(itemId)
      .then((docs) => {
        documentCountCache.set(itemId, { count: docs.length, timestamp: Date.now() });
        documentCountInFlight.delete(itemId);
        return docs.length;
      })
      .catch((err) => {
        documentCountInFlight.delete(itemId);
        throw err;
      });

    documentCountInFlight.set(itemId, request);
    return request;
  },

  // For anything that changes what is stored against an item outside this
  // service. No argument clears every item.
  invalidateDocumentCount(itemId?: number) {
    if (itemId == null) {
      documentCountCache.clear();
      documentCountInFlight.clear();
      return;
    }
    forgetDocumentCount(itemId);
  },

  // The content endpoint is [Authorize]d and the JWT is attached by the
  // apiClient request interceptor as an Authorization header. A browser
  // navigation - <a href>, window.open on the raw URL - cannot carry that
  // header, so linking straight at the URL always arrived unauthenticated.
  // getContentUrl used to hand out exactly such a URL; these two fetch the
  // bytes through apiClient instead and hand the browser a blob, the same
  // way material, equipment and document-control downloads work.
  async downloadDocument(documentId: number, fileName: string): Promise<void> {
    const res = await apiClient.get(`/item-documents/${documentId}/content`, {
      params: { download: true },
      responseType: "blob"
    });

    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  },

  async openDocument(documentId: number): Promise<void> {
    // Claim the tab synchronously, while still inside the click, or a popup
    // blocker rejects it once the request below resolves. Note that passing
    // "noopener" here would make window.open return null by spec, so the
    // opener is severed manually instead.
    const tab = window.open("", "_blank");
    if (tab) tab.opener = null;

    try {
      const res = await apiClient.get(`/item-documents/${documentId}/content`, {
        responseType: "blob"
      });

      // Axios types header values as string | number | string[] | AxiosHeaders.
      const contentType = String(res.headers?.["content-type"] ?? "") || "application/octet-stream";
      const url = window.URL.createObjectURL(new Blob([res.data], { type: contentType }));

      if (tab) {
        tab.location.href = url;
      } else {
        // Popups blocked outright - fall back to a normal download so the
        // click still produces the document.
        const link = document.createElement("a");
        link.href = url;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      }

      // The tab reads from this URL after we return, so it cannot be revoked
      // immediately.
      window.setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      tab?.close();
      throw err;
    }
  },
};
