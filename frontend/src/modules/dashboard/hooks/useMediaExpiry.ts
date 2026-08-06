import { useApi } from "../../../hooks/useApi";
import { MediaExpiryLot } from "../types/dashboard";

export function useMediaExpiry(withinDays = 7) {
  return useApi<MediaExpiryLot[]>(`/media/expiring?withinDays=${withinDays}`);
}
