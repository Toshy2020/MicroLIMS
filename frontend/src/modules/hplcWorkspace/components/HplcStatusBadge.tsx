import { Chip, ChipProps } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import PlayCircleOutlinedIcon from "@mui/icons-material/PlayCircleOutlined";
import HourglassTopIcon from "@mui/icons-material/HourglassTop";
import ScheduleIcon from "@mui/icons-material/Schedule";
import CancelIcon from "@mui/icons-material/Cancel";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import BlockIcon from "@mui/icons-material/Block";
import AssignmentIcon from "@mui/icons-material/Assignment";
import RemoveCircleOutlinedIcon from "@mui/icons-material/RemoveCircleOutlined";
import HelpOutlinedIcon from "@mui/icons-material/HelpOutlined";

export interface HplcStatusBadgeProps {
  status: string;
  label?: string;
  size?: "small" | "medium";
  variant?: "filled" | "outlined";
}

interface BadgeConfig {
  label: string;
  color: ChipProps["color"];
  icon: React.ReactElement;
}

function getBadgeConfig(status: string, customLabel?: string): BadgeConfig {
  const norm = status.trim().toLowerCase();

  switch (norm) {
    // Run status
    case "open":
      return {
        label: customLabel ?? "Open",
        color: "primary",
        icon: <HourglassTopIcon fontSize="small" />
      };
    case "completed":
      return {
        label: customLabel ?? "Completed",
        color: "success",
        icon: <CheckCircleIcon fontSize="small" />
      };
    case "abandoned":
      return {
        label: customLabel ?? "Abandoned",
        color: "default",
        icon: <CancelIcon fontSize="small" />
      };

    // SST status
    case "pending":
      return {
        label: customLabel ?? "Pending",
        color: "warning",
        icon: <ScheduleIcon fontSize="small" />
      };
    case "passed":
      return {
        label: customLabel ?? "Passed",
        color: "success",
        icon: <CheckCircleIcon fontSize="small" />
      };
    case "failed":
      return {
        label: customLabel ?? "Failed",
        color: "error",
        icon: <ErrorOutlinedIcon fontSize="small" />
      };

    // Instrument state
    case "available":
      return {
        label: customLabel ?? "Available",
        color: "success",
        icon: <CheckCircleOutlinedIcon fontSize="small" />
      };
    case "running":
      return {
        label: customLabel ?? "Running",
        color: "info",
        icon: <PlayCircleOutlinedIcon fontSize="small" />
      };
    case "unavailable":
      return {
        label: customLabel ?? "Unavailable",
        color: "error",
        icon: <BlockIcon fontSize="small" />
      };

    // Sample status
    case "assigned":
      return {
        label: customLabel ?? "Assigned",
        color: "primary",
        icon: <AssignmentIcon fontSize="small" />
      };
    case "removed":
      return {
        label: customLabel ?? "Removed",
        color: "default",
        icon: <RemoveCircleOutlinedIcon fontSize="small" />
      };

    default:
      return {
        label: customLabel ?? status,
        color: "default",
        icon: <HelpOutlinedIcon fontSize="small" />
      };
  }
}

export function HplcStatusBadge({
  status,
  label,
  size = "small",
  variant = "filled"
}: HplcStatusBadgeProps) {
  const config = getBadgeConfig(status, label);

  return (
    <Chip
      size={size}
      variant={variant}
      color={config.color}
      icon={config.icon}
      label={config.label}
      sx={{
        fontWeight: 600,
        "& .MuiChip-icon": {
          fontSize: size === "small" ? 16 : 18
        }
      }}
    />
  );
}
