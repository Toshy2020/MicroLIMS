import { useEffect, useRef, useState, type ReactNode } from "react";
import axios from "axios";
import { Box, Typography, TextField, Button, Stack, Alert, IconButton, useTheme } from "@mui/material";
import { toast } from "sonner";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import EditIcon from "@mui/icons-material/Edit";
import { StatusBadge } from "../../components/StatusBadge";
import { statusTone } from "../../theme/statusTokens";
import { errorsByRow } from "../../utils/fieldErrors";
import { lookupCache } from "../../services/lookupCache";
import { entryDefaultsService } from "../../services/entryDefaultsService";
import { useLastChoiceDefault } from "../../components/entry/LastChoiceField";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
import { LocationResultGridDialog } from "./LocationResultGridDialog";
import { PathogenLocationResultGridDialog } from "./PathogenLocationResultGridDialog";
import { WaterLocationResultGridDialog } from "./WaterLocationResultGridDialog";
import { PathogenStepDialog } from "./PathogenStepDialog";
import { StepChainStrip } from "./components/StepChainStrip";
import { OpenSopAction } from "./components/OpenSopAction";
import { LocationResultsPanel } from "./LocationResultsPanel";
import { MeasurementPanel } from "./MeasurementPanel";
import { GravimetricPanel } from "./GravimetricPanel";
import { TitrationPanel } from "./TitrationPanel";
import { UvPanel } from "./UvPanel";
import { QualitativePanel } from "./QualitativePanel";
import { DissolutionPanel } from "./DissolutionPanel";
import { DisintegrationPanel } from "./DisintegrationPanel";
import { WeightVariationPanel } from "./WeightVariationPanel";
import { OutsourcedPanel, OutsourceTestAction } from "./OutsourcedPanel";
import {
  StepActionBar, StepDetailList, StepHeading, StepChipTone, IncubationTimeline, FixedMediumRow,
  MediaLotIncubatorFields, IncubatorField
} from "./pathogenSteps/StepLayout";
import { IncubationWaitingView } from "./pathogenSteps/IncubationWaitingView";
import { BenchDialog } from "../../components/bench/BenchDialog";
import { DraftRestoreNotice } from "../../components/bench/DraftRestoreNotice";
import { NumericEntryField } from "../../components/bench/NumericEntryField";
import { useBenchDraft } from "../../hooks/useBenchDraft";
import { useBenchMode } from "../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, serverMessage, useKeptOffline } from "./benchSave";
import type { IncubatorEquipmentItem, ReleasedMediaItem } from "../../services/lookupCache";
import type { CountPreviewResult, PreviousStepDetail, TestWorkflowStepDto } from "./types/testWorkflowTypes";

type StepMediaRow = TestWorkflowStepDto["stepMedia"][number];
type StepStage = NonNullable<TestWorkflowStepDto["incubationStages"]>[number];
type OpenIncubationRow = PreviousStepDetail & { stageNumber?: number };

// The shared incubator picker's option shape, from the lookup cache's items.
const incubatorOptions = (items: IncubatorEquipmentItem[]) =>
  items.map((i) => ({ id: i.id, name: i.name, code: i.code, setTemperature: i.setPointTemperature ?? 0 }));

const COUNT_STEP_HELP =
  "Plate the sample on the permitted medium and start incubation. Once the minimum incubation has passed, count the colonies on each plate; the result is the average count multiplied by the dilution factor.";

// What a plate-count draft may hold. Only these fields are ever written to the tablet;
// the count result carries no password, signature or token.
interface CountDraftValues {
  readings: string[];
  dilutionFactor: string;
  dilutionFactorOverrideNote: string;
}

const pickCountDraft = (v: CountDraftValues): Partial<CountDraftValues> => ({
  readings: v.readings,
  dilutionFactor: v.dilutionFactor,
  dilutionFactorOverrideNote: v.dilutionFactorOverrideNote
});

interface BenchPlateRowsProps {
  readings: string[];
  errors?: Record<number, string>;
  isNonNumeric: (value: string) => boolean;
  onChange: (index: number, value: string) => void;
  onRemove: (index: number) => void;
}

// One plate per row for a tablet: a whole-number field filled from the keypad, and
// buttons for the two non-numeric readings the existing screen already accepts.
function BenchPlateRows({ readings, errors, isNonNumeric, onChange, onRemove }: BenchPlateRowsProps) {
  return (
    <Stack spacing={1}>
      {readings.map((r, i) => (
        <Box key={i} sx={{ display: "flex", alignItems: "flex-start", gap: 1, flexWrap: "wrap" }}>
          <Box sx={{ flex: "1 1 160px", minWidth: 0 }}>
            {isNonNumeric(r) ? (
              <TextField
                label={`Plate ${i + 1}`}
                value={r}
                fullWidth
                error={Boolean(errors?.[i])}
                helperText={errors?.[i] ?? "Non-numeric: flagged for reviewer decision"}
                slotProps={{
                  htmlInput: { readOnly: true },
                  formHelperText: errors?.[i] ? { role: "alert" } : undefined
                }}
              />
            ) : (
              <NumericEntryField label={`Plate ${i + 1}`} value={r} error={errors?.[i]} onChange={(v) => onChange(i, v)} kind="count" />
            )}
          </Box>
          {isNonNumeric(r) ? (
            <Button variant="outlined" aria-label={`Enter a count for plate ${i + 1}`} onClick={() => onChange(i, "")}>Enter a count</Button>
          ) : (
            <>
              <Button variant="outlined" aria-label={`Mark plate ${i + 1} as TNTC`} onClick={() => onChange(i, "TNTC")}>TNTC</Button>
              <Button variant="outlined" aria-label={`Mark plate ${i + 1} as uncountable`} onClick={() => onChange(i, "uncountable")}>Uncountable</Button>
            </>
          )}
          <IconButton aria-label={`Remove plate ${i + 1}`} onClick={() => onRemove(i)} disabled={readings.length <= 1}>
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>
      ))}
    </Stack>
  );
}

