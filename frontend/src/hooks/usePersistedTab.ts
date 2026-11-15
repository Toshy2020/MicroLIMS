import { useCallback, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useDebouncedUiState } from "./useDebouncedUiState";

// The tab a page shows. Order of authority: the address (when the page names
// a tab in `urlParam`), then the user's own choice, then the tab they used
// last time (state key nav.tab.<pageKey>), then the first tab. A stored tab
// that no longer exists is ignored. `aliases` maps retired address values to
// the tab that replaced them, so old links keep opening the right tab.
export function usePersistedTab<T extends string>(
  pageKey: string,
  tabs: readonly T[],
  urlParam?: string,
  aliases?: Readonly<Record<string, T>>
): [T, (tab: T) => void] {
  const [params, setParams] = useSearchParams();
  const { value: stored, save } = useDebouncedUiState(`nav.tab.${pageKey}`);
  const [chosen, setChosen] = useState<T | null>(null);

  const rawUrl = urlParam ? params.get(urlParam)?.toLowerCase() ?? null : null;
  const urlValue = rawUrl !== null && aliases && rawUrl in aliases ? aliases[rawUrl] : rawUrl;
  const fromUrl = tabs.find((t) => t === urlValue);
  const fromChoice = urlParam ? undefined : tabs.find((t) => t === chosen);
  const fromStore = tabs.find((t) => t === stored);
  const tab = fromUrl ?? fromChoice ?? fromStore ?? tabs[0];

  const select = useCallback((next: T) => {
    setChosen(next);
    if (urlParam) {
      setParams((p) => {
        const copy = new URLSearchParams(p);
        copy.set(urlParam, next);
        return copy;
      }, { replace: true });
    }
    save(next);
  }, [urlParam, setParams, save]);

  return [tab, select];
}
