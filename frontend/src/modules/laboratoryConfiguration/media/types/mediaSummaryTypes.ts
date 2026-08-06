import { SignatureTrailItem, SampleWorkflowEvent } from "../../../testingWorkspace/types/sampleSummaryTypes";

// Mirrors backend MicroLIMS.Application.DTOs.MediaSummaryDto.

export interface MediaChallengeSummary {
  organismName: string;
  challengeRole: string | null;
  cryovialCode: string | null;
  initialInoculum: string;
  incubatorName: string | null;
  temperature: string | null;
  duration: string | null;
  incubationStartedAt: string | null;
  expectedReadingAt: string | null;
  oldMediaCount: number | null;
  newMediaCount: number | null;
  recoveryPercent: number | null;
  growthObserved: boolean | null;
  observedDescription: string | null;
  expectedDescription: string | null;
  isTurbid: boolean | null;
  outcome: string | null;
  readAt: string | null;
  readByName: string | null;
}

export interface MediaEvaluationSummary {
  evaluationType: string;
  status: string;
  outcome: string | null;
  assignedAt: string;
  completedAt: string | null;
  completedByName: string | null;
  challenges: MediaChallengeSummary[];
}

export interface MediaSummary {
  mediaId: number;
  lotNumber: string;
  mediaClass: string;
  materialName: string;
  manufacturerName: string;
  manufacturerLot: string;
  totalWeight: number;
  totalVolume: string;
  autoclaveName: string | null;
  autoclaveProgram: string;
  loadType: string;
  temperature: number;
  cycleTime: number;
  cycleNumber: number;
  ph: number;
  expiryDate: string;
  preparedAt: string;
  preparedByName: string;
  status: string;
  approvalStatus: string;
  isReleasedForUse: boolean;
  approvedByName: string | null;
  approvedAt: string | null;
  evaluation: MediaEvaluationSummary | null;
  timeline: SampleWorkflowEvent[];
  signatures: SignatureTrailItem[];
}
