import { describe, it, expect } from "vitest";
import { defaultFilters, filtersError, toQueryParams } from "./kpiFilters";

describe("defaultFilters", () => {
  it("covers the last 30 days including today", () => {
    expect(defaultFilters(new Date(2026, 9, 11))).toEqual({
      from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null
    });
  });
});

describe("filtersError", () => {
  it("accepts a valid and a single-day range", () => {
    expect(filtersError({ from: "2026-09-01", to: "2026-10-01" })).toBeNull();
    expect(filtersError({ from: "2026-10-01", to: "2026-10-01" })).toBeNull();
  });
  it("rejects empty and inverted ranges", () => {
    expect(filtersError({ from: "", to: "2026-10-01" })).toBe("Choose both dates.");
    expect(filtersError({ from: "2026-10-02", to: "2026-10-01" })).toBe("The start date must not be after the end date.");
  });
});

describe("toQueryParams", () => {
  it("leaves unset filters out instead of sending empty strings", () => {
    const q = toQueryParams({ from: "2026-09-01", to: "2026-10-01", category: null, location: null, testCode: "TAMC" });
    expect(q).toEqual({ from: "2026-09-01", to: "2026-10-01", category: undefined, location: undefined, testCode: "TAMC" });
  });
});
