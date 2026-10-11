import { apiClient } from "../../../services/apiClient";
import type { ApiResponse } from "../masterDataSimple/services/EquipmentConfigurationService";
import type { MasterDataImport, MasterDataImportResult, WorkbookReport } from "./types";

const BASE = "/masterdata/workbook";

function saveBlob(data: Blob, fileName: string) {
  const url = window.URL.createObjectURL(data);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  window.URL.revokeObjectURL(url);
}

function fileNameFrom(disposition: string | undefined, fallback: string) {
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match ? decodeURIComponent(match[1]) : fallback;
}

export const masterDataWorkbookService = {
  async download() {
    const res = await apiClient.get(BASE, { responseType: "blob" });
    saveBlob(res.data, fileNameFrom(res.headers["content-disposition"], "MicroLIMS-master-data.xlsx"));
  },

  async validate(file: File): Promise<WorkbookReport> {
    const form = new FormData();
    form.append("file", file);
    const res = await apiClient.post<ApiResponse<WorkbookReport>>(`${BASE}/validate`, form);
    return res.data.data;
  },

  async load(file: File, password: string, comment: string): Promise<MasterDataImportResult> {
    const form = new FormData();
    form.append("file", file);
    form.append("password", password);
    form.append("comment", comment);
    const res = await apiClient.post<ApiResponse<MasterDataImportResult>>(`${BASE}/import`, form);
    return res.data.data;
  },

  async imports(): Promise<MasterDataImport[]> {
    const res = await apiClient.get<ApiResponse<MasterDataImport[]>>(`${BASE}/imports`);
    return res.data.data;
  },

  async verify(id: number, password: string, comment: string): Promise<MasterDataImport> {
    const res = await apiClient.post<ApiResponse<MasterDataImport>>(`${BASE}/imports/${id}/verify`, { password, comment });
    return res.data.data;
  },

  async downloadFile(item: MasterDataImport) {
    const res = await apiClient.get(`${BASE}/imports/${item.id}/file`, { responseType: "blob" });
    saveBlob(res.data, item.fileName);
  },
};
