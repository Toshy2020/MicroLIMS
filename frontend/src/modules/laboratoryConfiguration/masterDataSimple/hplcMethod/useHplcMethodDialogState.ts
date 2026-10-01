import { useState, useEffect, useMemo, useCallback } from "react";
import { toast } from "sonner";
import {
  HplcMethodService,
  HplcMethodResponse,
  SaveHplcMethodRequest
} from "../services/HplcMethodService";
import { SolutionMaster } from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import {
  HplcMethodErrors,
  HplcMethodFormState,
  createInitialHplcMethodFormState,
  hplcMethodEntityToFormState,
  validateHplcMethodForm,
  formToSaveRequest,
  getAvailableDiluents,
  getAvailableMobilePhases,
  getAvailableReferenceStandards
} from "./hplcMethodForm";
import { HPLC_FORM_ERROR_PREFIX, hplcErrorTab } from "./hplcMethodValidation";

export interface UseHplcMethodDialogStateProps {
  open: boolean;
  editingId: number | null;
  solutions: SolutionMaster[];
  materialMasters: MaterialMasterEntry[];
  mySections: LaboratorySection[];
  onClose: () => void;
  onSuccess: () => void;
}

export function useHplcMethodDialogState({
  open,
  editingId,
  solutions,
  materialMasters,
  mySections,
  onClose,
  onSuccess
}: UseHplcMethodDialogStateProps) {
  const [tabIndex, setTabIndex] = useState(0);
  const [loadingMethod, setLoadingMethod] = useState(false);
  const [loadedEntity, setLoadedEntity] = useState<HplcMethodResponse | null>(null);

  const defaultSectionId = mySections.length === 1 ? mySections[0].sectionId : "";
  const [form, setForm] = useState<HplcMethodFormState>(() =>
    createInitialHplcMethodFormState(defaultSectionId)
  );

  const [dialogError, setDialogError] = useState<string | null>(null);
  // Field errors appear after the first failed submit and then follow the form live.
  const [showErrors, setShowErrors] = useState(false);
  const [scrollTick, setScrollTick] = useState(0);
  const [saving, setSaving] = useState(false);
  const [reasonDialogOpen, setReasonDialogOpen] = useState(false);
  const [editReason, setEditReason] = useState("");
  const [pendingPayload, setPendingPayload] = useState<SaveHplcMethodRequest | null>(null);

  const initForm = useCallback(() => {
    setTabIndex(0);
    setDialogError(null);
    setShowErrors(false);
    setSaving(false);
    setReasonDialogOpen(false);
    setEditReason("");
    setPendingPayload(null);

    if (editingId) {
      setLoadingMethod(true);
      HplcMethodService.getById(editingId)
        .then((entity) => {
          setLoadedEntity(entity);
          setForm(hplcMethodEntityToFormState(entity));
        })
        .catch((err: unknown) => {
          const errObj = err as { response?: { data?: { message?: string } }; message?: string };
          setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not load method.");
        })
        .finally(() => {
          setLoadingMethod(false);
        });
    } else {
      setLoadedEntity(null);
      setForm(createInitialHplcMethodFormState(defaultSectionId));
    }
  }, [editingId, defaultSectionId]);

  useEffect(() => {
    if (open) {
      initForm();
    }
  }, [open, initForm]);

  const currentSectionId = useMemo(() => {
    if (loadedEntity) return loadedEntity.sectionId;
    if (form.sectionId) return Number(form.sectionId);
    if (mySections.length === 1) return mySections[0].sectionId;
    return null;
  }, [loadedEntity, form.sectionId, mySections]);

  const availableDiluents = useMemo(
    () => getAvailableDiluents(currentSectionId, solutions, loadedEntity?.diluentSolutionId),
    [currentSectionId, solutions, loadedEntity?.diluentSolutionId]
  );

  const availableMobilePhases = useMemo(
    () => getAvailableMobilePhases(currentSectionId, solutions, loadedEntity?.mobilePhases),
    [currentSectionId, solutions, loadedEntity?.mobilePhases]
  );

  const availableReferenceStandards = useMemo(
    () => getAvailableReferenceStandards(currentSectionId, materialMasters, loadedEntity?.analytes),
    [currentSectionId, materialMasters, loadedEntity?.analytes]
  );

  const validationErrors = useMemo<HplcMethodErrors>(
    () =>
      validateHplcMethodForm(form, {
        isEditing: Boolean(editingId),
        hasMultipleSections: mySections.length > 1
      }),
    [form, editingId, mySections.length]
  );
  const errors = useMemo<HplcMethodErrors>(
    () => (showErrors ? validationErrors : {}),
    [showErrors, validationErrors]
  );

  // Rules spanning a whole tab (e.g. at least one analyte) have no field and go to the top alert.
  const formErrorMessages = useMemo(
    () =>
      Object.entries(errors)
        .filter(([key]) => key.startsWith(HPLC_FORM_ERROR_PREFIX))
        .map(([, message]) => message),
    [errors]
  );

  const tabErrorCounts = useMemo(() => {
    const counts: Record<number, number> = {};
    for (const key of Object.keys(errors)) {
      const tab = hplcErrorTab(key);
      if (tab !== null) counts[tab] = (counts[tab] ?? 0) + 1;
    }
    return counts;
  }, [errors]);

  const updateField = <K extends keyof HplcMethodFormState>(field: K, val: HplcMethodFormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: val }));
  };

  const executeCreate = async (payload: SaveHplcMethodRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      await HplcMethodService.create(payload);
      toast.success(`HPLC method "${payload.name}" created.`);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not create HPLC method.");
    } finally {
      setSaving(false);
    }
  };

  const handleInitiateSave = () => {
    setDialogError(null);
    const keys = Object.keys(validationErrors);
    if (keys.length > 0) {
      setShowErrors(true);
      // The general header is always visible; otherwise jump to the first tab with an error.
      const hasGeneralError = keys.some((key) => hplcErrorTab(key) === null);
      const tabs = keys.map(hplcErrorTab).filter((tab): tab is number => tab !== null);
      if (!hasGeneralError && tabs.length > 0) setTabIndex(Math.min(...tabs));
      setScrollTick((tick) => tick + 1);
      return;
    }

    const payload = formToSaveRequest(form, currentSectionId);
    if (editingId) {
      setPendingPayload(payload);
      setEditReason("");
      setReasonDialogOpen(true);
    } else {
      executeCreate(payload);
    }
  };

  const handleConfirmEditWithReason = async () => {
    if (!editingId || !pendingPayload) return;
    const trimmedReason = editReason.trim();
    if (!trimmedReason) return;

    setSaving(true);
    try {
      const finalPayload: SaveHplcMethodRequest = { ...pendingPayload, reason: trimmedReason };
      await HplcMethodService.update(editingId, finalPayload, loadedEntity?.version);
      toast.success(`HPLC method "${finalPayload.name}" updated.`);
      setReasonDialogOpen(false);
      setPendingPayload(null);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not update HPLC method.");
      setReasonDialogOpen(false);
    } finally {
      setSaving(false);
    }
  };

  return {
    tabIndex,
    setTabIndex,
    loadingMethod,
    loadedEntity,
    form,
    updateField,
    dialogError,
    setDialogError,
    errors,
    formErrorMessages,
    tabErrorCounts,
    scrollTick,
    saving,
    reasonDialogOpen,
    setReasonDialogOpen,
    editReason,
    setEditReason,
    availableDiluents,
    availableMobilePhases,
    availableReferenceStandards,
    handleInitiateSave,
    handleConfirmEditWithReason
  };
}
