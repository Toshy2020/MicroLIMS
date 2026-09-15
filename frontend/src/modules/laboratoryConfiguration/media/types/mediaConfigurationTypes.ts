import { MediaIncubationConditionOption, MediaProductOption } from "../../../../services/masterDataOptions";

export type { MediaIncubationConditionOption, MediaProductOption };

export interface ChallengeItem {
  id: number;
  mediaConfigurationId: number;
  organismId: number;
  challengeRole?: string | null;
  expectedDescription?: string | null;
  initialInoculum?: string | null;
  organism?: {
    id: number;
    scientificName: string;
    atccNumber?: string | null;
    commonName?: string | null;
  } | null;
}

export interface MediaConfigurationItem {
  id: number;
  name: string;
  mediaProductId: number;
  mediaProductCode?: string | null;
  evaluationType: string;
  // The chosen incubation condition - the four values below are read from it.
  mediaIncubationConditionId: number;
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
  recoveryPercentMin?: number | null;
  recoveryPercentMax?: number | null;
  challenges: ChallengeItem[];
}

export interface StagedChallenge {
  organismId: number;
  organismName?: string;
  atccNumber?: string;
  challengeRole?: string | null;
  expectedDescription?: string | null;
  initialInoculum?: string | null;
}
