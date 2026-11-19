import { useCallback, useEffect, useRef, useState } from "react";
import { sectionCountersService, SectionCounter, CounterSource } from "../services/sectionCountersService";
import { useLiveRefresh } from "./useLiveRefresh";

const REFRESH_MS = 60_000;

// Live figures for a section page's cards, keyed by card, refreshed on live
// counter signals (or every minute while the page is open and live updates are paused).
// The figures are a convenience on a navigation page: if they cannot be loaded
// the cards simply show none, and every card still links to its page.
export function useSectionCounters(source: CounterSource | undefined): Record<string, SectionCounter[]> {
  const [byCard, setByCard] = useState<Record<string, SectionCounter[]>>({});
  const activeSourceRef = useRef<CounterSource | undefined>(source);

  useEffect(() => {
    activeSourceRef.current = source;
    return () => {
      activeSourceRef.current = undefined;
    };
  }, [source]);

  const load = useCallback(() => {
    if (!source) return;
    const requestedSource = source;
    sectionCountersService.get(requestedSource)
      .then((cards) => {
        if (activeSourceRef.current === requestedSource) {
          setByCard(Object.fromEntries(cards.map((c) => [c.card, c.counters])));
        }
      })
      .catch(() => {});
  }, [source]);

  useEffect(() => {
    setByCard({});
    if (!source) return;
    load();
  }, [source, load]);

  useLiveRefresh({
    kind: "counters",
    refetch: load,
    fallbackMs: REFRESH_MS,
    enabled: Boolean(source)
  });

  return byCard;
}
