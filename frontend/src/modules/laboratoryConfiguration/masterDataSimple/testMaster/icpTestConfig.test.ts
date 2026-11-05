import { describe, it, expect } from "vitest";
import {
  createInitialIcpForm,
  icpFormFromDefinition,
  icpPayloadFields,
  validateIcpForm,
  type IcpTestForm
} from "./icpTestConfig";

const assay = (over: Partial<IcpTestForm> = {}): IcpTestForm => ({
  mode: "Assay",
  sampleForm: "Solid",
  theoreticalAmount: "0.5",
  specificGravity: "",
  elements: [{ symbol: "Zn", targetPpm: "10" }],
  ...over
});

describe("icpTestConfig", () => {
  it("starts as a solid assay with one empty element row", () => {
    const f = createInitialIcpForm();
    expect(f.mode).toBe("Assay");
    expect(f.sampleForm).toBe("Solid");
    expect(f.elements).toEqual([{ symbol: "", targetPpm: "" }]);
  });

  it("loads a stored definition, ordered, keeping element ids", () => {
    const f = icpFormFromDefinition({
      icpMode: "Assay",
      icpSampleForm: "Liquid",
      icpTheoreticalAmount: 10,
      icpSpecificGravity: 1.2,
      icpElements: [
        { id: 9, displayOrder: 1, symbol: "Ca", targetPpm: 20 },
        { id: 8, displayOrder: 0, symbol: "Zn", targetPpm: 10 }
      ]
    });
    expect(f.sampleForm).toBe("Liquid");
    expect(f.theoreticalAmount).toBe("10");
    expect(f.specificGravity).toBe("1.2");
    expect(f.elements).toEqual([
      { id: 8, symbol: "Zn", targetPpm: "10" },
      { id: 9, symbol: "Ca", targetPpm: "20" }
    ]);
  });

  it("falls back to the initial form for a test that has no ICP settings yet", () => {
    expect(icpFormFromDefinition({})).toEqual(createInitialIcpForm());
  });

  describe("icpPayloadFields", () => {
    it("sends the assay basis and targets for a solid, without specific gravity", () => {
      const p = icpPayloadFields(assay({ specificGravity: "1.5" }));
      expect(p).toEqual({
        icpMode: "Assay",
        icpSampleForm: "Solid",
        icpTheoreticalAmount: 0.5,
        icpSpecificGravity: null,
        icpElements: [{ id: null, symbol: "Zn", targetPpm: 10 }]
      });
    });

    it("sends the specific gravity for a liquid", () => {
      const p = icpPayloadFields(assay({ sampleForm: "Liquid", theoreticalAmount: "10", specificGravity: "1.2" }));
      expect(p.icpSampleForm).toBe("Liquid");
      expect(p.icpSpecificGravity).toBe(1.2);
    });

    it("sends only symbols for an impurity test and keeps existing element ids", () => {
      const p = icpPayloadFields(
        assay({ mode: "Impurity", elements: [{ id: 4, symbol: "Pb", targetPpm: "5" }, { symbol: "As", targetPpm: "" }] })
      );
      expect(p).toEqual({
        icpMode: "Impurity",
        icpSampleForm: null,
        icpTheoreticalAmount: null,
        icpSpecificGravity: null,
        icpElements: [
          { id: 4, symbol: "Pb", targetPpm: null },
          { id: null, symbol: "As", targetPpm: null }
        ]
      });
    });

    it("drops blank element rows", () => {
      const p = icpPayloadFields(assay({ elements: [{ symbol: " ", targetPpm: "" }, { symbol: "Zn", targetPpm: "10" }] }));
      expect(p.icpElements).toHaveLength(1);
    });
  });

  describe("validateIcpForm", () => {
    it("accepts a complete assay and a complete impurity", () => {
      expect(validateIcpForm(assay())).toBeNull();
      expect(validateIcpForm(assay({ mode: "Impurity", elements: [{ symbol: "Pb", targetPpm: "" }] }))).toBeNull();
    });

    it("needs an element", () => {
      expect(validateIcpForm(assay({ elements: [{ symbol: "", targetPpm: "" }] }))).toBe("Add at least one element.");
    });

    it("needs the theoretical amount, naming the unit for the sample form", () => {
      expect(validateIcpForm(assay({ theoreticalAmount: "" }))).toBe("Enter the theoretical weight (g).");
      expect(validateIcpForm(assay({ sampleForm: "Liquid", theoreticalAmount: "" }))).toBe("Enter the theoretical volume (mL).");
    });

    it("needs the specific gravity for a liquid", () => {
      expect(validateIcpForm(assay({ sampleForm: "Liquid", specificGravity: "" }))).toBe("Enter the specific gravity.");
    });

    it("needs a target for every assay element, but not for an impurity", () => {
      expect(validateIcpForm(assay({ elements: [{ symbol: "Zn", targetPpm: "" }] }))).toBe("Enter the target concentration (ppm) for Zn.");
    });
  });
});
