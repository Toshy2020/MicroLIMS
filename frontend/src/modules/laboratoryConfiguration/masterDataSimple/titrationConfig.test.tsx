import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import {
  createInitialTitrationForm,
  normaliseTitrationForm,
  titrationPayloadFields,
  titrationVisibility,
  validateTitrationForm,
  TitrationFormState
} from "./titrationConfig";
import { TitrationConfigSection } from "./TitrationConfigSection";
import { validateTitrationSpecBasis, titrationBasisNeedsLabelClaim } from "../items/components/TitrationSpecBasisFields";

vi.mock("./services/SolutionMasterService", () => ({
  SolutionMasterService: {
    getAll: vi.fn().mockResolvedValue([
      { id: 1, sectionId: 1, name: "Iodine VS", nominalStrength: 0.1, strengthUnit: "Normal" },
      { id: 2, sectionId: 1, name: "KF reagent", nominalStrength: 5, strengthUnit: "MgWaterPerMl" }
    ])
  }
}));
vi.mock("./services/MaterialMasterService", () => ({
  MaterialMasterService: {
    getAll: vi.fn((category: string) =>
      Promise.resolve(
        category === "Indicator"
          ? [{ id: 4, sectionId: 1, name: "Phenolphthalein TS", code: "IND-1" }]
          : [{ id: 9, sectionId: 1, name: "Ascorbic acid RS", code: "RS-1" }]
      )
    )
  }
}));

afterEach(cleanup);

const form = (patch: Partial<TitrationFormState>): TitrationFormState => ({ ...createInitialTitrationForm(), ...patch });

describe("titration visibility rules", () => {
  it("shows F only for USP factor, non-KF", () => {
    expect(titrationVisibility(form({ type: "Redox" })).factor).toBe(true);
    expect(titrationVisibility(form({ type: "KarlFischer" })).factor).toBe(false);
    expect(titrationVisibility(form({ type: "Redox", calculation: "Relative" })).factor).toBe(false);
  });
  it("shows excess only for residual, standard only for relative direct", () => {
    expect(titrationVisibility(form({ type: "Redox", mode: "Residual" })).excess).toBe(true);
    expect(titrationVisibility(form({ type: "Redox" })).excess).toBe(false);
    const rel = titrationVisibility(form({ type: "Redox", calculation: "Relative" }));
    expect(rel.standard).toBe(true);
  });
  it("temperature correction only for acid-base non-aqueous; indicator only for visual", () => {
    expect(titrationVisibility(form({ type: "AcidBase", nonAqueous: true })).tempCorrection).toBe(true);
    expect(titrationVisibility(form({ type: "AcidBase" })).tempCorrection).toBe(false);
    expect(titrationVisibility(form({ type: "Redox", nonAqueous: true })).tempCorrection).toBe(false);
    expect(titrationVisibility(form({ endpoint: "Visual" })).indicator).toBe(true);
    expect(titrationVisibility(form({ endpoint: "Potentiometric" })).indicator).toBe(false);
  });
  it("KF forces direct + USP factor; relative cannot stay with residual", () => {
    const kf = normaliseTitrationForm(form({ type: "KarlFischer", mode: "Residual", calculation: "Relative", nonAqueous: true }));
    expect(kf).toMatchObject({ mode: "Direct", calculation: "UspFactor", nonAqueous: false });
    expect(normaliseTitrationForm(form({ type: "Redox", mode: "Residual", calculation: "Relative" })).calculation).toBe("UspFactor");
  });
});

