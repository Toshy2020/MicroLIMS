import { useEffect, useState } from "react";
import {
  Typography, Select, MenuItem, Button, Stack, Alert, AlertTitle, Box,
  List, ListItem,
  ListItemIcon, ListItemText, CircularProgress, useTheme
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import CheckIcon from "@mui/icons-material/Check";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, CurrentStepResponse, SiblingPathogenOrder
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { FloatingDialog } from "../../../components/FloatingDialog";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current?: CurrentStepResponse;
  onSubmitted: () => void;
}

// BrothEnrichment/SelectiveBroth: preparation only. The incubation
// window is server-controlled from Test Master (analyst cannot override
// it). There is deliberately no result-interpretation UI here - the chain
// runs to completion regardless of what the analyst observes here
// (the method requires it), so this must never be framed as a pass/fail
// decision point. No observation field is presented.
export function BrothStepPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const theme = useTheme();
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

  useEffect(() => {
    setLoading(true);
    setError(null);
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
  }, [testOrderId, step.stepName]);

  // Entry Point C: Check if shared TSB is already recorded for this sample.
  // Gated strictly to BrothEnrichment (primary non-selective enrichment) — never SelectiveBroth (e.g. RVS)
  const isBrothEnrichmentStep = step.stepType === "BrothEnrichment";
  const sharedTsb = current?.sharedTsbSummary;
  const isSharedBrothComplete = current?.previousSteps?.some(
    (p) => (p.stepName === "Broth Enrichment" || p.stepName.includes("TSB")) &&
           p.isSharedSessionStep === true &&
           (p.status === "Complete" || p.status === "Incubating")
  );

  const isSharedTsbApplied = isBrothEnrichmentStep && Boolean(sharedTsb || isSharedBrothComplete);

  const handleProceedSharedTsb = async () => {
    setError(null);
    setSaving(true);
    try {
      await TestWorkflowService.submitBroth(testOrderId, step.stepName, null);
      onSubmitted();
    } catch (e: any) {
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
    const summary = sharedTsb ?? {
      mediaLotNumber: current?.previousSteps?.find((p) => p.isSharedSessionStep)?.lotNumber ?? "TSB Lot",
      incubatorCode: current?.previousSteps?.find((p) => p.isSharedSessionStep)?.incubatorName ?? "INC",
      incubationStartUtc: current?.previousSteps?.find((p) => p.isSharedSessionStep)?.incubationStartUtc ?? new Date().toISOString(),
      minReadyAt: current?.previousSteps?.find((p) => p.isSharedSessionStep)?.incubationEndUtc ?? new Date().toISOString(),
      startedByUserName: "Analyst",
      isCompleted: true
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
          <AlertTitle sx={{ fontWeight: 700 }}>Shared TSB Applied</AlertTitle>
          This test is linked to the shared TSB for this sample. There is one shared tube per sample — no separate broth entry required.
        </Alert>

        <Box sx={{ backgroundColor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1.5, p: 2, mb: 2 }}>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <strong>TSB Lot:</strong> {summary.mediaLotNumber ?? "Standard Lot"}
          </Typography>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <strong>Incubator:</strong> {summary.incubatorCode ?? "Standard Incubator"}
          </Typography>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <strong>Started:</strong> {summary.incubationStartUtc ? new Date(summary.incubationStartUtc).toLocaleString() : "Recorded"}
          </Typography>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <strong>By:</strong> {summary.startedByUserName}
          </Typography>
          {summary.minReadyAt && (
            <Typography variant="body2" sx={{ color: "text.secondary", mt: 1 }}>
              Available from: {new Date(summary.minReadyAt).toLocaleString()}
            </Typography>
          )}
        </Box>

        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            onClick={handleProceedSharedTsb}
            disabled={saving}
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {saving ? "Advancing..." : "Proceed to Next Step →"}
          </Button>
        </Box>
      </Box>
    );
  }

  const doStartIncubation = async () => {
    setError(null);
    setSaving(true);
    try {
      await TestWorkflowService.selectMedia(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId)
      );
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSaving(false);
    }
  };

  const handleStartIncubation = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) {
      setError("Select a media lot and an incubator.");
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
      const siblings = await TestWorkflowService.getSiblingPathogenOrders(testOrderId);
      if (siblings && siblings.length > 0) {
        const selectedLot = medium.availableLots.find((l) => l.id === Number(mediaLotId));
        const selectedInc = incubators.find((i) => i.id === Number(equipmentId));

        setConfirmationDialog({
          open: true,
          siblingOrders: siblings,
          mediaLotLabel: selectedLot?.lotNumber ?? `Lot #${mediaLotId}`,
          incubatorLabel: selectedInc?.code ?? `Incubator #${equipmentId}`,
          incubationStart: new Date().toLocaleString()
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

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;
  if (error && !medium) return <Alert severity="error">{error}</Alert>;
  if (!medium) return <Alert severity="error">This step has no assigned medium configured in Test Master.</Alert>;

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      <Alert severity="info">
        This step is preparation only - it is not a pass/fail result. The workflow proceeds to the next step once
        the assigned incubation window completes.
      </Alert>
      <Typography variant="body2">Medium: <strong>{medium.mediaName}</strong></Typography>
      <Select displayEmpty size="small" value={mediaLotId} onChange={(e) => setMediaLotId(Number(e.target.value))}>
        <MenuItem value=""><em>Media Lot</em></MenuItem>
        {medium.availableLots.map((l) => (
          <MenuItem key={l.id} value={l.id}>{l.lotNumber} — expires {new Date(l.expiryDate).toLocaleDateString()}</MenuItem>
        ))}
      </Select>
      <Select displayEmpty size="small" value={equipmentId} onChange={(e) => setEquipmentId(Number(e.target.value))}>
        <MenuItem value=""><em>Incubator ({medium.tempMin}-{medium.tempMax} °C)</em></MenuItem>
        {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setTemperature}°C</MenuItem>)}
      </Select>
      <Typography variant="body2">
        <strong>Assigned Incubation:</strong> {step.incubationMinHours}–{step.incubationMaxHours} h
      </Typography>
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" onClick={handleStartIncubation} disabled={saving}>
          {saving ? "Starting..." : "Start Incubation"}
        </Button>
      </Stack>

      {/* Entry Point B — Sibling Confirmation Dialog */}
      <FloatingDialog
        open={confirmationDialog.open}
        maxWidth="sm"
        onClose={() => setConfirmationDialog((prev) => ({ ...prev, open: false }))}
        titleSx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700 }}
        title={
          <>
            <InfoOutlinedIcon sx={{ color: theme.custom.status.info.text }} />
            Apply Shared TSB to All Pathogen Tests
          </>
        }
        actions={
          <>
            <Button
              variant="outlined"
              onClick={() => setConfirmationDialog((prev) => ({ ...prev, open: false }))}
            >
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleConfirmApplyToAll}
              disabled={saving}
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <CheckIcon />}
            >
              Confirm & Apply to All
            </Button>
          </>
        }
      >
          <Alert severity="info" sx={{ mb: 2 }}>
            One TSB tube is shared across all pathogen tests for this sample.
            There is one physical shared tube per sample — no per-test variation is permitted.
          </Alert>

          {/* TSB details */}
          <Box sx={{ mb: 2, p: 1.5, backgroundColor: "background.default", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Typography variant="body2">
              <strong>TSB Lot:</strong> {confirmationDialog.mediaLotLabel}
            </Typography>
            <Typography variant="body2">
              <strong>Incubator:</strong> {confirmationDialog.incubatorLabel}
            </Typography>
            <Typography variant="body2">
              <strong>Started:</strong> {confirmationDialog.incubationStart}
            </Typography>
          </Box>

          {/* Applies to list */}
          <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>
            Applies to all pathogen tests for this sample:
          </Typography>
          <List dense sx={{ mb: 1, bgcolor: "background.default", borderRadius: 1 }}>
            {confirmationDialog.siblingOrders?.map((order) => (
              <ListItem key={order.testOrderId} sx={{ py: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 28 }}>
                  <CheckCircleOutlineIcon sx={{ fontSize: 18, color: theme.custom.status.notDetected.text }} />
                </ListItemIcon>
                <ListItemText
                  primary={<strong>{order.pathogenName}</strong>}
                  secondary={`Test Order #${order.testOrderId} (${order.testCode})`}
                />
              </ListItem>
            ))}
          </List>

          {/* GMP note */}
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
            This action is recorded in the audit trail per ALCOA+ contemporaneous recording requirements.
          </Typography>
      </FloatingDialog>
    </Stack>
  );
}
