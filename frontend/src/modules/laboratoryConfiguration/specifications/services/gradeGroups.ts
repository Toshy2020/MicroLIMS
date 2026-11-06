import type { SpecificationDto } from "./SpecificationService";

// A specification row is stored once per grade. Rows of the same item that are identical except for the
// grade (BP and USP with the same limits) are shown and edited as one row carrying all their grades.
export type GradedSpecification = SpecificationDto & { gradeSiblings: SpecificationDto[] };

const IGNORED = new Set(["id", "version", "grade", "displayOrder"]);

const keyOf = (s: SpecificationDto): string =>
  JSON.stringify(
    Object.keys(s)
      .filter((k) => !IGNORED.has(k))
      .sort()
      .map((k) => [k, (s as unknown as Record<string, unknown>)[k] ?? null])
  );

export function collapseGrades(specs: SpecificationDto[]): GradedSpecification[] {
  const out: GradedSpecification[] = [];
  const byKey = new Map<string, GradedSpecification>();
  for (const s of specs) {
    if (!s.grade) {
      out.push({ ...s, gradeSiblings: [s] });
      continue;
    }
    const k = keyOf(s);
    const hit = byKey.get(k);
    if (hit && !hit.gradeSiblings.some((g) => g.grade?.toLowerCase() === s.grade?.toLowerCase())) {
      hit.gradeSiblings.push(s);
      continue;
    }
    const row = { ...s, gradeSiblings: [s] };
    byKey.set(k, row);
    out.push(row);
  }
  return out;
}

export const gradesOf = (s: SpecificationDto): string[] =>
  ((s as Partial<GradedSpecification>).gradeSiblings ?? [s]).map((g) => g.grade).filter((g): g is string => !!g);

export const siblingsOf = (s: SpecificationDto): SpecificationDto[] =>
  (s as Partial<GradedSpecification>).gradeSiblings ?? [s];

// Which stored row each chosen grade writes to. Rows already holding that grade keep it; a new grade
// reuses a row whose grade was dropped (so its history continues) before a new row is created;
// rows left over are removed. No grade chosen = one ungraded row.
export function planGradeWrites(siblings: SpecificationDto[], chosen: string[]) {
  const targets: (string | null)[] = chosen.length ? chosen : [null];
  const norm = (g: string | null | undefined) => (g ?? "").trim().toLowerCase();
  const pool = siblings.filter((s) => !targets.some((t) => norm(t) === norm(s.grade)));
  const writes = targets.map((grade) => ({
    grade,
    existing: siblings.find((s) => norm(s.grade) === norm(grade)) ?? pool.shift() ?? null
  }));
  return { writes, removes: pool };
}
