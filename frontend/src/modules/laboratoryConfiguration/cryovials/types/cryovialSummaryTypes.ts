import { SignatureTrailItem, SampleWorkflowEvent } from "../../../testingWorkspace/types/sampleSummaryTypes";

// Mirrors backend MicroLIMS.Application.DTOs.CryovialSummaryDto.

export interface IdentityConfirmationSummary {
  mediaLotNumber: string | null;
  incubatorName: string | null;
  incubationStart: string;
  incubationEnd: string;
  observationText: string;
}

export interface ThawEventSummary {
  thawedAt: string;
  thawedByName: string;
  notes: string | null;
}

export interface CryovialSummary {
  cryovialId: number;
  code: string;
  organismName: string;
  manufacturerName: string;
  materialName: string;
  materialBatchNumber: string;
  expiryDate: string;
  numberOfVialsPrepared: number;
  vialsRemaining: number;
  storageCondition: string;
  physicalCheckConfirmed: boolean;
  physicalCheckText: string;
  organismDescription?: string | null;
  preparedAt: string;
  preparedByName: string;
  approvalStatus: string;
  approvedByName: string | null;
  approvedAt: string | null;
  isDestroyed: boolean;
  identityConfirmations: IdentityConfirmationSummary[];
  thawHistory: ThawEventSummary[];
  timeline: SampleWorkflowEvent[];
  signatures: SignatureTrailItem[];
}
