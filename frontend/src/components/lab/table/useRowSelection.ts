import { useCallback, useEffect, useMemo, useRef, useState } from "react";

export interface RowSelection<Id> {
  selected: ReadonlySet<Id>;
  allMatching: boolean;
  count: number;
  isSelected(id: Id): boolean;
  toggle(id: Id): void;
  togglePage(ids: Id[]): void;
  selectAllMatching(total: number): void;
  clear(): void;
}

export function useRowSelection<Id>(resetKey: string): RowSelection<Id> {
  const [selected, setSelected] = useState<Set<Id>>(() => new Set());
  const [allMatching, setAllMatching] = useState(false);
  const [matchingTotal, setMatchingTotal] = useState(0);
  const prevResetKey = useRef(resetKey);

  useEffect(() => {
    if (prevResetKey.current !== resetKey) {
      prevResetKey.current = resetKey;
      setSelected(new Set());
      setAllMatching(false);
      setMatchingTotal(0);
    }
  }, [resetKey]);

  const isSelected = useCallback(
    (id: Id) => allMatching || selected.has(id),
    [allMatching, selected]
  );

  const toggle = useCallback((id: Id) => {
    setAllMatching(false);
    setMatchingTotal(0);
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const togglePage = useCallback(
    (ids: Id[]) => {
      if (ids.length === 0) return;
      if (allMatching) {
        setAllMatching(false);
        setMatchingTotal(0);
        setSelected((prev) => {
          const next = new Set(prev);
          for (const id of ids) next.delete(id);
          return next;
        });
        return;
      }
      setSelected((prev) => {
        const allPageSelected = ids.every((id) => prev.has(id));
        const next = new Set(prev);
        if (allPageSelected) {
          for (const id of ids) next.delete(id);
        } else {
          for (const id of ids) next.add(id);
        }
        return next;
      });
    },
    [allMatching]
  );

  const selectAllMatching = useCallback((total: number) => {
    setAllMatching(true);
    setMatchingTotal(total);
  }, []);

  const clear = useCallback(() => {
    setSelected(new Set());
    setAllMatching(false);
    setMatchingTotal(0);
  }, []);

  const count = allMatching ? matchingTotal : selected.size;

  return useMemo(
    () => ({
      selected,
      allMatching,
      count,
      isSelected,
      toggle,
      togglePage,
      selectAllMatching,
      clear,
    }),
    [selected, allMatching, count, isSelected, toggle, togglePage, selectAllMatching, clear]
  );
}
