import type { TFunction } from "i18next";
import { i18n } from "../../i18n";
import type {
  ItemPreparationConfiguration,
  PreparationMaterialSource
} from "./services/ItemPreparationConfigurationService";
import type { PreparationMaterialPayload } from "./services/SamplePreparationService";

export type PreparationRole = "diluent" | "neutralizer";

// What a picker holds: a source and, unless the neutralizer is contained in
// the medium, one master id of that source.
export interface PreparationMaterialSelection {
  source: PreparationMaterialSource | "";
  masterId: number | null;
}

export const EMPTY_SELECTION: PreparationMaterialSelection = { source: "", masterId: null };

export function getSourceLabels(t: TFunction<"testPreparation">): Record<PreparationMaterialSource, string> {
  return {
    MediaProduct: t("sources.MediaProduct"),
    SolutionRecipe: t("sources.SolutionRecipe"),
    MaterialEntry: t("sources.MaterialEntry"),
    ContainedInMedium: t("sources.ContainedInMedium")
  };
}

export const SOURCE_LABELS: Record<PreparationMaterialSource, string> = getSourceLabels(
  i18n.getFixedT("en", "testPreparation")
);

export const sourceLabel = (
  s: PreparationMaterialSource | null | undefined,
  t: TFunction<"testPreparation"> = i18n.getFixedT("en", "testPreparation")
) => (s ? getSourceLabels(t)[s] : null);

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

type DiluentPayload = Pick<
  PreparationMaterialPayload,
  "diluentSource" | "diluentMediaProductId" | "diluentSolutionMasterId" | "diluentMaterialMasterEntryId"
>;
type NeutralizerPayload = Pick<
  PreparationMaterialPayload,
  "neutralizerSource" | "neutralizerMediaProductId" | "neutralizerSolutionMasterId" | "neutralizerMaterialMasterEntryId"
>;

export function selectionPayload(role: "diluent", sel: PreparationMaterialSelection): DiluentPayload;
export function selectionPayload(role: "neutralizer", sel: PreparationMaterialSelection): NeutralizerPayload;
export function selectionPayload(role: PreparationRole, sel: PreparationMaterialSelection): DiluentPayload | NeutralizerPayload;
// The wire fields for one role: the source plus exactly the one id it names
// (the server rejects any other id), so an unrelated id never leaks through.
export function selectionPayload(role: PreparationRole, sel: PreparationMaterialSelection): DiluentPayload | NeutralizerPayload {
  const source = sel.source as PreparationMaterialSource;
  if (role === "diluent") {
    return {
      diluentSource: source,
      diluentMediaProductId: source === "MediaProduct" ? sel.masterId : null,
      diluentSolutionMasterId: source === "SolutionRecipe" ? sel.masterId : null,
      diluentMaterialMasterEntryId: source === "MaterialEntry" ? sel.masterId : null
    };
  }
  return {
    neutralizerSource: source,
    neutralizerMediaProductId: source === "MediaProduct" ? sel.masterId : null,
    neutralizerSolutionMasterId: source === "SolutionRecipe" ? sel.masterId : null,
    neutralizerMaterialMasterEntryId: source === "MaterialEntry" ? sel.masterId : null
  };
}

// " — lot X" suffix for report and summary displays.
export const withLot = (
  name: string,
  lotCode?: string | null,
  t: TFunction<"testPreparation"> = i18n.getFixedT("en", "testPreparation")
) => (lotCode ? t("withLot", { name, lotCode }) : name);
