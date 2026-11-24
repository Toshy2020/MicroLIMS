import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { laboratorySectionService, LaboratorySection } from "../services/laboratorySectionService";
import { PERMISSIONS } from "../routes/routes";
import { clearCachedLabs, readCachedLabs, writeCachedLabs } from "./myLabsCache";

// A user holding Sections.ViewAll (the System Administrator by default) gets
// every active section from /org/my-sections for reading (see
// GetReadableSectionIdsAsync returning null for that permission), but the menu
// still forces both lab codes here rather than trusting that response
// literally: such a user must always see both laboratory areas even if a
// section is momentarily inactive or the endpoint returns nothing. It is a
// read-only view - the server still refuses every write to a section the user
// is not a member of - and it follows the permission, not the role.
const VIEW_ALL_LAB_CODES = ["MICRO", "FP"];
const VIEW_ALL_PHYSCHEM_AREAS: ("fp" | "rmpm")[] = ["fp", "rmpm"];

// Several components on one screen (layout, menu, page) ask for the sections at once. Callers that
// arrive while a request for the same user is in flight share it; nothing is kept after it settles,
// so the next load always asks the server again.
const SHARE_WINDOW_MS = 15_000;
const inFlight = new Map<string, { startedAt: number; request: Promise<LaboratorySection[]> }>();

/** Test hook: forget any request still being shared. */
export function resetSharedMyLabsRequests() {
  inFlight.clear();
  newestRequest.clear();
}

// The newest request started per user. An older request that finishes late (after the share window
// let a newer one start) must not overwrite the cache the newer answer wrote.
const newestRequest = new Map<string, Promise<LaboratorySection[]>>();

function fetchMySectionsShared(username: string | null | undefined): { request: Promise<LaboratorySection[]>; isNewest: () => boolean } {
  const key = username ?? "";
  const request = startOrJoin(key);
  return { request, isNewest: () => newestRequest.get(key) === request };
}

function startOrJoin(key: string): Promise<LaboratorySection[]> {
  const running = inFlight.get(key);
  // A request that has hung for a long time is not shared with a newer caller.
  if (running && Date.now() - running.startedAt < SHARE_WINDOW_MS) return running.request;
  const request: Promise<LaboratorySection[]> = Promise.resolve(laboratorySectionService.getMySections()).finally(() => {
    if (inFlight.get(key)?.request === request) inFlight.delete(key);
  });
  inFlight.set(key, { startedAt: Date.now(), request });
  newestRequest.set(key, request);
  return request;
}

export function useMyLabs(): {
  labs: LaboratorySection[];
  codes: string[];
  physchemAreas: ("fp" | "rmpm")[];
  loading: boolean;
} {
  const { permissions, username } = useAuth();
  const viewsEverySection = permissions.includes(PERMISSIONS.SECTIONS_VIEW_ALL);
  const [labs, setLabs] = useState<LaboratorySection[]>(() => readCachedLabs(username) ?? []);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    // A different user signed in on this page: never keep the previous user's laboratories.
    setLabs(readCachedLabs(username) ?? []);
    const { request, isNewest } = fetchMySectionsShared(username);
    request
      .then((data) => {
        if (!cancelled) {
          setLabs(data ?? []);
          if (isNewest()) writeCachedLabs(username, data ?? []);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          clearCachedLabs(username);
          setLabs([]);
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [username]);

  // Memoized so callers can use it as a hook dependency.
  const codes = useMemo(
    () =>
      viewsEverySection
        ? VIEW_ALL_LAB_CODES
        : Array.from(new Set(labs.map((s) => s.sectionCode))),
    [viewsEverySection, labs]
  );

  const physchemAreas = useMemo<("fp" | "rmpm")[]>(
    () => {
      if (viewsEverySection) {
        return VIEW_ALL_PHYSCHEM_AREAS;
      }
      const fpSection = labs.find((s) => s.sectionCode === "FP");
      return fpSection?.physchemAreas ?? [];
    },
    [viewsEverySection, labs]
  );

  return { labs, codes, physchemAreas, loading };
}
