// A result's reported value is stored as the analyst reported it. For count
// tests that already includes the unit ("140 cfu/g", "<20 cfu/g"); for other
// tests it is a bare number ("98.5"). Show the unit once either way.
export function withUnit(reportedValue: string, unit: string | null | undefined): string {
  const u = unit?.trim();
  if (!u) return reportedValue;
  const value = reportedValue.trimEnd();
  const lower = value.toLowerCase();
  const lowerUnit = u.toLowerCase();
  // Already carries the unit as its own trailing token, not merely as trailing letters.
  const carriesUnit = lower === lowerUnit || (lower.endsWith(lowerUnit) && /\s/.test(lower.charAt(lower.length - lowerUnit.length - 1)));
  return carriesUnit ? value : `${value} ${u}`;
}
