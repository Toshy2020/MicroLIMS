import { useCallback, useMemo, useRef } from "react";
import { toast } from "sonner";
import { ConfigWizard } from "../../../../components/config/ConfigWizard";
import type { WizardStep } from "../../../../components/config/useWizard";
import { useTranslation } from "../../../../i18n";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import type { EquipmentItem } from "../../../inventory/equipment/types/equipmentTypes";
import {
  EquipmentConfigurationService,
  type ConfiguredEquipmentSummary,
  type UpdateEquipmentRequest
} from "../services/EquipmentConfigurationService";
import { createDocumentsStep } from "./DocumentsStep";
import { createIdentityStep } from "./IdentityStep";
import { createTypeSettingsStep } from "./TypeSettingsStep";
import type { EquipmentWizardLookup } from "./equipmentLookup";
import {
  createEmptyEquipmentFormModel,
  fromEquipment,
  toEquipmentPayload,
  type EquipmentFormModel,
  type EquipmentModelContext
} from "./equipmentFormModel";

export interface EquipmentWizardProps {
  open: boolean;
  // Equipment being edited (edit mode).
  editing?: ConfiguredEquipmentSummary | null;
  // Equipment to copy settings from ("New from existing").
  copyFrom?: ConfiguredEquipmentSummary | null;
  // Fields to pre-fill a new record with (for example from an asset register entry).
  prefill?: Partial<EquipmentFormModel> | null;
  mySections: LaboratorySection[];
  inventory: EquipmentItem[];
  onSaved: (createdId: number | null) => void | Promise<void>;
  onClose: () => void;
}

export const EQUIPMENT_DRAFT_FIELDS: readonly (keyof EquipmentFormModel)[] = [
  "name",
  "code",
  "type",
  "location",
  "vendor",
  "cdsSoftware",
  "connectionSettings",
  "setPointTemperature",
  "calibrationDueDate",
  "equipmentInventoryId",
  "sectionId"
];

// "New from existing" keeps the type and its settings. The code, calibration due
// date, asset register link (serial number and documents) are never copied.
export function copyFromEquipment(
  source: ConfiguredEquipmentSummary,
  mySections: LaboratorySection[]
): EquipmentFormModel {
  const base = fromEquipment(source);
  const sourceSectionUsable = mySections.some((s) => s.sectionId === base.sectionId);
  return {
    ...base,
    code: "",
    calibrationDueDate: "",
    equipmentInventoryId: null,
    sectionId: mySections.length === 1 ? mySections[0].sectionId : sourceSectionUsable ? base.sectionId : ""
  };
}

/**
 * Three-step wizard (identity, type settings, calibration and documents) for
 * adding, copying ("New from existing") and editing equipment.
 */
export function EquipmentWizard(props: EquipmentWizardProps) {
  if (!props.open) return null;
  return <OpenEquipmentWizard {...props} />;
}

function OpenEquipmentWizard({
  open,
  editing = null,
  copyFrom = null,
  prefill = null,
  mySections,
  inventory,
  onSaved,
  onClose
}: EquipmentWizardProps) {
  const { t } = useTranslation("masterData");
  const isEditing = editing !== null;
  const ctx = useMemo<EquipmentModelContext>(
    () => ({ isEditing, sectionCount: mySections.length }),
    [isEditing, mySections.length]
  );
  const lookup = useMemo<EquipmentWizardLookup>(() => ({ mySections, inventory }), [mySections, inventory]);
  const lookupRef = useRef(lookup);
  lookupRef.current = lookup;
  const ctxRef = useRef(ctx);
  ctxRef.current = ctx;
  const tRef = useRef(t);
  tRef.current = t;

  const steps = useMemo<WizardStep<EquipmentFormModel>[]>(
    () => [
      createIdentityStep(() => lookupRef.current, () => ctxRef.current, () => tRef.current),
      createTypeSettingsStep(() => ctxRef.current, () => tRef.current),
      createDocumentsStep(() => lookupRef.current, () => tRef.current)
    ],
    []
  );

  const initial = useMemo<EquipmentFormModel>(() => {
    if (editing) return fromEquipment(editing);
    if (copyFrom) return copyFromEquipment(copyFrom, mySections);
    return createEmptyEquipmentFormModel({
      ...prefill,
      sectionId: mySections.length === 1 ? mySections[0].sectionId : ""
    });
  }, [editing, copyFrom, prefill, mySections]);

  const usageFn = useMemo(
    () => (editing ? () => masterDataUsageService.getUsage("Equipment", editing.id) : undefined),
    [editing]
  );

  const handleSave = useCallback(
    async (model: EquipmentFormModel, reason: string | null) => {
      const payload = toEquipmentPayload(model, ctxRef.current);
      if (editing) {
        if (reason) {
          await EquipmentConfigurationService.updateEquipment(
            editing.id,
            payload as UpdateEquipmentRequest,
            editing.version,
            reason
          );
        } else {
          await EquipmentConfigurationService.updateEquipment(editing.id, payload as UpdateEquipmentRequest, editing.version);
        }
        toast.success(t("equipmentWizard.updatedToast"));
        await onSaved(null);
        return;
      }
      const created = await EquipmentConfigurationService.createEquipment(payload);
      toast.success(t("equipmentWizard.createdToast"));
      await onSaved(created?.id ?? null);
    },
    [editing, onSaved, t]
  );

  const title = editing
    ? t("equipmentWizard.editTitle", { code: editing.code })
    : copyFrom
      ? t("equipmentWizard.copyTitle", { code: copyFrom.code })
      : t("equipmentWizard.addTitle");
  const draftKey = editing
    ? `equipment-edit-${editing.id}`
    : copyFrom
      ? `equipment-copy-${copyFrom.id}`
      : prefill?.equipmentInventoryId != null
        ? `equipment-create-asset-${prefill.equipmentInventoryId}`
        : "equipment-create";

  return (
    <ConfigWizard
      open={open}
      title={title}
      mode={isEditing ? "edit" : "create"}
      steps={steps}
      initial={initial}
      original={isEditing ? initial : undefined}
      draftKey={draftKey}
      draftFields={[...EQUIPMENT_DRAFT_FIELDS]}
      usage={usageFn}
      reasonRequired={(u) => (u?.openWorkCount ?? 0) > 0}
      onSave={handleSave}
      onClose={onClose}
    />
  );
}
