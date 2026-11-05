import type {
  ItemPreparationConfiguration,
  PreparationMaterialSource
} from "./services/ItemPreparationConfigurationService";

export type PreparationRole = "diluent" | "neutralizer";

// What a picker holds: a source and, unless the neutralizer is contained in
// the medium, one master id of that source.
export interface PreparationMaterialSelection {
  source: PreparationMaterialSource | "";
  masterId: number | null;
}

export const EMPTY_SELECTION: PreparationMaterialSelection = { source: "", masterId: null };

export const SOURCE_LABELS: Record<PreparationMaterialSource, string> = {
  MediaProduct: "Media product",
  SolutionRecipe: "Solution recipe",
  MaterialEntry: "Material entry",
  ContainedInMedium: "Contained in the medium"
};

export const sourceLabel = (s: PreparationMaterialSource | null | undefined) => (s ? SOURCE_LABELS[s] : null);

export function isSelectionComplete(sel: PreparationMaterialSelection): boolean {
  if (!sel.source) return false;
  return sel.source === "ContainedInMedium" || sel.masterId != null;
}

export function selectionFromConfig(
  c: ItemPreparationConfiguration,
  role: PreparationRole
): PreparationMaterialSelection {
  const source = role === "diluent" ? c.diluentSource : c.neutralizerSource;
  if (!source) return EMPTY_SELECTION;
  const media = role === "diluent" ? c.diluentMediaProductId : c.neutralizerMediaProductId;
  const solution = role === "diluent" ? c.diluentSolutionMasterId : c.neutralizerSolutionMasterId;
  const material = role === "diluent" ? c.diluentMaterialMasterEntryId : c.neutralizerMaterialMasterEntryId;
  const masterId =
    source === "MediaProduct" ? media : source === "SolutionRecipe" ? solution : source === "MaterialEntry" ? material : null;
  return { source, masterId: masterId ?? null };
}

// The wire fields for one role: the source plus exactly the one id it names
// (the server rejects any other id), so an unrelated id never leaks through.
export function selectionPayload(role: PreparationRole, sel: PreparationMaterialSelection) {
  const prefix = role;
  const source = sel.source as PreparationMaterialSource;
  return {
    [`${prefix}Source`]: source,
    [`${prefix}MediaProductId`]: source === "MediaProduct" ? sel.masterId : null,
    [`${prefix}SolutionMasterId`]: source === "SolutionRecipe" ? sel.masterId : null,
    [`${prefix}MaterialMasterEntryId`]: source === "MaterialEntry" ? sel.masterId : null
  } as Record<string, unknown>;
}

// " — lot X" suffix for report and summary displays.
export const withLot = (name: string, lotCode?: string | null) =>
  lotCode ? `${name} — lot ${lotCode}` : name;
