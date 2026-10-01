import { useCallback, useState } from "react";

// Tracks which supporting lists (autoclaves, incubators, lots...) a form or
// panel failed to load, so the screen can say so instead of presenting an
// empty dropdown as "none available". Pair with <LoadFailuresAlert>.
//
//   const { failed, fail, reset } = useLoadFailures();
//   reset();                                   // when (re)loading
//   service.get().then(setX).catch(fail("autoclaves", () => setX([])));
export function useLoadFailures() {
  const [failed, setFailed] = useState<string[]>([]);
  const fail = useCallback(
    (what: string, clear?: () => void) => () => {
      clear?.();
      setFailed((f) => (f.includes(what) ? f : [...f, what]));
    },
    []
  );
  const reset = useCallback(() => setFailed([]), []);
  return { failed, fail, reset };
}
