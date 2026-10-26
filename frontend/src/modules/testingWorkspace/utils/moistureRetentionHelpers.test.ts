import { describe, expect, it } from "vitest";
import {
  formatMoistureLabel,
  getMissingMoistureMessage,
  isRawMaterialPhyschemReview,
  isRetentionValid,
  formatRetentionDisplay,
  RETENTION_UNITS
} from "./moistureRetentionHelpers";

describe("moistureRetentionHelpers", () => {
  describe("formatMoistureLabel", () => {
    it("formats LossOnDrying role with percent and testCode", () => {
      expect(
        formatMoistureLabel({
          role: "LossOnDrying",
          percent: 2.5,
          testCode: "LOD-042"
        })
      ).toBe("Loss on drying 2.5 % (from LOD-042)");
    });

    it("formats Water role with percent and testCode", () => {
      expect(
        formatMoistureLabel({
          role: "Water",
          percent: 0.45,
          testCode: "KF-109"
        })
      ).toBe("Water 0.45 % (from KF-109)");
    });
  });

  describe("getMissingMoistureMessage", () => {
    it("returns LOD prompt when spec basis is PercentDriedBasis", () => {
      const specs = [{ resultBasis: "PercentDriedBasis" }];
      expect(getMissingMoistureMessage(specs)).toBe(
        "Record the sample's loss on drying result first."
      );
    });

    it("returns Karl Fischer prompt when any spec basis is PercentAnhydrousBasis", () => {
      const specs = [{ resultBasis: "PercentAnhydrousBasis" }];
      expect(getMissingMoistureMessage(specs)).toBe(
        "Record the sample's water content (Karl Fischer) result first."
      );
    });

    it("prioritizes Karl Fischer prompt when both or anhydrous basis is present", () => {
      const specs = [
        { resultBasis: "PercentDriedBasis" },
        { resultBasis: "PercentAnhydrousBasis" }
      ];
      expect(getMissingMoistureMessage(specs)).toBe(
        "Record the sample's water content (Karl Fischer) result first."
      );
    });

    it("defaults to LOD prompt when empty or non-anhydrous basis", () => {
      expect(getMissingMoistureMessage([])).toBe(
        "Record the sample's loss on drying result first."
      );
    });
  });

  describe("isRawMaterialPhyschemReview", () => {
    it("returns true for RawMaterial and FP section code", () => {
      expect(isRawMaterialPhyschemReview("RawMaterial", "FP")).toBe(true);
      expect(isRawMaterialPhyschemReview("Raw Material", "fp")).toBe(true);
    });

    it("returns false for FinishedProduct and FP section code", () => {
      expect(isRawMaterialPhyschemReview("FinishedProduct", "FP")).toBe(false);
    });

    it("returns false for RawMaterial and MICRO section code", () => {
      expect(isRawMaterialPhyschemReview("RawMaterial", "MICRO")).toBe(false);
    });

    it("returns false when category or section code is null or empty", () => {
      expect(isRawMaterialPhyschemReview(null, "FP")).toBe(false);
      expect(isRawMaterialPhyschemReview("RawMaterial", null)).toBe(false);
      expect(isRawMaterialPhyschemReview("", "FP")).toBe(false);
      expect(isRawMaterialPhyschemReview("RawMaterial", "")).toBe(false);
    });
  });

  describe("isRetentionValid", () => {
    it("allows valid positive quantity with allowed unit", () => {
      for (const unit of RETENTION_UNITS) {
        expect(isRetentionValid(50, unit)).toBe(true);
        expect(isRetentionValid("25.5", unit)).toBe(true);
      }
    });

    it("rejects zero or negative quantities", () => {
      expect(isRetentionValid(0, "g")).toBe(false);
      expect(isRetentionValid("0", "g")).toBe(false);
      expect(isRetentionValid(-5, "g")).toBe(false);
      expect(isRetentionValid("-1.2", "g")).toBe(false);
    });

    it("rejects non-numeric quantities", () => {
      expect(isRetentionValid("", "g")).toBe(false);
      expect(isRetentionValid(null, "g")).toBe(false);
      expect(isRetentionValid(undefined, "g")).toBe(false);
      expect(isRetentionValid("abc", "g")).toBe(false);
    });

    it("rejects missing or invalid units", () => {
      expect(isRetentionValid(50, "")).toBe(false);
      expect(isRetentionValid(50, null)).toBe(false);
      expect(isRetentionValid(50, undefined)).toBe(false);
      expect(isRetentionValid(50, "bottles")).toBe(false);
      expect(isRetentionValid(50, "kg ")).toBe(true); // trims correctly
    });
  });

  describe("formatRetentionDisplay", () => {
    it("formats retention string when quantity is present", () => {
      expect(formatRetentionDisplay(50, "g")).toBe("Retention: 50 g");
      expect(formatRetentionDisplay(1.25, "kg")).toBe("Retention: 1.25 kg");
    });

    it("returns null when quantity is null or undefined", () => {
      expect(formatRetentionDisplay(null, "g")).toBeNull();
      expect(formatRetentionDisplay(undefined, "g")).toBeNull();
    });
  });
});
