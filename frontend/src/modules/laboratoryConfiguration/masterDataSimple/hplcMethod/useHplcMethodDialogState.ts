import { useState, useEffect, useMemo, useCallback } from "react";
import { toast } from "sonner";
import {
  HplcMethodService,
  HplcMethodResponse,
  SaveHplcMethodRequest,
  HplcTechnique,
  HplcResultMode
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
      const tab = hplcErrorTab(key, form.technique);
      if (tab !== null) counts[tab] = (counts[tab] ?? 0) + 1;
    }
    return counts;
  }, [errors, form.technique]);

  const updateField = <K extends keyof HplcMethodFormState>(field: K, val: HplcMethodFormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: val }));
  };

  const handleTechniqueChange = (nextTech: HplcTechnique) => {
    if (editingId) return; // Read-only on edit
    setTabIndex(0);
    setForm((prev) => {
      if (nextTech === "Gc") {
        return {
          ...prev,
          technique: "Gc",
          resultMode: prev.resultMode ?? "Assay",
          columnDesignation: "",
          columnLength: "30",
          columnInternalDiameterMm: "0.32",
          particleSizeUm: "",
          filmThicknessUm: "1.8",
          columnTemperatureC: "",
          elutionMode: "Isocratic",
          equilibrationMin: "",
          flowRateMlPerMin: "2.0",
          detectorType: "Fid",
          injectionVolumeUl: "1",
          runTimeMin: "20",
          carrierGas: "Nitrogen",
          splitRatio: "",
          inletTemperatureC: "200",
          detectorTemperatureC: "250",
          headspaceEnabled: false,
          headspaceEquilibrationTemperatureC: "",
          headspaceEquilibrationMin: "",
          headspaceTransferLineTemperatureC: "",
          sampleSolutionVolumeMl: "",
          ovenSteps: [{ rateCPerMin: "", temperatureC: "40", holdMin: "5" }],
          mobilePhases: [],
          gradientSteps: [],
          analytes: prev.analytes.map((a) => ({
            ...a,
            wavelengthNm: "",
            standardConcentrationUgPerMl: ""
          }))
        };
      } else {
        return {
          ...prev,
          technique: "Hplc",
          resultMode: "Assay",
          columnDesignation: "",
          columnLength: "150",
          columnInternalDiameterMm: "4.6",
          particleSizeUm: "5",
          filmThicknessUm: "",
          columnTemperatureC: "25",
          elutionMode: "Isocratic",
          equilibrationMin: "",
          flowRateMlPerMin: "1.0",
          detectorType: "UV",
          injectionVolumeUl: "10",
          runTimeMin: "15",
          carrierGas: "",
          splitRatio: "",
          inletTemperatureC: "",
          detectorTemperatureC: "",
          headspaceEnabled: false,
          headspaceEquilibrationTemperatureC: "",
          headspaceEquilibrationMin: "",
          headspaceTransferLineTemperatureC: "",
          sampleSolutionVolumeMl: "",
          ovenSteps: [],
          mobilePhases: [{ channel: "A", solutionMasterId: "", ratioPercent: "100" }],
          gradientSteps: [
            { timeMin: "0", percentA: "100", percentB: "0", percentC: "0", percentD: "0" },
            { timeMin: "10", percentA: "50", percentB: "50", percentC: "0", percentD: "0" }
          ],
          analytes: prev.analytes.map((a) => ({
            ...a,
            wavelengthNm: a.wavelengthNm || "254",
            standardConcentrationUgPerMl: ""
          }))
        };
      }
    });
  };

  const handleResultModeChange = (nextMode: HplcResultMode) => {
    if (editingId) return; // Read-only on edit
    setForm((prev) => {
      const isResidual = nextMode === "ResidualSolvents";
      return {
        ...prev,
        resultMode: nextMode,
        sampleSolutionVolumeMl: isResidual ? (prev.sampleSolutionVolumeMl || "5") : "",
        analytes: prev.analytes.map((a) => ({
          ...a,
          theoreticalWeightStdMg: isResidual ? "0" : a.theoreticalWeightStdMg || "50",
          theoreticalWeightTestMg: isResidual ? "0" : a.theoreticalWeightTestMg || "50",
          standardDilution: isResidual ? "" : a.standardDilution,
          standardConcentrationUgPerMl: isResidual ? a.standardConcentrationUgPerMl || "100" : ""
        }))
      };
    });
  };

  const executeCreate = async (payload: SaveHplcMethodRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      await HplcMethodService.create(payload);
      const label = payload.technique === "Gc" ? "GC" : "HPLC";
      toast.success(`${label} method "${payload.name}" created.`);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      const label = payload.technique === "Gc" ? "GC" : "HPLC";
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? `Could not create ${label} method.`);
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
      const hasGeneralError = keys.some((key) => hplcErrorTab(key, form.technique) === null);
      const tabs = keys
        .map((k) => hplcErrorTab(k, form.technique))
        .filter((tab): tab is number => tab !== null);
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
      const label = finalPayload.technique === "Gc" ? "GC" : "HPLC";
      toast.success(`${label} method "${finalPayload.name}" updated.`);
      setReasonDialogOpen(false);
      setPendingPayload(null);
      onClose();
      onSuccess();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      const label = pendingPayload.technique === "Gc" ? "GC" : "HPLC";
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? `Could not update ${label} method.`);
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
    handleTechniqueChange,
    handleResultModeChange,
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
