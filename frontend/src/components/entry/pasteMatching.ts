export interface NameTarget {
  index: number;
  name: string;
}

export interface NamedPasteRow {
  target: number | null;
  values: Record<string, string>;
  problem?: string;
}

export interface NamedPasteMatch {
  rows: NamedPasteRow[];
  unmatchedTargets: string[];
  refusal?: string;
}

export const NAMES_NOT_UNIQUE_MESSAGE = "Location names are not unique in this session; enter counts by hand";

const fold = (name: string) => name.trim().toLowerCase();

// Pairs pasted "name, value" rows with grid rows by name only (trimmed, case
// folded), never by position. A grid with two rows of the same name is refused
// because a pasted name could not say which row it means.
export function matchRowsByName(
  cells: string[][],
  targets: NameTarget[],
  keys: { name: string; value: string }
): NamedPasteMatch {
  const width = cells.reduce((max, r) => Math.max(max, r.length), 0);
  if (width < 2) {
    return {
      rows: [],
      unmatchedTargets: [],
      refusal: "The paste has one column. Paste two columns: the name, then the count."
    };
  }

  const indexByName = new Map<string, number>();
  const duplicateInGrid = targets.some((t) => {
    const key = fold(t.name);
    if (indexByName.has(key)) return true;
    indexByName.set(key, t.index);
    return false;
  });
  if (duplicateInGrid) {
    return { rows: [], unmatchedTargets: [], refusal: NAMES_NOT_UNIQUE_MESSAGE };
  }

  const timesInPaste = new Map<string, number>();
  for (const r of cells) {
    const key = fold(r[0] ?? "");
    timesInPaste.set(key, (timesInPaste.get(key) ?? 0) + 1);
  }

  const seen = new Set<number>();
  const rows = cells.map<NamedPasteRow>((r) => {
    const name = (r[0] ?? "").trim();
    const key = fold(name);
    const target = indexByName.get(key) ?? null;
    const values = { [keys.name]: name, [keys.value]: (r[1] ?? "").trim() };
    if (target === null) return { target, values, problem: "Unknown location name." };
    seen.add(target);
    if ((timesInPaste.get(key) ?? 0) > 1) {
      return { target, values, problem: "This name appears more than once in the paste." };
    }
    return { target, values };
  });

  return {
    rows,
    unmatchedTargets: targets.filter((t) => !seen.has(t.index)).map((t) => t.name)
  };
}