// What the server answers to a recorded count step; only the fields this screen reads.
interface CountOutcome {
  allStepsComplete?: boolean;
  isDefinitive?: boolean;
  outcomeSummary?: string;
  average?: number | null;
  calculatedResult?: number | string | null;
  status?: string | null;
  finalResult?: string | null;
}

function isAbortError(err: unknown): boolean {
  if (axios.isCancel(err)) return true;
  return typeof err === "object" && err !== null && (err as { name?: unknown }).name === "AbortError";
}

interface Props {
  testOrderId: number;
  testCode: string;
  category: string;
  displayName: string;
  itemId?: number | null;
  sampleId?: number | null;
  // The order's status and external lab, from the workspace list.
  status?: string | null;
  outsourcedToLab?: string | null;
  onClose?: () => void;
}

type Phase = "loading" | "select-media" | "awaiting-result" | "transfer-stage-2" | "enter-result" | "step-complete" | "all-complete";

// Universal workflow dialog for any TestDefinition with a configured
// step template (WorkflowType + TestWorkflowStep) - reads
// GET current-step to find out what phase to render, instead of
// routing by test code the way the old Pathogen/CountTest dialogs did.
// Nothing here is specific to any test - the step template drives the
// media-class filter and locked temperature/duration.
//
// CountTest keeps its own select-media/awaiting-result/enter-result
// phases below. Any other workflow (the five-stage pathogen chain) is
// handed off entirely to PathogenStepDialog, except for EM/AfterCleaning
// samples, which still incubate through this component's phases and
// only hand off to LocationResultGridDialog/PathogenLocationResultGrid-
// Dialog for their per-location batch result entry, exactly as today.
export function TestWorkflowDialog(props: Props) {
  // Samples with no item (water, environmental monitoring, after cleaning) get no Open SOP action.
  return (
    <>
      {props.itemId != null && <OpenSopAction itemId={props.itemId} />}
      <TestWorkflowDialogBody {...props} />
    </>
  );
}

