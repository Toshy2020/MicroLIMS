import { useEffect, useState } from "react";
import { sectionCountersService, SectionCounter } from "../services/sectionCountersService";

const REFRESH_MS = 60_000;

// Live figures for a laboratory section page's cards, keyed by card, refreshed
// every minute while the page is open. The figures are a convenience on a
// navigation page: if they cannot be loaded the cards simply show none, and
// every card still links to its page.
export function useSectionCounters(lab: "MICRO" | "FP" | undefined): Record<string, SectionCounter[]> {
  const [byCard, setByCard] = useState<Record<string, SectionCounter[]>>({});

  useEffect(() => {
    setByCard({});
    if (!lab) return;
    let live = true;
    const load = () => {
      sectionCountersService.get(lab)
        .then((cards) => {
          if (live) setByCard(Object.fromEntries(cards.map((c) => [c.card, c.counters])));
        })
        .catch(() => {});
    };
    load();
    const timer = setInterval(load, REFRESH_MS);
    return () => { live = false; clearInterval(timer); };
  }, [lab]);

  return byCard;
}
