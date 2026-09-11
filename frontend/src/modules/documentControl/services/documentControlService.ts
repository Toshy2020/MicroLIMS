import { apiClient } from "../../../services/apiClient";
import type {
  DocumentMasterDto,
  DocumentLibraryResponse,
  DocumentLibraryFilterRequest,
  RegisterDocumentMasterRequest,
  UpdateDocumentMasterDraftRequest,
  VoidDocumentMasterRequest,
  CancelDraftRevisionRequest,
  DocumentRevisionDto,
  RevisionFileDto,
  FileRole,
  CreateAssignmentRequest,
  DocumentMasterAssignmentDto,
  DocumentTypeDto,
  CreateDocumentTypeRequest,
  UpdateDocumentTypeRequest,
  DocumentDepartmentDto,
  CreateDocumentDepartmentRequest,
  UpdateDocumentDepartmentRequest,
  DocumentSectionDto,
  CreateDocumentSectionRequest,
  UpdateDocumentSectionRequest,
  DocumentNumberingConfigDto,
  UpdateDocumentNumberingConfigRequest,
  ConfigurationSettingDto,
  DocumentAuditResponse,
  DocumentAuditFilterRequest,
  DocumentAuditItemDto
} from "../types/documentControlTypes";

export const documentControlService = {
  // ---- Document Library & Masters ----

  getLibrary: async (filter?: DocumentLibraryFilterRequest): Promise<DocumentLibraryResponse> => {
    const res = await apiClient.get("/document-control/library", { params: filter });
    return res.data.data;
  },

  getById: async (id: number): Promise<DocumentMasterDto> => {
    const res = await apiClient.get(`/document-control/documents/${id}`);
    return res.data.data;
  },

  registerDocument: async (request: RegisterDocumentMasterRequest): Promise<DocumentMasterDto> => {
    const res = await apiClient.post("/document-control/documents", request);
    return res.data.data;
  },

  updateDraftMetadata: async (
    id: number,
    request: UpdateDocumentMasterDraftRequest
  ): Promise<DocumentMasterDto> => {
    const res = await apiClient.put(`/document-control/documents/${id}/metadata`, request);
    return res.data.data;
  },

  voidMaster: async (id: number, reason: string): Promise<DocumentMasterDto> => {
    const payload: VoidDocumentMasterRequest = { reason };
    const res = await apiClient.post(`/document-control/documents/${id}/void`, payload);
    return res.data.data;
  },

  cancelDraftRevision: async (
    revisionId: number,
    reason: string
  ): Promise<DocumentRevisionDto> => {
    const payload: CancelDraftRevisionRequest = { reason };
    const res = await apiClient.post(`/document-control/revisions/${revisionId}/cancel`, payload);
    return res.data.data;
  },

  // ---- Workflow Assignments ----

  addAssignment: async (
    masterId: number,
    request: CreateAssignmentRequest
  ): Promise<DocumentMasterAssignmentDto> => {
    const res = await apiClient.post(`/document-control/documents/${masterId}/assignments`, request);
    return res.data.data;
  },

  // Deactivates the assignment (IsActive = false) and retains the record.
  // Controlled records are never deleted - DC-URS-184, BR-015, FS-1a-170.
  removeAssignment: async (masterId: number, assignmentId: number): Promise<void> => {
    await apiClient.post(`/document-control/documents/${masterId}/assignments/${assignmentId}/deactivate`);
  },

  // ---- Controlled Files ----

  uploadFile: async (
    revisionId: number,
    fileRole: FileRole,
    file: File
  ): Promise<RevisionFileDto> => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("fileRole", fileRole);

    const res = await apiClient.post(`/document-control/revisions/${revisionId}/files`, formData, {
      headers: { "Content-Type": "multipart/form-data" }
    });
    return res.data.data;
  },

  downloadFile: async (fileId: number, fileName: string): Promise<void> => {
    const res = await apiClient.get(`/document-control/files/${fileId}/download`, {
      responseType: "blob"
    });

    const blob = new Blob([res.data]);
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  },

  viewFileBlob: async (fileId: number): Promise<{ blob: Blob; url: string }> => {
    const res = await apiClient.get(`/document-control/files/${fileId}/view`, {
      responseType: "blob"
    });
    const blob = new Blob([res.data], { type: "application/pdf" });
    const url = window.URL.createObjectURL(blob);
    return { blob, url };
  },

  // ---- Configuration ----

  getTypes: async (includeInactive: boolean = false): Promise<DocumentTypeDto[]> => {
    const res = await apiClient.get("/document-control/config/types", {
      params: { includeInactive }
    });
    return res.data.data;
  },

  createType: async (request: CreateDocumentTypeRequest): Promise<DocumentTypeDto> => {
    const res = await apiClient.post("/document-control/config/types", request);
    return res.data.data;
  },

  updateType: async (id: number, request: UpdateDocumentTypeRequest): Promise<DocumentTypeDto> => {
    const res = await apiClient.put(`/document-control/config/types/${id}`, request);
    return res.data.data;
  },

  getDepartments: async (includeInactive: boolean = false): Promise<DocumentDepartmentDto[]> => {
    const res = await apiClient.get("/document-control/config/departments", {
      params: { includeInactive }
    });
    return res.data.data;
  },

  createDepartment: async (
    request: CreateDocumentDepartmentRequest
  ): Promise<DocumentDepartmentDto> => {
    const res = await apiClient.post("/document-control/config/departments", request);
    return res.data.data;
  },

  updateDepartment: async (
    id: number,
    request: UpdateDocumentDepartmentRequest
  ): Promise<DocumentDepartmentDto> => {
    const res = await apiClient.put(`/document-control/config/departments/${id}`, request);
    return res.data.data;
  },

  getSections: async (
    departmentId: number,
    includeInactive: boolean = false
  ): Promise<DocumentSectionDto[]> => {
    const res = await apiClient.get(`/document-control/config/departments/${departmentId}/sections`, {
      params: { includeInactive }
    });
    return res.data.data;
  },

  createSection: async (request: CreateDocumentSectionRequest): Promise<DocumentSectionDto> => {
    const res = await apiClient.post("/document-control/config/sections", request);
    return res.data.data;
  },

  updateSection: async (
    id: number,
    request: UpdateDocumentSectionRequest
  ): Promise<DocumentSectionDto> => {
    const res = await apiClient.put(`/document-control/config/sections/${id}`, request);
    return res.data.data;
  },

  getNumberingConfig: async (): Promise<DocumentNumberingConfigDto> => {
    const res = await apiClient.get("/document-control/config/numbering");
    return res.data.data;
  },

  updateNumberingConfig: async (
    request: UpdateDocumentNumberingConfigRequest
  ): Promise<DocumentNumberingConfigDto> => {
    const res = await apiClient.put("/document-control/config/numbering", request);
    return res.data.data;
  },

  getSettings: async (): Promise<ConfigurationSettingDto[]> => {
    const res = await apiClient.get("/document-control/config/settings");
    return res.data.data;
  },

  updateSetting: async (
    key: string,
    settingValue: string
  ): Promise<ConfigurationSettingDto> => {
    const res = await apiClient.put(`/document-control/config/settings/${encodeURIComponent(key)}`, {
      settingValue
    });
    return res.data.data;
  },

  // ---- Audit Trail ----

  searchAudit: async (filter?: DocumentAuditFilterRequest): Promise<DocumentAuditResponse> => {
    const res = await apiClient.get("/document-control/audit", { params: filter });
    return res.data.data;
  },

  getDocumentAudit: async (
    documentMasterId: number,
    revisionId?: number
  ): Promise<DocumentAuditItemDto[]> => {
    const res = await apiClient.get(`/document-control/documents/${documentMasterId}/audit`, {
      params: revisionId ? { revisionId } : undefined
    });
    return res.data.data;
  },

  exportAuditCsv: async (filter?: DocumentAuditFilterRequest): Promise<void> => {
    const res = await apiClient.post("/document-control/audit/export", filter ?? {}, {
      responseType: "blob"
    });

    const blob = new Blob([res.data], { type: "text/csv;charset=utf-8;" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `audit_trail_${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
};
