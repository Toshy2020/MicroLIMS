import { useEffect, useState } from "react";
import { sectionCountersService, SectionCounter, CounterSource } from "../services/sectionCountersService";

const REFRESH_MS = 60_000;

// Live figures for a section page's cards, keyed by card, refreshed
// every minute while the page is open. The figures are a convenience on a
// navigation page: if they cannot be loaded the cards simply show none, and
// every card still links to its page.
export function useSectionCounters(source: CounterSource | undefined): Record<string, SectionCounter[]> {
  const [byCard, setByCard] = useState<Record<string, SectionCounter[]>>({});

  useEffect(() => {
    setByCard({});
    if (!source) return;
    let live = true;
    const load = () => {
      sectionCountersService.get(source)
        .then((cards) => {
          if (live) setByCard(Object.fromEntries(cards.map((c) => [c.card, c.counters])));
        })
        .catch(() => {});
    };
    load();
    const timer = setInterval(load, REFRESH_MS);
    return () => { live = false; clearInterval(timer); };
  }, [source]);

  return byCard;
}
