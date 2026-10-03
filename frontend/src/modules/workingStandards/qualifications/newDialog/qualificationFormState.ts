export interface NewQualificationFormState {
  kind: "Initial" | "Requalification";
  sourceMode: "Manual" | "Received";
  materialMasterEntryId: number | "";
  sourceSampleId?: number | null;
  sourceMaterialName: string;
  sourceBatchNumber: string;
  quantityGrams: string;
  location: string;
  moisturePercent: string;
  workingStandardMaterialId?: number | null;
}

export function isNewQualificationSaveEnabled(state: NewQualificationFormState): boolean {
  if (state.kind === "Initial") {
    if (state.sourceMode === "Manual") {
      return Boolean(
        state.sourceMaterialName.trim() &&
        state.sourceBatchNumber.trim() &&
        state.materialMasterEntryId &&
        state.quantityGrams &&
        Number(state.quantityGrams) > 0 &&
        state.location.trim()
      );
    }
    return Boolean(
      state.sourceSampleId &&
      state.materialMasterEntryId &&
      state.quantityGrams &&
      Number(state.quantityGrams) > 0 &&
      state.location.trim()
    );
  }

  return Boolean(
    state.workingStandardMaterialId &&
    state.moisturePercent.trim() !== "" &&
    !isNaN(Number(state.moisturePercent))
  );
}
