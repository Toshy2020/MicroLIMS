import { describe, it, expect } from "vitest";
import { buildSaveCalibrationPayload } from "./calibrationPayload";

describe("buildSaveCalibrationPayload", () => {
  it("sends the standard lot and one entry per element the analyst listed", () => {
    const payload = buildSaveCalibrationPayload(101, [
      { symbol: "Zn", correlationR: "0.999512" },
      { symbol: "Ca", correlationR: "0.9991" }
    ]);

    expect(payload.calibrationStandardMaterialId).toBe(101);
    expect(payload.elements).toEqual([
      { symbol: "Zn", correlationR: 0.999512 },
      { symbol: "Ca", correlationR: 0.9991 }
    ]);
  });

  it("always includes the lot property, as null when none is selected", () => {
    const payload = buildSaveCalibrationPayload(null, [{ symbol: "Zn", correlationR: "" }]);

    expect(payload).toHaveProperty("calibrationStandardMaterialId", null);
    expect(payload.elements[0]).toEqual({ symbol: "Zn", correlationR: null });
  });

  it("drops blank-symbol rows and trims symbols", () => {
    const payload = buildSaveCalibrationPayload(1, [
      { symbol: "  Mg ", correlationR: "0.9999" },
      { symbol: "   ", correlationR: "0.9" },
      { symbol: "", correlationR: "" }
    ]);

    expect(payload.elements).toEqual([{ symbol: "Mg", correlationR: 0.9999 }]);
  });

  it("sends a non-numeric r as null so the server reports it is required", () => {
    const payload = buildSaveCalibrationPayload(1, [{ symbol: "Zn", correlationR: "abc" }]);
    expect(payload.elements[0].correlationR).toBeNull();
  });

  it("sends an empty list when no element was added", () => {
    expect(buildSaveCalibrationPayload(5, []).elements).toEqual([]);
  });
});
