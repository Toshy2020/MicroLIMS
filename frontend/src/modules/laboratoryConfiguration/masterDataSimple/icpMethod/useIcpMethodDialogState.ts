import { useState, useEffect, useMemo, useCallback } from "react";
import { toast } from "sonner";
import {
  IcpMethodService,
  IcpMethodResponse,
  SaveIcpMethodRequest
} from "../services/IcpMethodService";
import type { MaterialMasterEntry } from "../services/MaterialMasterService";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import {
  IcpMethodFormState,
  IcpElementRowState,
  createInitialIcpMethodFormState,
  icpMethodEntityToFormState,
  formToSaveRequest,
  getAvailableReferenceStandards
} from "./icpMethodForm";
import {
  IcpMethodErrors,
  validateIcpMethodForm,
  ICP_FORM_ERROR_PREFIX,
  icpErrorTab
} from "./icpMethodValidation";

export interface UseIcpMethodDialogStateProps {
  open: boolean;
  editingId: number | null;
  materialMasters: MaterialMasterEntry[];
  mySections: LaboratorySection[];
  onClose: () => void;
  onSuccess: () => void;
}

export function useIcpMethodDialogState({
  open,
  editingId,
  materialMasters,
  mySections,
  onClose,
  onSuccess
}: UseIcpMethodDialogStateProps) {
  const [tabIndex, setTabIndex] = useState(0);
  const [loadingMethod, setLoadingMethod] = useState(false);
  const [loadedEntity, setLoadedEntity] = useState<IcpMethodResponse | null>(null);

  const defaultSectionId = mySections.length === 1 ? mySections[0].sectionId : "";
  const [form, setForm] = useState<IcpMethodFormState>(() =>
    createInitialIcpMethodFormState(defaultSectionId)
  );

  const [dialogError, setDialogError] = useState<string | null>(null);
  const [showErrors, setShowErrors] = useState(false);
  const [scrollTick, setScrollTick] = useState(0);
  const [saving, setSaving] = useState(false);
  const [reasonDialogOpen, setReasonDialogOpen] = useState(false);
  const [editReason, setEditReason] = useState("");
  const [pendingPayload, setPendingPayload] = useState<SaveIcpMethodRequest | null>(null);

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
      IcpMethodService.getById(editingId)
        .then((entity) => {
          setLoadedEntity(entity);
          setForm(icpMethodEntityToFormState(entity));
        })
        .catch((err: unknown) => {
          const errObj = err as { response?: { data?: { message?: string } }; message?: string };
          setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not load ICP method.");
        })
        .finally(() => {
          setLoadingMethod(false);
        });
    } else {
      setLoadedEntity(null);
      setForm(createInitialIcpMethodFormState(defaultSectionId));
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

  const availableReferenceStandards = useMemo(
    () =>
      getAvailableReferenceStandards(currentSectionId, materialMasters, [
        loadedEntity?.calibrationStandardEntryId,
        loadedEntity?.icvStandardEntryId
      ]),
    [currentSectionId, materialMasters, loadedEntity?.calibrationStandardEntryId, loadedEntity?.icvStandardEntryId]
  );

  const validationErrors = useMemo<IcpMethodErrors>(
    () =>
      validateIcpMethodForm(form, {
        isEditing: Boolean(editingId),
        hasMultipleSections: mySections.length > 1
      }),
    [form, editingId, mySections.length]
  );

  const errors = useMemo<IcpMethodErrors>(
    () => (showErrors ? validationErrors : {}),
    [showErrors, validationErrors]
  );

  const formErrorMessages = useMemo(
    () =>
      Object.entries(errors)
        .filter(([key]) => key.startsWith(ICP_FORM_ERROR_PREFIX))
        .map(([, message]) => message),
    [errors]
  );

  const tabErrorCounts = useMemo(() => {
    const counts: Record<number, number> = {};
    for (const key of Object.keys(errors)) {
      const tab = icpErrorTab(key);
      if (tab !== null) counts[tab] = (counts[tab] ?? 0) + 1;
    }
    return counts;
  }, [errors]);

  const updateField = <K extends keyof IcpMethodFormState>(field: K, val: IcpMethodFormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: val }));
  };

  const addElementRow = () => {
    setForm((prev) => ({
      ...prev,
      elements: [
        ...prev.elements,
        {
          symbol: "",
          wavelengthNm: "",
          view: "Axial",
          conversionFactor: "1"
        }
      ]
    }));
  };

  const removeElementRow = (idx: number) => {
    setForm((prev) => ({
      ...prev,
      elements: prev.elements.filter((_, i) => i !== idx)
    }));
  };

  const moveElementRow = (idx: number, direction: "up" | "down") => {
    setForm((prev) => {
      const targetIdx = direction === "up" ? idx - 1 : idx + 1;
      if (targetIdx < 0 || targetIdx >= prev.elements.length) return prev;
      const copy = [...prev.elements];
      const temp = copy[idx];
      copy[idx] = copy[targetIdx];
      copy[targetIdx] = temp;
      return { ...prev, elements: copy };
    });
  };

  const updateElementRow = <K extends keyof IcpElementRowState>(
    idx: number,
    field: K,
    val: IcpElementRowState[K]
  ) => {
    setForm((prev) => {
      const copy = [...prev.elements];
      copy[idx] = { ...copy[idx], [field]: val };
      return { ...prev, elements: copy };
    });
  };

  const executeCreate = async (payload: SaveIcpMethodRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      await IcpMethodService.create(payload);
      toast.success(`ICP method "${payload.name}" created.`);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not create ICP method.");
    } finally {
      setSaving(false);
    }
  };

  const handleInitiateSave = () => {
    setDialogError(null);
    const keys = Object.keys(validationErrors);
    if (keys.length > 0) {
      setShowErrors(true);
      const tabs = keys
        .map((k) => icpErrorTab(k))
        .filter((tab): tab is number => tab !== null);
      if (tabs.length > 0) {
        setTabIndex(Math.min(...tabs));
      }
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
      const finalPayload: SaveIcpMethodRequest = { ...pendingPayload, reason: trimmedReason };
      await IcpMethodService.update(editingId, finalPayload, loadedEntity?.version);
      toast.success(`ICP method "${finalPayload.name}" updated.`);
      setReasonDialogOpen(false);
      setPendingPayload(null);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not update ICP method.");
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
    addElementRow,
    removeElementRow,
    moveElementRow,
    updateElementRow,
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
    availableReferenceStandards,
    handleInitiateSave,
    handleConfirmEditWithReason
  };
}
