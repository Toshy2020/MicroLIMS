import React from "react";
import { Box, useTheme } from "@mui/material";
import HourglassEmptyOutlinedIcon from "@mui/icons-material/HourglassEmptyOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import EventBusyOutlinedIcon from "@mui/icons-material/EventBusyOutlined";
import DeleteOutlineOutlinedIcon from "@mui/icons-material/DeleteOutlineOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import type { StatusTone } from "../../../theme/statusTokens";
import type { SolutionPreparationStatus } from "../types";

interface Props {
  status: SolutionPreparationStatus | string;
  size?: "small" | "medium";
}

interface StatusMeta {
  label: string;
  tone: StatusTone;
  icon: React.ReactElement;
}

export function PreparationStatusBadge({ status, size = "small" }: Props) {
  const theme = useTheme();

  const meta: Record<SolutionPreparationStatus, StatusMeta> = {
    InProgress: {
      label: "In Progress",
      tone: "info",
      icon: <HourglassEmptyOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
    },
    Prepared: {
      label: "Prepared",
      tone: "notDetected",
      icon: <CheckCircleOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
    },
    Expired: {
      label: "Expired",
      tone: "detected",
      icon: <EventBusyOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
    },
    Discarded: {
      label: "Discarded",
      tone: "action",
      icon: <DeleteOutlineOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
    },
    Cancelled: {
      label: "Cancelled",
      tone: "pending",
      icon: <CancelOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
    }
  };

  const current = meta[status as SolutionPreparationStatus] ?? {
    label: String(status),
    tone: "pending" as StatusTone,
    icon: <HourglassEmptyOutlinedIcon sx={{ fontSize: size === "small" ? 13 : 15, mr: 0.5 }} />
  };

  const tokens = theme.custom.status[current.tone];

  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        px: size === "small" ? 1 : 1.25,
        py: size === "small" ? 0.25 : 0.5,
        borderRadius: 5,
        fontSize: size === "small" ? 11 : 12,
        fontWeight: 700,
        color: tokens.text,
        bgcolor: tokens.bg,
        border: `1px solid ${tokens.border}`,
        lineHeight: 1.4,
        whiteSpace: "nowrap"
      }}
    >
      {current.icon}
      {current.label}
    </Box>
  );
}
