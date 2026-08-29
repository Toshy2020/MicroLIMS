export interface CryovialItem {
  id: number;
  code: string;
  materialId: number;
  material?: {
    id: number;
    materialName: string;
    batchNumber: string;
    manufacturerName?: string;
    quantityRemaining?: number;
    unit?: string;
    status?: string;
  };
  organismId?: number;
  organism?: {
    id: number;
    scientificName: string;
    atccNumber?: string | null;
    strainNumber?: string | null;
    description?: string | null;
  };
  organismNameSnapshot: string;
  manufacturerName: string;
  expiryDate: string;
  numberOfVialsPrepared: number;
  vialsRemaining: number;
  storageCondition: string;
  physicalCheckConfirmed: boolean;
  physicalCheckText: string;
  preparedAt: string;
  preparedByUserId: number;
  preparedByName?: string;
  approvalStatus: "PendingReview" | "Approved" | "Rejected" | string;
  approvedByUserId?: number | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
  isDestroyed: boolean;
  identityConfirmations?: {
    id?: number;
    mediaId: number;
    incubatorEquipmentId: number;
    incubationStart: string;
    incubationEnd: string;
    observationText: string;
  }[];
}

export type CryovialKpiFilter = "all" | "approved" | "pending" | "rejected";

export interface CryovialFilterState {
  search: string;
  status: string; // "" | "Approved" | "PendingReview" | "Rejected" | "Destroyed" | "Depleted"
  organism: string;
  expiryRange: string; // "" | "expiring_30" | "expired" | "valid"
}

export interface PanelRow {
  mediaId: string;
  incubatorEquipmentId: string;
  incubationStart: string;
  incubationEnd: string;
  observationText: string;
}

export interface PrepareCryovialPayload {
  materialId: number;
  numberOfVialsPrepared: number;
  expiryDate: string;
  storageCondition: string;
  physicalCheckConfirmed: boolean;
  physicalCheckText: string;
  discsUsed: number;
  panel: {
    mediaId: number;
    incubatorEquipmentId: number;
    incubationStart: string;
    incubationEnd: string;
    observationText: string;
  }[];
}
