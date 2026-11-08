import { describe, expect, it } from "vitest";
import { cleaningTechniqueOf, CLEANING_TECHNIQUES } from "./cleaningTechniques";

describe("cleaningTechniques", () => {
  it("exports allowed techniques", () => {
    expect(CLEANING_TECHNIQUES).toEqual(["UV", "HPLC", "ICP"]);
  });

  it("maps workflowType correctly", () => {
    expect(cleaningTechniqueOf("HplcMethodAssay")).toBe("HPLC");
    expect(cleaningTechniqueOf("IcpMethodAssay")).toBe("ICP");
    expect(cleaningTechniqueOf("Measurement")).toBe("UV");
    expect(cleaningTechniqueOf("Qualitative")).toBe("UV");
    expect(cleaningTechniqueOf("QuantitativeTest")).toBe("UV");
    expect(cleaningTechniqueOf(null)).toBe("UV");
    expect(cleaningTechniqueOf(undefined)).toBe("UV");
  });
});
