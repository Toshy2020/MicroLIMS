import { describe, expect, it } from "vitest";
import { collapseGrades, gradesOf, planGradeWrites } from "./gradeGroups";

const row = (id: number, grade: string | null, upperLimit = 1) =>
  ({ id, version: id, testCode: "RM-LOD", parameterName: "LOD", limitType: "NotMoreThan", upperLimit, grade, displayOrder: id });

describe("gradeGroups", () => {
  it("shows identical BP and USP rows as one, keeps different limits and ungraded rows apart", () => {
    const out = collapseGrades([row(1, "BP"), row(2, "USP"), row(3, "In-house", 2), row(4, null)]);
    expect(out.map(gradesOf)).toEqual([["BP", "USP"], ["In-house"], []]);
  });

  it("updates kept grades, reuses a dropped row for a new grade, removes the rest", () => {
    const { writes, removes } = planGradeWrites([row(1, "BP"), row(2, "USP"), row(5, "EP")], ["USP", "In-house"]);
    expect(writes.map((w) => [w.grade, w.existing?.id ?? null])).toEqual([["USP", 2], ["In-house", 1]]);
    expect(removes.map((r) => r.id)).toEqual([5]);
  });

  it("creates one row per grade for a new specification, one ungraded row when none is chosen", () => {
    expect(planGradeWrites([], ["BP", "USP"]).writes.map((w) => [w.grade, w.existing])).toEqual([["BP", null], ["USP", null]]);
    expect(planGradeWrites([], []).writes.map((w) => w.grade)).toEqual([null]);
  });
});
