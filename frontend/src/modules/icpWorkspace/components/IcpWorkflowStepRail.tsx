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
import type { IcpRunDto } from "../types";

export interface IcpWorkflowStepRailProps {
  run: IcpRunDto;
  activeTabKey: string;
  onSelectTab: (tabKey: string) => void;
}

interface StepDefinition {
  index: number;
  label: string;
  tabKey: string;
  isCompleted: boolean;
  isCurrent: boolean;
  isLocked: boolean;
  isFailed?: boolean;
  lockReason?: string;
}

export function IcpWorkflowStepRail({ run, activeTabKey, onSelectTab }: IcpWorkflowStepRailProps) {
  const theme = useTheme();

  const calConfirmed = run.calibration?.status === "Confirmed";
  const calPending = run.calibration?.status === "Pending";
  const calFailed = run.calibration?.elements?.some((e) => !e.passed) ?? false;

  const sampleCount = run.samples?.length ?? 0;
  const allSamplesProcessed =
    sampleCount > 0 && run.samples.every((s) => s.status === "Removed");

  const steps: StepDefinition[] = [
    {
      index: 0,
      label: "Run Setup",
      tabKey: "overview",
      isCompleted: true,
      isCurrent: activeTabKey === "overview",
      isLocked: false
    },
    {
      index: 1,
      label: "Calibration",
      tabKey: "calibration",
      isCompleted: calConfirmed,
      isFailed: calFailed && calPending,
      isCurrent: activeTabKey === "calibration" || (calPending && activeTabKey === "overview"),
      isLocked: false
    },
    {
      index: 2,
      label: "Samples",
      tabKey: "samples",
      isCompleted: calConfirmed && sampleCount > 0,
      isCurrent: activeTabKey === "samples",
      isLocked: !run.canAssignSamples && sampleCount === 0,
      lockReason:
        run.canAssignSamplesReason ||
        (calFailed
          ? "Calibration has failing elements. Update calibration before assigning samples."
          : "Sample assignment is locked until calibration is confirmed.")
    },
    {
      index: 3,
      label: "Testing & Evidence",
      tabKey: "evidence",
      isCompleted: allSamplesProcessed,
      isCurrent: activeTabKey === "evidence",
      isLocked: !calConfirmed || sampleCount === 0,
      lockReason: !calConfirmed
        ? "Locked until calibration is confirmed."
        : sampleCount === 0
        ? "Locked until samples are assigned to the run."
        : undefined
    },
    {
      index: 4,
      label: "Review & Complete",
      tabKey: "overview",
      isCompleted: run.status === "Completed",
      isCurrent: activeTabKey === "overview" && run.status === "Completed",
      isLocked: run.status === "Open" && sampleCount === 0,
      lockReason:
        run.status === "Open" && sampleCount === 0
          ? "Assigned samples must be processed before completing the run."
          : undefined
    }
  ];

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        mb: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "nowrap",
          overflowX: "auto",
          gap: 1,
          py: 0.5
        }}
      >
        {steps.map((step, idx) => {
          const isLast = idx === steps.length - 1;

          let icon = <RadioButtonCheckedIcon sx={{ fontSize: 20, color: "text.disabled" }} />;
          let statusColor = "text.secondary";

          if (step.isCompleted) {
            icon = <CheckCircleIcon sx={{ fontSize: 20, color: "success.main" }} />;
            statusColor = "success.main";
          } else if (step.isFailed) {
            icon = <ErrorOutlinedIcon sx={{ fontSize: 20, color: "error.main" }} />;
            statusColor = "error.main";
          } else if (step.isCurrent) {
            icon = <PlayCircleFilledWhiteIcon sx={{ fontSize: 20, color: "primary.main" }} />;
            statusColor = "primary.main";
          } else if (step.isLocked) {
            icon = <LockOutlinedIcon sx={{ fontSize: 18, color: "text.disabled" }} />;
            statusColor = "text.disabled";
          }

          const stepButton = (
            <Box
              key={step.index}
              onClick={() => {
                if (!step.isLocked) {
                  onSelectTab(step.tabKey);
                }
              }}
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.25,
                px: 1.5,
                py: 0.75,
                borderRadius: 1.5,
                cursor: step.isLocked ? "not-allowed" : "pointer",
                backgroundColor: step.isCurrent
                  ? theme.palette.mode === "dark"
                    ? "rgba(144, 202, 249, 0.08)"
                    : "rgba(25, 118, 210, 0.06)"
                  : "transparent",
                border: step.isCurrent
                  ? `1px solid ${theme.palette.primary.light}`
                  : "1px solid transparent",
                transition: "all 0.15s ease",
                "&:hover": {
                  backgroundColor: step.isLocked
                    ? "transparent"
                    : theme.palette.action.hover
                },
                flexShrink: 0
              }}
            >
              {icon}
              <Box>
                <Typography
                  variant="caption"
                  sx={{
                    display: "block",
                    fontWeight: 700,
                    fontSize: 10,
                    textTransform: "uppercase",
                    color: statusColor,
                    letterSpacing: 0.5
                  }}
                >
                  Step {step.index + 1}
                </Typography>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: step.isCurrent ? 700 : 600,
                    fontSize: 13,
                    color: step.isLocked ? "text.disabled" : "text.primary",
                    whiteSpace: "nowrap"
                  }}
                >
                  {step.label}
                </Typography>
              </Box>
            </Box>
          );

          return (
            <Stack key={step.index} direction="row" spacing={1} sx={{ alignItems: "center", flexShrink: 0 }}>
              {step.isLocked && step.lockReason ? (
                <Tooltip title={step.lockReason} arrow placement="bottom">
                  {stepButton}
                </Tooltip>
              ) : (
                stepButton
              )}

              {!isLast && (
                <Box
                  sx={{
                    width: { xs: 16, sm: 24, md: 36 },
                    height: 2,
                    backgroundColor: step.isCompleted
                      ? theme.palette.success.light
                      : theme.palette.divider,
                    transition: "background-color 0.2s ease"
                  }}
                />
              )}
            </Stack>
          );
        })}
      </Box>
    </Paper>
  );
}
