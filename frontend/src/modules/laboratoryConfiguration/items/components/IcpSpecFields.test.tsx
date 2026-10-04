import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { IcpSpecFields, icpParameterName } from "./IcpSpecFields";
import type { IcpMethodResponse } from "../../masterDataSimple/services/IcpMethodService";

afterEach(cleanup);

const mockMineralMethod: IcpMethodResponse = {
  id: 10,
  version: 1,
  sectionId: 1,
  name: "Multivitamin Mineral Assay",
  abbreviation: "MIN-01",
  effectiveDate: "2026-01-01T00:00:00Z",
  isActive: true,
  mode: "MineralAssay",
  standardLevelsMgPerL: "0.1, 0.5, 1, 3, 6",
  calibrationStandardEntryId: 101,
  minCorrelation: 0.999,
  sampleVolumeMl: 50,
  dilutionFactor: 1,
  maxCalibrationAgeHours: 24,
  requireBlank: true,
  requireIcv: true,
  requireCcv: false,
  elements: [
    { id: 1, displayOrder: 1, symbol: "Zn", wavelengthNm: 213.857, view: "Axial", conversionFactor: 1 },
    { id: 2, displayOrder: 2, symbol: "Fe", wavelengthNm: 238.204, view: "Radial", conversionFactor: 1 }
  ],
  createdAt: "2026-01-01T00:00:00Z",
  lastModifiedAt: "2026-01-01T00:00:00Z"
};

const mockImpuritiesMethod: IcpMethodResponse = {
  ...mockMineralMethod,
  id: 20,
  name: "Elemental Impurities USP 232",
  abbreviation: "IMP-01",
  mode: "ElementalImpurities",
  elements: [
    { id: 3, displayOrder: 1, symbol: "Pb", wavelengthNm: 220.353, view: "Axial", conversionFactor: 1 },
    { id: 4, displayOrder: 2, symbol: "As", wavelengthNm: 188.980, view: "Axial", conversionFactor: 1 }
  ]
};

describe("IcpSpecFields", () => {
  describe("mode switches basis options", () => {
    it("renders mineral assay mode with basis select (% of label claim, mg per unit) and label claim fields", async () => {
      const onBasisChange = vi.fn();
      const onLabelClaimChange = vi.fn();
      const onLabelClaimUnitChange = vi.fn();

      render(
        <PinnedLightTheme>
          <IcpSpecFields
            method={mockMineralMethod}
            loadingMethod={false}
            selectedElementId={1}
            onElementChange={vi.fn()}
            resultBasis="PercentLabelClaim"
            onBasisChange={onBasisChange}
            labelClaim="15"
            onLabelClaimChange={onLabelClaimChange}
            labelClaimUnit="mg"
            onLabelClaimUnitChange={onLabelClaimUnitChange}
          />
        </PinnedLightTheme>
      );

      expect(screen.getByText("Mineral Assay (ICP) Specification")).toBeTruthy();
      expect(screen.getByLabelText(/Label Claim \*/i)).toBeTruthy();
      expect(screen.getByLabelText(/Label Claim Unit \*/i)).toBeTruthy();

      // Open basis select
      const basisSelect = screen.getByRole("combobox", { name: /Basis/i });
      fireEvent.mouseDown(basisSelect);

      const mgPerUnitOpt = await screen.findByRole("option", { name: /mg per unit/i });
      const percentOpt = await screen.findByRole("option", { name: /% of label claim/i });
      expect(mgPerUnitOpt).toBeTruthy();
      expect(percentOpt).toBeTruthy();

      fireEvent.click(mgPerUnitOpt);
      expect(onBasisChange).toHaveBeenCalledWith("MgPerUnit");

      // Change label claim input
      const claimInput = screen.getByLabelText(/Label Claim \*/i);
      fireEvent.change(claimInput, { target: { value: "25" } });
      expect(onLabelClaimChange).toHaveBeenCalledWith("25");

      // Change label claim unit input
      const unitInput = screen.getByLabelText(/Label Claim Unit \*/i);
      fireEvent.change(unitInput, { target: { value: "mcg" } });
      expect(onLabelClaimUnitChange).toHaveBeenCalledWith("mcg");
    });
  });

  describe("impurities fixes NMT + µg/g", () => {
    it("renders elemental impurities mode with fixed µg/g basis, fixed NMT limit type, and no label claim fields", () => {
      render(
        <PinnedLightTheme>
          <IcpSpecFields
            method={mockImpuritiesMethod}
            loadingMethod={false}
            selectedElementId={3}
            onElementChange={vi.fn()}
            resultBasis="MgPerKg"
            onBasisChange={vi.fn()}
            labelClaim=""
            onLabelClaimChange={vi.fn()}
            labelClaimUnit=""
            onLabelClaimUnitChange={vi.fn()}
          />
        </PinnedLightTheme>
      );

      expect(screen.getByText("Elemental Impurities (ICP) Specification")).toBeTruthy();

      // Fixed Result Basis: µg/g
      expect(screen.getByDisplayValue("µg/g")).toBeTruthy();
      expect(screen.getByText("Fixed basis for elemental impurities (MgPerKg)")).toBeTruthy();

      // Fixed Limit Type: NMT (Not More Than)
      expect(screen.getByDisplayValue("NMT (Not More Than)")).toBeTruthy();
      expect(screen.getByText("Fixed limit type for elemental impurities")).toBeTruthy();

      // No label claim fields in ElementalImpurities mode
      expect(screen.queryByLabelText(/Label Claim \*/i)).toBeNull();
      expect(screen.queryByLabelText(/Label Claim Unit \*/i)).toBeNull();
    });
  });

  describe("element picker", () => {
    it("calls onElementChange when an element is chosen", async () => {
      const onElementChange = vi.fn();
      render(
        <PinnedLightTheme>
          <IcpSpecFields
            method={mockMineralMethod}
            loadingMethod={false}
            selectedElementId=""
            onElementChange={onElementChange}
            resultBasis="PercentLabelClaim"
            onBasisChange={vi.fn()}
            labelClaim=""
            onLabelClaimChange={vi.fn()}
            labelClaimUnit=""
            onLabelClaimUnitChange={vi.fn()}
          />
        </PinnedLightTheme>
      );

      const elementSelect = screen.getByRole("combobox", { name: /Element/i });
      fireEvent.mouseDown(elementSelect);

      const feOption = await screen.findByRole("option", { name: /Fe \(238\.204 nm\)/i });
      fireEvent.click(feOption);

      expect(onElementChange).toHaveBeenCalledWith(2, mockMineralMethod.elements[1]);
    });
  });

  describe("icpParameterName", () => {
    it("appends (per unit) for MgPerUnit basis", () => {
      expect(icpParameterName("Zn", "MgPerUnit")).toBe("Zn (per unit)");
    });

    it("returns element symbol for other bases", () => {
      expect(icpParameterName("Zn", "PercentLabelClaim")).toBe("Zn");
      expect(icpParameterName("Pb", "MgPerKg")).toBe("Pb");
    });
  });
});
