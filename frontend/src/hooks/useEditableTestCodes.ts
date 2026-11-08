import { useEffect, useState, useMemo } from "react";
import { masterDataOptions } from "../services/masterDataOptions";

export function useEditableTestCodes() {
  const [codes, setCodes] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = async () => {
    try {
      const data = await masterDataOptions.getEditableTestCodes();
      setCodes(data ?? []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    reload();
  }, []);

  const editableCodes = useMemo(() => new Set(codes), [codes]);

  return { codes, editableCodes, loading, reload };
}
