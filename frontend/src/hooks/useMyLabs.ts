import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { laboratorySectionService, LaboratorySection } from "../services/laboratorySectionService";
import { PERMISSIONS } from "../routes/routes";

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

// The sections a user belongs to rarely change, so the last answer is kept per user and shown on the
// first paint while /org/my-sections is fetched again. Without it a Section Head's Laboratories menu
// is missing for the first seconds of every page load. The fresh answer always replaces the kept one,
// and the server still decides what the user may open.
const cacheKey = (username: string | null | undefined) => `microlims.myLabs.v1:${username ?? ""}`;

function readCachedLabs(username: string | null | undefined): LaboratorySection[] | null {
  if (!username) return null;
  try {
    const raw = window.localStorage.getItem(cacheKey(username));
    const parsed = raw ? (JSON.parse(raw) as unknown) : null;
    return Array.isArray(parsed) ? (parsed as LaboratorySection[]) : null;
  } catch {
    return null;
  }
}

function writeCachedLabs(username: string | null | undefined, labs: LaboratorySection[]) {
  if (!username) return;
  try {
    window.localStorage.setItem(cacheKey(username), JSON.stringify(labs));
  } catch {
    /* storage unavailable: the menu just waits for the fetch */
  }
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
    laboratorySectionService
      .getMySections()
      .then((data) => {
        if (!cancelled) {
          setLabs(data ?? []);
          writeCachedLabs(username, data ?? []);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
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
