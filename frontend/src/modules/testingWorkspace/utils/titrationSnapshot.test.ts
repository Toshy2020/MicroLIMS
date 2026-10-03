import { describe, expect, it } from "vitest";
import { parseTitrationSnapshot } from "./titrationSnapshot";

describe("parseTitrationSnapshot", () => {
  it("reads the titrant details nested under titrant, as TitrationRecorder stores them", () => {
    const json = JSON.stringify({
      type: "Redox",
      titrant: { preparationId: 7, code: "VS-0012", factorUsed: 1.0023, factorState: "Valid" },
      warnings: ["RSD exceeds the configured maximum of 1 % - requires review."]
    });

    expect(parseTitrationSnapshot(json)).toEqual({
      titrantCode: "VS-0012",
      factor: 1.0023,
      factorState: "Valid",
      warnings: ["RSD exceeds the configured maximum of 1 % - requires review."]
    });
  });

  it("returns null for empty or invalid JSON", () => {
    expect(parseTitrationSnapshot(null)).toBeNull();
    expect(parseTitrationSnapshot("not json")).toBeNull();
  });
});
