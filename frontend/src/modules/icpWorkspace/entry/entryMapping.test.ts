import { describe, it, expect } from "vitest";
import {
  buildSaveEntryPayload,
  draftFromEntry,
  formatQuantityLabel,
  isEntryComplete,
  parseDecimal
} from "./entryMapping";
import type { IcpEntryElementDto } from "../types";

const zn: IcpEntryElementDto = { icpTestElementId: 7, symbol: "Zn", targetPpm: 10, valid: true, resultPpm: null };
const ca: IcpEntryElementDto = { icpTestElementId: 8, symbol: "Ca", targetPpm: 20, valid: true, resultPpm: 19.5 };

describe("entryMapping", () => {
  it("labels the two quantities", () => {
    expect(formatQuantityLabel("IcpAssayPercent")).toBe("Assay (%)");
    expect(formatQuantityLabel("IcpImpurityPpm")).toBe("Impurity (ppm)");
    expect(formatQuantityLabel("Other")).toBe("Other");
  });

  it("parses decimals, treating blanks and junk as null", () => {
    expect(parseDecimal("0.5")).toBe(0.5);
    expect(parseDecimal("  12 ")).toBe(12);
    expect(parseDecimal("0")).toBe(0);
    expect(parseDecimal("")).toBeNull();
    expect(parseDecimal("abc")).toBeNull();
    expect(parseDecimal(undefined)).toBeNull();
  });

  it("starts the draft from what the server stored", () => {
    expect(draftFromEntry([zn, ca], 0.5)).toEqual({ weight: "0.5", results: { 7: "", 8: "19.5" } });
    expect(draftFromEntry([zn], null).weight).toBe("");
  });

  it("builds an assay payload with the weight and every typed result", () => {
    const payload = buildSaveEntryPayload("Assay", [zn, ca], { weight: "0.5", results: { 7: "9.5", 8: "19" } });
    expect(payload).toEqual({
      actualWeightG: 0.5,
      concentrations: [
        { icpTestElementId: 7, resultPpm: 9.5 },
        { icpTestElementId: 8, resultPpm: 19 }
      ]
    });
  });

  it("sends no weight for an impurity test and keeps a zero result", () => {
    const payload = buildSaveEntryPayload("Impurity", [zn], { weight: "5", results: { 7: "0" } });
    expect(payload.actualWeightG).toBeNull();
    expect(payload.concentrations).toEqual([{ icpTestElementId: 7, resultPpm: 0 }]);
  });

  it("omits elements whose result was not typed", () => {
    const payload = buildSaveEntryPayload("Assay", [zn, ca], { weight: "0.5", results: { 7: "", 8: "19" } });
    expect(payload.concentrations).toEqual([{ icpTestElementId: 8, resultPpm: 19 }]);
  });

  describe("isEntryComplete", () => {
    it("needs a positive weight for an assay and a result for every element", () => {
      const results = { 7: "9.5", 8: "19" };
      expect(isEntryComplete("Assay", [zn, ca], { weight: "0.5", results })).toBe(true);
      expect(isEntryComplete("Assay", [zn, ca], { weight: "", results })).toBe(false);
      expect(isEntryComplete("Assay", [zn, ca], { weight: "0", results })).toBe(false);
      expect(isEntryComplete("Assay", [zn, ca], { weight: "0.5", results: { 7: "9.5", 8: "" } })).toBe(false);
    });

    it("ignores the weight for an impurity test and accepts zero", () => {
      expect(isEntryComplete("Impurity", [zn], { weight: "", results: { 7: "0" } })).toBe(true);
      expect(isEntryComplete("Impurity", [zn], { weight: "", results: { 7: "-1" } })).toBe(false);
    });

    it("is false when the test has no elements", () => {
      expect(isEntryComplete("Assay", [], { weight: "1", results: {} })).toBe(false);
    });
  });
});
