import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  Box,
  Typography,
  Stack,
  IconButton,
  Stepper,
  Step,
  StepButton,
  Chip,
  Alert,
  Paper,
  Button
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { SessionOverviewPanel } from "./SessionOverviewPanel";
import { SharedTsbEnrichmentPanel } from "./SharedTsbEnrichmentPanel";
import { DownstreamWorkflowsPanel } from "./DownstreamWorkflowsPanel";
import { PrimaryObservationMatrixPanel } from "./PrimaryObservationMatrixPanel";
import { BatchConfirmatoryPlatingPanel } from "./BatchConfirmatoryPlatingPanel";
import { SessionReviewPanel } from "./SessionReviewPanel";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { brandColors } from "../../../theme";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
  onSessionUpdated?: (updatedSession: PathogenTestingSessionDto) => void;
  onSessionCompleted?: () => void;
}

const STEPS = [
  "Overview",              // Step 0
  "Shared TSB",            // Step 1
  "Test Workflows",        // Step 2
  "Primary Matrix",        // Step 3 (Panel A)
  "Confirmatory Plating",  // Step 4 (Panel B)
  "Review & Complete"      // Step 5 (Panel C)
];

export function PathogenSessionDialog({ open, sampleId, onClose, onSessionUpdated, onSessionCompleted }: Props) {
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
      .catch((err) => {
        setError(err?.response?.data?.message ?? "Could not load pathogen testing session.");
      })
      .finally(() => setLoading(false));
  }, [open, sampleId]);

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

  const hasEligibleConfirmations = session
    ? session.resultMatrix.some(
        (c) =>
          c.isEligibleForConfirmation ||
          c.primaryObservation === "GrowthConforming" ||
          c.primaryObservation === "GrowthNonConforming" ||
          c.confirmationStatus === "Eligible" ||
          c.confirmationStatus === "InProgress" ||
          c.confirmationStatus === "Completed"
      )
    : false;

  const canReview = session &&
    session.completedResultCount === session.requiredResultCount &&
    session.requiredResultCount > 0 &&
    !session.sharedTsb.isIncubating;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="xl"
      fullWidth
      PaperProps={{
        sx: {
          borderRadius: 2.5,
          minHeight: "85vh",
          maxHeight: "92vh",
          bgcolor: "#fbfbfe"
        }
      }}
    >
      {/* Branded Purple Dialog Title */}
      <DialogTitle
        sx={{
          bgcolor: brandColors.sectionTitle,
          color: "#ffffff",
          py: 2,
          px: 3
        }}
      >
        <Stack direction="row" justifyContent="space-between" alignItems="center">
          <Stack direction="row" spacing={1.5} alignItems="center">
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
              <ScienceOutlinedIcon sx={{ fontSize: 24, color: "#ffffff" }} />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 17, fontWeight: 800, color: "#ffffff", lineHeight: 1.2 }}>
                Pathogen Testing Session Workspace
              </Typography>
              {session && (
                <Typography sx={{ fontSize: 12, color: "rgba(255,255,255,0.85)", mt: 0.25 }}>
                  Session: <strong>{session.sessionId}</strong> · Sample: <strong>{session.sampleReferenceNumber}</strong> ({session.departmentOrAreaName})
                </Typography>
              )}
            </Box>
          </Stack>

          <Stack direction="row" spacing={1.5} alignItems="center">
            {session && (
              <Chip
                label={session.overallSessionStatusDisplay ?? session.overallSessionStatus}
                size="small"
                sx={{
                  bgcolor: "rgba(255,255,255,0.2)",
                  color: "#ffffff",
                  fontWeight: 700,
                  fontSize: 12
                }}
              />
            )}
            <IconButton onClick={onClose} sx={{ color: "#ffffff" }} size="small">
              <CloseIcon />
            </IconButton>
          </Stack>
        </Stack>
      </DialogTitle>

      {/* Navigation Stepper with Step-Gating */}
      {session && (
        <Box sx={{ px: 4, pt: 2.5, pb: 1.5, bgcolor: "#ffffff", borderBottom: "1px solid #e5e7eb" }}>
          <Stepper nonLinear activeStep={activeStep}>
            {STEPS.map((label, index) => {
              const isTsbStep = index === 1;
              const hasTsb = session.assignedTests.some((t) => t.requiresTsb);
              const isConfirmatoryStep = index === 4;
              const isConfDisabled = isConfirmatoryStep && !hasEligibleConfirmations;
              const isReviewStep = index === 5;
              const isReviewDisabled = isReviewStep && !canReview;
              const isStepDisabled = isConfDisabled || isReviewDisabled;

              let stepLabelSuffix = "";
              if (isTsbStep && !hasTsb) stepLabelSuffix = "(N/A)";
              if (isConfirmatoryStep && !hasEligibleConfirmations) stepLabelSuffix = "(N/A)";

              return (
                <Step key={label} disabled={isStepDisabled}>
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
                    <Stack direction="row" spacing={0.5} alignItems="center">
                      <span>{label} {stepLabelSuffix}</span>
                      {isStepDisabled && <LockOutlinedIcon sx={{ fontSize: 12, color: "#9ca3af" }} />}
                    </Stack>
                  </StepButton>
                </Step>
              );
            })}
          </Stepper>
        </Box>
      )}

      {/* Dialog Body */}
      <DialogContent sx={{ p: 3 }}>
        {loading && <LoadingSpinner />}
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
      </DialogContent>
    </Dialog>
  );
}
