import { describe, expect, it } from "vitest";
import { diffValues } from "./diffValues";

describe("diffValues", () => {
  it("returns only changed fields with before and after values", () => {
    const before = [
      { field: "name", label: "Name", value: "Alpha" },
      { field: "code", label: "Code", value: "A-01" },
      { field: "notes", label: "Notes", value: "Initial" },
    ];
    const after = [
      { field: "name", label: "Name", value: "Alpha" },
      { field: "code", label: "Code", value: "A-02" },
      { field: "notes", label: "Notes", value: "Updated" },
    ];

    expect(diffValues(before, after)).toEqual([
      { label: "Code", before: "A-01", after: "A-02" },
      { label: "Notes", before: "Initial", after: "Updated" },
    ]);
  });

  it("returns an empty array when all values are unchanged", () => {
    const rows = [
      { field: "name", label: "Name", value: "Alpha" },
      { field: "code", label: "Code", value: "A-01" },
    ];

    expect(diffValues(rows, [...rows])).toEqual([]);
  });

  it("reports newly added or removed non-empty fields", () => {
    const before = [
      { field: "name", label: "Name", value: "Alpha" },
      { field: "legacy", label: "Legacy", value: "Old" },
    ];
    const after = [
      { field: "name", label: "Name", value: "Alpha" },
      { field: "limit", label: "Limit", value: "100" },
      { field: "blank", label: "Blank", value: "" },
    ];

    expect(diffValues(before, after)).toEqual([
      { label: "Limit", before: "", after: "100" },
      { label: "Legacy", before: "Old", after: "" },
    ]);
  });
});
