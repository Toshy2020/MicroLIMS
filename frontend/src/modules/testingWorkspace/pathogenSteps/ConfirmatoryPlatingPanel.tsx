import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Button, Stack, Alert,
  Checkbox, FormControlLabel, CircularProgress
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, CurrentStepResponse, PermittedConfirmatoryMediaEntry,
  GrowthObservation, AnalystDecision
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { serverIncubationWindow } from "../utils/incubationWindow";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, useKeptOffline } from "../benchSave";
import {
  StepActionBar, IncubationTimeline, FixedMediumRow, MediaLotIncubatorFields,
  ExpectedAppearanceNote, ObservationOptions
} from "./StepLayout";
import { IncubationWaitingView } from "./IncubationWaitingView";

interface ConfirmatoryDraftValues {
  setupSelections: Record<number, { checked: boolean; mediaLotId: number | ""; equipmentId: number | "" }>;
  observations: Record<number, GrowthObservation | "">;
  readoutChecked: Record<number, boolean>;
}

function pickConfirmatorySetupDraft(v: ConfirmatoryDraftValues): Partial<ConfirmatoryDraftValues> {
  return { setupSelections: v.setupSelections };
}

function pickConfirmatoryReadoutDraft(v: ConfirmatoryDraftValues): Partial<ConfirmatoryDraftValues> {
  return { observations: v.observations, readoutChecked: v.readoutChecked };
}

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: (saved?: boolean) => void;
}

type Phase = "setup" | "waiting" | "readout" | "decision";

interface IncubatorOption { id: number; name: string; code: string; setTemperature: number; }

interface SetupRow {
  entry: PermittedConfirmatoryMediaEntry;
  checked: boolean;
  mediaLotId: number | "";
  equipmentId: number | "";
  incubators: IncubatorOption[];
}

interface ReadoutMedium {
  stepMediaId: number;
  mediaProductId: number;
  // The analyst's actual lot's material: observations are recorded against the lot used, not the product.
  materialId: number;
  mediaName: string;
  expectedAppearance: string | null;
}

export function ConfirmatoryPlatingPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const { benchMode } = useBenchMode();
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [permitted, setPermitted] = useState<PermittedConfirmatoryMediaEntry[]>([]);
  const [organism, setOrganism] = useState<{ id: number; name: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [alreadyRecordedMessage, setAlreadyRecordedMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  // Set by the waiting screen when its countdown reaches zero.
  const [countdownReady, setCountdownReady] = useState(false);
  const handleReady = useCallback(() => setCountdownReady(true), []);

  const observationOptions: { value: GrowthObservation; title: string; detail?: string }[] = [
    { value: "NoGrowth", title: t("pathogenSteps.plating.obs.noGrowth") },
    { value: "GrowthNonConforming", title: t("pathogenSteps.plating.obs.growthNonConforming") },
    { value: "GrowthConforming", title: t("pathogenSteps.plating.obs.growthConforming"), detail: t("pathogenSteps.plating.obs.conforming") }
  ];

  // Setup phase state, keyed by stepMediaId.
  const [setupRows, setSetupRows] = useState<Record<number, SetupRow>>({});

  // Read-out phase state.
  const [readoutMedia, setReadoutMedia] = useState<ReadoutMedium[]>([]);
  const [readoutUncertain, setReadoutUncertain] = useState(false);
  const [readoutChecked, setReadoutChecked] = useState<Record<number, boolean>>({});
  const [observations, setObservations] = useState<Record<number, GrowthObservation | "">>({});

  // Check incubation status from current step response (aligned with BrothWaitingPanel / SelectivePlatingPanel)
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find(
        (p) => p.stepName === step.stepName && p.status === "Incubating"
      )
    : null;
  const isStepIncubating = Boolean(openIncubationRow || current?.incubationLock?.isLocked);

  // Window, start, readiness and expected end exactly as the server resolved
  // them (the panel's longest window across its chosen media) - no local
  // hour calculations.
  const { windowNotConfigured, isTimeReady } = serverIncubationWindow(step, current);

  const initialPhase: Phase =
    !isStepIncubating ? "setup" :
    !(isTimeReady || countdownReady) || windowNotConfigured ? "waiting" :
    "readout";

  const [phase, setPhase] = useState<Phase>(initialPhase);

  useEffect(() => {
    setPhase(initialPhase);
  }, [initialPhase]);

  const setupSelections: ConfirmatoryDraftValues["setupSelections"] = {};
  for (const [idStr, row] of Object.entries(setupRows)) {
    setupSelections[Number(idStr)] = { checked: row.checked, mediaLotId: row.mediaLotId, equipmentId: row.equipmentId };
  }

  // Decisions are never drafted: enabled and pick cover only setup and readout.
  const draft = useBenchDraft<ConfirmatoryDraftValues>({
    formKey: `pathogen-confirmatory-${phase === "setup" ? "setup" : "readout"}`,
    recordKey: `${testOrderId}:${step.stepName}`,
    recordVersion: null,
    title: `${step.stepName}: #${testOrderId}`,
    values: { setupSelections, observations, readoutChecked },
    pick: phase === "setup" ? pickConfirmatorySetupDraft : pickConfirmatoryReadoutDraft,
    enabled: benchMode && !loading && (phase === "setup" || phase === "readout")
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved) return;
    if (phase === "setup" && saved.setupSelections) {
      setSetupRows((prev) => {
        const next = { ...prev };
        for (const [idStr, sel] of Object.entries(saved.setupSelections ?? {})) {
          const id = Number(idStr);
          if (!next[id] || !sel) continue;
          next[id] = {
            ...next[id],
            checked: typeof sel.checked === "boolean" ? sel.checked : next[id].checked,
            mediaLotId: typeof sel.mediaLotId === "number" && next[id].entry.availableLots.some((l) => l.id === sel.mediaLotId) ? sel.mediaLotId : "",
            equipmentId: typeof sel.equipmentId === "number" && next[id].incubators.some((i) => i.id === sel.equipmentId) ? sel.equipmentId : ""
          };
        }
        return next;
      });
    } else if (phase === "readout") {
      if (saved.observations) {
        setObservations((prev) => {
          const next = { ...prev };
          for (const [idStr, obs] of Object.entries(saved.observations ?? {})) {
            const id = Number(idStr);
            if (!Number.isNaN(id) && (obs === "NoGrowth" || obs === "GrowthNonConforming" || obs === "GrowthConforming" || obs === "")) {
              next[id] = obs;
            }
          }
          return next;
        });
      }
      if (saved.readoutChecked) {
        setReadoutChecked((prev) => {
          const next = { ...prev };
          for (const [idStr, chk] of Object.entries(saved.readoutChecked ?? {})) {
            const id = Number(idStr);
            if (!Number.isNaN(id) && typeof chk === "boolean") next[id] = chk;
          }
          return next;
        });
      }
    }
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
      .then(async (res) => {
        if (cancelled) return;
        setPermitted(res.permittedMedia);
        setOrganism(res.organism);
        const withIncubators = await Promise.all(
          res.permittedMedia.map((m) =>
            TestWorkflowService.getEligibleIncubators(testOrderId, m.stepMediaId)
              .then((r) => [m.stepMediaId, r.eligibleIncubators] as const)
          )
        );
        if (cancelled) return;
        const rows: Record<number, SetupRow> = {};
        for (const m of res.permittedMedia) {
          const incubators = withIncubators.find(([id]) => id === m.stepMediaId)?.[1] ?? [];
          rows[m.stepMediaId] = { entry: m, checked: false, mediaLotId: "", equipmentId: "", incubators };
        }
        setSetupRows(rows);
      })
      .catch((e) => { if (!cancelled) setError(workflowErrorDisplayMessage(parseWorkflowError(e))); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [testOrderId, step.stepName]);

  // When in readout phase and readoutMedia is empty, populate from permitted
  useEffect(() => {
    if (phase === "readout" && readoutMedia.length === 0 && permitted.length > 0) {
      // The server's recorded selection says which lot (and so which material) was plated.
      const rows = permitted.filter((m) => m.selection).map((m) => ({
        stepMediaId: m.stepMediaId,
        mediaProductId: m.mediaProductId,
        materialId: m.selection!.materialId,
        mediaName: m.mediaName,
        expectedAppearance: m.expectedAppearance
      }));
      if (rows.length > 0) {
        setReadoutMedia(rows);
        setReadoutUncertain(false);
      }
    }
  }, [phase, permitted, readoutMedia.length]);

  const toggleChecked = (stepMediaId: number, checked: boolean) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], checked } }));
  const setLot = (stepMediaId: number, mediaLotId: number) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], mediaLotId } }));
  const setEquipment = (stepMediaId: number, equipmentId: number) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], equipmentId } }));

  const submitSetup = async () => {
    setError(null);
    const checkedRows = Object.values(setupRows).filter((r) => r.checked);
    if (checkedRows.length === 0) { setError(t("pathogenSteps.confirmatory.checkOneMedium")); return; }
    if (checkedRows.some((r) => !r.mediaLotId || !r.equipmentId)) {
      setError(t("pathogenSteps.confirmatory.needLotAndIncubator"));
      return;
    }
    const startUtc = new Date().toISOString();
    const durationMax = step.incubationMaxHours > 0 ? step.incubationMaxHours : 24;
    const endUtc = new Date(Date.now() + durationMax * 3600 * 1000).toISOString();
    const selections = checkedRows.map((r) => ({
      stepMediaId: r.entry.stepMediaId, mediaLotId: Number(r.mediaLotId), equipmentId: Number(r.equipmentId)
    }));
    setSubmitting(true);
    try {
      await TestWorkflowService.submitConfirmatorySetup(
        testOrderId, step.stepName, selections, startUtc, endUtc, ...draftHeaderArgs(draft.submitHeaders())
      );
      await draft.onSubmitted();
      setKeptOffline(false);
      setReadoutMedia(checkedRows.map((r) => ({
        stepMediaId: r.entry.stepMediaId, mediaProductId: r.entry.mediaProductId,
        materialId: r.entry.availableLots.find((l) => l.id === Number(r.mediaLotId))!.materialId,
        mediaName: r.entry.mediaName, expectedAppearance: r.entry.expectedAppearance
      })));
      setReadoutUncertain(false);
      setObservations({});
      setReadoutChecked({});
      onSubmitted();
    } catch (e) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
        return;
      }
      if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
        return;
      }
      const parsed = parseWorkflowError(e);
      if (parsed.code === "CONFIRMATORY_SETUP_ALREADY_SUBMITTED") {
        onSubmitted(false);
        return;
      }
      if (parsed.code === "CONFIRMATORY_ALREADY_RECORDED") {
        setAlreadyRecordedMessage(workflowErrorDisplayMessage(parsed));
        return;
      }
      setError(workflowErrorDisplayMessage(parsed));
    } finally {
      setSubmitting(false);
    }
  };

  const isReadoutActive = (materialId: number) =>
    readoutUncertain ? (readoutChecked[materialId] ?? false) : true;

  const submitReadout = async () => {
    setError(null);
    const active = readoutMedia.filter((m) => isReadoutActive(m.materialId));
    if (active.length === 0) { setError(t("pathogenSteps.confirmatory.confirmOneMedium")); return; }
    const missing = active.filter((m) => !observations[m.materialId]);
    if (missing.length > 0) {
      setError(t("pathogenSteps.confirmatory.selectObservationFor", { names: missing.map((m) => m.mediaName).join(", ") }));
      return;
    }
    const payload = active.map((m) => ({ materialId: m.materialId, observation: observations[m.materialId] as GrowthObservation }));
    setSubmitting(true);
    try {
      const outcome = await TestWorkflowService.submitConfirmatoryObservations(
        testOrderId, step.stepName, payload, ...draftHeaderArgs(draft.submitHeaders())
      );
      await draft.onSubmitted();
      setKeptOffline(false);
      if (outcome.analystDecisionRequired) {
        setPhase("decision");
      } else {
        onSubmitted();
      }
    } catch (e) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const decide = async (decision: AnalystDecision) => {
    setError(null);
    setSubmitting(true);
    try {
      await TestWorkflowService.recordAnalystDecision(testOrderId, decision);
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <DialogBodySkeleton blocks={3} blockHeight={48} />;
  if (alreadyRecordedMessage) {
    return (
      <Alert severity="info">
        {alreadyRecordedMessage}
      </Alert>
    );
  }

  const checkedSetupRows = Object.values(setupRows).filter((r) => r.checked);
  const setupComplete = checkedSetupRows.length > 0 && checkedSetupRows.every((r) => r.mediaLotId && r.equipmentId);
  const activeReadout = readoutMedia.filter((m) => isReadoutActive(m.materialId));
  const readoutComplete = activeReadout.length > 0 && activeReadout.every((m) => observations[m.materialId]);

  return (
    <Stack spacing={2}>
      {benchMode && (phase === "setup" || phase === "readout") && (
        <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />
      )}
      {benchMode && keptOffline && (phase === "setup" || phase === "readout") && (
        <OfflineDraftNotice onTryAgain={() => void (phase === "setup" ? submitSetup() : submitReadout())} />
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {organism && <FixedMediumRow label={t("pathogenSteps.confirmatory.targetOrganism")} mediaName={organism.name} />}

      {phase === "setup" && (
        <>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("pathogenSteps.confirmatory.setupInstruction")}
          </Typography>
          {permitted.map((m) => {
            const row = setupRows[m.stepMediaId] ?? {
              entry: m, checked: false, mediaLotId: "", equipmentId: "", incubators: []
            };
            return (
              <Box
                key={m.stepMediaId}
                sx={{
                  border: "1px solid", borderColor: row.checked ? "primary.main" : "divider", borderRadius: 2,
                  p: 1.5, display: "grid", gap: 1.5
                }}
              >
                <FormControlLabel
                  sx={{ m: 0 }}
                  control={<Checkbox checked={row.checked} onChange={(e) => toggleChecked(m.stepMediaId, e.target.checked)} />}
                  label={<Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>}
                />
                <ExpectedAppearanceNote mediaName={m.mediaName} expectedAppearance={m.expectedAppearance} />
                {row.checked && (
                  <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" } }}>
                    <MediaLotIncubatorFields
                      idPrefix={`confirmatory-${m.stepMediaId}`}
                      lots={m.availableLots}
                      incubators={row.incubators}
                      tempMin={m.tempMin}
                      tempMax={m.tempMax}
                      mediaLotId={row.mediaLotId}
                      equipmentId={row.equipmentId}
                      onLotChange={(id) => setLot(m.stepMediaId, id)}
                      onIncubatorChange={(id) => setEquipment(m.stepMediaId, id)}
                    />
                  </Box>
                )}
              </Box>
            );
          })}
          {step.incubationMaxHours > 0 ? (
            <IncubationTimeline
              points={[
                { label: t("pathogenSteps.plating.start"), value: <Ltr>0 h</Ltr>, active: true },
                { label: t("pathogenSteps.plating.readyFrom"), value: <Ltr>{`${step.incubationMinHours} h`}</Ltr> },
                { label: t("pathogenSteps.plating.readBy"), value: <Ltr>{`${step.incubationMaxHours} h`}</Ltr> }
              ]}
            />
          ) : (
            <Alert severity="warning">{t("pathogenSteps.confirmatory.durationNotConfigured")}</Alert>
          )}
          <Typography variant="caption" sx={{ color: "text.secondary", mt: -1 }}>
            {t("pathogenSteps.confirmatory.longestWindowNote")}
          </Typography>
          <StepActionBar
            note={setupComplete
              ? t("pathogenSteps.confirmatory.setupNoteReady")
              : t("pathogenSteps.confirmatory.setupNoteIncomplete")}
          >
            <Button
              variant="contained"
              onClick={submitSetup}
              disabled={submitting || !setupComplete}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? t("pathogenSteps.plating.starting") : t("pathogenSteps.plating.startIncubation")}
            </Button>
          </StepActionBar>
        </>
      )}

      {phase === "waiting" && (
        <IncubationWaitingView
          testOrderId={testOrderId}
          step={step}
          current={current}
          onSubmitted={onSubmitted}
          nextLabel={t("pathogenSteps.plating.plateReading")}
          onReady={handleReady}
          renderAction={() => (
            <Button variant="contained" disabled>{t("pathogenSteps.confirmatory.submitObservations")}</Button>
          )}
        />
      )}

      {phase === "readout" && (
        <>
          {readoutUncertain ? (
            <Alert severity="warning">
              {t("pathogenSteps.confirmatory.readoutUncertain")}
            </Alert>
          ) : (
            <Alert severity="success">{t("pathogenSteps.confirmatory.readoutComplete")}</Alert>
          )}
          {readoutMedia.map((m) => {
            const checked = isReadoutActive(m.materialId);
            const value = observations[m.materialId] ?? "";
            return (
              <Box key={m.materialId} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, p: 1.5, display: "grid", gap: 1.5 }}>
                <Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>
                {readoutUncertain && (
                  <FormControlLabel
                    sx={{ m: 0 }}
                    control={<Checkbox checked={checked} onChange={(e) =>
                      setReadoutChecked((prev) => ({ ...prev, [m.materialId]: e.target.checked }))} />}
                    label={t("pathogenSteps.confirmatory.actuallyPlated")}
                  />
                )}
                <ExpectedAppearanceNote mediaName={m.mediaName} expectedAppearance={m.expectedAppearance} />
                <ObservationOptions
                  name={`confirmatory-observation-${m.materialId}`}
                  value={value}
                  disabled={!checked}
                  onChange={(v) => setObservations((prev) => ({ ...prev, [m.materialId]: v }))}
                  options={observationOptions}
                />
              </Box>
            );
          })}
          <StepActionBar note={readoutComplete ? undefined : t("pathogenSteps.confirmatory.readoutNoteIncomplete")}>
            <Button
              variant="contained"
              onClick={submitReadout}
              disabled={submitting || !readoutComplete}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? t("pathogenSteps.confirmatory.submitting") : t("pathogenSteps.confirmatory.submitObservations")}
            </Button>
          </StepActionBar>
        </>
      )}

      {phase === "decision" && (
        <>
          <Alert severity="success">
            {t("pathogenSteps.confirmatory.decisionAlert")}
          </Alert>
          <Typography variant="body2">
            {t("pathogenSteps.confirmatory.decisionPrompt")}
          </Typography>
          <StepActionBar>
            <Button
              variant="outlined" color="error" onClick={() => decide("SubmitAsDetected")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {t("pathogenSteps.confirmatory.concludeDetected")}
            </Button>
            <Button
              variant="contained" onClick={() => decide("ProceedToBiochemical")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {t("pathogenSteps.confirmatory.proceedBiochemical")}
            </Button>
          </StepActionBar>
        </>
      )}
    </Stack>
  );
}
