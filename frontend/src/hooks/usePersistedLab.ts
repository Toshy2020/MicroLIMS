import { useCallback } from "react";
import { useSearchParams } from "react-router-dom";
import { useDebouncedUiState } from "./useDebouncedUiState";

// The laboratory a page is for. ?lab= in the address wins, then the one the
// user used last (state key nav.lastLab) when they still have it, then the
// first available. Returns "" when the user has no laboratory.
export function usePersistedLab(available: string[]): [string, (lab: string) => void] {
  const [params, setParams] = useSearchParams();
  const { value: stored, save } = useDebouncedUiState("nav.lastLab");

  const requested = params.get("lab")?.toUpperCase();
  const fromUrl = available.find((l) => l.toUpperCase() === requested);
  const fromStore = available.find((l) => l === stored);
  const lab = fromUrl ?? fromStore ?? available[0] ?? "";

  const select = useCallback((next: string) => {
    setParams((p) => {
      const copy = new URLSearchParams(p);
      copy.set("lab", next);
      return copy;
    }, { replace: true });
    save(next);
  }, [setParams, save]);

  return [lab, select];
}
