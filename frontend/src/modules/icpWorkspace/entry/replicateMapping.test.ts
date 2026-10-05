import { describe, it, expect } from "vitest";
import {
  buildSaveReplicatesPayload,
  formatQuantityLabel,
  isReplicatesComplete,
  parseDraftNumber
} from "./replicateMapping";
import type { IcpReplicateDto } from "../types";

describe("replicateMapping", () => {
  describe("formatQuantityLabel", () => {
    it("maps ICP quantity codes to standard laboratory labels", () => {
      expect(formatQuantityLabel("IcpMgPerKg")).toBe("Content (µg/g)");
      expect(formatQuantityLabel("IcpMgPerUnit")).toBe("Amount per unit");
      expect(formatQuantityLabel("IcpPercentLabelClaim")).toBe("% of label claim");
      expect(formatQuantityLabel("OtherCode")).toBe("OtherCode");
    });
  });

  describe("parseDraftNumber", () => {
    it("parses valid numbers and decimals", () => {
      expect(parseDraftNumber("1.25")).toBe(1.25);
      expect(parseDraftNumber("0.005")).toBe(0.005);
      expect(parseDraftNumber("0.")).toBe(0);
    });

    it("returns 0 for empty or whitespace strings", () => {
      expect(parseDraftNumber("")).toBe(0);
      expect(parseDraftNumber("   ")).toBe(0);
    });

    it("returns fallback for undefined or invalid input", () => {
      expect(parseDraftNumber(undefined, 42)).toBe(42);
      expect(parseDraftNumber("invalid", 10)).toBe(10);
    });
  });

  describe("buildSaveReplicatesPayload", () => {
    const mockReplicate: IcpReplicateDto = {
      replicateNo: 1,
      sampleAmount: 1.0,
      volumeMl: 50.0,
      dilutionFactor: 1.0,
      concentrations: [
        { icpMethodElementId: 10, solutionMgPerL: 2.5 },
        { icpMethodElementId: 20, solutionMgPerL: 0.1 }
      ]
    };

    it("maps MineralAssay mode with unitAmount and selected amountUnit", () => {
      const payload = buildSaveReplicatesPayload({
        amountUnit: "Gram",
        mode: "MineralAssay",
        unitAmountDraft: "0.5500",
        unitAmountFallback: 0.5,
        replicates: [mockReplicate],
        drafts: {
          "0:amount": "1.0500",
          "0:volume": "50.0",
          "0:dilution": "2.0",
          "0:conc_10": "3.125",
          "0:conc_20": "0.15"
        },
        neededElementIds: [10, 20]
      });

      expect(payload.amountUnit).toBe("Gram");
      expect(payload.unitAmount).toBe(0.55);
      expect(payload.replicates).toHaveLength(1);

      const rep0 = payload.replicates[0];
      expect(rep0.sampleAmount).toBe(1.05);
      expect(rep0.volumeMl).toBe(50.0);
      expect(rep0.dilutionFactor).toBe(2.0);
      expect(rep0.concentrations).toEqual([
        { icpMethodElementId: 10, solutionMgPerL: 3.125 },
        { icpMethodElementId: 20, solutionMgPerL: 0.15 }
      ]);
    });

    it("maps Milliliter amountUnit in MineralAssay mode", () => {
      const payload = buildSaveReplicatesPayload({
        amountUnit: "Milliliter",
        mode: "MineralAssay",
        unitAmountDraft: "5.0",
        replicates: [mockReplicate],
        drafts: {},
        neededElementIds: [10]
      });

      expect(payload.amountUnit).toBe("Milliliter");
      expect(payload.unitAmount).toBe(5.0);
      expect(payload.replicates[0].concentrations).toEqual([
        { icpMethodElementId: 10, solutionMgPerL: 2.5 }
      ]);
    });

    it("enforces Gram unit and null unitAmount in ElementalImpurities mode", () => {
      const payload = buildSaveReplicatesPayload({
        amountUnit: "Milliliter", // even if attempted, forced to Gram
        mode: "ElementalImpurities",
        unitAmountDraft: "10.0",
        replicates: [mockReplicate],
        drafts: {
          "0:amount": "2.0000",
          "0:conc_10": "0.05",
          "0:conc_20": "0.01"
        },
        neededElementIds: [10, 20]
      });

      expect(payload.amountUnit).toBe("Gram");
      expect(payload.unitAmount).toBeNull();
      expect(payload.replicates[0].sampleAmount).toBe(2.0);
      expect(payload.replicates[0].concentrations).toEqual([
        { icpMethodElementId: 10, solutionMgPerL: 0.05 },
        { icpMethodElementId: 20, solutionMgPerL: 0.01 }
      ]);
    });

    it("correctly parses typed string drafts like '0.' and '1.25'", () => {
      const payload = buildSaveReplicatesPayload({
        amountUnit: "Gram",
        mode: "ElementalImpurities",
        replicates: [mockReplicate],
        drafts: {
          "0:amount": "1.25",
          "0:conc_10": "0."
        },
        neededElementIds: [10]
      });

      expect(payload.replicates[0].sampleAmount).toBe(1.25);
      expect(payload.replicates[0].concentrations[0].solutionMgPerL).toBe(0);
    });
  });

  describe("isReplicatesComplete", () => {
    const baseRep: IcpReplicateDto = {
      replicateNo: 1,
      sampleAmount: 1.0,
      volumeMl: 50.0,
      dilutionFactor: 1.0,
      concentrations: [{ icpMethodElementId: 10, solutionMgPerL: 1.5 }]
    };

    it("returns true when all fields and needed concentrations are valid", () => {
      const valid = isReplicatesComplete(
        [baseRep],
        [10],
        {},
        "ElementalImpurities"
      );
      expect(valid).toBe(true);
    });

    it("returns false when replicates array is empty", () => {
      expect(isReplicatesComplete([], [10], {}, "ElementalImpurities")).toBe(false);
    });

    it("returns false in MineralAssay mode when unit amount is missing or zero", () => {
      const invalid = isReplicatesComplete(
        [baseRep],
        [10],
        {},
        "MineralAssay",
        "",
        null
      );
      expect(invalid).toBe(false);

      const valid = isReplicatesComplete(
        [baseRep],
        [10],
        {},
        "MineralAssay",
        "0.5",
        null
      );
      expect(valid).toBe(true);
    });

    it("returns false when a concentration is missing or invalid", () => {
      const invalid = isReplicatesComplete(
        [baseRep],
        [10, 99], // 99 is needed but missing
        {},
        "ElementalImpurities"
      );
      expect(invalid).toBe(false);
    });

    it("returns false when dilution factor is less than 1", () => {
      const invalid = isReplicatesComplete(
        [baseRep],
        [10],
        { "0:dilution": "0.5" },
        "ElementalImpurities"
      );
      expect(invalid).toBe(false);
    });
  });
});
