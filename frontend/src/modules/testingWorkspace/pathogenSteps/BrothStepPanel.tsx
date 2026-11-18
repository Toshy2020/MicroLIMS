import { useEffect, useRef, useState } from "react";
import {
  Typography, Button, Stack, Alert, AlertTitle, Box,
  List, ListItem,
  ListItemIcon, ListItemText, CircularProgress, useTheme
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import CheckIcon from "@mui/icons-material/Check";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, CurrentStepResponse, SiblingPathogenOrder
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { Ltr } from "../../../components/Ltr";
import { activeLanguage, useTranslation } from "../../../i18n";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, useKeptOffline } from "../benchSave";
import { StepActionBar, StepDetailList, IncubationTimeline, FixedMediumRow, MediaLotIncubatorFields } from "./StepLayout";

interface BrothDraftValues {
  mediaLotId: number | "";
  equipmentId: number | "";
}

function pickBrothDraft(v: BrothDraftValues): Partial<BrothDraftValues> {
  return { mediaLotId: v.mediaLotId, equipmentId: v.equipmentId };
}

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current?: CurrentStepResponse;
  onSubmitted: () => void;
}

const fmtLocale = (d: Date) =>
  d.toLocaleString(activeLanguage() === "ar" ? "ar-u-nu-latn-ca-gregory" : undefined);

