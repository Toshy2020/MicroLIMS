import { useCallback, useMemo, useRef } from "react";
import { useTranslation } from "../../../../i18n";
import { ConfigWizard } from "../../../../components/config/ConfigWizard";
import type { WizardStep } from "../../../../components/config/useWizard";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";
import { createDatesStep } from "./DatesStep";
import { createLotStep } from "./LotStep";
import { createMaterialStep } from "./MaterialStep";
import { useMaterialLookups, type MaterialLookups } from "./materialLookups";
import { firstMaterialError, toMaterialPayload, type MaterialFormModel } from "./materialFormModel";
import { buildMaterialContext } from "./materialSelections";

export interface CreatedMaterialRef {
  id: number;
  stockId: string | null;
  materialName?: string;
}

export interface MaterialWizardProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string, created?: CreatedMaterialRef) => void;
  // The lot being edited; null receives a new lot.
  editingItem: MaterialItem | null;
}

export const MATERIAL_DRAFT_FIELDS: readonly (keyof MaterialFormModel)[] = [
  "materialType",
  "customType",
  "materialName",
  "manufacturerName",
  "batchNumber",
  "receivingDate",
  "expiryDate",
  "code",
  "location",
  "storageCondition",
  "quantityReceived",
  "unit",
  "minimumStockLevel",
  "purity",
  "moisturePercent",
  "atccNumber",
  "organismId",
  "referenceOrganismId",
  "mediaProductId",
  "materialMasterEntryId",
  "categoryId",
  "sectionId"
];

/**
 * Three-step wizard (material, batch and storage, dates) for receiving a new stock lot and
 * correcting a received one. Replaces the single-page Add Material dialog; the body sent to
 * MaterialService is unchanged.
 */
export function MaterialWizard(props: MaterialWizardProps) {
  if (!props.open) return null;
  return <OpenMaterialWizard {...props} />;
}

function OpenMaterialWizard({ open, onClose, onSuccess, editingItem }: MaterialWizardProps) {
  const { t } = useTranslation("inventory");
  const lookups = useMaterialLookups(open, editingItem);
  const lookupsRef = useRef<MaterialLookups>(lookups);
  lookupsRef.current = lookups;
  const editingRef = useRef(editingItem);
  editingRef.current = editingItem;

  const getContext = useCallback(
    (model: MaterialFormModel) =>
      buildMaterialContext(model, editingRef.current, lookupsRef.current.sections.length, lookupsRef.current.categories),
    []
  );

  const steps = useMemo<WizardStep<MaterialFormModel>[]>(
    () => [
      createMaterialStep(() => editingRef.current, () => lookupsRef.current, getContext),
      createLotStep(() => editingRef.current, () => lookupsRef.current, getContext),
      createDatesStep(() => editingRef.current, () => lookupsRef.current, getContext)
    ],
    [getContext]
  );

  const handleSave = useCallback(
    async (model: MaterialFormModel) => {
      const ctx = getContext(model);
      // The steps check as the user goes; this also covers a form reached by jumping to Review.
      const message = firstMaterialError(model, ctx);
      if (message) throw new Error(message);
      const payload = toMaterialPayload(model, ctx);

      if (editingItem) {
        await MaterialService.update(editingItem.id, payload, editingItem.version);
        onSuccess(t("wizard.saved.updated"));
        return;
      }
      const created = (await MaterialService.create(payload)) as
        | { id?: number; stockId?: string | null; materialName?: string }
        | undefined;
      const createdRef: CreatedMaterialRef | undefined =
        created && typeof created.id === "number"
          ? { id: created.id, stockId: created.stockId ?? null, materialName: created.materialName }
          : undefined;
      onSuccess(
        created?.stockId ? t("wizard.saved.addedAs", { stockId: created.stockId }) : t("wizard.saved.added"),
        createdRef
      );
    },
    [editingItem, onSuccess, getContext, t]
  );

  if (!lookups.initial) return null;
  return (
    <WizardFrame
      title={editingItem ? (editingItem.stockId ? t("wizard.title.editWithId", { stockId: editingItem.stockId }) : t("wizard.title.edit")) : t("wizard.title.add")}
      mode={editingItem ? "edit" : "create"}
      draftKey={editingItem ? `material-edit-${editingItem.id}` : "material-create"}
      steps={steps}
      initial={lookups.initial}
      onSave={handleSave}
      onClose={onClose}
    />
  );
}

interface WizardFrameProps {
  title: string;
  mode: "create" | "edit";
  draftKey: string;
  steps: WizardStep<MaterialFormModel>[];
  initial: MaterialFormModel;
  onSave: (model: MaterialFormModel) => Promise<void>;
  onClose: () => void;
}

function WizardFrame({ title, mode, draftKey, steps, initial, onSave, onClose }: WizardFrameProps) {
  return (
    <ConfigWizard
      open
      title={title}
      mode={mode}
      steps={steps}
      initial={initial}
      original={mode === "edit" ? initial : undefined}
      draftKey={draftKey}
      draftFields={[...MATERIAL_DRAFT_FIELDS]}
      onSave={onSave}
      onClose={onClose}
    />
  );
}
