import { useState } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  Paper,
  Tabs,
  Tab,
  Chip,
  Alert,
  Stepper,
  Step,
  StepLabel,
  StepContent,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { PathogenTestingSessionDto, SessionAssignedTestDto } from "../types/pathogenSessionTypes";

interface Props {
  session: PathogenTestingSessionDto;
  onNext: () => void;
}

export function DownstreamWorkflowsPanel({ session, onNext }: Props) {
  const theme = useTheme();
  const [activeTab, setActiveTab] = useState(0);

  const selectedTest: SessionAssignedTestDto | undefined = session.assignedTests[activeTab];
  const isTsbIncubating = session.sharedTsb.isIncubating;

  return (
    <Stack spacing={3}>
      {isTsbIncubating && (
        <Alert severity="warning" icon={<LockOutlinedIcon />} sx={{ fontWeight: 600 }}>
          Shared TSB Enrichment incubation is currently in progress. Downstream pathogen workflow steps and final result entry are strictly locked until TSB incubation completes.
        </Alert>
      )}

      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ sm: "center" }} spacing={2} sx={{ mb: 2 }}>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
              Assigned Test Workflows ({session.assignedTests.length})
            </Typography>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Procedural workflow definitions configured from Test Master. Executed once per test for the session.
            </Typography>
          </Box>

          <Button
            variant="contained"
            color="primary"
            endIcon={<ArrowForwardIcon />}
            onClick={onNext}
            sx={{ px: 3, fontWeight: 700 }}
          >
            Go to Result Matrix
          </Button>
        </Stack>

        {/* Test Tabs */}
        <Tabs
          value={activeTab}
          onChange={(_, val) => setActiveTab(val)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{
            borderBottom: "1px solid",
            borderBottomColor: "divider",
            "& .MuiTab-root": {
              fontWeight: 700,
              textTransform: "none",
              fontSize: 13
            }
          }}
        >
          {session.assignedTests.map((t) => (
            <Tab
              key={t.testCode}
              label={
                <Stack direction="row" spacing={1} alignItems="center">
                  <span>{t.testCode}</span>
                  {t.requiresTsb && (
                    <Chip
                      label="TSB"
                      size="small"
                      sx={{ fontSize: 10, height: 16, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text }}
                    />
                  )}
                </Stack>
              }
            />
          ))}
        </Tabs>

        {/* Selected Test Detail */}
        {selectedTest && (
          <Box sx={{ mt: 3 }}>
            <Box
              sx={{
                p: 2,
                mb: 3,
                bgcolor: "background.default",
                borderRadius: 2,
                border: "1px solid",
                borderColor: "divider",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center"
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
                  {selectedTest.displayName} ({selectedTest.testCode})
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  Workflow Model: <strong>{selectedTest.workflowType}</strong> · Operational Scope: <strong>{session.totalLocations} Sampling Locations</strong>
                </Typography>
              </Box>
              <Chip
                label={selectedTest.testSessionStateDisplay}
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: selectedTest.testSessionState === "TSB_INCUBATING" ? theme.custom.status.info.bg : theme.custom.status.purple.bg,
                  color: selectedTest.testSessionState === "TSB_INCUBATING" ? theme.custom.status.info.text : theme.custom.status.purple.text
                }}
              />
            </Box>

            {/* Workflow Steps Vertical Stepper */}
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.secondary", mb: 2, textTransform: "uppercase" }}>
              Configured Step Sequence ({selectedTest.steps.length} Steps)
            </Typography>

            <Box sx={{ maxWidth: 800 }}>
              <Stepper orientation="vertical" nonLinear>
                {selectedTest.steps.map((s, sIdx) => {
                  const isTsbStep = sIdx === 0 && selectedTest.requiresTsb;
                  const isStepDone = s.isCompleted || (isTsbStep && session.sharedTsb.isCompleted);
                  const isStepLocked = isTsbIncubating && !isTsbStep;

                  return (
                    <Step key={s.stepName} active={true} completed={isStepDone}>
                      <StepLabel
                        StepIconComponent={() => (
                          <Box
                            sx={{
                              width: 28,
                              height: 28,
                              borderRadius: "50%",
                              // done/locked stay solid (mode-invariant) fills - they're a
                              // deliberately "filled" state vs. pending's outline-style
                              // treatment below, which does need a dark-aware pale token.
                              bgcolor: isStepDone ? "#059669" : isStepLocked ? "#64748b" : theme.custom.status.pending.bg,
                              color: isStepDone || isStepLocked ? "#ffffff" : "text.secondary",
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "center",
                              fontSize: 12,
                              fontWeight: 700
                            }}
                          >
                            {isStepDone ? (
                              <CheckCircleIcon sx={{ fontSize: 18 }} />
                            ) : isStepLocked ? (
                              <LockOutlinedIcon sx={{ fontSize: 15 }} />
                            ) : (
                              s.stepOrder
                            )}
                          </Box>
                        )}
                      >
                        <Stack direction="row" spacing={1.5} alignItems="center">
                          <Typography sx={{ fontSize: 14, fontWeight: 700, color: isStepLocked ? "text.secondary" : "text.primary" }}>
                            {s.stepName}
                          </Typography>
                          <Chip
                            label={s.stepType}
                            size="small"
                            sx={{ fontSize: 11, height: 20, bgcolor: theme.custom.status.pending.bg, color: theme.custom.status.pending.text }}
                          />
                          {isTsbStep && (
                            <Chip
                              label={isTsbIncubating ? "TSB Incubating" : "Shared Session Step"}
                              size="small"
                              sx={{
                                fontSize: 10,
                                height: 18,
                                bgcolor: isTsbIncubating ? theme.custom.status.info.bg : theme.custom.status.purple.bg,
                                color: isTsbIncubating ? theme.custom.status.info.text : theme.custom.status.purple.text,
                                fontWeight: 700
                              }}
                            />
                          )}
                          {isStepLocked && (
                            <Chip
                              icon={<LockOutlinedIcon sx={{ fontSize: 12 }} />}
                              label="Locked until TSB Complete"
                              size="small"
                              sx={{ fontSize: 10, height: 18, bgcolor: theme.custom.status.detected.bg, color: theme.custom.status.detected.text, fontWeight: 600 }}
                            />
                          )}
                        </Stack>
                      </StepLabel>
                      <StepContent>
                        <Box sx={{ p: 2, my: 1, bgcolor: isStepLocked ? "background.default" : "background.paper", borderRadius: 1.5, border: "1px solid", borderColor: "divider", boxShadow: "0 1px 2px rgba(0,0,0,0.03)" }}>
                          <Box
                            sx={{
                              display: "grid",
                              gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
                              gap: 2
                            }}
                          >
                            <Box>
                              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 600 }}>
                                Incubation Parameters
                              </Typography>
                              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                                {s.temperatureMin} – {s.temperatureMax} °C ({s.incubationMinHours}–{s.incubationMaxHours} h)
                              </Typography>
                            </Box>
                            <Box>
                              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 600 }}>
                                Target Medium
                              </Typography>
                              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                                {s.mediaTypeName ?? "Standard Culture Media"}
                              </Typography>
                            </Box>
                            <Box>
                              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 600 }}>
                                Execution State
                              </Typography>
                              <Typography
                                sx={{
                                  fontSize: 13,
                                  fontWeight: 700,
                                  color: isStepDone ? "#059669" : isStepLocked ? theme.custom.status.detected.text : "text.secondary"
                                }}
                              >
                                {isStepDone
                                  ? "✓ Executed & Recorded"
                                  : isStepLocked
                                  ? "🔒 Locked (TSB Incubating)"
                                  : "Pending Matrix Evaluation"}
                              </Typography>
                            </Box>
                          </Box>
                        </Box>
                      </StepContent>
                    </Step>
                  );
                })}
              </Stepper>
            </Box>
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
