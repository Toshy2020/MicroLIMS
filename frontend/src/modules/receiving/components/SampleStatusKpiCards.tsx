import React from "react";
import { Box, Paper, Typography, Grid, useTheme } from "@mui/material";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { SampleRecord } from "../types/receivingTypes";
import { StatusTone } from "../../../theme/statusTokens";

export type KpiFilterKey =
  | "ALL"
  | "InTesting"
  | "PendingReview"
  | "Approved"
  | "Rejected"
  | "CancelledVoided";

interface Props {
  samples: SampleRecord[];
  activeKpi: KpiFilterKey | null;
  onSelectKpi: (kpi: KpiFilterKey) => void;
}

interface KpiCardConfig {
  key: KpiFilterKey;
  label: string;
  count: number;
  icon: React.ReactNode;
  tone: StatusTone;
}

export function SampleStatusKpiCards({ samples, activeKpi, onSelectKpi }: Props) {
  const theme = useTheme();
  const totalCount = samples.length;
  const underTestingCount = samples.filter((s) => s.status === "InTesting").length;
  const pendingReviewCount = samples.filter(
    (s) => s.status === "UnderReview" || s.status === "UnderApproval" || s.status === "PendingReview"
  ).length;
  const approvedCount = samples.filter((s) => s.status === "Approved").length;
  const rejectedCount = samples.filter((s) => s.status === "Rejected").length;
  const cancelledVoidedCount = samples.filter(
    (s) => s.status === "RetestRequested" || s.status === "Cancelled" || s.status === "Voided"
  ).length;

  const cards: KpiCardConfig[] = [
    { key: "ALL", label: "Total Samples", count: totalCount, icon: <Inventory2OutlinedIcon sx={{ fontSize: 20 }} />, tone: "purple" },
    { key: "InTesting", label: "Under Testing", count: underTestingCount, icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />, tone: "info" },
    { key: "PendingReview", label: "Pending Review", count: pendingReviewCount, icon: <RateReviewOutlinedIcon sx={{ fontSize: 20 }} />, tone: "action" },
    { key: "Approved", label: "Approved", count: approvedCount, icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />, tone: "notDetected" },
    { key: "Rejected", label: "Rejected", count: rejectedCount, icon: <CancelOutlinedIcon sx={{ fontSize: 20 }} />, tone: "detected" },
    { key: "CancelledVoided", label: "Cancelled / Voided", count: cancelledVoidedCount, icon: <BlockOutlinedIcon sx={{ fontSize: 20 }} />, tone: "pending" }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {cards.map((card) => {
        const isActive = activeKpi === card.key || (card.key === "ALL" && !activeKpi);
        const iconTokens = theme.custom.status[card.tone];
        const activeTokens = theme.custom.status.purple;
        return (
          <Grid item xs={6} sm={4} md={2} key={card.key}>
            <Paper
              elevation={isActive ? 2 : 0}
              onClick={() => onSelectKpi(card.key)}
              sx={{
                p: 1.75,
                borderRadius: 2,
                cursor: "pointer",
                border: isActive ? `2px solid ${activeTokens.border}` : "1px solid",
                borderColor: isActive ? activeTokens.border : "divider",
                bgcolor: isActive ? activeTokens.bg : "background.paper",
                transition: "all 0.15s ease-in-out",
                display: "flex",
                flexDirection: "column",
                justifyContent: "space-between",
                minHeight: 88,
                position: "relative",
                overflow: "hidden",
                "&:hover": {
                  borderColor: activeTokens.border,
                  boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
                  transform: "translateY(-1px)"
                }
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 0.75 }}>
                <Typography
                  sx={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: isActive ? activeTokens.text : "text.secondary",
                    lineHeight: 1.2
                  }}
                >
                  {card.label}
                </Typography>
                <Box
                  sx={{
                    color: iconTokens.text,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 28,
                    height: 28,
                    borderRadius: 1.5,
                    bgcolor: iconTokens.bg
                  }}
                >
                  {card.icon}
                </Box>
              </Box>

              <Box sx={{ display: "flex", alignItems: "baseline", gap: 0.5 }}>
                <Typography
                  sx={{
                    fontSize: 22,
                    fontWeight: 700,
                    color: isActive ? activeTokens.text : "text.primary",
                    lineHeight: 1
                  }}
                >
                  {card.count}
                </Typography>
                {isActive && card.key !== "ALL" && (
                  <Typography sx={{ fontSize: 11, color: activeTokens.text, fontWeight: 600 }}>
                    Active
                  </Typography>
                )}
              </Box>
            </Paper>
          </Grid>
        );
      })}
    </Grid>
  );
}
