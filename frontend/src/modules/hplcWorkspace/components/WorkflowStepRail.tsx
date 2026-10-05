import {
  Box,
  Paper,
  Typography,
  Tooltip,
  useTheme,
  Stack
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import RadioButtonCheckedIcon from "@mui/icons-material/RadioButtonChecked";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import PlayCircleFilledWhiteIcon from "@mui/icons-material/PlayCircleFilledWhite";
import type { HplcRunDto } from "../types";

export interface WorkflowStepRailProps {
  run: HplcRunDto;
  activeTab: number;
  onTabChange?: (tabIndex: number) => void;
}

interface StepDefinition {
  index: number;
  label: string;
  tabIndex: number;
  isCompleted: boolean;
  isCurrent: boolean;
  isLocked: boolean;
  isFailed?: boolean;
  lockReason?: string;
}

export function WorkflowStepRail({ run, activeTab, onTabChange }: WorkflowStepRailProps) {
  const theme = useTheme();

  const sstStatus = run.sst?.status ?? "Pending";
  const sstPassed = sstStatus === "Passed";
  const sstFailed = sstStatus === "Failed";

  const sampleCount = run.samples?.length ?? 0;
  const allSamplesSubmitted =
    sampleCount > 0 &&
    run.samples.every((s) => s.submitted || s.status === "Removed");

  // Determine steps state
  const steps: StepDefinition[] = [
    {
      index: 0,
      label: "Run Setup",
      tabIndex: 0,
      isCompleted: true,
      isCurrent: activeTab === 0,
      isLocked: false
    },
    {
      index: 1,
      label: "System Suitability",
      tabIndex: 1,
      isCompleted: sstPassed,
      isFailed: sstFailed,
      isCurrent: activeTab === 1 || (!sstPassed && !sstFailed && activeTab === 0),
      isLocked: false
    },
    {
      index: 2,
      label: "Sample Assignment",
      tabIndex: 2,
      isCompleted: sstPassed && sampleCount > 0,
      isCurrent: activeTab === 2,
      isLocked: !run.canAssignSamples,
      lockReason:
        run.canAssignSamplesReason ||
        (sstFailed
          ? "SST failed. Abandon this run and start a new run."
          : "Sample assignment is locked until system suitability passes.")
    },
    {
      index: 3,
      label: "Testing & Evidence",
      tabIndex: 3,
      isCompleted: allSamplesSubmitted,
      isCurrent: activeTab === 3,
      isLocked: !sstPassed || sampleCount === 0,
      lockReason: !sstPassed
        ? "Locked until system suitability passes."
        : sampleCount === 0
        ? "Locked until samples are assigned to the run."
        : undefined
    },
    {
      index: 4,
      label: "Review & Complete",
      tabIndex: 4,
      isCompleted: run.status === "Completed",
      isCurrent: activeTab === 4,
      isLocked: run.status === "Open" && (!allSamplesSubmitted || sampleCount === 0),
      lockReason:
        run.status === "Open" && (!allSamplesSubmitted || sampleCount === 0)
          ? "All assigned samples must be submitted before completion."
          : undefined
    }
  ];

  return (
    <Paper
      elevation={0}
      sx={{
        p: 1.5,
        mb: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Stack
        direction={{ xs: "column", md: "row" }}
        spacing={{ xs: 1, md: 0 }}
        sx={{
          alignItems: { xs: "flex-start", md: "center" },
          justifyContent: "space-between"
        }}
      >
        {steps.map((step, idx) => {
          const isClickable = !step.isLocked && onTabChange;
          // A finished step stays readable even when it can no longer be opened.
          const lockedLook = step.isLocked && !step.isCompleted;

          const stepContent = (
            <Box
              onClick={() => {
                if (isClickable) {
                  onTabChange(step.tabIndex);
                }
              }}
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1,
                cursor: isClickable ? "pointer" : "default",
                opacity: lockedLook ? 0.6 : 1,
                py: 0.5,
                px: 1.5,
                borderRadius: 1.5,
                backgroundColor: step.isCurrent
                  ? theme.palette.action.selected
                  : "transparent",
                transition: "all 0.2s ease",
                "&:hover": isClickable
                  ? {
                      backgroundColor: theme.palette.action.hover
                    }
                  : {}
              }}
            >
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  width: 28,
                  height: 28,
                  borderRadius: "50%",
                  backgroundColor: step.isFailed
                    ? theme.palette.error.light
                    : step.isCompleted
                    ? theme.palette.success.light
                    : step.isCurrent
                    ? theme.palette.primary.light
                    : theme.palette.action.disabledBackground,
                  color: step.isFailed
                    ? theme.palette.error.contrastText
                    : step.isCompleted
                    ? theme.palette.success.contrastText
                    : step.isCurrent
                    ? theme.palette.primary.contrastText
                    : theme.palette.text.disabled
                }}
              >
                {step.isFailed ? (
                  <ErrorOutlinedIcon sx={{ fontSize: 18 }} />
                ) : step.isCompleted ? (
                  <CheckCircleIcon sx={{ fontSize: 18 }} />
                ) : step.isLocked ? (
                  <LockOutlinedIcon sx={{ fontSize: 16 }} />
                ) : step.isCurrent ? (
                  <PlayCircleFilledWhiteIcon sx={{ fontSize: 18 }} />
                ) : (
                  <RadioButtonCheckedIcon sx={{ fontSize: 16 }} />
                )}
              </Box>

              <Box>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: step.isCurrent ? 700 : 500,
                    color: step.isFailed
                      ? theme.palette.error.main
                      : step.isCurrent
                      ? theme.palette.primary.main
                      : lockedLook
                      ? theme.palette.text.disabled
                      : theme.palette.text.primary,
                    lineHeight: 1.2
                  }}
                >
                  {step.label}
                </Typography>
                <Typography
                  variant="caption"
                  sx={{
                    fontSize: 12,
                    color: step.isFailed
                      ? theme.palette.error.main
                      : lockedLook
                      ? theme.palette.warning.main
                      : theme.palette.text.secondary
                  }}
                >
                  {step.isFailed
                    ? "Failed"
                    : step.isCompleted
                    ? "Completed"
                    : step.isLocked
                    ? "Locked"
                    : step.isCurrent
                    ? "Active"
                    : "Pending"}
                </Typography>
              </Box>
            </Box>
          );

          return (
            <Box
              key={step.index}
              sx={{
                display: "flex",
                alignItems: "center",
                flex: { md: 1 },
                width: { xs: "100%", md: "auto" }
              }}
            >
              {step.isLocked && step.lockReason ? (
                <Tooltip title={step.lockReason} arrow placement="top">
                  <Box sx={{ width: "100%" }}>{stepContent}</Box>
                </Tooltip>
              ) : (
                <Box sx={{ width: "100%" }}>{stepContent}</Box>
              )}

              {idx < steps.length - 1 && (
                <Box
                  sx={{
                    display: { xs: "none", md: "block" },
                    flex: 1,
                    height: 2,
                    mx: 1,
                    backgroundColor: steps[idx].isCompleted
                      ? theme.palette.success.main
                      : theme.palette.divider
                  }}
                />
              )}
            </Box>
          );
        })}
      </Stack>
    </Paper>
  );
}
