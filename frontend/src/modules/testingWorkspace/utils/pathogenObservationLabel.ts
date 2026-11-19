import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";
import { PathogenObservationDetail } from "../types/sampleSummaryTypes";

// Mirrors backend ReportDocumentMapper.ObservationText exactly, so the
// on-screen Sample Summary dialog, the printable report, and the archived
// PDF/Word copies all describe the same observation with the same words -
// a reviewer should never see "Growth observed" on screen and a different
// sentence on the signed record. GrowthNonConforming gets its own sentence
// (growth occurred, but not the target organism) rather than collapsing
// into "No growth" - conflating the two would hide a genuine growth event
// from the reviewer.
const OBSERVATION_LABELS: Record<PathogenObservationDetail["observation"], string> = {
  NoGrowth: "No growth",
  GrowthNonConforming: "Growth observed - does not match target organism",
  GrowthConforming: "Growth observed - matches target organism"
};

export const pathogenObservationLabel = (
  observation: PathogenObservationDetail["observation"],
  t?: TFunction
): string => {
  if (!(observation in OBSERVATION_LABELS)) return observation;
  const translate = t ?? i18n.getFixedT("en", "testingWorkspace");
  return translate(`pathogenObservation.${observation}`, {
    ns: "testingWorkspace",
    defaultValue: OBSERVATION_LABELS[observation]
  });
};
