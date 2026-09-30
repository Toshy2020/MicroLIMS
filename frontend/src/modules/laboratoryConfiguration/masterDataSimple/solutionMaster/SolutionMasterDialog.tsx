import { useState, useEffect, useMemo } from "react";
import {
  Box,
  Button,
  Stack,
  Alert,
  Typography
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { toast } from "sonner";
import {
  SolutionMaster,
  SolutionType,
  ShelfLifeUnit,
  TitrantStrengthUnit,
  StandardizationMode,
  SaveSolutionMasterRequest,
  SolutionMasterService
} from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import {
  SolutionFormState,
  ComponentRowState,
  createInitialSolutionFormState,
  solutionEntityToFormState,
  validateSolutionForm,
  formToSaveRequest,
  getAvailableMaterialEntries,
  getAvailableReferenceTitrants
} from "./solutionForm";
import { SolutionGeneralSection } from "./SolutionGeneralSection";
import { SolutionPhSection } from "./SolutionPhSection";
import { SolutionComponentsSection } from "./SolutionComponentsSection";
import { SolutionTitrantSection } from "./SolutionTitrantSection";
import { ReasonDialog } from "./ReasonDialog";

export interface SolutionMasterDialogProps {
  open: boolean;
  editingEntry: SolutionMaster | null;
  solutions: SolutionMaster[];
  materialMasters: MaterialMasterEntry[];
  sections: LaboratorySection[];
  mySections: LaboratorySection[];
  onClose: () => void;
  onSuccess: () => void;
  resolveSectionDisplay: (secId: number, secNameProp?: string) => string;
}

export function SolutionMasterDialog({
  open,
  editingEntry,
  solutions,
  materialMasters,
  sections,
  mySections,
  onClose,
  onSuccess,
  resolveSectionDisplay
}: SolutionMasterDialogProps) {
  const [form, setForm] = useState<SolutionFormState>(() =>
    editingEntry
      ? solutionEntityToFormState(editingEntry)
      : createInitialSolutionFormState(mySections.length === 1 ? mySections[0].sectionId : "")
  );

  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveReasonDialogOpen, setSaveReasonDialogOpen] = useState(false);
  const [saveReason, setSaveReason] = useState("");
  const [pendingSavePayload, setPendingSavePayload] = useState<SaveSolutionMasterRequest | null>(null);

  useEffect(() => {
    if (open) {
      if (editingEntry) {
        setForm(solutionEntityToFormState(editingEntry));
      } else {
        const defaultSecId = mySections.length === 1 ? mySections[0].sectionId : "";
        setForm(createInitialSolutionFormState(defaultSecId));
      }
      setDialogError(null);
      setSaving(false);
      setSaveReasonDialogOpen(false);
      setSaveReason("");
      setPendingSavePayload(null);
    }
  }, [open, editingEntry, mySections]);

  const currentSectionId = useMemo(() => {
    if (editingEntry) return editingEntry.sectionId;
    if (form.sectionId) return Number(form.sectionId);
    if (mySections.length === 1) return mySections[0].sectionId;
    return null;
  }, [editingEntry, form.sectionId, mySections]);

  const availableMaterialEntries = useMemo(() => {
    return getAvailableMaterialEntries(currentSectionId, materialMasters, editingEntry);
  }, [currentSectionId, materialMasters, editingEntry]);

  const availablePhAdjustingEntries = useMemo(() => {
    return availableMaterialEntries.filter((m) => m.category === "Reagent");
  }, [availableMaterialEntries]);

  const availableTitrantStandardEntries = useMemo(() => {
    return availableMaterialEntries.filter(
      (m) => m.category === "ReferenceStandard" || m.category === "Reagent"
    );
  }, [availableMaterialEntries]);

  const availableReferenceTitrants = useMemo(() => {
    return getAvailableReferenceTitrants(currentSectionId, solutions, editingEntry);
  }, [currentSectionId, solutions, editingEntry]);

  const updateFormField = <K extends keyof SolutionFormState>(field: K, value: SolutionFormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleAddComponentRow = () => {
    setForm((prev) => ({
      ...prev,
      components: [...prev.components, { materialMasterEntryId: "", quantity: "", unit: "Milliliter" }]
    }));
  };

  const handleRemoveComponentRow = (index: number) => {
    setForm((prev) => ({
      ...prev,
      components: prev.components.filter((_, i) => i !== index)
    }));
  };

  const handleMoveComponentRow = (index: number, direction: "up" | "down") => {
    setForm((prev) => {
      const copy = [...prev.components];
      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= copy.length) return prev;
      const temp = copy[index];
      copy[index] = copy[targetIndex];
      copy[targetIndex] = temp;
      return { ...prev, components: copy };
    });
  };

  const handleComponentChange = (index: number, field: keyof ComponentRowState, value: string | number) => {
    setForm((prev) => {
      const copy = [...prev.components];
      copy[index] = { ...copy[index], [field]: value };
      return { ...prev, components: copy };
    });
  };

  const handleInitiateSave = () => {
    setDialogError(null);
    const validationError = validateSolutionForm(form, {
      isEditing: Boolean(editingEntry),
      hasMultipleSections: mySections.length > 1,
      editingEntryId: editingEntry?.id
    });
    if (validationError) {
      setDialogError(validationError);
      return;
    }

    const payload = formToSaveRequest(form, currentSectionId);
    if (editingEntry) {
      setPendingSavePayload(payload);
      setSaveReason("");
      setSaveReasonDialogOpen(true);
    } else {
      executeCreate(payload);
    }
  };

  const executeCreate = async (payload: SaveSolutionMasterRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      await SolutionMasterService.create(payload);
      toast.success(`Solution master "${payload.name}" created.`);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not create solution master.");
    } finally {
      setSaving(false);
    }
  };

  const handleConfirmSaveWithReason = async () => {
    if (!editingEntry || !pendingSavePayload) return;
    const trimmedReason = saveReason.trim();
    if (!trimmedReason) return;

    setSaving(true);
    try {
      const finalPayload: SaveSolutionMasterRequest = { ...pendingSavePayload, reason: trimmedReason };
      await SolutionMasterService.update(editingEntry.id, finalPayload, editingEntry.version);
      toast.success(`Solution master "${editingEntry.name}" updated.`);
      setSaveReasonDialogOpen(false);
      setPendingSavePayload(null);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not update solution master.");
      setSaveReasonDialogOpen(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <FloatingDialog
        open={open}
        title={editingEntry ? `Edit Solution Master: ${editingEntry.name}` : "Add Solution Master"}
        onClose={onClose}
        maxWidth="md"
        actions={
          <>
            <Button onClick={onClose} disabled={saving} sx={{ textTransform: "none" }}>Cancel</Button>
            <Button
              variant="contained"
              onClick={handleInitiateSave}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingEntry ? "Save Changes" : "Create Solution"}
            </Button>
          </>
        }
      >
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {dialogError && (
            <Alert severity="error" onClose={() => setDialogError(null)}>
              {dialogError}
            </Alert>
          )}

          <SolutionGeneralSection
            name={form.name}
            type={form.type}
            shelfLifeValue={form.shelfLifeValue}
            shelfLifeUnit={form.shelfLifeUnit}
            storageCondition={form.storageCondition}
            finalVolumeMl={form.finalVolumeMl}
            instructions={form.instructions}
            sectionId={form.sectionId}
            isEditing={Boolean(editingEntry)}
            sections={sections}
            mySections={mySections}
            onNameChange={(val) => updateFormField("name", val)}
            onTypeChange={(val: SolutionType) => updateFormField("type", val)}
            onShelfLifeValueChange={(val) => updateFormField("shelfLifeValue", val)}
            onShelfLifeUnitChange={(val: ShelfLifeUnit) => updateFormField("shelfLifeUnit", val)}
            onStorageConditionChange={(val) => updateFormField("storageCondition", val)}
            onFinalVolumeMlChange={(val) => updateFormField("finalVolumeMl", val)}
            onInstructionsChange={(val) => updateFormField("instructions", val)}
            onSectionIdChange={(val) => updateFormField("sectionId", val)}
          />

          <SolutionPhSection
            phTarget={form.phTarget}
            phTolerance={form.phTolerance}
            phAdjustingEntryId={form.phAdjustingEntryId}
            availablePhAdjustingEntries={availablePhAdjustingEntries}
            onPhTargetChange={(val) => updateFormField("phTarget", val)}
            onPhToleranceChange={(val) => updateFormField("phTolerance", val)}
            onPhAdjustingEntryIdChange={(val) => updateFormField("phAdjustingEntryId", val)}
          />

          <SolutionComponentsSection
            components={form.components}
            availableMaterialEntries={availableMaterialEntries}
            onAddComponent={handleAddComponentRow}
            onRemoveComponent={handleRemoveComponentRow}
            onMoveComponent={handleMoveComponentRow}
            onComponentChange={handleComponentChange}
          />

          {form.type === "Titrant" && (
            <SolutionTitrantSection
              nominalStrength={form.nominalStrength}
              strengthUnit={form.strengthUnit}
              standardizationMode={form.standardizationMode}
              standardEntryId={form.standardEntryId}
              equivalenceMgPerMl={form.equivalenceMgPerMl}
              referenceSolutionId={form.referenceSolutionId}
              blankRequired={form.blankRequired}
              replicateCount={form.replicateCount}
              factorMin={form.factorMin}
              factorMax={form.factorMax}
              maxRsdPercent={form.maxRsdPercent}
              validityDays={form.validityDays}
              availableTitrantStandardEntries={availableTitrantStandardEntries}
              availableReferenceTitrants={availableReferenceTitrants}
              onNominalStrengthChange={(val) => updateFormField("nominalStrength", val)}
              onStrengthUnitChange={(val: TitrantStrengthUnit) => updateFormField("strengthUnit", val)}
              onStandardizationModeChange={(val: StandardizationMode) => updateFormField("standardizationMode", val)}
              onStandardEntryIdChange={(val) => updateFormField("standardEntryId", val)}
              onEquivalenceMgPerMlChange={(val) => updateFormField("equivalenceMgPerMl", val)}
              onReferenceSolutionIdChange={(val) => updateFormField("referenceSolutionId", val)}
              onBlankRequiredChange={(val) => updateFormField("blankRequired", val)}
              onReplicateCountChange={(val) => updateFormField("replicateCount", val)}
              onFactorMinChange={(val) => updateFormField("factorMin", val)}
              onFactorMaxChange={(val) => updateFormField("factorMax", val)}
              onMaxRsdPercentChange={(val) => updateFormField("maxRsdPercent", val)}
              onValidityDaysChange={(val) => updateFormField("validityDays", val)}
            />
          )}
        </Stack>
      </FloatingDialog>

      {/* Audit Reason Dialog for Editing Solution */}
      <ReasonDialog
        open={saveReasonDialogOpen}
        title="Reason for Modification"
        onClose={() => {
          if (!saving) setSaveReasonDialogOpen(false);
        }}
        onConfirm={handleConfirmSaveWithReason}
        confirmText="Confirm & Save"
        confirmColor="primary"
        loading={saving}
        loadingText="Saving..."
        reason={saveReason}
        onReasonChange={setSaveReason}
        label="Reason for Modification *"
        placeholder="Explain why this solution master recipe is being modified..."
        disabled={!saveReason.trim() || saving}
      >
        <Alert severity="info" sx={{ fontSize: 13 }}>
          GMP and ALCOA+ data integrity guidelines require a documented reason when modifying an
          existing solution master recipe.
        </Alert>

        {editingEntry && (
          <Box
            sx={{
              p: 1.5,
              bgcolor: "background.default",
              borderRadius: 1,
              border: "1px solid",
              borderColor: "divider"
            }}
          >
            <Typography variant="body2">
              Solution: <strong>{editingEntry.name}</strong>
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Type: {editingEntry.type} · Section: {resolveSectionDisplay(editingEntry.sectionId, editingEntry.sectionName)}
            </Typography>
          </Box>
        )}
      </ReasonDialog>
    </>
  );
}
