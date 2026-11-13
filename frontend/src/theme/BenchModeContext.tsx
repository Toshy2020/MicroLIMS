import { createContext, ReactNode, useCallback, useContext, useMemo, useState } from "react";

// Bench mode is a property of the device (a shared lab tablet), not of the
// user: it is stored in localStorage under its own keys and is deliberately
// NOT among the keys cleared at logout, so the next person to pick up the
// tablet finds it as the last user left it.
export const BENCH_MODE_STORAGE_KEY = "microlims_bench_mode";
export const BENCH_OFFER_STORAGE_KEY = "microlims_bench_mode_offered";

interface BenchModeContextValue {
  benchMode: boolean;
  setBenchMode: (on: boolean) => void;
  /** True on a touch-first device that has not yet been asked about bench mode. */
  shouldOffer: boolean;
  dismissOffer: () => void;
}

const defaultValue: BenchModeContextValue = {
  benchMode: false,
  setBenchMode: () => {},
  shouldOffer: false,
  dismissOffer: () => {}
};

const BenchModeContext = createContext<BenchModeContextValue>(defaultValue);

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    // Blocked or unavailable storage: behave as if nothing was saved.
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // The choice still applies for this page view; it just is not remembered.
  }
}

function hasCoarsePointer(): boolean {
  try {
    return typeof window !== "undefined" && !!window.matchMedia?.("(pointer: coarse)").matches;
  } catch {
    return false;
  }
}

export function BenchModeProvider({ children }: { children: ReactNode }) {
  const [benchMode, setBenchModeState] = useState<boolean>(() => readStorage(BENCH_MODE_STORAGE_KEY) === "on");
  const [offerDismissed, setOfferDismissed] = useState<boolean>(() => readStorage(BENCH_OFFER_STORAGE_KEY) === "true");
  const [coarsePointer] = useState<boolean>(hasCoarsePointer);

  const setBenchMode = useCallback((on: boolean) => {
    setBenchModeState(on);
    writeStorage(BENCH_MODE_STORAGE_KEY, on ? "on" : "off");
  }, []);

  const dismissOffer = useCallback(() => {
    setOfferDismissed(true);
    writeStorage(BENCH_OFFER_STORAGE_KEY, "true");
  }, []);

  const value = useMemo<BenchModeContextValue>(
    () => ({
      benchMode,
      setBenchMode,
      shouldOffer: coarsePointer && !offerDismissed && !benchMode,
      dismissOffer
    }),
    [benchMode, setBenchMode, coarsePointer, offerDismissed, dismissOffer]
  );

  return <BenchModeContext.Provider value={value}>{children}</BenchModeContext.Provider>;
}

// Outside a provider bench mode is simply off, so components and their tests
// that never mount the provider keep working.
export function useBenchMode(): BenchModeContextValue {
  return useContext(BenchModeContext);
}
