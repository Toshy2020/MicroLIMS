import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  Box,
  Typography,
  Stack,
  Stepper,
  Step,
  StepButton,
  Chip,
  Alert,
  Button,
  TextField,
  DialogActions,
  CircularProgress
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { SessionOverviewPanel } from "./SessionOverviewPanel";
import { SharedTsbEnrichmentPanel } from "./SharedTsbEnrichmentPanel";
import { DownstreamWorkflowsPanel } from "./DownstreamWorkflowsPanel";
import { PrimaryObservationMatrixPanel } from "./PrimaryObservationMatrixPanel";
import { BatchConfirmatoryPlatingPanel } from "./BatchConfirmatoryPlatingPanel";
import { SessionReviewPanel } from "./SessionReviewPanel";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { brandColors } from "../../../theme";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { FieldHint, hintDescribedBy } from "../../../components/FieldHint";
import { hints } from "../hints";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
  onSessionUpdated?: (updatedSession: PathogenTestingSessionDto) => void;
  onSessionCompleted?: () => void;
}

const STEP_KEYS = [
  "overview",              // Step 0
  "sharedTsb",             // Step 1
  "testWorkflows",         // Step 2
  "primaryMatrix",         // Step 3 (Panel A)
  "confirmatoryPlating",   // Step 4 (Panel B)
  "reviewComplete"         // Step 5 (Panel C)
] as const;

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === "object") {
    const apiErr = err as { response?: { data?: { message?: string } }; message?: string };
    return apiErr.response?.data?.message ?? apiErr.message ?? fallback;
  }
  return fallback;
}

