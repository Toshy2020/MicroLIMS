import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { laboratorySectionService, LaboratorySection } from "../services/laboratorySectionService";

// A System Administrator's /org/my-sections response already reflects an
// unrestricted scope (every active section - see GetAccessibleSectionIdsAsync
// returning null for that role), but the menu still forces both lab codes
// here rather than trusting that response literally: an administrator must
// always see both laboratory areas even if a section is momentarily
// inactive or the endpoint returns nothing.
const ADMIN_LAB_CODES = ["MICRO", "FP"];
const ADMIN_PHYSCHEM_AREAS: ("fp" | "rmpm")[] = ["fp", "rmpm"];

export function useMyLabs(): {
  labs: LaboratorySection[];
  codes: string[];
  physchemAreas: ("fp" | "rmpm")[];
  loading: boolean;
} {
  const { role } = useAuth();
  const [labs, setLabs] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    laboratorySectionService
      .getMySections()
      .then((data) => {
        if (!cancelled) {
          setLabs(data ?? []);
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
  }, []);

  // Memoized so callers can use it as a hook dependency.
  const codes = useMemo(
    () =>
      role === "SystemAdministrator"
        ? ADMIN_LAB_CODES
        : Array.from(new Set(labs.map((s) => s.sectionCode))),
    [role, labs]
  );

  const physchemAreas = useMemo<("fp" | "rmpm")[]>(
    () => {
      if (role === "SystemAdministrator") {
        return ADMIN_PHYSCHEM_AREAS;
      }
      const fpSection = labs.find((s) => s.sectionCode === "FP");
      return fpSection?.physchemAreas ?? [];
    },
    [role, labs]
  );

  return { labs, codes, physchemAreas, loading };
}
