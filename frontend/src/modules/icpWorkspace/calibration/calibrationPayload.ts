import type { SaveIcpCalibrationRequest } from "../types";

// One editable calibration row: an element the analyst calibrated and the r read from the curve.
export interface CalibrationRowInput {
  symbol: string;
  correlationR: string;
}

// Builds the save request. Blank-symbol rows are dropped; an empty or non-numeric r is sent as null so the
// server reports "r is required". Whether r passes is the server's decision.
export function buildSaveCalibrationPayload(
  calLotId: number | null | undefined,
  rows: CalibrationRowInput[]
): SaveIcpCalibrationRequest {
  return {
    calibrationStandardMaterialId: calLotId ?? null,
    elements: rows
      .filter((row) => row.symbol.trim() !== "")
      .map((row) => {
        const text = row.correlationR.trim();
        const r = text === "" ? null : Number(text);
        return { symbol: row.symbol.trim(), correlationR: r !== null && Number.isFinite(r) ? r : null };
      })
  };
}
