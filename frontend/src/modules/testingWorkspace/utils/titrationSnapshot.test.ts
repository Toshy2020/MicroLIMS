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
      warnings: ["RSD exceeds the configured maximum of 1 % - requires review."],
      dueTitrantAcknowledgement: null,
      engineVersion: null
    });
  });

  it("parses the due titrant acknowledgement and engineVersion when present", () => {
    const json = JSON.stringify({
      engineVersion: "titration-1",
      type: "AcidBase",
      titrant: { preparationId: 3, code: "TP-004", factorUsed: 0.998, factorState: "Due" },
      warnings: ["Titrant standardization is due."],
      dueTitrantAcknowledgement: {
        justification: "Urgent batch release with QA approval",
        titrantCodes: ["TP-004"],
        acknowledgedAt: "2026-10-05T12:00:00.000Z"
      }
    });

    expect(parseTitrationSnapshot(json)).toEqual({
      titrantCode: "TP-004",
      factor: 0.998,
      factorState: "Due",
      warnings: ["Titrant standardization is due."],
      dueTitrantAcknowledgement: {
        justification: "Urgent batch release with QA approval",
        titrantCodes: ["TP-004"],
        acknowledgedAt: "2026-10-05T12:00:00.000Z"
      },
      engineVersion: "titration-1"
    });
  });

  it("parses PascalCase keys from C# serialization", () => {
    const json = JSON.stringify({
      EngineVersion: "titration-1",
      DueTitrantAcknowledgement: {
        Justification: "Supervisor override requested",
        TitrantCodes: ["TP-005"],
        AcknowledgedAt: "2026-10-05T12:30:00.000Z"
      },
      Titrant: { Code: "TP-005", Factor: 1.001, FactorState: "Due" },
      Warnings: ["Titrant factor is Due."]
    });

    expect(parseTitrationSnapshot(json)).toEqual({
      titrantCode: "TP-005",
      factor: 1.001,
      factorState: "Due",
      warnings: ["Titrant factor is Due."],
      dueTitrantAcknowledgement: {
        justification: "Supervisor override requested",
        titrantCodes: ["TP-005"],
        acknowledgedAt: "2026-10-05T12:30:00.000Z"
      },
      engineVersion: "titration-1"
    });
  });

  it("returns null for empty or invalid JSON", () => {
    expect(parseTitrationSnapshot(null)).toBeNull();
    expect(parseTitrationSnapshot("not json")).toBeNull();
  });
});
