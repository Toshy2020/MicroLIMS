import { masterDataOptions } from "./masterDataOptions";

export interface ReleasedMediaItem {
  id: number;
  lotNumber: string;
  materialId: number;
  materialName?: string;
  expiryDate: string;
  isReleasedForUse: boolean;
  status: string;
  currentStockUnits?: number;
}

export interface IncubatorEquipmentItem {
  id: number;
  code: string;
  name: string;
  type: string;
  setPointTemperature?: number | null;
  calibrationStatus?: string | null;
}

let mediaCache: { data: ReleasedMediaItem[]; timestamp: number } | null = null;
let incubatorCache: { data: IncubatorEquipmentItem[]; timestamp: number } | null = null;
let mediaPromise: Promise<ReleasedMediaItem[]> | null = null;
let incubatorPromise: Promise<IncubatorEquipmentItem[]> | null = null;

const CACHE_TTL_MS = 3 * 60 * 1000; // 3 minutes

export const lookupCache = {
  getReleasedMedia: async (forceRefresh = false): Promise<ReleasedMediaItem[]> => {
    const now = Date.now();
    if (!forceRefresh && mediaCache && now - mediaCache.timestamp < CACHE_TTL_MS) {
      return mediaCache.data;
    }
    if (mediaPromise) {
      return mediaPromise;
    }
    mediaPromise = masterDataOptions
      .getReleasedMedia()
      .then((data: any) => {
        mediaCache = { data: data || [], timestamp: Date.now() };
        mediaPromise = null;
        return mediaCache.data;
      })
      .catch((err) => {
        mediaPromise = null;
        throw err;
      });
    return mediaPromise;
  },

  getIncubators: async (forceRefresh = false): Promise<IncubatorEquipmentItem[]> => {
    const now = Date.now();
    if (!forceRefresh && incubatorCache && now - incubatorCache.timestamp < CACHE_TTL_MS) {
      return incubatorCache.data;
    }
    if (incubatorPromise) {
      return incubatorPromise;
    }
    incubatorPromise = masterDataOptions
      .getEquipment("Incubator")
      .then((data: any) => {
        incubatorCache = { data: data || [], timestamp: Date.now() };
        incubatorPromise = null;
        return incubatorCache.data;
      })
      .catch((err) => {
        incubatorPromise = null;
        throw err;
      });
    return incubatorPromise;
  },

  invalidate: () => {
    mediaCache = null;
    incubatorCache = null;
    mediaPromise = null;
    incubatorPromise = null;
  }
};