describe("titration required-field hints", () => {
  const ok = form({ type: "Redox", titrantSolutionMasterId: 1, equivalencyFactor: "88.06", indicatorEntryId: 4 });
  it("accepts a complete direct USP-factor test", () => expect(validateTitrationForm(ok)).toBeNull());
  it("requires titrant, F and indicator", () => {
    expect(validateTitrationForm({ ...ok, titrantSolutionMasterId: "" })).toMatch(/Titrant/);
    expect(validateTitrationForm({ ...ok, equivalencyFactor: "" })).toMatch(/factor/i);
    expect(validateTitrationForm({ ...ok, indicatorEntryId: "" })).toMatch(/Indicator/);
  });
  it("residual always needs excess master and volume (blank or not)", () => {
    const r = { ...ok, mode: "Residual" as const, blankRequired: true };
    expect(validateTitrationForm(r)).toMatch(/excess/i);
    expect(validateTitrationForm({ ...r, excessSolutionMasterId: 2, excessVolumeMl: "25" })).toBeNull();
  });
  it("relative needs the reference standard and no factor", () => {
    const rel = { ...ok, calculation: "Relative" as const, equivalencyFactor: "" };
    expect(validateTitrationForm(rel)).toMatch(/reference standard/);
    expect(validateTitrationForm({ ...rel, standardEntryId: 9 })).toBeNull();
  });
  it("replicate count 1-10", () => {
    expect(validateTitrationForm({ ...ok, replicateCount: "11" })).toMatch(/between 1 and 10/);
  });
  it("sends hidden fields as null", () => {
    const p = titrationPayloadFields({ ...ok, tempCorrection: true, excessVolumeMl: "5" }, true);
    expect(p.titrationExcessVolumeMl).toBeNull();
    expect(p.titrationTempCorrection).toBeNull();
    expect(p.titrationEquivalencyFactor).toBe(88.06);
    expect(p.titrationIndicatorEntryId).toBe(4);
    expect(titrationPayloadFields({ ...ok, endpoint: "Potentiometric" }, true).titrationIndicatorEntryId).toBeNull();
    expect(titrationPayloadFields(ok, false).titrationType).toBeNull();
  });
});

describe("titration specification bases", () => {
  it("needs label claim only for label claim / mg per unit", () => {
    expect(titrationBasisNeedsLabelClaim("PercentAsIs")).toBe(false);
    expect(titrationBasisNeedsLabelClaim("MgPerUnit")).toBe(true);
    expect(validateTitrationSpecBasis("PercentLabelClaim", "")).toMatch(/Label claim/);
    expect(validateTitrationSpecBasis("PercentLabelClaim", "500")).toBeNull();
    expect(validateTitrationSpecBasis("MgPerKg", "")).toMatch(/basis/);
  });
});

describe("TitrationConfigSection", () => {
  it("KF lists only mg H2O/mL reagents and hides mode/calculation/factor", async () => {
    render(<TitrationConfigSection form={form({ type: "KarlFischer" })} onChange={() => {}} />);
    await waitFor(() => expect(screen.queryByLabelText(/Mode/)).toBeNull());
    expect(screen.queryByLabelText(/Equivalency factor/)).toBeNull();
    expect(screen.queryByLabelText(/Calculation method/)).toBeNull();
    expect(screen.getByLabelText(/KF reagent/)).toBeTruthy();
  });
  it("shows the excess fields for residual and the standard picker for relative", async () => {
    const { rerender } = render(<TitrationConfigSection form={form({ type: "Redox", mode: "Residual" })} onChange={() => {}} />);
    expect(screen.getByLabelText(/Excess volume/)).toBeTruthy();
    expect(screen.queryByLabelText(/Reference standard/)).toBeNull();
    rerender(<TitrationConfigSection form={form({ type: "Redox", calculation: "Relative" })} onChange={() => {}} />);
    expect(screen.getByLabelText(/Reference standard/)).toBeTruthy();
    expect(screen.queryByLabelText(/Equivalency factor/)).toBeNull();
    expect(screen.queryByLabelText(/theoretical/i)).toBeNull();
  });
  it("picks the visual-endpoint indicator from the Indicator entries", async () => {
    render(<TitrationConfigSection form={form({ type: "AcidBase", endpoint: "Visual" })} onChange={() => {}} sectionId={1} />);
    fireEvent.mouseDown(screen.getByLabelText(/Indicator/));
    expect(await screen.findByRole("option", { name: /Phenolphthalein TS/ })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /Ascorbic acid RS/ })).toBeNull();
  });
});
