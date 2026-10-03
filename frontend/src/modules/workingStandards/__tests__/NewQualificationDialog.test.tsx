import { describe, it, expect } from "vitest";
import {
  NewQualificationDialog,
  isNewQualificationSaveEnabled,
  type NewQualificationFormState
} from "../qualifications/NewQualificationDialog";

describe("NewQualificationDialog", () => {
  it("Initial + Manual with empty batch -> Save disabled; filling name, batch, entry, quantity, location -> Save enabled", () => {
    // 1. Initial + Manual with empty batch -> Save disabled
    const formWithEmptyBatch: NewQualificationFormState = {
      kind: "Initial",
      sourceMode: "Manual",
      sourceMaterialName: "Paracetamol Reference Standard",
      sourceBatchNumber: "", // empty batch
      materialMasterEntryId: 101,
      quantityGrams: "25.0",
      location: "Cabinet A-1",
      moisturePercent: "0.45"
    };

    expect(isNewQualificationSaveEnabled(formWithEmptyBatch)).toBe(false);

    // 2. Filling name, batch, entry, quantity, location -> Save enabled
    const validForm: NewQualificationFormState = {
      ...formWithEmptyBatch,
      sourceBatchNumber: "BATCH-2026-001"
    };

    expect(isNewQualificationSaveEnabled(validForm)).toBe(true);
  });

  it("verifies required fields for Initial + Manual qualification", () => {
    const baseValid: NewQualificationFormState = {
      kind: "Initial",
      sourceMode: "Manual",
      sourceMaterialName: "Aspirin RS",
      sourceBatchNumber: "LOT-ASP-01",
      materialMasterEntryId: 42,
      quantityGrams: "10.0",
      location: "Shelf 2",
      moisturePercent: ""
    };

    expect(isNewQualificationSaveEnabled(baseValid)).toBe(true);

    // Missing name -> disabled
    expect(isNewQualificationSaveEnabled({ ...baseValid, sourceMaterialName: "   " })).toBe(false);

    // Missing master entry -> disabled
    expect(isNewQualificationSaveEnabled({ ...baseValid, materialMasterEntryId: "" })).toBe(false);

    // Zero or invalid quantity -> disabled
    expect(isNewQualificationSaveEnabled({ ...baseValid, quantityGrams: "0" })).toBe(false);
    expect(isNewQualificationSaveEnabled({ ...baseValid, quantityGrams: "" })).toBe(false);

    // Missing location -> disabled
    expect(isNewQualificationSaveEnabled({ ...baseValid, location: "" })).toBe(false);
  });

  it("verifies required fields for Requalification", () => {
    const requalForm: NewQualificationFormState = {
      kind: "Requalification",
      sourceMode: "Manual",
      sourceMaterialName: "Paracetamol",
      sourceBatchNumber: "BATCH-1",
      materialMasterEntryId: "",
      sourceSampleId: null,
      quantityGrams: "",
      location: "",
      moisturePercent: "1.25",
      workingStandardMaterialId: 501
    };

    expect(isNewQualificationSaveEnabled(requalForm)).toBe(true);

    // Missing moisture percent -> disabled
    expect(isNewQualificationSaveEnabled({ ...requalForm, moisturePercent: "" })).toBe(false);
    expect(isNewQualificationSaveEnabled({ ...requalForm, moisturePercent: "invalid" })).toBe(false);

    // Missing target lot -> disabled
    expect(isNewQualificationSaveEnabled({ ...requalForm, workingStandardMaterialId: null })).toBe(false);
  });

  it("exports NewQualificationDialog component", () => {
    expect(NewQualificationDialog).toBeDefined();
    expect(typeof NewQualificationDialog).toBe("function");
  });
});
