import React from "react";
import { Grid, Paper, Typography, Box } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import PendingActionsOutlinedIcon from "@mui/icons-material/PendingActionsOutlined";
import HowToRegOutlinedIcon from "@mui/icons-material/HowToRegOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { brandColors } from "../../../../theme";

export type MediaKpiFilterKey = "ALL" | "PendingEvaluation" | "AwaitingApproval" | "Released" | "Quarantined";

interface Props {
  lots: any[];
  awaitingApprovalIds: Set<number>;
  activeKpi: MediaKpiFilterKey | null;
  onSelectKpi: (kpi: MediaKpiFilterKey) => void;
}

export function lifecycleOf(lot: any, awaitingApprovalIds: Set<number>): string {
  if (lot.isReleasedForUse) return "Released";
  if (lot.approvalStatus === "Rejected" || lot.status === "QuarantineFailed") return "Quarantined";
  if (awaitingApprovalIds.has(lot.id)) return "Awaiting Approval";
  return "Pending Evaluation";
}

export function MediaLotKpiCards({ lots, awaitingApprovalIds, activeKpi, onSelectKpi }: Props) {
  const totalCount = lots.length;

  let pendingCount = 0;
  let awaitingApprovalCount = 0;
  let releasedCount = 0;
  let quarantinedCount = 0;

  for (const lot of lots) {
    const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
    if (lifecycle === "Released") releasedCount++;
    else if (lifecycle === "Quarantined") quarantinedCount++;
    else if (lifecycle === "Awaiting Approval") awaitingApprovalCount++;
    else pendingCount++;
  }

  const cards = [
    {
      key: "ALL" as MediaKpiFilterKey,
      label: "Total Lots",
      count: totalCount,
      icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />,
      color: brandColors.pageTitle,
      bgTint: "#fbf8fc"
    },
    {
      key: "PendingEvaluation" as MediaKpiFilterKey,
      label: "Pending Evaluation",
      count: pendingCount,
      icon: <PendingActionsOutlinedIcon sx={{ fontSize: 20 }} />,
      color: "#6b7280",
      bgTint: "#f3f4f6"
    },
    {
      key: "AwaitingApproval" as MediaKpiFilterKey,
      label: "Awaiting Approval",
      count: awaitingApprovalCount,
      icon: <HowToRegOutlinedIcon sx={{ fontSize: 20 }} />,
      color: "#d97706",
      bgTint: "#fffbeb"
    },
    {
      key: "Released" as MediaKpiFilterKey,
      label: "Released for Use",
      count: releasedCount,
      icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />,
      color: "#16a34a",
      bgTint: "#f0fdf4"
    },
    {
      key: "Quarantined" as MediaKpiFilterKey,
      label: "Quarantined",
      count: quarantinedCount,
      icon: <BlockOutlinedIcon sx={{ fontSize: 20 }} />,
      color: "#dc2626",
      bgTint: "#fef2f2"
    }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {cards.map((card) => {
        const isActive = activeKpi === card.key || (card.key === "ALL" && !activeKpi);
        return (
          <Grid item xs={6} sm={4} md={2.4} key={card.key}>
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
                    color: card.color,
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
