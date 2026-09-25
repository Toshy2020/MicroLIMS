import { useEffect, useState, useCallback } from "react";
import { laboratorySectionService, LaboratorySection } from "../services/laboratorySectionService";

export function useLaboratorySections() {
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    laboratorySectionService
      .getSections()
      .then((data) => {
        if (!cancelled) {
          setSections(data ?? []);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSections([]);
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const sectionName = useCallback(
    (id: number | null | undefined): string => {
      if (id == null) return "";
      const match = sections.find((s) => s.sectionId === id);
      return match ? match.sectionName : "";
    },
    [sections]
  );

  return { sections, sectionName, loading };
}
