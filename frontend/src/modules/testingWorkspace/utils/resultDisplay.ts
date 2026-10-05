// Reported text without a trailing unit that the table already shows in its own Unit column.
export function withoutUnit(display: string | null | undefined, unit: string | null | undefined): string {
  const text = (display ?? "").trim();
  const u = (unit ?? "").trim();
  return u && text.endsWith(` ${u}`) ? text.slice(0, -(u.length + 1)) : text;
}

// Decimal places of the first number in a reported value ("99.88 %" -> 2); 2 when none.
export function decimalsOf(display: string | null | undefined): number {
  const m = /-?\d+(?:\.(\d+))?/.exec(display ?? "");
  return m ? (m[1]?.length ?? 0) : 2;
}
