export interface EligibleThawedVial {
  thawEventId: number;
  cryovialId: number;
  cryovialCode: string;
  organismName: string;
  thawedAt: string;
  thawedByName: string;
  eligibleBecause: string;
  usedInMediaLots: string[];
}

export interface EligibleWithdrawnBatch {
  cryovialId: number;
  cryovialCode: string;
  organismName: string;
  vialsRemaining: number;
}

export interface BiologicalWasteEligible {
  thawedVials: EligibleThawedVial[];
  withdrawnBatches: EligibleWithdrawnBatch[];
}

export interface BiologicalWasteItem {
  id: number;
  itemType: "ThawedVial" | "UnthawedCryovials" | "Other" | string;
  description: string;
  quantity: number;
  unit: string;
  thawEventId: number | null;
  cryovialId: number | null;
}

export interface BiologicalWasteDestruction {
  id: number;
  reference: string;
  sectionId: number;
  autoclaveName: string;
  autoclaveCode: string;
  programCode: string;
  programName: string;
  loadType: string;
  temperature: number;
  cycleTimeMinutes: number;
  cycleNumber: string;
  cycleStartedAt: string;
  notes: string | null;
  recordedByName: string;
  recordedAt: string;
  signedByName: string;
  signedByRole: string;
  signedAt: string;
  cryovialItemCount: number;
  otherItemCount: number;
  items: BiologicalWasteItem[];
}

export interface OtherWasteItemInput {
  description: string;
  quantity: number;
  unit: string;
}

// Body of POST /biological-waste-destructions.
export interface RecordBiologicalWastePayload {
  autoclaveEquipmentId: number;
  autoclaveProgramId: number;
  cycleNumber: string;
  cycleStartedAt: string;
  thawEventIds: number[];
  withdrawnCryovialIds: number[];
  otherItems: OtherWasteItemInput[];
  notes: string | null;
  password: string;
}
