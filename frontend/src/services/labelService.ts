import { apiClient, ifMatch } from "./apiClient";

export type LabelEntityType =
  | "Sample"
  | "MediaLot"
  | "SolutionPreparation"
  | "Cryovial"
  | "MaterialStockLot"
  | "EquipmentAsset";

export type LabelSymbology = "Code128" | "DataMatrix";
export type LabelLayout = "Standard" | "Small";
export type ScanOutcome = "Found" | "NotFound" | "Ambiguous";

export interface LabelTarget {
  entityType: LabelEntityType;
  entityId: number;
  code: string;
  displayName: string;
}

export interface LabelPrintStatus {
  entityType: LabelEntityType;
  entityId: number;
  canPrint: boolean;
  blockedReason: string | null;
  printCount: number;
  defaultCopies: number;
}

export interface PrintLabelItem {
  entityType: LabelEntityType;
  entityId: number;
  copies: number;
}

export interface LabelFormat {
  id: number;
  version: number;
  entityType: LabelEntityType;
  widthMm: number;
  heightMm: number;
  layout: LabelLayout;
  defaultCopies: number;
}

export interface UpdateLabelFormat {
  widthMm: number;
  heightMm: number;
  layout: LabelLayout;
  defaultCopies: number;
}

export interface ScanMatch {
  entityType: LabelEntityType;
  id: number;
  code: string;
  displayName: string;
  status: string;
  usable: boolean;
  blockedReason: string | null;
  navigationTarget: string;
  linkedEquipmentId: number | null;
}

export interface ScanResolveResponse {
  outcome: ScanOutcome;
  layoutCorrected: boolean;
  normalizedValue: string;
  match: ScanMatch | null;
  candidates: ScanMatch[];
  hint: string | null;
}

interface ApiEnvelope<T> {
  success: boolean;
  message: string | null;
  data: T;
  errors: string[] | null;
}

export async function getStatus(
  targets: readonly (Pick<LabelTarget, "entityType" | "entityId"> & Partial<LabelTarget>)[],
  signal?: AbortSignal
): Promise<LabelPrintStatus[]> {
  const res = await apiClient.post<ApiEnvelope<LabelPrintStatus[]>>(
    "/labels/status",
    {
      items: targets.map((t) => ({
        entityType: t.entityType,
        entityId: t.entityId
      }))
    },
    { signal }
  );
  return res.data.data;
}

export async function print(items: PrintLabelItem[], reason?: string): Promise<Blob> {
  const payload =
    reason !== undefined
      ? { items, reason }
      : { items };
  const res = await apiClient.post<Blob>("/labels/print", payload, {
    responseType: "blob"
  });
  return res.data;
}

export async function getFormats(signal?: AbortSignal): Promise<LabelFormat[]> {
  const res = await apiClient.get<ApiEnvelope<LabelFormat[]>>("/labels/formats", { signal });
  return res.data.data;
}

export async function updateFormat(id: number, version: number, body: UpdateLabelFormat): Promise<void> {
  await apiClient.put(`/labels/formats/${id}`, body, ifMatch(version));
}

export async function resolveScan(value: string, signal?: AbortSignal): Promise<ScanResolveResponse> {
  const res = await apiClient.get<ApiEnvelope<ScanResolveResponse>>("/scan/resolve", {
    params: { value },
    signal
  });
  return res.data.data;
}

export const labelService = {
  getStatus,
  print,
  getFormats,
  updateFormat,
  resolveScan
};
