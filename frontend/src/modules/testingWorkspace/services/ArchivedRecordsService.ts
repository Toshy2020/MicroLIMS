import { apiClient } from "../../../services/apiClient";

export interface ArchivedRecordSummary {
  id: number;
  entityType: string;
  entityId: number;
  documentId: string;
  fileName: string;
  sizeBytes: number;
  contentSha256: string;
  reason: string;
  generatedByNameSnapshot: string;
  generatedAt: string;
}

// The frozen PDFs cut at each final decision - read-only, same as the
// signature trail. Downloading re-verifies the SHA-256 server-side; a
// mismatch comes back as an X-Archive-Integrity: FAILED response header.
export const ArchivedRecordsService = {
  async getForEntity(entityType: string, entityId: number): Promise<ArchivedRecordSummary[]> {
    return (await apiClient.get("/archived-records", { params: { entityType, entityId } })).data.data;
  },

  async download(id: number, fileName: string): Promise<void> {
    const res = await apiClient.get(`/archived-records/${id}/download`, { responseType: "blob" });
    if (res.headers["x-archive-integrity"] === "FAILED") {
      // eslint-disable-next-line no-alert
      window.alert(
        "Warning: this archived copy failed its integrity check - the stored file no longer matches the hash recorded when it was signed. Do not treat it as authoritative; report this to a System Administrator."
      );
    }
    const url = window.URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();
    window.URL.revokeObjectURL(url);
  }
};