export function PathogenSessionDialog({ open, sampleId, onClose, onSessionUpdated, onSessionCompleted }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const { permissions } = useAuth();
  // Reset discards every step record and result for the sample - Section Head only.
  const canResetSession = permissions.includes(PERMISSIONS.TEST_WORKFLOW_SUPERVISE);
  const [activeStep, setActiveStep] = useState(0);
  const [session, setSession] = useState<PathogenTestingSessionDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !sampleId) {
      setSession(null);
      setActiveStep(0);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);
    PathogenSessionService.getSession(sampleId)
      .then((data) => {
        setSession(data);
        if (data.completedResultCount > 0 && data.completedResultCount === data.requiredResultCount) {
          setActiveStep(5); // Review & Complete
        } else if (data.completedResultCount > 0 || data.resultMatrix.some((c) => c.primaryObservation)) {
          setActiveStep(3); // Primary Matrix
        } else if (data.sharedTsb.isStarted) {
          setActiveStep(1); // Shared TSB
        } else {
          setActiveStep(0);
        }
      })
      .catch((err: unknown) => {
        setError(getErrorMessage(err, t("pathogenSession.dialog.loadError")));
      })
      .finally(() => setLoading(false));
  }, [open, sampleId, t]);

  const handleSessionUpdated = (updated: PathogenTestingSessionDto) => {
    setSession(updated);
    if (onSessionUpdated) {
      onSessionUpdated(updated);
    }
  };

  const handleCompleted = (completed: PathogenTestingSessionDto) => {
    setSession(completed);
    if (onSessionCompleted) {
      onSessionCompleted();
    }
    onClose();
  };

  const [resetDialogOpen, setResetDialogOpen] = useState(false);
  const [resetReason, setResetReason] = useState("");
  const [resetting, setResetting] = useState(false);

  const handleResetSession = async () => {
    if (!sampleId) return;
    setResetting(true);
    setError(null);
    try {
      const updated = await PathogenSessionService.resetSession(sampleId, resetReason || t("pathogenSession.dialog.defaultResetReason"));
      setSession(updated);
      setActiveStep(0);
      setResetDialogOpen(false);
      setResetReason("");
      if (onSessionUpdated) {
        onSessionUpdated(updated);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("pathogenSession.dialog.resetFailed")));
    } finally {
      setResetting(false);
    }
  };

  const hasEligibleConfirmations = session
    ? session.resultMatrix.some(
        (c) =>
          c.isEligibleForConfirmation ||
          c.primaryObservation === "GrowthConforming" ||
          c.primaryObservation === "GrowthNonConforming" ||
          c.confirmationStatus === "Eligible" ||
          c.confirmationStatus === "InProgress" ||
          c.confirmationStatus === "Complete" ||
          (c.confirmatoryPlates && c.confirmatoryPlates.length > 0)
      )
    : false;

  const canReview = session &&
    session.completedResultCount === session.requiredResultCount &&
    session.requiredResultCount > 0 &&
    !session.sharedTsb.isIncubating;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="xl"
      paperSx={{
        borderRadius: 2.5,
        minHeight: "85vh",
        maxHeight: "92vh",
        bgcolor: "background.paper"
      }}
      titleSx={{
        bgcolor: brandColors.sectionTitle,
        color: "common.white",
        py: 2,
        px: 3
      }}
      title={
        <Stack
          direction="row"
          sx={{
            justifyContent: "space-between",
            alignItems: "center",
            flex: 1,
            minWidth: 0
          }}>
          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Box
              sx={{
                width: 38,
                height: 38,
                borderRadius: 2,
                bgcolor: "rgba(255,255,255,0.15)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <ScienceOutlinedIcon sx={{ fontSize: 24, color: "common.white" }} />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 17, fontWeight: 800, color: "common.white", lineHeight: 1.2 }}>
                {t("pathogenSession.dialog.title")}
              </Typography>
              {session && (
                <Typography sx={{ fontSize: 12, color: "rgba(255,255,255,0.85)", mt: 0.25 }}>
                  <Trans
                    i18nKey="pathogenSession.dialog.sessionSubtitle"
                    ns="testingWorkspace"
                    values={{
                      sessionId: session.sessionId,
                      sampleRef: session.sampleReferenceNumber,
                      department: session.departmentOrAreaName
                    }}
                    components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
                  />
                </Typography>
              )}
            </Box>
          </Stack>

          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            {session && (
              <Chip
                label={session.overallSessionStatusDisplay ?? session.overallSessionStatus}
                size="small"
                sx={{
                  bgcolor: "rgba(255,255,255,0.2)",
                  color: "common.white",
                  fontWeight: 700,
                  fontSize: 12
                }}
              />
            )}
            {session && canResetSession && (
              <Button
                variant="outlined"
                size="small"
                startIcon={<RestartAltIcon />}
                onClick={() => setResetDialogOpen(true)}
                sx={{
                  color: "common.white",
                  borderColor: "rgba(255,255,255,0.4)",
                  fontSize: 12,
                  fontWeight: 600,
                  "&:hover": { borderColor: "common.white", bgcolor: "rgba(255,255,255,0.1)" }
                }}
              >
                {t("pathogenSession.dialog.resetSession")}
              </Button>
            )}
          </Stack>
        </Stack>
      }
      subHeader={
        session && (
          <Box sx={{ px: 4, pt: 2.5, pb: 1.5, bgcolor: "background.paper" }}>
            <Stepper nonLinear activeStep={activeStep}>
              {STEP_KEYS.map((stepKey, index) => {
                const isTsbStep = index === 1;
                const hasTsb = session.assignedTests.some((testItem) => testItem.requiresTsb);
                const isConfirmatoryStep = index === 4;
                const isConfDisabled = isConfirmatoryStep && !hasEligibleConfirmations;
                const isReviewStep = index === 5;
                const isReviewDisabled = isReviewStep && !canReview;
                const isStepDisabled = isConfDisabled || isReviewDisabled;

                let stepLabelSuffix = "";
                if (isTsbStep && !hasTsb) stepLabelSuffix = t("pathogenSession.dialog.naSuffix");
                if (isConfirmatoryStep && !hasEligibleConfirmations) stepLabelSuffix = t("pathogenSession.dialog.naSuffix");

                return (
                  <Step key={stepKey} disabled={isStepDisabled}>
                    <StepButton
                      onClick={() => {
                        if (!isStepDisabled) {
                          setActiveStep(index);
                        }
                      }}
                      sx={{
                        "& .MuiStepLabel-label": {
                          fontWeight: activeStep === index ? 800 : 600,
                          fontSize: 13
                        }
                      }}
                    >
                      <Stack direction="row" spacing={0.5} sx={{
                        alignItems: "center"
                      }}>
                        <span>{t(`pathogenSession.dialog.steps.${stepKey}`)} {stepLabelSuffix}</span>
                        {isStepDisabled && <LockOutlinedIcon sx={{ fontSize: 12, color: "text.secondary" }} />}
                      </Stack>
                    </StepButton>
                  </Step>
                );
              })}
            </Stepper>
          </Box>
        )
      }
    >
      {loading && <DialogBodySkeleton blocks={4} blockHeight={56} />}
      {error && !session && <Alert severity="error">{error}</Alert>}

      {session && (
        <Box>
          {activeStep === 0 && (
            <SessionOverviewPanel
              session={session}
              onStartWorkflow={() => {
                const hasTsb = session.assignedTests.some((t) => t.requiresTsb);
                setActiveStep(hasTsb ? 1 : 2);
              }}
            />
          )}

          {activeStep === 1 && (
            <SharedTsbEnrichmentPanel
              session={session}
              onUpdated={handleSessionUpdated}
              onNext={() => setActiveStep(2)}
            />
          )}

          {activeStep === 2 && (
            <DownstreamWorkflowsPanel
              session={session}
              onNext={() => setActiveStep(3)}
            />
          )}

          {activeStep === 3 && (
            <PrimaryObservationMatrixPanel
              session={session}
              onUpdated={handleSessionUpdated}
              onNext={(skipToReview) => {
                if (skipToReview || !hasEligibleConfirmations) {
                  setActiveStep(5);
                } else {
                  setActiveStep(4);
                }
              }}
            />
          )}

          {activeStep === 4 && (
            <BatchConfirmatoryPlatingPanel
              session={session}
              onBack={() => setActiveStep(3)}
              onNext={() => setActiveStep(5)}
              onUpdated={handleSessionUpdated}
            />
          )}

          {activeStep === 5 && (
            <SessionReviewPanel
              session={session}
              onSessionCompleted={handleCompleted}
              onBackToMatrix={() => setActiveStep(3)}
            />
          )}
        </Box>
      )}

      {/* Reset Confirmation Dialog */}
      <Dialog
        open={resetDialogOpen}
        onClose={() => !resetting && setResetDialogOpen(false)}
        maxWidth="sm"
        fullWidth
        slotProps={{
          paper: { sx: { borderRadius: 2 } }
        }}
      >
        <DialogTitle sx={{ fontWeight: 700 }}>{t("pathogenSession.dialog.resetConfirmTitle")}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            <Trans
              i18nKey="pathogenSession.dialog.resetConfirmBody"
              ns="testingWorkspace"
              values={{ sampleRef: session?.sampleReferenceNumber ?? "" }}
              components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
            <TextField
              slotProps={{ input: { "aria-describedby": hintDescribedBy("pathogen-reason-for-reset") } }}
              label={t("pathogenSession.dialog.resetReasonLabel")}
              fullWidth
              size="small"
              value={resetReason}
              onChange={(e) => setResetReason(e.target.value)}
              placeholder={t("pathogenSession.dialog.resetReasonPlaceholder")}
              multiline
              rows={2}
            />
            <FieldHint id="pathogen-reason-for-reset" label={t("pathogenSession.dialog.resetReasonHintLabel")} hint={hints.reasonForReset} />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setResetDialogOpen(false)} disabled={resetting}>{t("pathogenSession.dialog.cancel")}</Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleResetSession}
            disabled={resetting || !resetReason.trim()}
            startIcon={resetting ? <CircularProgress size={16} color="inherit" /> : <RestartAltIcon />}
          >
            {resetting ? t("pathogenSession.dialog.resetting") : t("pathogenSession.dialog.confirmReset")}
          </Button>
        </DialogActions>
      </Dialog>
    </FloatingDialog>
  );
}
