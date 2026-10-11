import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";

// Mirrors backend MicroLIMS.Application.DTOs.Responses (LaboratoryWorkResponses.cs):
// MediaResponse, MediaEvaluationResponse, MediaEvaluationChallengeResponse, IncubationResponse.
// Enums are serialised as strings (ApiJsonOptions).

export type EvaluationType = "GrowthPromotion" | "IndicationInhibition" | "EnrichmentCharacteristics";
export type EvaluationOutcome = "Conform" | "NonConform";
export type ChallengeRole = "Inhibition" | "Indication";
export type MediaEvaluationStatus = "Assigned" | "InProgress" | "Completed";
export type MediaStatus = "Prepared" | "Active" | "Expired" | "QuarantineFailed" | "Destroyed" | "OutOfStock";

// MediaResponse. Scalar fields only; the nested autoclave equipment is not read by any screen.
export interface MediaLot {
  id: number;
  materialId: number;
  material: MaterialItem | null;
  lotNumber: string;
  manufacturerLot: string;
  manufacturerName: string;
  totalWeight: number;
  totalVolume: string;
  autoclaveEquipmentId: number | null;
  autoclaveProgram: string;
  loadType: string;
  temperature: number;
  cycleTime: number;
  cycleNumber: number;
  ph: number;
  expiryDate: string;
  preparedAt: string;
  preparedByUserId: number;
  status: MediaStatus;
  approvalStatus: "PendingReview" | "Approved" | "Rejected";
  approvedByUserId: number | null;
  approvedAt: string | null;
  isReleasedForUse: boolean;
}

export interface MediaIncubation {
  id: number;
  testOrderId: number | null;
  stepNumber: number;
  stepName: string;
  startedAt: string;
  completedAt: string | null;
  completedByUserId: number | null;
  outcome: string | null;
  mediaId: number | null;
  incubatorEquipmentId: number | null;
  temperature: string | null;
  duration: string | null;
  expectedReadingAt: string | null;
  incubationStartUtc: string | null;
  incubationEndUtc: string | null;
  windowReceivedAtUtc: string | null;
  startedByUserId: number | null;
  parentIncubationId: number | null;
  stageNumber: number;
  isIncubationComplete: boolean;
}

export interface MediaEvaluationChallenge {
  id: number;
  mediaEvaluationId: number;
  organismId: number;
  organism: { id: number; scientificName: string; atccNumber: string | null; commonName: string | null; description: string | null } | null;
  cryovialId: number | null;
  cryovial: CryovialItem | null;
  lyophilizedDiskId: number | null;
  lyophilizedDisk: MaterialItem | null;
  challengeRole: ChallengeRole | null;
  initialInoculum: string;
  incubationId: number | null;
  incubation: MediaIncubation | null;
  oldMediaCount: number | null;
  newMediaCount: number | null;
  recoveryPercent: number | null;
  referenceMediaId: number | null;
  referenceMedia: MediaLot | null;
  referenceMediaLabel: string | null;
  growthObserved: boolean | null;
  observedDescription: string | null;
  expectedDescription: string | null;
  isTurbid: boolean | null;
  outcome: EvaluationOutcome | null;
  readAt: string | null;
  readByUserId: number | null;
}

export interface MediaEvaluationDetail {
  id: number;
  mediaId: number;
  media: MediaLot | null;
  evaluationType: EvaluationType;
  status: MediaEvaluationStatus;
  outcome: EvaluationOutcome | null;
  assignedAt: string;
  completedAt: string | null;
  completedByUserId: number | null;
  challenges: MediaEvaluationChallenge[];
}

// Body of POST /media-evaluations/challenges/{id}/result (RecordResultHttpRequest).
export interface RecordResultPayload {
  oldMediaCount?: number;
  newMediaCount?: number;
  referenceMediaId?: number | null;
  referenceMediaLabel?: string | null;
  growthObserved?: boolean;
  observedDescription?: string;
  manualConform?: boolean;
  isTurbid?: boolean;
}

// Per-challenge input state held by the workflow screens (raw select/text values).
export interface ChallengeFormState {
  cryovialId?: number | string;
  sourceSelection?: string;
  incubatorEquipmentId?: number | string;
  oldMediaCount?: string;
  newMediaCount?: string;
  referenceFreeText?: boolean;
  referenceMediaId?: number | string;
  referenceMediaLabel?: string;
  growthObserved?: string;
  observedDescription?: string;
  manualConform?: string;
  isTurbid?: string;
}
