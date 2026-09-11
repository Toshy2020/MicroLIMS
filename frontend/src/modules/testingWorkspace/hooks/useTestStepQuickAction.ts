import { useState, useEffect, useCallback, useMemo } from "react";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { lookupCache, ReleasedMediaItem, IncubatorEquipmentItem } from "../../../services/lookupCache";
import { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const STEP_CACHE_TTL_MS = 60 * 1000; // 1 minute
const stepCache = new Map<number, { data: CurrentStepResponse; timestamp: number }>();

export function invalidateStepCache(testOrderId?: number) {
  if (testOrderId != null) {
    stepCache.delete(testOrderId);
  } else {
    stepCache.clear();
  }
}

interface UseTestStepQuickActionOptions {
  testOrderId: number;
  testCode: string;
  expanded: boolean;
  onSuccess?: () => void;
}

export function useTestStepQuickAction({
  testOrderId,
  testCode,
  expanded,
  onSuccess
}: UseTestStepQuickActionOptions) {
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [currentStepData, setCurrentStepData] = useState<CurrentStepResponse | null>(() => {
    const cached = stepCache.get(testOrderId);
    if (cached && Date.now() - cached.timestamp < STEP_CACHE_TTL_MS) {
      return cached.data;
    }
    return null;
  });

  const [releasedMedia, setReleasedMedia] = useState<ReleasedMediaItem[]>([]);
  const [incubators, setIncubators] = useState<IncubatorEquipmentItem[]>([]);

  const [selectedMediaId, setSelectedMediaId] = useState<number | "">("");
  const [selectedIncubatorId, setSelectedIncubatorId] = useState<number | "">("");

  // Load step details and lookups when expanded
  const loadData = useCallback(async (forceRefresh = false) => {
    setLoading(true);
    setError(null);
    try {
      const cached = stepCache.get(testOrderId);
      let stepDataPromise: Promise<CurrentStepResponse>;
      if (!forceRefresh && cached && Date.now() - cached.timestamp < STEP_CACHE_TTL_MS) {
        stepDataPromise = Promise.resolve(cached.data);
      } else {
        stepDataPromise = TestWorkflowService.getCurrentStep(testOrderId).then((data) => {
          stepCache.set(testOrderId, { data, timestamp: Date.now() });
          return data;
        });
      }

      const [stepData, mediaList, incubatorList] = await Promise.all([
        stepDataPromise,
        lookupCache.getReleasedMedia(forceRefresh),
        lookupCache.getIncubators(forceRefresh)
      ]);

      setCurrentStepData(stepData);
      setReleasedMedia(mediaList);
      setIncubators(incubatorList);
    } catch (err: any) {
      setError(err?.response?.data?.message || `Could not load step details for ${testCode}.`);
    } finally {
      setLoading(false);
    }
  }, [testOrderId, testCode]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const step: TestWorkflowStepDto | null = currentStepData?.step ?? null;

  // Derive permitted media
  const permittedMaterialIds = useMemo(() => {
    return new Set((step?.stepMedia ?? []).map((m) => m.materialId));
  }, [step]);

  const matchingMedia = useMemo(() => {
    const now = new Date();
    return releasedMedia.filter((m) => {
      const isAllowedMaterial = permittedMaterialIds.has(m.materialId);
      const isReleased = m.isReleasedForUse && m.status === "Active";
      const notExpired = !m.expiryDate || new Date(m.expiryDate) > now;
      return isAllowedMaterial && isReleased && notExpired;
    });
  }, [releasedMedia, permittedMaterialIds]);

  const permittedMaterialNames = useMemo(() => {
    return (step?.stepMedia ?? []).map((m) => m.materialName).filter(Boolean).join(" or ");
  }, [step]);

  // Derive temperature and duration constraints based on selected media or step media
  const selectedMediaLot = useMemo(() => {
    return releasedMedia.find((m) => m.id === selectedMediaId);
  }, [releasedMedia, selectedMediaId]);

  const selectedStepMedium = useMemo(() => {
    return (step?.stepMedia ?? []).find((m) => m.materialId === selectedMediaLot?.materialId);
  }, [step, selectedMediaLot]);

  const activeMedium = selectedStepMedium ?? (step?.stepMedia?.length === 1 ? step.stepMedia[0] : null);

  const stage1TempMin = activeMedium?.tempMin ?? (step?.stepMedia && step.stepMedia.length > 0 ? Math.min(...step.stepMedia.map((m) => m.tempMin)) : (step?.temperatureMin ?? 0));
  const stage1TempMax = activeMedium?.tempMax ?? (step?.stepMedia && step.stepMedia.length > 0 ? Math.max(...step.stepMedia.map((m) => m.tempMax)) : (step?.temperatureMax ?? 0));
  const stage1IncMinHours = activeMedium?.incubationMinHours ?? (step?.stepMedia && step.stepMedia.length > 0 ? Math.min(...step.stepMedia.map((m) => m.incubationMinHours ?? 0)) : (step?.incubationMinHours ?? 0));
  const stage1IncMaxHours = activeMedium?.incubationMaxHours ?? (step?.stepMedia && step.stepMedia.length > 0 ? Math.max(...step.stepMedia.map((m) => m.incubationMaxHours ?? 0)) : (step?.incubationMaxHours ?? 0));

  // Eligible incubators matching temperature range
  const matchingIncubators = useMemo(() => {
    return incubators.filter((i) => {
      if (i.setPointTemperature == null) return false;
      if (i.calibrationStatus && i.calibrationStatus.toLowerCase() === "expired") return false;
      return i.setPointTemperature >= stage1TempMin && i.setPointTemperature <= stage1TempMax;
    });
  }, [incubators, stage1TempMin, stage1TempMax]);

  // Active Incubation resolution (inspects current active step, shared TSB, or incubation lock)
  const activeIncubation = useMemo(() => {
    // 1. Direct incubationLock from currentStepData
    if (currentStepData?.incubationLock?.incubationEndUtc) {
      const endUtc = currentStepData.incubationLock.incubationEndUtc;
      const remSec = currentStepData.incubationLock.remainingSeconds ??
        Math.max(0, Math.floor((new Date(endUtc).getTime() - Date.now()) / 1000));
      const matchedPrev = currentStepData.previousSteps?.find(
        (p) => p.stepName === step?.stepName
      );
      return {
        incubationEndUtc: endUtc,
        remainingSeconds: remSec,
        isLocked: currentStepData.incubationLock.isLocked,
        mediaName: matchedPrev?.mediaName || matchedPrev?.lotNumber,
        incubatorName: matchedPrev?.incubatorName
      };
    }

    // 2. Shared TSB summary (for pathogen session test orders)
    if (
      step &&
      (step.stepType === "BrothEnrichment" || step.stepName.toLowerCase().includes("tsb")) &&
      currentStepData?.sharedTsbSummary
    ) {
      const tsb = currentStepData.sharedTsbSummary;
      const endUtc = tsb.incubationEndUtc || tsb.minReadyAt;
      const remSec = endUtc ? Math.max(0, Math.floor((new Date(endUtc).getTime() - Date.now()) / 1000)) : 0;
      return {
        incubationEndUtc: endUtc,
        remainingSeconds: remSec,
        isLocked: !tsb.isCompleted,
        mediaName: tsb.mediaLotNumber,
        incubatorName: tsb.incubatorCode
      };
    }

    // 3. Search in currentStepData.previousSteps for an incubation row matching the ACTIVE step that is still incubating
    if (step && currentStepData?.previousSteps) {
      const activeStepInc = currentStepData.previousSteps.find(
        (p: any) => p.stepName === step.stepName && p.status === "Incubating"
      );
      if (activeStepInc) {
        const endUtc = activeStepInc.incubationEndUtc;
        const remSec = endUtc ? Math.max(0, Math.floor((new Date(endUtc).getTime() - Date.now()) / 1000)) : 0;
        return {
          incubationEndUtc: endUtc,
          remainingSeconds: remSec,
          isLocked: true,
          mediaName: activeStepInc.mediaName || activeStepInc.lotNumber,
          incubatorName: activeStepInc.incubatorName
        };
      }
    }

    return null;
  }, [step, currentStepData]);

  const openIncubationRow = useMemo(() => {
    if (!step) return null;
    return (currentStepData?.previousSteps ?? []).find(
      (p: any) => p.stepName === step.stepName && p.status === "Incubating"
    );
  }, [step, currentStepData]);

  const isIncubating = Boolean(
    currentStepData?.incubationLock != null ||
    openIncubationRow ||
    activeIncubation != null
  );

  const remainingSeconds = activeIncubation?.remainingSeconds ?? 0;
  const incubationEndUtc = activeIncubation?.incubationEndUtc;

  // Step requires media + incubator setup
  const requiresMediaSetup = Boolean(
    step &&
    !isIncubating &&
    !currentStepData?.allStepsComplete &&
    (step.stepType === "PlateCount" ||
      step.stepType === "BrothEnrichment" ||
      step.stepType === "SelectiveBroth" ||
      step.stepType === "SelectivePlating")
  );

  // Dynamic CTA label
  const ctaLabel = useMemo(() => {
    if (!step) return "Start Incubation";
    if (step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth") return "Start Broth Incubation";
    if (step.stepType === "SelectivePlating") return "Start Plating Incubation";
    if (step.stepType === "PlateCount") return "Start Incubation";
    return "Start Incubation";
  }, [step]);

  // Execute start incubation
  const handleStartIncubation = async () => {
    if (!step || !selectedMediaId || !selectedIncubatorId) {
      setError("Please select both a media lot and an incubator.");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      let startedIncubation: any = null;
      if (step.stepType === "SelectivePlating") {
        startedIncubation = await TestWorkflowService.startSelectivePlatingIncubation(
          testOrderId,
          step.stepName,
          Number(selectedMediaId),
          Number(selectedIncubatorId)
        );
      } else {
        startedIncubation = await TestWorkflowService.selectMedia(
          testOrderId,
          step.stepName,
          Number(selectedMediaId),
          Number(selectedIncubatorId)
        );
      }
      invalidateStepCache(testOrderId);
      setSelectedMediaId("");
      setSelectedIncubatorId("");
      // Immediately reload data from server so currentStepData has the active incubation & lock
      await loadData(true);
      if (onSuccess) {
        onSuccess();
      }
      return startedIncubation;
    } catch (err: any) {
      setError(err?.response?.data?.message || "Failed to start incubation.");
      // Rethrow. Swallowing this returned undefined instead of failing, so a
      // caller that had already flipped to an optimistic "incubating" state
      // never learned the server had refused and left a countdown on screen
      // for an incubation that does not exist. The inline message above is for
      // this panel; the throw is what lets the caller undo what it showed.
      throw err;
    } finally {
      setSubmitting(false);
    }
  };

  return {
    loading,
    submitting,
    error,
    currentStepData,
    step,
    requiresMediaSetup,
    matchingMedia,
    permittedMaterialNames,
    matchingIncubators,
    selectedMediaId,
    setSelectedMediaId,
    selectedIncubatorId,
    setSelectedIncubatorId,
    stage1TempMin,
    stage1TempMax,
    stage1IncMinHours,
    stage1IncMaxHours,
    isIncubating,
    activeIncubation,
    remainingSeconds,
    incubationEndUtc,
    openIncubationRow,
    ctaLabel,
    handleStartIncubation,
    reload: () => loadData(true)
  };
}
