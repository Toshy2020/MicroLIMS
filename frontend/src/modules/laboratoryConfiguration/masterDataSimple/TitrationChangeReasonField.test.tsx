import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import {
  TitrationChangeReasonField,
  hasTitrationSettingsChanged,
  validateTitrationChangeReason,
  titrationChangeReasonCheck
} from "./TitrationChangeReasonField";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { createInitialTitrationForm, TitrationFormState } from "./titrationConfig";

afterEach(cleanup);

describe("TitrationChangeReasonField", () => {
  const baseTest: TestDefinitionOption = {
    id: 1,
    code: "TIT-01",
    displayName: "Citric Acid Assay",
    isActive: true,
    workflowType: "Titration",
    titrationType: "AcidBase",
    titrationMode: "Direct",
    titrationCalculation: "UspFactor",
    titrationEndpoint: "Visual",
    titrantSolutionMasterId: 10,
    titrationEquivalencyFactor: 64.03,
    titrationBlankRequired: false,
    titrationIndicatorEntryId: 5,
    replicateCount: 3
  };

  const baseForm: TitrationFormState = {
    ...createInitialTitrationForm(),
    type: "AcidBase",
    mode: "Direct",
    calculation: "UspFactor",
    endpoint: "Visual",
    titrantSolutionMasterId: 10,
    equivalencyFactor: "64.03",
    blankRequired: false,
    indicatorEntryId: 5,
    replicateCount: "3"
  };

  describe("hasTitrationSettingsChanged", () => {
    it("returns false when initial test is null", () => {
      expect(hasTitrationSettingsChanged(null, baseForm, "Titration")).toBe(false);
    });

    it("returns false for non-titration test with no change", () => {
      const nonTitrationTest: TestDefinitionOption = {
        ...baseTest,
        workflowType: "HplcMethodAssay"
      };
      expect(hasTitrationSettingsChanged(nonTitrationTest, baseForm, "HplcMethodAssay")).toBe(false);
    });

    it("returns true when switching workflow to Titration", () => {
      const nonTitrationTest: TestDefinitionOption = {
        ...baseTest,
        workflowType: "HplcMethodAssay"
      };
      expect(hasTitrationSettingsChanged(nonTitrationTest, baseForm, "Titration")).toBe(true);
    });

    it("returns false when titration fields match the loaded values", () => {
      expect(hasTitrationSettingsChanged(baseTest, baseForm, "Titration")).toBe(false);
    });

    it("returns true when only the physchem area changes", () => {
      const t = { ...baseTest, physchemArea: "FinishedProduct" as const };
      expect(hasTitrationSettingsChanged(t, baseForm, "Titration", "FinishedProduct")).toBe(false);
      expect(hasTitrationSettingsChanged(t, baseForm, "Titration", "Both")).toBe(true);
      expect(titrationChangeReasonCheck(t, baseForm, "Titration", "", "Both").required).toBe(true);
    });

    it("returns true when equivalency factor changes", () => {
      const changedForm: TitrationFormState = {
        ...baseForm,
        equivalencyFactor: "64.04"
      };
      expect(hasTitrationSettingsChanged(baseTest, changedForm, "Titration")).toBe(true);
    });

    it("returns true when titrant solution master changes", () => {
      const changedForm: TitrationFormState = {
        ...baseForm,
        titrantSolutionMasterId: 11
      };
      expect(hasTitrationSettingsChanged(baseTest, changedForm, "Titration")).toBe(true);
    });

    it("returns true when replicateCount changes", () => {
      const changedForm: TitrationFormState = {
        ...baseForm,
        replicateCount: "4"
      };
      expect(hasTitrationSettingsChanged(baseTest, changedForm, "Titration")).toBe(true);
    });

    it("returns true when endpoint changes", () => {
      const changedForm: TitrationFormState = {
        ...baseForm,
        endpoint: "Potentiometric"
      };
      expect(hasTitrationSettingsChanged(baseTest, changedForm, "Titration")).toBe(true);
    });
  });

  describe("validateTitrationChangeReason", () => {
    it("rejects empty or whitespace string", () => {
      expect(validateTitrationChangeReason("")).not.toBeNull();
      expect(validateTitrationChangeReason("   ")).not.toBeNull();
      expect(validateTitrationChangeReason(null)).not.toBeNull();
      expect(validateTitrationChangeReason(undefined)).not.toBeNull();
    });

    it("rejects strings shorter than 5 chars", () => {
      expect(validateTitrationChangeReason("test")).not.toBeNull();
      expect(validateTitrationChangeReason(" a  ")).not.toBeNull();
    });

    it("rejects strings longer than 500 chars", () => {
      const long = "a".repeat(501);
      expect(validateTitrationChangeReason(long)).not.toBeNull();
    });

    it("accepts valid reasons between 5 and 500 chars", () => {
      expect(validateTitrationChangeReason("USP 2026 revision")).toBeNull();
      expect(validateTitrationChangeReason("a".repeat(500))).toBeNull();
    });
  });

  describe("component rendering", () => {
    it("renders field with label and placeholder, updates on input", () => {
      const onChange = vi.fn();
      render(<TitrationChangeReasonField value="" onChange={onChange} />);

      expect(screen.getByLabelText(/Reason for Change/)).toBeTruthy();
      const input = screen.getByPlaceholderText(/Enter reason for changing titration settings/);
      expect(input).toBeTruthy();

      fireEvent.change(input, { target: { value: "Updated factor" } });
      expect(onChange).toHaveBeenCalledWith("Updated factor");
    });

    it("displays error message when error prop is passed", () => {
      render(
        <TitrationChangeReasonField
          value="abc"
          onChange={vi.fn()}
          error="A reason for change is required when titration settings change (5 to 500 characters)."
        />
      );

      expect(
        screen.getByText("A reason for change is required when titration settings change (5 to 500 characters).")
      ).toBeTruthy();
    });
  });

  describe("titrationChangeReasonCheck", () => {
    it("requires and sends a reason when switching away from Titration", () => {
      const missing = titrationChangeReasonCheck(baseTest, createInitialTitrationForm(), "Measurement", "");
      expect(missing.required).toBe(true);
      expect(missing.error).not.toBeNull();
      const ok = titrationChangeReasonCheck(baseTest, createInitialTitrationForm(), "Measurement", "  Method retired  ");
      expect(ok).toEqual({ required: true, error: null, payload: "Method retired" });
    });

    it("needs nothing when titration settings are unchanged", () => {
      expect(titrationChangeReasonCheck(baseTest, baseForm, "Titration", "")).toEqual({ required: false, error: null, payload: null });
    });
  });
});
