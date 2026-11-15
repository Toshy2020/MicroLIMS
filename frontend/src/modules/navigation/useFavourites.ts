import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { useUiState } from "../../hooks/useUiState";
import type { MenuGroup } from "../../routes/menuConfig";
import { tableViewService, type TableViewDto } from "../../components/lab/table/services/tableViewService";
import { getErrorMessage } from "../../utils/errorMessage";
import { useProvidedMenuGroups } from "./MenuGroupsContext";
import {
  FAVOURITES_FULL_MESSAGE,
  FAVOURITES_KEY,
  FAVOURITES_TOO_LONG_MESSAGE,
  MAX_FAVOURITES,
  MAX_FAVOURITES_BYTES,
  byteLength,
  menuPages,
  parseFavourites,
  sameFavourite,
  serialiseFavourites,
  viewFavouriteTarget,
  type Favourite,
  type ResolvedFavourite
} from "./favourites";

export type { Favourite, ResolvedFavourite } from "./favourites";

export interface UseFavourites {
  items: ResolvedFavourite[];
  loaded: boolean;
  isFavourite(f: Favourite): boolean;
  // Resolves true when the list changed (false when refused or the save failed).
  toggle(f: Favourite): Promise<boolean>;
  move(index: number, dir: -1 | 1): Promise<void>;
  remove(index: number): Promise<void>;
  full: boolean;
}

type ViewLists = Record<string, TableViewDto[] | "failed">;

const NO_GROUPS: MenuGroup[] = [];

export function useFavourites(groupsOverride?: MenuGroup[]): UseFavourites {
  const provided = useProvidedMenuGroups();
  const groups = groupsOverride ?? provided ?? NO_GROUPS;
  const state = useUiState(FAVOURITES_KEY);
  const stored = useMemo(() => parseFavourites(state.value), [state.value]);
  const storedRef = useRef(stored);
  storedRef.current = stored;
  const { set } = state;

  // Saved-view names are fetched only when a view favourite exists, one list per table.
  const tablesKey = useMemo(
    () => Array.from(new Set(stored.flatMap((f) => (f.t === "v" ? [f.table] : [])))).sort().join("|"),
    [stored]
  );
  const [viewLists, setViewLists] = useState<ViewLists>({});
  useEffect(() => {
    if (!tablesKey) return;
    const controller = new AbortController();
    tablesKey.split("|").forEach((table) => {
      tableViewService
        .list(table, controller.signal)
        .then((list) => {
          if (!controller.signal.aborted) setViewLists((prev) => ({ ...prev, [table]: list }));
        })
        .catch(() => {
          if (!controller.signal.aborted) setViewLists((prev) => ({ ...prev, [table]: "failed" }));
        });
    });
    return () => controller.abort();
  }, [tablesKey]);

  const items = useMemo<ResolvedFavourite[]>(() => {
    const pages = menuPages(groups);
    return stored.map((fav): ResolvedFavourite => {
      if (fav.t === "p") {
        const page = pages.get(fav.path);
        return page
          ? { fav, label: page.label, to: page.path, available: true }
          : { fav, label: fav.path, to: null, available: false };
      }
      const to = viewFavouriteTarget(fav.table, fav.view);
      const list = viewLists[fav.table];
      if (!Array.isArray(list)) {
        // Not loaded yet (or unreadable): keep it usable rather than claiming it is gone.
        return { fav, label: "Saved view", to, available: to !== null };
      }
      const view = list.find((v) => v.id === fav.view);
      return view
        ? { fav, label: view.name, to, available: to !== null }
        : { fav, label: "Saved view", to: null, available: false };
    });
  }, [stored, groups, viewLists]);

  const save = useCallback(
    async (next: Favourite[]): Promise<boolean> => {
      const json = serialiseFavourites(next);
      if (byteLength(json) > MAX_FAVOURITES_BYTES) {
        toast.error(FAVOURITES_TOO_LONG_MESSAGE);
        return false;
      }
      try {
        await set(json);
        return true;
      } catch (err) {
        toast.error(getErrorMessage(err, "Could not save your favourites."));
        return false;
      }
    },
    [set]
  );

  const isFavourite = useCallback((f: Favourite) => stored.some((s) => sameFavourite(s, f)), [stored]);

  const toggle = useCallback(
    async (f: Favourite): Promise<boolean> => {
      const current = storedRef.current;
      if (current.some((s) => sameFavourite(s, f))) {
        return save(current.filter((s) => !sameFavourite(s, f)));
      }
      if (current.length >= MAX_FAVOURITES) {
        toast.error(FAVOURITES_FULL_MESSAGE);
        return false;
      }
      return save([...current, f]);
    },
    [save]
  );

  const move = useCallback(
    async (index: number, dir: -1 | 1) => {
      const current = [...storedRef.current];
      const target = index + dir;
      if (index < 0 || index >= current.length || target < 0 || target >= current.length) return;
      [current[index], current[target]] = [current[target], current[index]];
      await save(current);
    },
    [save]
  );

  const remove = useCallback(
    async (index: number) => {
      const current = storedRef.current;
      if (index < 0 || index >= current.length) return;
      await save(current.filter((_, i) => i !== index));
    },
    [save]
  );

  return {
    items,
    loaded: state.loaded,
    isFavourite,
    toggle,
    move,
    remove,
    full: stored.length >= MAX_FAVOURITES
  };
}
