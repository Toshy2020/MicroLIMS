import { describe, it, expect } from "vitest";
import type { SaveIcpCalibrationRequest, SaveIcpCalibrationElementInput } from "../types";

/**
 * Builds the payload for SaveIcpCalibrationRequest matching the logic used in IcpCalibrationPanel.
 * Enforces that both lot IDs (calibrationStandardMaterialId and icvStandardMaterialId) are ALWAYS sent.
 */
export function buildSaveCalibrationPayload(
  calLotId: number | null | undefined,
  icvLotId: number | null | undefined,
  requireIcv: boolean,
  elementInputs: Array<{
    calElementId: number;
    correlationR?: string | null;
    blankMgPerL?: string | null;
    icvMeasuredMgPerL?: string | null;
  }>,
  methodOptions: {
    requireBlank: boolean;
    requireIcv: boolean;
  }
): SaveIcpCalibrationRequest {
  const elements: SaveIcpCalibrationElementInput[] = elementInputs.map((input) => {
    const rNum = input.correlationR && input.correlationR.trim() !== "" ? Number(input.correlationR) : null;
    const blankNum = input.blankMgPerL && input.blankMgPerL.trim() !== "" ? Number(input.blankMgPerL) : null;
    const icvNum = input.icvMeasuredMgPerL && input.icvMeasuredMgPerL.trim() !== "" ? Number(input.icvMeasuredMgPerL) : null;

    return {
      icpCalibrationElementId: input.calElementId,
      correlationR: rNum,
      blankMgPerL: methodOptions.requireBlank ? blankNum : null,
      icvMeasuredMgPerL: methodOptions.requireIcv ? icvNum : null
    };
  });

  return {
    calibrationStandardMaterialId: calLotId ?? null,
    icvStandardMaterialId: requireIcv ? (icvLotId ?? null) : null,
    elements
  };
}

describe("buildSaveCalibrationPayload", () => {
  it("always includes both lot IDs in payload when both are selected", () => {
    const payload = buildSaveCalibrationPayload(
      101,
      202,
      true,
      [{ calElementId: 1, correlationR: "0.999512", blankMgPerL: "0.001", icvMeasuredMgPerL: "5.02" }],
      { requireBlank: true, requireIcv: true }
    );

    expect(payload).toHaveProperty("calibrationStandardMaterialId");
    expect(payload).toHaveProperty("icvStandardMaterialId");
    expect(payload.calibrationStandardMaterialId).toBe(101);
    expect(payload.icvStandardMaterialId).toBe(202);
    expect(payload.elements).toHaveLength(1);
    expect(payload.elements[0]).toEqual({
      icpCalibrationElementId: 1,
      correlationR: 0.999512,
      blankMgPerL: 0.001,
      icvMeasuredMgPerL: 5.02
    });
  });

  it("always includes both lot IDs as null when none are selected", () => {
    const payload = buildSaveCalibrationPayload(
      null,
      null,
      true,
      [{ calElementId: 2, correlationR: "", blankMgPerL: "", icvMeasuredMgPerL: "" }],
      { requireBlank: true, requireIcv: true }
    );

    expect(payload).toHaveProperty("calibrationStandardMaterialId", null);
    expect(payload).toHaveProperty("icvStandardMaterialId", null);
    expect(payload.elements[0]).toEqual({
      icpCalibrationElementId: 2,
      correlationR: null,
      blankMgPerL: null,
      icvMeasuredMgPerL: null
    });
  });

  it("sets icvStandardMaterialId to null when method does not require ICV but sends property", () => {
    const payload = buildSaveCalibrationPayload(
      105,
      205,
      false, // method does not require ICV
      [{ calElementId: 3, correlationR: "0.999900" }],
      { requireBlank: false, requireIcv: false }
    );

    expect(payload).toHaveProperty("calibrationStandardMaterialId", 105);
    expect(payload).toHaveProperty("icvStandardMaterialId", null);
    expect(payload.elements[0].blankMgPerL).toBeNull();
    expect(payload.elements[0].icvMeasuredMgPerL).toBeNull();
  });
});
