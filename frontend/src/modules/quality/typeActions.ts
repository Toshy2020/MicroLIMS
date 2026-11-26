import type { ComponentType } from "react";
import type { QualityRecordDetailDto, QualityRecordType } from "../../services/qualityService";

// The extension point for the step forms of each record type (deviation assessment, CAPA plan, change-control
// impact, complaint conclusion, ...). The record page renders the component registered for the record's type
// in its "actions for this record" slot and knows nothing else about it. A type with nothing registered shows
// no slot.
//
// A step component receives the record as the server sent it and the permissions of the signed-in person. It
// sends its own requests (the exact If-Match record version, signature dialogs for signed steps) and calls
// onChanged after a step was saved or signed, so the page reloads the record, its path and its panels.
export interface QualityTypeActionsProps {
  record: QualityRecordDetailDto;
  permissions: {
    raise: boolean;
    investigate: boolean;
    labConclude: boolean;
    approve: boolean;
  };
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

const registry = new Map<QualityRecordType, ComponentType<QualityTypeActionsProps>>();

// Registers the step forms of one type; returns the function that removes them again.
export function registerQualityTypeActions(type: QualityRecordType, component: ComponentType<QualityTypeActionsProps>): () => void {
  registry.set(type, component);
  return () => {
    if (registry.get(type) === component) registry.delete(type);
  };
}

export function qualityTypeActionsFor(type: QualityRecordType): ComponentType<QualityTypeActionsProps> | undefined {
  return registry.get(type);
}
