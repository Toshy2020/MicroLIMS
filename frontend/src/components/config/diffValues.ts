export interface WizardSummaryItem {
  label: string;
  value: string;
  field: string;
}

export interface WizardDiffItem {
  label: string;
  before: string;
  after: string;
}

/**
 * Computes field-level before/after diffs between two summary lists (`a` before, `b` after),
 * returning only entries whose value changed.
 */
export function diffValues(
  a: { label: string; value: string; field: string }[],
  b: { label: string; value: string; field: string }[]
): { label: string; before: string; after: string }[] {
  const keyOf = (row: { label: string; field: string }) => row.field || row.label;
  const beforeMap = new Map<string, { label: string; value: string; field: string }>();
  for (const row of a) {
    beforeMap.set(keyOf(row), row);
  }

  const afterKeys = new Set<string>();
  const diffs: WizardDiffItem[] = [];

  for (const row of b) {
    const key = keyOf(row);
    afterKeys.add(key);
    const prev = beforeMap.get(key);
    if (prev) {
      if (prev.value !== row.value) {
        diffs.push({ label: row.label, before: prev.value, after: row.value });
      }
    } else if (row.value !== "") {
      diffs.push({ label: row.label, before: "", after: row.value });
    }
  }

  for (const row of a) {
    const key = keyOf(row);
    if (!afterKeys.has(key) && row.value !== "") {
      diffs.push({ label: row.label, before: row.value, after: "" });
    }
  }

  return diffs;
}