// BrothEnrichment/SelectiveBroth: preparation only. The incubation
// window is server-controlled from Test Master (analyst cannot override
// it). There is deliberately no result-interpretation UI here - the chain
// runs to completion regardless of what the analyst observes here
// (the method requires it), so this must never be framed as a pass/fail
// decision point. No observation field is presented.
export function BrothStepPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const { benchMode } = useBenchMode();
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [confirmationDialog, setConfirmationDialog] = useState<{
    open: boolean;
    siblingOrders: SiblingPathogenOrder[];
    mediaLotLabel?: string;
    incubatorLabel?: string;
    incubationStart?: string;
  }>({
    open: false,
    siblingOrders: []
  });

  // Entry Point C: Check if shared TSB is already recorded for this sample.
  // Gated strictly to BrothEnrichment (primary non-selective enrichment) — never SelectiveBroth (e.g. RVS)
  const isBrothEnrichmentStep = step.stepType === "BrothEnrichment";

  // The sibling-order lookup behind the shared TSB prompt is started with the
  // step configuration instead of on click, so Start Incubation responds at
  // once. A rejection is still handled where it is awaited.
  const siblingsRequest = useRef<Promise<SiblingPathogenOrder[]> | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    if (isBrothEnrichmentStep) {
      const request = TestWorkflowService.getSiblingPathogenOrders(testOrderId);
      request.catch(() => undefined);
      siblingsRequest.current = request;
    } else {
      siblingsRequest.current = null;
    }
    TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
      .then(async (res) => {
        const only = res.permittedMedia[0] ?? null;
        setMedium(only);
        if (only) {
          const eligible = await TestWorkflowService.getEligibleIncubators(testOrderId, only.stepMediaId);
          setIncubators(eligible.eligibleIncubators);
        }
      })
      .catch((e) => setError(workflowErrorDisplayMessage(parseWorkflowError(e))))
      .finally(() => setLoading(false));
  }, [testOrderId, step.stepName, isBrothEnrichmentStep]);

  const sharedTsb = current?.sharedTsbSummary;
  const isSharedBrothComplete = current?.previousSteps?.some(
    (p) => (p.stepName === "Broth Enrichment" || p.stepName.includes("TSB")) &&
           p.isSharedSessionStep === true &&
           (p.status === "Complete" || p.status === "Incubating")
  );

  const isSharedTsbApplied = isBrothEnrichmentStep && Boolean(sharedTsb || isSharedBrothComplete);

  const draft = useBenchDraft<BrothDraftValues>({
    formKey: "pathogen-broth",
    recordKey: `${testOrderId}:${step.stepName}`,
    recordVersion: null,
    title: `${step.stepName}: #${testOrderId}`,
    values: { mediaLotId, equipmentId },
    pick: pickBrothDraft,
    enabled: benchMode && !loading && Boolean(medium) && !isSharedTsbApplied
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved) return;
    if (typeof saved.mediaLotId === "number" && medium?.availableLots.some((l) => l.id === saved.mediaLotId)) {
      setMediaLotId(saved.mediaLotId);
    }
    if (typeof saved.equipmentId === "number" && incubators.some((i) => i.id === saved.equipmentId)) {
      setEquipmentId(saved.equipmentId);
    }
  };

  const handleProceedSharedTsb = async () => {
    setError(null);
    setSaving(true);
    try {
      await TestWorkflowService.submitBroth(testOrderId, step.stepName, null, ...draftHeaderArgs(draft.submitHeaders()));
      onSubmitted();
    } catch (e: unknown) {
      const parsed = parseWorkflowError(e);
      if (parsed.message?.includes("order violation") || parsed.message?.includes("already")) {
        onSubmitted();
      } else {
        setError(workflowErrorDisplayMessage(parsed));
      }
    } finally {
      setSaving(false);
    }
  };

  if (isSharedTsbApplied) {
    // Without the server's shared TSB summary, show only what the recorded
    // incubation row says - never a made-up readiness time or completion.
    const sharedRow = current?.previousSteps?.find((p) => p.isSharedSessionStep);
    const summary = sharedTsb ?? {
      mediaLotNumber: sharedRow?.lotNumber ?? null,
      incubatorCode: sharedRow?.incubatorName ?? null,
      incubationStartUtc: sharedRow?.incubationStartUtc ?? null,
      minReadyAt: null,
      startedByUserName: t("pathogenSteps.broth.analystFallback"),
      isCompleted: sharedRow?.status === "Complete",
      windowNotConfigured: false
    };

    if (error) {
      return (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      );
    }

    return (
      <Box sx={{ p: 1 }}>
        <Alert severity="success" icon={<CheckCircleIcon />} sx={{ mb: 2 }}>
          <AlertTitle sx={{ fontWeight: 700 }}>{t("pathogenSteps.broth.sharedTsbApplied")}</AlertTitle>
          {t("pathogenSteps.broth.sharedTsbDetail")}
        </Alert>

        {summary.windowNotConfigured && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {t("pathogenSteps.windowNotConfigured")}
          </Alert>
        )}

        <Box sx={{ border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 2, p: 2, mb: 2 }}>
          <StepDetailList
            rows={[
              { label: t("pathogenSteps.broth.tsbLot"), value: summary.mediaLotNumber ? <Ltr>{summary.mediaLotNumber}</Ltr> : t("pathogenSteps.broth.standardLot") },
              { label: t("pathogenSteps.broth.incubator"), value: summary.incubatorCode ? <Ltr>{summary.incubatorCode}</Ltr> : t("pathogenSteps.broth.standardIncubator") },
              {
                label: t("pathogenSteps.broth.started"),
                value: summary.incubationStartUtc ? <Ltr>{fmtLocale(new Date(summary.incubationStartUtc))}</Ltr> : t("pathogenSteps.broth.recorded")
              },
              { label: t("pathogenSteps.broth.by"), value: <Ltr>{summary.startedByUserName}</Ltr> },
              ...(summary.minReadyAt ? [{ label: t("pathogenSteps.broth.availableFrom"), value: <Ltr>{fmtLocale(new Date(summary.minReadyAt))}</Ltr> }] : [])
            ]}
          />
        </Box>

        <StepActionBar>
          <Button
            variant="contained"
            onClick={handleProceedSharedTsb}
            disabled={saving || Boolean(summary.windowNotConfigured)}
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {saving ? t("pathogenSteps.broth.advancing") : t("pathogenSteps.broth.proceedNextStep")}
          </Button>
        </StepActionBar>
      </Box>
    );
  }

  const doStartIncubation = async () => {
    setError(null);
    setSaving(true);
    try {
      await TestWorkflowService.selectMedia(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId), ...draftHeaderArgs(draft.submitHeaders())
      );
      await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
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
      setSaving(false);
    }
  };

  const handleStartIncubation = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) {
      setError(t("pathogenSteps.broth.selectLotAndIncubator"));
      return;
    }

    // The shared-TSB-across-siblings prompt only ever applies to the true
    // Broth Enrichment step - a SelectiveBroth step (e.g. RVS, MBP) is a
    // species-specific medium selected per test order, never shared. This
    // panel is also rendered for SelectiveBroth steps, so this check must
    // gate the prompt here too, not just the "already applied" banner above.
    if (!isBrothEnrichmentStep) {
      await doStartIncubation();
      return;
    }

    try {
      const siblings = await (siblingsRequest.current ?? TestWorkflowService.getSiblingPathogenOrders(testOrderId));
      if (siblings && siblings.length > 0) {
        const selectedLot = medium.availableLots.find((l) => l.id === Number(mediaLotId));
        const selectedInc = incubators.find((i) => i.id === Number(equipmentId));

        setConfirmationDialog({
          open: true,
          siblingOrders: siblings,
          mediaLotLabel: selectedLot?.lotNumber ?? `Lot #${mediaLotId}`,
          incubatorLabel: selectedInc?.code ?? `Incubator #${equipmentId}`,
          incubationStart: fmtLocale(new Date())
        });
        return;
      }
    } catch {
      // If sibling query fails, proceed to save directly
    }

    await doStartIncubation();
  };

  const handleConfirmApplyToAll = async () => {
    setConfirmationDialog((prev) => ({ ...prev, open: false }));
    await doStartIncubation();
  };

  if (loading) return <DialogBodySkeleton blocks={3} blockHeight={48} />;
  if (error && !medium) return <Alert severity="error">{error}</Alert>;
  if (!medium) return <Alert severity="error">{t("pathogenSteps.broth.noAssignedMedium")}</Alert>;

  const canStart = Boolean(mediaLotId && equipmentId);

  return (
    <Stack spacing={2}>
      {benchMode && <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />}
      {benchMode && keptOffline && <OfflineDraftNotice onTryAgain={() => void doStartIncubation()} />}
      {error && <Alert severity="error">{error}</Alert>}

      <FixedMediumRow mediaName={medium.mediaName} />

      <MediaLotIncubatorFields
        idPrefix="broth"
        lots={medium.availableLots}
        incubators={incubators}
        tempMin={medium.tempMin}
        tempMax={medium.tempMax}
        mediaLotId={mediaLotId}
        equipmentId={equipmentId}
        onLotChange={setMediaLotId}
        onIncubatorChange={setEquipmentId}
      />

      <IncubationTimeline
        points={[
          { label: t("pathogenSteps.broth.start"), value: <Ltr>0 h</Ltr>, active: true },
          { label: t("pathogenSteps.broth.readyFrom"), value: <Ltr>{`${step.incubationMinHours} h`}</Ltr> },
          { label: t("pathogenSteps.broth.readBy"), value: <Ltr>{`${step.incubationMaxHours} h`}</Ltr> }
        ]}
      />

      <StepActionBar
        note={canStart
          ? t("pathogenSteps.broth.noteReady")
          : t("pathogenSteps.broth.noteIncomplete")}
      >
        <Button
          variant="contained"
          onClick={handleStartIncubation}
          disabled={saving || !canStart}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {saving ? t("pathogenSteps.broth.starting") : t("pathogenSteps.broth.startIncubation")}
        </Button>
      </StepActionBar>

      {/* Entry Point B — Sibling Confirmation Dialog */}
      <FloatingDialog
        open={confirmationDialog.open}
        maxWidth="sm"
        onClose={() => setConfirmationDialog((prev) => ({ ...prev, open: false }))}
        titleSx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700 }}
        title={
          <>
            <InfoOutlinedIcon sx={{ color: theme.custom.status.info.text }} />
            {t("pathogenSteps.broth.dialogTitle")}
          </>
        }
        actions={
          <>
            <Button
              variant="outlined"
              onClick={() => setConfirmationDialog((prev) => ({ ...prev, open: false }))}
            >
              {t("pathogenSteps.broth.cancel")}
            </Button>
            <Button
              variant="contained"
              onClick={handleConfirmApplyToAll}
              disabled={saving}
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <CheckIcon />}
            >
              {t("pathogenSteps.broth.confirmApplyAll")}
            </Button>
          </>
        }
      >
          <Alert severity="info" sx={{ mb: 2 }}>
            {t("pathogenSteps.broth.dialogInfo")}
          </Alert>

          {/* TSB details */}
          <Box sx={{ mb: 2, p: 1.5, backgroundColor: "background.default", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Typography variant="body2">
              <strong>{t("pathogenSteps.broth.tsbLotLabel")}</strong> <Ltr>{confirmationDialog.mediaLotLabel}</Ltr>
            </Typography>
            <Typography variant="body2">
              <strong>{t("pathogenSteps.broth.incubatorLabel")}</strong> <Ltr>{confirmationDialog.incubatorLabel}</Ltr>
            </Typography>
            <Typography variant="body2">
              <strong>{t("pathogenSteps.broth.startedLabel")}</strong> <Ltr>{confirmationDialog.incubationStart}</Ltr>
            </Typography>
          </Box>

          {/* Applies to list */}
          <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>
            {t("pathogenSteps.broth.appliesToHeading")}
          </Typography>
          <List dense sx={{ mb: 1, bgcolor: "background.default", borderRadius: 1 }}>
            {confirmationDialog.siblingOrders?.map((order) => (
              <ListItem key={order.testOrderId} sx={{ py: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 28 }}>
                  <CheckCircleOutlineIcon sx={{ fontSize: 18, color: theme.custom.status.notDetected.text }} />
                </ListItemIcon>
                <ListItemText
                  primary={<strong>{order.pathogenName}</strong>}
                  secondary={t("pathogenSteps.broth.testOrderItem", { id: order.testOrderId, code: order.testCode })}
                />
              </ListItem>
            ))}
          </List>

          {/* GMP note */}
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
            {t("pathogenSteps.broth.gmpNote")}
          </Typography>
      </FloatingDialog>
    </Stack>
  );
}
