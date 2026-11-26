import { useEffect, useState } from "react";
import { oosInvestigationService } from "../../../services/oosInvestigationService";
import { qualityService } from "../../../services/qualityService";

export type LookupStatus = "loading" | "ready" | "failed";

export interface LookupOption {
  id: number;
  label: string;
}

export interface Lookup {
  options: LookupOption[];
  status: LookupStatus;
}

// Loads the choices of one select once, aborting the request when the form goes away. A failed load is
// reported as a status so the select can say so instead of looking empty.
function useLookup(load: (signal: AbortSignal) => Promise<LookupOption[]>, enabled: boolean): Lookup {
  const [state, setState] = useState<Lookup>({ options: [], status: "loading" });

  useEffect(() => {
    if (!enabled) return undefined;
    const controller = new AbortController();
    setState({ options: [], status: "loading" });
    load(controller.signal)
      .then((options) => {
        if (!controller.signal.aborted) setState({ options, status: "ready" });
      })
      .catch(() => {
        if (!controller.signal.aborted) setState({ options: [], status: "failed" });
      });
    return () => controller.abort();
    // The loaders below are module-level functions with a stable identity.
  }, [load, enabled]);

  return state;
}

const PAGE_SIZE = 100;

// Active users who may be named owner or responsible person: the directory is open to every signed-in user,
// and the server still checks the person's role and laboratory when the request arrives.
const loadUsers = async (signal: AbortSignal): Promise<LookupOption[]> =>
  (await qualityService.listUserDirectory("Quality.View", signal))
    .filter((user) => user.isActive)
    .map((user) => ({ id: user.id, label: `${user.fullName} (${user.username})` }));

const loadClassifications = async (signal: AbortSignal): Promise<LookupOption[]> =>
  (await qualityService.listClassifications(1, PAGE_SIZE, signal))
    .filter((classification) => classification.isActive)
    .sort((a, b) => a.order - b.order)
    .map((classification) => ({ id: classification.id, label: classification.name }));

// Root-cause categories are shared with the OOS investigation (package D1).
const loadCauseCategories = async (signal: AbortSignal): Promise<LookupOption[]> =>
  (await oosInvestigationService.listCategories(1, PAGE_SIZE, signal)).items
    .filter((category) => category.isActive)
    .map((category) => ({ id: category.id, label: category.name }));

export const useUserOptions = (enabled = true): Lookup => useLookup(loadUsers, enabled);
export const useClassificationOptions = (enabled = true): Lookup => useLookup(loadClassifications, enabled);
export const useCauseCategoryOptions = (enabled = true): Lookup => useLookup(loadCauseCategories, enabled);
