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

export function useMyLabs(): { labs: LaboratorySection[]; codes: string[]; loading: boolean } {
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

  return { labs, codes, loading };
}
