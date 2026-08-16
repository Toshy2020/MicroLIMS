import React from "react";
import { Box, Paper, Typography, Grid } from "@mui/material";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { SampleRecord } from "../types/receivingTypes";
import { brandColors } from "../../../theme";

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
  accentColor: string;
  bgTint: string;
}

export function SampleStatusKpiCards({ samples, activeKpi, onSelectKpi }: Props) {
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
    {
      key: "ALL",
      label: "Total Samples",
      count: totalCount,
      icon: <Inventory2OutlinedIcon sx={{ fontSize: 20 }} />,
      accentColor: brandColors.pageTitle,
      bgTint: "#fbf8fc"
    },
    {
      key: "InTesting",
      label: "Under Testing",
      count: underTestingCount,
      icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />,
      accentColor: "#2563eb",
      bgTint: "#eff6ff"
    },
    {
      key: "PendingReview",
      label: "Pending Review",
      count: pendingReviewCount,
      icon: <RateReviewOutlinedIcon sx={{ fontSize: 20 }} />,
      accentColor: "#d97706",
      bgTint: "#fffbeb"
    },
    {
      key: "Approved",
      label: "Approved",
      count: approvedCount,
      icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />,
      accentColor: "#16a34a",
      bgTint: "#f0fdf4"
    },
    {
      key: "Rejected",
      label: "Rejected",
      count: rejectedCount,
      icon: <CancelOutlinedIcon sx={{ fontSize: 20 }} />,
      accentColor: "#dc2626",
      bgTint: "#fef2f2"
    },
    {
      key: "CancelledVoided",
      label: "Cancelled / Voided",
      count: cancelledVoidedCount,
      icon: <BlockOutlinedIcon sx={{ fontSize: 20 }} />,
      accentColor: "#64748b",
      bgTint: "#f8fafc"
    }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {cards.map((card) => {
        const isActive = activeKpi === card.key || (card.key === "ALL" && !activeKpi);
        return (
          <Grid item xs={6} sm={4} md={2} key={card.key}>
            <Paper
              elevation={isActive ? 2 : 0}
              onClick={() => onSelectKpi(card.key)}
              sx={{
                p: 1.75,
                borderRadius: 2,
                cursor: "pointer",
                border: isActive
                  ? `2px solid ${brandColors.sectionTitle}`
                  : "1px solid #e5e7eb",
                bgcolor: isActive ? "#faf5ff" : "#ffffff",
                transition: "all 0.15s ease-in-out",
                display: "flex",
                flexDirection: "column",
                justifyContent: "space-between",
                minHeight: 88,
                position: "relative",
                overflow: "hidden",
                "&:hover": {
                  borderColor: brandColors.sectionTitle,
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
                    color: isActive ? brandColors.sectionTitle : "text.secondary",
                    lineHeight: 1.2
                  }}
                >
                  {card.label}
                </Typography>
                <Box
                  sx={{
                    color: card.accentColor,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 28,
                    height: 28,
                    borderRadius: 1.5,
                    bgcolor: card.bgTint
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
                    color: isActive ? brandColors.pageTitle : "#1f2937",
                    lineHeight: 1
                  }}
                >
                  {card.count}
                </Typography>
                {isActive && card.key !== "ALL" && (
                  <Typography sx={{ fontSize: 11, color: brandColors.sectionTitle, fontWeight: 600 }}>
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