function TestWorkflowDialogBody({ testOrderId, testCode, category, displayName, itemId, sampleId, status, outsourcedToLab, onClose }: Props) {
  const theme = useTheme();
  const isEmOrAfterCleaning = category === "EnvironmentalMonitoring" || category === "AfterCleaning";
  const [phase, setPhase] = useState<Phase>("loading");
  const [current, setCurrent] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showLocationGrid, setShowLocationGrid] = useState(false);
  const [outsourcedLab, setOutsourcedLab] = useState<string | null>(outsourcedToLab ?? null);

  const [mediaId, setMediaId] = useState<number | "">("");
  const [incubatorId, setIncubatorId] = useState<number | "">("");
  const [stage2IncubatorId, setStage2IncubatorId] = useState<number | "">("");
  const [releasedMedia, setReleasedMedia] = useState<ReleasedMediaItem[]>([]);
  const [incubators, setIncubators] = useState<IncubatorEquipmentItem[]>([]);
  const lotLastChoice = useLastChoiceDefault<number>("lot");
  const incubatorLastChoice = useLastChoiceDefault<number>("equipment");
  const stage2IncubatorLastChoice = useLastChoiceDefault<number>("equipment");

  const [readings, setReadings] = useState<string[]>(["", ""]);
  const [dilutionFactor, setDilutionFactor] = useState("1");
  const [dilutionFactorOverriding, setDilutionFactorOverriding] = useState(false);
  const [dilutionFactorOverrideNote, setDilutionFactorOverrideNote] = useState("");
  const [preview, setPreview] = useState<CountPreviewResult | null>(null);
  const [previewError, setPreviewError] = useState(false);
  const [readingErrors, setReadingErrors] = useState<Record<number, string>>({});

  const [lastOutcome, setLastOutcome] = useState<CountOutcome | null>(null);

  const load = async () => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId);
      setCurrent(data);
      setMediaId(""); setIncubatorId(""); setStage2IncubatorId("");
      lotLastChoice.reset();
      incubatorLastChoice.reset();
      stage2IncubatorLastChoice.reset();
      const sampleType = data?.sampleContext?.sampleType ?? "";
      const isDirect = ["Water", "EnvironmentalMonitoring", "AfterCleaning"].includes(sampleType);
      const configuredDf = data?.sampleContext?.configuredDilutionFactor;
      setReadings(["", ""]);
      setDilutionFactor(isDirect ? "1" : configuredDf != null ? String(configuredDf) : "");
      setDilutionFactorOverriding(false);
      setDilutionFactorOverrideNote("");
      setPreview(null);
      setPreviewError(false);
      setReadingErrors({});
      setPhase(data.allStepsComplete ? "all-complete" : data.incubationLock != null ? "awaiting-result" : "select-media");
    } catch (e: unknown) {
      setError(serverMessage(e) ?? "Could not load this test's workflow.");
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [testOrderId]);

  const currentRef = useRef(current);
  currentRef.current = current;
  const pickedRef = useRef({ mediaId, incubatorId, stage2IncubatorId });
  pickedRef.current = { mediaId, incubatorId, stage2IncubatorId };
  const { applyDefault: applyLotDefault } = lotLastChoice;
  const { applyDefault: applyIncubatorDefault } = incubatorLastChoice;
  const { applyDefault: applyStage2Default } = stage2IncubatorLastChoice;

  useEffect(() => {
    if (phase !== "select-media" && phase !== "transfer-stage-2") return;
    const controller = new AbortController();
    const stepRow: TestWorkflowStepDto | null = currentRef.current?.step ?? null;
    const singleMedium: StepMediaRow | null =
      stepRow?.stepMedia?.length === 1 ? stepRow.stepMedia[0] : null;

    // The server only offers equipment and lots that would pass the save-time checks.
    // The picker below lists what is selectable; a default is applied only if it is listed.
    const loadDefaults = (query: Parameters<typeof entryDefaultsService.get>[0]) =>
      entryDefaultsService
        .get(query, controller.signal)
        .catch(() => ({ defaults: [] as { field: string; id: number | null }[] }));

    if (phase === "select-media") {
      Promise.all([
        lookupCache.getReleasedMedia(),
        lookupCache.getIncubators(),
        loadDefaults({
          context: "incubation",
          testOrderId,
          stepMediaId: singleMedium?.id,
          mediaProductId: singleMedium?.mediaProductId
        })
      ]).then(([mediaItems, incItems, defaultsRes]) => {
        if (controller.signal.aborted) return;
        setReleasedMedia(mediaItems);
        setIncubators(incItems);

        const allowedProductIds = new Set((stepRow?.stepMedia ?? []).map((m) => m.mediaProductId));
        const lotDef = defaultsRes.defaults.find((d) => d.field === "mediaLotId");
        const matchedLot =
          lotDef?.id != null
            ? mediaItems.find(
                (m) =>
                  m.id === lotDef.id &&
                  m.material?.mediaProductId != null &&
                  allowedProductIds.has(m.material.mediaProductId)
              )
            : undefined;
        if (matchedLot && pickedRef.current.mediaId === "") {
          setMediaId(matchedLot.id);
          applyLotDefault(matchedLot.id);
        }

        const chosenStepMedium =
          (stepRow?.stepMedia ?? []).find(
            (m) => matchedLot?.material?.mediaProductId != null && m.mediaProductId === matchedLot.material.mediaProductId
          ) ?? singleMedium;
        const minTemp = chosenStepMedium?.tempMin ?? stepRow?.temperatureMin ?? 0;
        const maxTemp = chosenStepMedium?.tempMax ?? stepRow?.temperatureMax ?? 0;
        const incDef = defaultsRes.defaults.find((d) => d.field === "incubatorEquipmentId");
        const matchedInc =
          incDef?.id != null
            ? incItems.find(
                (i) =>
                  i.id === incDef.id &&
                  i.setPointTemperature != null &&
                  i.setPointTemperature >= minTemp &&
                  i.setPointTemperature <= maxTemp
              )
            : undefined;
        if (matchedInc && pickedRef.current.incubatorId === "") {
          setIncubatorId(matchedInc.id);
          applyIncubatorDefault(matchedInc.id);
        }
      });
    } else {
      const stage2 = (stepRow?.incubationStages ?? []).find((x: StepStage) => x.stageNumber === 2);
      Promise.all([
        lookupCache.getIncubators(),
        loadDefaults({ context: "incubation", testOrderId })
      ]).then(([incItems, defaultsRes]) => {
        if (controller.signal.aborted) return;
        setIncubators(incItems);
        const incDef = defaultsRes.defaults.find((d) => d.field === "incubatorEquipmentId");
        const matchedInc =
          incDef?.id != null && stage2
            ? incItems.find(
                (i) =>
                  i.id === incDef.id &&
                  i.setPointTemperature != null &&
                  i.setPointTemperature >= stage2.tempMin &&
                  i.setPointTemperature <= stage2.tempMax
              )
            : undefined;
        if (matchedInc && pickedRef.current.stage2IncubatorId === "") {
          setStage2IncubatorId(matchedInc.id);
          applyStage2Default(matchedInc.id);
        }
      });
    }

    return () => controller.abort();
  }, [phase, testOrderId, applyLotDefault, applyIncubatorDefault, applyStage2Default]);

  const step = current?.step;
  const isTwoStageTransfer = !!step?.requiresIncubationTransfer;
  const stage2Config = (step?.incubationStages ?? []).find((x: StepStage) => x.stageNumber === 2);

  // Permitted media come directly from the step's own configured
  // StepMedia (Test Master, one media product per row) - the same set TestWorkflowEngine.SelectMediaAsync
  // enforces server-side, so the picker can't offer a lot the backend
  // would reject anyway. Replaces the old MediaType-class filter plus a
  // step-name-matching fallback, both of which relied on fields that no
  // longer exist on the step/lot after the Media Configuration Migration.
  const permittedProductIds = new Set((step?.stepMedia ?? []).map((m: StepMediaRow) => m.mediaProductId));
  const matchingMedia = releasedMedia.filter((m) => m.material?.mediaProductId != null && permittedProductIds.has(m.material.mediaProductId));
  const permittedMaterialNames = (step?.stepMedia ?? []).map((m: StepMediaRow) => m.mediaProductName).join(" or ");

  const selectedMediaLot = releasedMedia.find((m) => m.id === mediaId);
  const selectedStepMedium = (step?.stepMedia ?? []).find((m: StepMediaRow) => selectedMediaLot?.material?.mediaProductId != null && m.mediaProductId === selectedMediaLot.material.mediaProductId);
  const activeStage1Medium = selectedStepMedium ?? ((step?.stepMedia?.length === 1) ? step.stepMedia[0] : null);

  // The chosen medium's own Test Master window; otherwise the step window the
  // server resolved (the open incubation's medium, or the permitted media's
  // range) - never a locally computed or default value.
  const stage1TempMin = activeStage1Medium?.tempMin ?? step?.temperatureMin ?? 0;
  const stage1TempMax = activeStage1Medium?.tempMax ?? step?.temperatureMax ?? 0;
  const stage1IncMinHours = activeStage1Medium?.incubationMinHours ?? step?.incubationMinHours ?? 0;
  const stage1IncMaxHours = activeStage1Medium?.incubationMaxHours ?? step?.incubationMaxHours ?? 0;

  // Only offer incubators whose set point actually falls within this
  // step's required temperature range - one out of calibration/set to
  // the wrong temperature for this test shouldn't even be selectable.
  const matchingIncubators = incubators.filter((i) =>
    i.setPointTemperature != null && i.setPointTemperature >= stage1TempMin && i.setPointTemperature <= stage1TempMax
  );

  const matchingStage2Incubators = incubators.filter((i) =>
    i.setPointTemperature != null && stage2Config && i.setPointTemperature >= stage2Config.tempMin && i.setPointTemperature <= stage2Config.tempMax
  );

  // current-step never serialized a top-level "incubation" object - only
  // incubationLock (isLocked/incubationEndUtc/remainingSeconds/stageNumber)
  // and previousSteps (one row per Incubation, including the still-open
  // one, tagged status: "Incubating"). The open row's own start time comes
  // from there instead.
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find((p: OpenIncubationRow) => p.stepName === step.stepName && p.status === "Incubating")
    : null;

  const currentStageNumber = current?.incubationLock?.stageNumber ?? openIncubationRow?.stageNumber ?? 1;
  const isStage2 = currentStageNumber === 2;

  const activeTempMin = isStage2 && stage2Config ? stage2Config.tempMin : stage1TempMin;
  const activeTempMax = isStage2 && stage2Config ? stage2Config.tempMax : stage1TempMax;
  const activeIncMinHours = isStage2 && stage2Config ? stage2Config.incubationMinHours : stage1IncMinHours;
  const activeIncMaxHours = isStage2 && stage2Config ? stage2Config.incubationMaxHours : stage1IncMaxHours;

  // Minimum-duration gate as the server computed it (stage-aware, from the
  // open incubation's own medium) so the button disables itself instead of
  // bouncing off a server error - the server still enforces it.
  const minReadyAt = current?.incubationLock?.minReadyAt ? new Date(current.incubationLock.minReadyAt) : null;
  const windowNotConfigured = current?.incubationLock?.windowNotConfigured ?? false;
  const minimumDurationOverridden = current?.incubationLock?.minimumDurationOverridden ?? false;
  const isTimeReady = !windowNotConfigured && (!minReadyAt || new Date() >= minReadyAt || minimumDurationOverridden);

  const startIncubation = async () => {
    setError(null);
    if (!mediaId || !incubatorId) return;
    try {
      await TestWorkflowService.selectMedia(testOrderId, step.stepName, Number(mediaId), Number(incubatorId));
      await load();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? "Could not start incubation for this step.");
    }
  };

  const startStage2Incubation = async () => {
    setError(null);
    if (!stage2IncubatorId) return;
    try {
      await TestWorkflowService.startStage2Incubation(testOrderId, step.stepName, Number(stage2IncubatorId));
      setStage2IncubatorId("");
      await load();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? "Could not start stage 2 incubation.");
    }
  };

  const advanceIncubationWindow = async () => {
    setError(null);
    try {
      await TestWorkflowService.closeIncubationWindow(testOrderId);
      await load();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? "Could not close this incubation window.");
    }
  };

  const updateReading = (i: number, value: string) => {
    setReadings((r) => r.map((v, idx) => (idx === i ? value : v)));
    setReadingErrors((prev) => {
      if (!(i in prev)) return prev;
      const next = { ...prev };
      delete next[i];
      return next;
    });
  };
  const addReading = () => setReadings((r) => [...r, ""]);
  const removeReading = (i: number) => {
    setReadings((r) => (r.length > 1 ? r.filter((_, idx) => idx !== i) : r));
    setReadingErrors({});
  };

  const isNonNumeric = (val: string) =>
    ["tntc", "uncountable"].includes(val.toLowerCase().trim());

  const isDirectCount = ["Water", "EnvironmentalMonitoring", "AfterCleaning"]
    .includes(current?.sampleContext?.sampleType ?? "");

  const isCountTestWorkflow = current?.workflowType === "CountTest";
  const configuredDilutionFactor: number | null = current?.sampleContext?.configuredDilutionFactor ?? null;
  // Item-based count test (TAMC/TYMC) with nothing configured yet on
  // Specifications - result entry is blocked, mirroring the backend rule
  // in TestWorkflowEngine.RecordCountTestAsync rather than letting the
  // analyst free-type a value that would just be rejected on submit.
  // Gated to CountTest so these never affect the pathogen/observation
  // Submit button, which shares the same disabled-state expression below.
  const dilutionFactorNotConfigured = isCountTestWorkflow && !isDirectCount && configuredDilutionFactor == null;
  const dilutionFactorDiffersFromConfigured =
    isCountTestWorkflow && !isDirectCount && configuredDilutionFactor != null && (parseFloat(dilutionFactor) || 0) !== configuredDilutionFactor;

  const stepNameForPreview = step?.stepName ?? null;

  useEffect(() => {
    if (phase !== "enter-result" || !isCountTestWorkflow || !stepNameForPreview || dilutionFactorNotConfigured) {
      return;
    }

    const nonEmptyEntries = readings
      .map((value, index) => ({ value, index }))
      .filter((entry) => entry.value.trim() !== "");

    if (nonEmptyEntries.length === 0) {
      setPreview(null);
      setPreviewError(false);
      setReadingErrors({});
      return;
    }

    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      const payload = {
        stepName: stepNameForPreview,
        rawPlateReadings: nonEmptyEntries.map((e) => e.value),
        dilutionFactor: Number(dilutionFactor) || 1,
        dilutionFactorOverrideNote: dilutionFactorDiffersFromConfigured ? dilutionFactorOverrideNote.trim() : undefined
      };

      TestWorkflowService.previewCountResult(testOrderId, payload, controller.signal)
        .then((result) => {
          if (controller.signal.aborted) return;
          setPreview(result);
          setPreviewError(false);
          setReadingErrors({});
        })
        .catch((err: unknown) => {
          if (controller.signal.aborted || isAbortError(err)) return;
          const rowErrs = errorsByRow(err);
          const mappedErrors: Record<number, string> = {};
          for (const [rowKey, fields] of Object.entries(rowErrs)) {
            const sentIdx = Number(rowKey);
            const msg = fields.rawPlateReadings;
            if (msg) {
              const targetIdx = nonEmptyEntries[sentIdx]?.index ?? sentIdx;
              mappedErrors[targetIdx] = msg;
            }
          }
          if (Object.keys(mappedErrors).length > 0) {
            setPreview(null);
            setPreviewError(false);
            setReadingErrors(mappedErrors);
            return;
          }
          setPreview(null);
          setReadingErrors({});
          setPreviewError(true);
        });
    }, 400);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [
    phase,
    isCountTestWorkflow,
    stepNameForPreview,
    dilutionFactorNotConfigured,
    readings,
    dilutionFactor,
    dilutionFactorDiffersFromConfigured,
    dilutionFactorOverrideNote,
    testOrderId
  ]);

  const countSaveBlockedReason = !isTimeReady
    ? minReadyAt
      ? `Results cannot be submitted before ${minReadyAt.toLocaleString()}.`
      : "This step is not ready for a result yet."
    : dilutionFactorNotConfigured
      ? "No Dilution Factor is configured for this test on the item's Specifications tab."
      : dilutionFactorDiffersFromConfigured && !dilutionFactorOverrideNote.trim()
        ? "A justification note is required when the entered Dilution Factor differs from the configured value."
        : undefined;

  // Bench mode (a shared touch tablet): the plate counts are entered in a full-screen form
  // and kept as a local draft until the analyst presses Save. Outside bench mode nothing
  // here changes.
  const { benchMode } = useBenchMode();
  const benchCount = benchMode && current?.workflowType === "CountTest";
  const [saving, setSaving] = useState(false);
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const draft = useBenchDraft<CountDraftValues>({
    formKey: "count-reading",
    recordKey: `${testOrderId}:${step?.stepName ?? ""}`,
    recordVersion: null,
    title: `${testCode} plate counts`,
    values: { readings, dilutionFactor, dilutionFactorOverrideNote },
    pick: pickCountDraft,
    enabled: benchCount && phase === "enter-result" && Boolean(step)
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved) return;
    if (Array.isArray(saved.readings) && saved.readings.length > 0 && saved.readings.every((r) => typeof r === "string")) {
      setReadings(saved.readings);
    }
    // A dilution factor that differs from the configured one is an override again, and
    // still needs its justification before it can be saved.
    if (!isDirectCount && typeof saved.dilutionFactor === "string" && saved.dilutionFactor !== String(configuredDilutionFactor ?? "")) {
      setDilutionFactor(saved.dilutionFactor);
      setDilutionFactorOverriding(true);
      if (typeof saved.dilutionFactorOverrideNote === "string") setDilutionFactorOverrideNote(saved.dilutionFactorOverrideNote);
    }
  };

  const countEntryDirty = readings.some((r) => r.trim() !== "") || dilutionFactorOverrideNote.trim() !== "";

  const closeCountEntry = async () => {
    // Leaving a form with entries was confirmed as a discard; an offered draft nobody accepted is left alone.
    if (countEntryDirty && !draft.restored) await draft.discard();
    setReadings(["", ""]);
    setDilutionFactor(isDirectCount ? "1" : configuredDilutionFactor != null ? String(configuredDilutionFactor) : "");
    setDilutionFactorOverriding(false);
    setDilutionFactorOverrideNote("");
    setPreview(null);
    setPreviewError(false);
    setReadingErrors({});
    setKeptOffline(false);
    setError(null);
    setPhase("awaiting-result");
  };

  const keepCountDraft = () => {
    toast.info("Draft kept on this tablet. It is not saved to MicroLIMS.");
    setPhase("awaiting-result");
  };

  const submitResult = async (advance = false) => {
    setError(null);
    try {
      if (dilutionFactorNotConfigured) {
        setError("No Dilution Factor is configured for this test on the item's Specifications. Ask a Section Head/Admin to configure it before recording a result.");
        return;
      }
      if (readings.every((r) => r.trim() === "")) {
        setError("Enter at least one plate reading.");
        return;
      }
      if (dilutionFactorDiffersFromConfigured && !dilutionFactorOverrideNote.trim()) {
        setError("A justification note is required when the entered Dilution Factor differs from the configured value.");
        return;
      }
      const payload = {
        stepName: step.stepName,
        rawPlateReadings: readings.filter((r) => r.trim() !== ""),
        dilutionFactor: Number(dilutionFactor) || 1,
        dilutionFactorOverrideNote: dilutionFactorDiffersFromConfigured ? dilutionFactorOverrideNote.trim() : undefined
      };

      setSaving(true);
      const result = await TestWorkflowService.recordCountResult(testOrderId, payload, ...draftHeaderArgs(draft.submitHeaders()));
      await draft.onSubmitted();
      setKeptOffline(false);
      setLastOutcome(result);
      if (advance) {
        // Save and next: straight on to whatever the server says comes next.
        toast.success(result.outcomeSummary ? `Recorded: ${result.outcomeSummary}` : "Reading recorded.");
        await load();
      } else if (result.allStepsComplete) {
        setPhase("all-complete");
      } else {
        setPhase("step-complete");
      }
    } catch (e) {
      if (!benchCount) {
        setError(serverMessage(e) ?? "Could not record this result.");
        return;
      }
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        // Nothing reached MicroLIMS: the entries stay in the form and in the local draft.
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(failure.message ?? "Could not record this result.");
      }
    } finally {
      setSaving(false);
    }
  };

  // An outsourced test is reported from the external lab's report, whatever its workflow type.
  if (outsourcedLab) {
    return (
      <OutsourcedPanel
        key={testOrderId}
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        outsourcedToLab={outsourcedLab}
        sampleId={sampleId}
        status={status}
      />
    );
  }

  if (phase === "loading" || !current) {
    return (
      <Box sx={{ py: 4 }}>
        {error ? <Alert severity="error">{error}</Alert> : <DialogBodySkeleton blocks={3} blockHeight={56} />}
      </Box>
    );
  }

  // Physicochemical tests can be sent to an outside laboratory instead.
  const withOutsource = (panel: ReactNode) => (
    <>
      <OutsourceTestAction testOrderId={testOrderId} status={status} onOutsourced={setOutsourcedLab} />
      {panel}
    </>
  );

  // Water / AC per-location result entry
  if (current.locationEntry) {
    return (
      <LocationResultsPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Measurement workflow (pH, density, etc.)
  if (current.workflowType === "Measurement") {
    return withOutsource(
      <MeasurementPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Gravimetric workflow (loss on drying, ash/residue)
  if (current.workflowType === "Gravimetric") {
    return withOutsource(
      <GravimetricPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Titration assay (volumetric titrimetry, USP <541>)
  if (current.workflowType === "Titration") {
    return withOutsource(
      <TitrationPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // UV Spectrophotometry (Plan 4)
  if (current.workflowType === "UvSpectrophotometry") {
    return withOutsource(
      <UvPanel
        key={testOrderId}
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Qualitative workflow (appearance, identification, etc.)
  if (current.workflowType === "Qualitative") {
    return withOutsource(
      <QualitativePanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Dissolution workflow (HPLC finish, staged S1-S3)
  if (current.workflowType === "Dissolution") {
    return withOutsource(
      <DissolutionPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Disintegration workflow (time per unit, staged S1-S2)
  if (current.workflowType === "Disintegration") {
    return withOutsource(
      <DisintegrationPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Weight Variation workflow (tablet/capsule weight variation, staged S1-S2)
  if (current.workflowType === "WeightVariation") {
    return withOutsource(
      <WeightVariationPanel
        testOrderId={testOrderId}
        displayName={displayName}
        testCode={testCode}
        itemId={itemId}
        sampleId={sampleId}
        current={current}
        onRecorded={load}
        onClose={onClose}
      />
    );
  }

  // Water's TAMC-Water TestOrder is also SampleLocation-batched (Phase 1
  // PrepareAsync), but its result computation is the per-location
  // plate-reading average (WaterLocationResultGridDialog), not EM/AC's
  // CFU x shared-dilution-factor grid.
  const isWaterCountBatch = category === "Water" && current.workflowType === "CountTest";
  const isBatchOrder = isEmOrAfterCleaning || isWaterCountBatch;

  // Five-stage pathogen chain, except EM/AfterCleaning (which still uses
  // this component's own incubation phases below, only handing off to
  // the location-grid dialogs for its final per-location result entry).
  if (current.workflowType !== "CountTest" && !isEmOrAfterCleaning) {
    return <PathogenStepDialog testOrderId={testOrderId} testCode={testCode} displayName={displayName} onClose={onClose} />;
  }

  const stepChip: { label: string; tone: StepChipTone } | null =
    phase === "select-media" ? { label: "Set up", tone: "info" }
    : phase === "transfer-stage-2" ? { label: "Stage 2 transfer", tone: "info" }
    : phase === "awaiting-result" ? (isTimeReady ? { label: "Ready to read", tone: "notDetected" } : { label: "Incubating", tone: "purple" })
    : phase === "enter-result" ? { label: "Result entry", tone: "info" }
    : phase === "step-complete" ? { label: "Recorded", tone: "notDetected" }
    : null;
  const stepHelp = current.workflowType === "CountTest" ? COUNT_STEP_HELP : undefined;

  if (phase === "all-complete") {
    const outcome = lastOutcome?.finalResult ?? current.finalResult;
    return (
      <Box>
        <StepChainStrip current={current} />
        <Alert severity={outcome === "Detected" ? "error" : "success"} sx={{ mb: 2 }}>
          Final result: <strong>{outcome}</strong>
        </Alert>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded, Pending Review" />
          {onClose && (
            <Button variant="contained" onClick={onClose} sx={{ fontWeight: 600, textTransform: "none" }}>
              Done / Close
            </Button>
          )}
        </Box>
      </Box>
    );
  }

  const previewTone = preview?.status ? theme.custom.status[statusTone(preview.status)] : null;

  // The count entry fields, shared by the inline form and the bench full-screen form.
  const countEntryFields = current.workflowType === "CountTest" ? (
            <>
              {dilutionFactorNotConfigured ? (
                <Alert severity="error">
                  No Dilution Factor is configured for this test on the item's Specifications tab. Ask a Section Head/Admin to configure it before recording a result.
                </Alert>
              ) : (
                <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1 }}>
                  <TextField
                    id="count-dilution-factor"
                    label="Dilution factor"
                    type="number"
                    value={dilutionFactor}
                    onChange={(e) => setDilutionFactor(e.target.value)}
                    disabled={isDirectCount || !dilutionFactorOverriding}
                    helperText={
                      isDirectCount
                        ? "Direct count: dilution factor fixed at 1"
                        : dilutionFactorOverriding
                          ? "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100"
                          : `Configured value from Specifications${configuredDilutionFactor != null ? ` (${configuredDilutionFactor})` : ""}`
                    }
                    sx={{ flex: 1, maxWidth: 320 }}
                    slotProps={{
                      inputLabel: { shrink: true },
                      htmlInput: { step: "1", min: "1", inputMode: "numeric" }
                    }}
                  />
                  {!isDirectCount && !dilutionFactorOverriding && (
                    <IconButton aria-label="Override dilution factor"
                      onClick={() => setDilutionFactorOverriding(true)}
                      title="Override configured Dilution Factor"
                      sx={{ mt: 0.5 }}
                    >
                      <EditIcon fontSize="small" />
                    </IconButton>
                  )}
                  {dilutionFactorOverriding && (
                    <Button
                      sx={{ mt: 1 }}
                      onClick={() => {
                        setDilutionFactorOverriding(false);
                        setDilutionFactorOverrideNote("");
                        setDilutionFactor(configuredDilutionFactor != null ? String(configuredDilutionFactor) : "");
                      }}
                    >
                      Cancel Override
                    </Button>
                  )}
                </Box>
              )}

              {dilutionFactorDiffersFromConfigured && (
                <TextField
                  id="count-dilution-justification"
                  label="Justification for override"
                  required
                  multiline
                  minRows={2}
                  value={dilutionFactorOverrideNote}
                  onChange={(e) => setDilutionFactorOverrideNote(e.target.value)}
                  error={!dilutionFactorOverrideNote.trim()}
                  helperText="Required: the entered value differs from what's configured on Specifications. This is captured in the audit trail."
                  fullWidth
                />
              )}

              <Box>
                <Typography variant="caption" sx={{ display: "block", color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.06em", mb: 1 }}>
                  Plate counts
                </Typography>
                {benchCount ? (
                  <BenchPlateRows readings={readings} errors={readingErrors} isNonNumeric={isNonNumeric} onChange={updateReading} onRemove={removeReading} />
                ) : (
                  <Box sx={{ display: "grid", gap: 1.5, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" } }}>
                    {readings.map((r, i) => {
                      const fieldError = readingErrors[i];
                      return (
                        <Box key={i} sx={{ display: "flex", alignItems: "flex-start", gap: 0.5 }}>
                          <TextField
                            id={`count-plate-${i + 1}`}
                            label={`Plate ${i + 1}`}
                            value={r}
                            onChange={(e) => updateReading(i, e.target.value)}
                            placeholder="Colonies or TNTC"
                            error={Boolean(fieldError)}
                            helperText={fieldError ?? (isNonNumeric(r) ? "Non-numeric: flagged for reviewer decision" : " ")}
                            slotProps={{
                              inputLabel: { shrink: true },
                              htmlInput: { inputMode: "numeric" },
                              formHelperText: fieldError ? { role: "alert" } : undefined
                            }}
                            sx={{
                              flex: 1,
                              "& .MuiOutlinedInput-root fieldset": {
                                borderColor: !fieldError && isNonNumeric(r) ? theme.custom.status.inconclusive.text : undefined
                              }
                            }}
                          />
                          <IconButton aria-label={`Remove plate ${i + 1}`} onClick={() => removeReading(i)} disabled={readings.length <= 1} sx={{ mt: 0.5 }}>
                            <CloseIcon fontSize="small" />
                          </IconButton>
                        </Box>
                      );
                    })}
                  </Box>
                )}
                <Button startIcon={<AddIcon />} onClick={addReading} sx={{ mt: 0.5 }}>Add Plate</Button>
              </Box>

              <Box
                aria-live="polite"
                sx={{
                  p: 2, borderRadius: 2, border: "1px solid",
                  bgcolor: previewTone ? previewTone.bg : "action.hover",
                  borderColor: previewTone ? previewTone.border : "divider"
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
                  <Typography variant="caption" sx={{ color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.06em" }}>
                    Preview — not recorded
                  </Typography>
                  {preview?.status && <StatusBadge status={preview.status} />}
                </Box>
                {previewError ? (
                  <>
                    <Typography sx={{ fontSize: 16, fontWeight: 600, color: "text.secondary" }}>
                      Preview unavailable
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary" }}>
                      Save will still calculate and record the result.
                    </Typography>
                  </>
                ) : preview ? (
                  <>
                    <Typography sx={{ fontSize: 22, fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                      {preview.reported}
                    </Typography>
                    {(preview.average != null || preview.calculated != null) && (
                      <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontVariantNumeric: "tabular-nums" }}>
                        {[
                          preview.average != null ? `Average: ${preview.average}` : null,
                          preview.calculated != null ? `Calculated: ${preview.calculated}` : null
                        ].filter(Boolean).join(" · ")}
                      </Typography>
                    )}
                    {preview.messages.length > 0 && (
                      <Typography variant="caption" sx={{ display: "block", color: previewTone ? previewTone.text : "text.secondary", mt: 0.5 }}>
                        {preview.messages.join(" ")}
                      </Typography>
                    )}
                  </>
                ) : (
                  <>
                    <Typography sx={{ fontSize: 22, fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                      —
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary" }}>
                      Enter plate counts to see the result.
                    </Typography>
                  </>
                )}
              </Box>
            </>
  ) : null;

  return (
    <Box>
      <StepChainStrip current={current} />
      {current?.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? `Returned for revision: ${current.returnInfo.reason}`
            : "Returned by reviewer for revision"}
        </Alert>
      )}
      {error && !(benchCount && phase === "enter-result") && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <StepHeading
        title={step.stepName}
        chip={stepChip}
        caption={[
          isTwoStageTransfer ? `Stage ${currentStageNumber} of 2` : null,
          step.isFinalStep ? "(determines the final result)" : null
        ].filter(Boolean).join(" · ") || undefined}
        help={stepHelp}
      />

      {phase === "select-media" && (
        <Stack spacing={2}>
          <FixedMediumRow label="Permitted medium" mediaName={permittedMaterialNames || "Approved media"} />

          <MediaLotIncubatorFields
            idPrefix="count"
            lots={matchingMedia}
            incubators={incubatorOptions(matchingIncubators)}
            tempMin={stage1TempMin}
            tempMax={stage1TempMax}
            mediaLotId={mediaId}
            equipmentId={incubatorId}
            lotIsDefault={lotLastChoice.isDefault}
            lotConfirmed={lotLastChoice.confirmed}
            onConfirmLot={lotLastChoice.confirm}
            incubatorIsDefault={incubatorLastChoice.isDefault}
            onLotChange={(id) => {
              setMediaId(id);
              setIncubatorId("");
              lotLastChoice.notifyValueChanged();
              incubatorLastChoice.reset();
            }}
            onIncubatorChange={(id) => {
              setIncubatorId(id);
              incubatorLastChoice.notifyValueChanged();
            }}
          />

          <IncubationTimeline
            points={[
              { label: isTwoStageTransfer ? "Stage 1 start" : "Start", value: "0 h", active: true },
              { label: "Ready from", value: `${stage1IncMinHours} h` },
              { label: "Read by", value: `${stage1IncMaxHours} h` }
            ]}
          />

          <StepActionBar
            note={
              lotLastChoice.saveBlockedReason
                ? lotLastChoice.saveBlockedReason
                : mediaId && incubatorId
                  ? "Start time, media lot and incubator are recorded in the audit trail."
                  : "Select a media lot and an incubator to continue."
            }
          >
            <Button
              variant="contained"
              disabled={!mediaId || !incubatorId || Boolean(lotLastChoice.saveBlockedReason)}
              onClick={startIncubation}
            >
              Start Incubation
            </Button>
          </StepActionBar>
        </Stack>
      )}

      {phase === "transfer-stage-2" && stage2Config && (
        <Stack spacing={2}>
          <Alert severity="info">
            Stage 1 incubation is complete. Move the plates to an incubator set within {stage2Config.tempMin}–{stage2Config.tempMax} °C to start Stage 2.
          </Alert>

          <StepDetailList
            rows={[
              { label: "Media lot (from Stage 1)", value: openIncubationRow?.lotNumber ?? "Selected in Stage 1" },
              { label: "Stage 2 temperature", value: `${stage2Config.tempMin}–${stage2Config.tempMax} °C` },
              { label: "Stage 2 incubation", value: `${stage2Config.incubationMinHours}–${stage2Config.incubationMaxHours} h` }
            ]}
          />

          <IncubatorField
            id="count-stage2-incubator"
            label="Stage 2 incubator"
            incubators={incubatorOptions(matchingStage2Incubators)}
            tempMin={stage2Config.tempMin}
            tempMax={stage2Config.tempMax}
            value={stage2IncubatorId}
            isDefault={stage2IncubatorLastChoice.isDefault}
            onChange={(id) => {
              setStage2IncubatorId(id);
              stage2IncubatorLastChoice.notifyValueChanged();
            }}
          />

          <StepActionBar note={stage2IncubatorId ? undefined : "Select a Stage 2 incubator to continue."}>
            <Button variant="outlined" onClick={() => setPhase("awaiting-result")}>Back</Button>
            <Button variant="contained" disabled={!stage2IncubatorId} onClick={startStage2Incubation}>
              Start Stage 2 Incubation
            </Button>
          </StepActionBar>
        </Stack>
      )}

      {phase === "awaiting-result" && current.incubationLock && (
        <IncubationWaitingView
          testOrderId={testOrderId}
          step={step}
          current={current}
          onSubmitted={load}
          incubationWindow={{ tempMin: activeTempMin, tempMax: activeTempMax, minHours: activeIncMinHours, maxHours: activeIncMaxHours }}
          nextLabel={
            isTwoStageTransfer && !isStage2 ? "Stage 2 transfer"
            : isEmOrAfterCleaning && !step.isFinalStep && !isTwoStageTransfer ? "the next incubation window"
            : "plate reading"
          }
          renderAction={(ready) =>
            isTwoStageTransfer && !isStage2 ? (
              <Button variant="contained" disabled={!ready} onClick={() => setPhase("transfer-stage-2")}>
                Transfer to Stage 2 Incubation
              </Button>
            ) : isTwoStageTransfer && isStage2 ? (
              isBatchOrder ? (
                <Button variant="contained" disabled={!ready} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
              ) : (
                <Button variant="contained" disabled={!ready} onClick={() => setPhase("enter-result")}>Record Result</Button>
              )
            ) : isEmOrAfterCleaning && !step.isFinalStep ? (
              <Button variant="contained" disabled={!ready} onClick={advanceIncubationWindow}>Advance to Next Incubation Window</Button>
            ) : isBatchOrder ? (
              <Button variant="contained" disabled={!ready} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
            ) : (
              <Button variant="contained" disabled={!ready} onClick={() => setPhase("enter-result")}>Record Result</Button>
            )
          }
        />
      )}

      {current.workflowType === "CountTest" && isWaterCountBatch ? (
        <WaterLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : current.workflowType === "CountTest" ? (
        <LocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : (
        <PathogenLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      )}

      {phase === "enter-result" && !benchCount && (
        <Stack spacing={2}>
          {countEntryFields}

          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          <StepActionBar
            note={readings.every((r) => r.trim() === "") ? "Enter at least one plate count to continue." : "The result is recorded in the audit trail."}
          >
            <Button
              variant="contained"
              disabled={!isTimeReady || dilutionFactorNotConfigured || (dilutionFactorDiffersFromConfigured && !dilutionFactorOverrideNote.trim())}
              onClick={() => submitResult()}
            >
              {current.workflowType === "CountTest" ? "Calculate" : "Submit"}
            </Button>
          </StepActionBar>
        </Stack>
      )}

      {benchCount && (
        <BenchDialog
          open={phase === "enter-result"}
          title={`${testCode} plate counts`}
          onClose={() => void closeCountEntry()}
          onSave={keptOffline ? keepCountDraft : () => void submitResult()}
          onSaveAndNext={keptOffline || step?.isFinalStep ? undefined : () => void submitResult(true)}
          saving={saving}
          saveLabel={keptOffline ? "Keep draft" : "Save"}
          saveDisabledReason={countSaveBlockedReason}
          dirty={countEntryDirty}
        >
          <Stack spacing={2}>
            <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />
            {keptOffline && <OfflineDraftNotice onTryAgain={() => setKeptOffline(false)} />}
            {error && <Alert severity="error">{error}</Alert>}
          {countEntryFields}

          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          </Stack>
        </BenchDialog>
      )}

      {phase === "step-complete" && lastOutcome && (
        <Stack spacing={2}>
          {lastOutcome.isDefinitive ? (
            <Alert severity="success">
              Step recorded: <strong>{lastOutcome.outcomeSummary}</strong>
            </Alert>
          ) : (
            <Alert severity="warning">
              {lastOutcome.outcomeSummary} - this step must be repeated with a fresh media lot.
            </Alert>
          )}
          {lastOutcome.isDefinitive && lastOutcome.average != null && (
            <StepDetailList
              rows={[
                { label: "Average", value: String(lastOutcome.average) },
                { label: "Calculated", value: String(lastOutcome.calculatedResult) },
                { label: "Status", value: String(lastOutcome.status) }
              ]}
            />
          )}
          <StepActionBar>
            <Button variant="contained" onClick={load}>{lastOutcome.isDefinitive ? "Proceed to Next Step" : "Retry This Step"}</Button>
          </StepActionBar>
        </Stack>
      )}
    </Box>
  );
}
