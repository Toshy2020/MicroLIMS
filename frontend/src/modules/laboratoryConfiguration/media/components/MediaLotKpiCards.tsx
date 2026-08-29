import React from "react";
import { Grid, Paper, Typography, Box, useTheme } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import PendingActionsOutlinedIcon from "@mui/icons-material/PendingActionsOutlined";
import HowToRegOutlinedIcon from "@mui/icons-material/HowToRegOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import { StatusTone } from "../../../../theme/statusTokens";

export type MediaKpiFilterKey =
  | "ALL"
  | "Pending Evaluation"
  | "Awaiting Approval"
  | "Released"
  | "Rejected"
  | "Out of Stock";

interface Props {
  lots: any[];
  awaitingApprovalIds: Set<number>;
  activeKpi: MediaKpiFilterKey | null;
  onSelectKpi: (kpi: MediaKpiFilterKey) => void;
}

export function lifecycleOf(lot: any, awaitingApprovalIds: Set<number>): string {
  if (lot.status === "OutOfStock") return "Out of Stock";
  if (lot.isReleasedForUse) return "Released";
  if (lot.approvalStatus === "Rejected" || lot.status === "QuarantineFailed") return "Rejected";
  if (awaitingApprovalIds.has(lot.id)) return "Awaiting Approval";
  return "Pending Evaluation";
}

export function MediaLotKpiCards({ lots, awaitingApprovalIds, activeKpi, onSelectKpi }: Props) {
  const theme = useTheme();
  const totalCount = lots.length;

  let pendingCount = 0;
  let awaitingApprovalCount = 0;
  let releasedCount = 0;
  let rejectedCount = 0;
  let outOfStockCount = 0;

  for (const lot of lots) {
    const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
    if (lifecycle === "Out of Stock") outOfStockCount++;
    else if (lifecycle === "Released") releasedCount++;
    else if (lifecycle === "Rejected") rejectedCount++;
    else if (lifecycle === "Awaiting Approval") awaitingApprovalCount++;
    else pendingCount++;
  }

  const cards: { key: MediaKpiFilterKey; label: string; count: number; icon: React.ReactNode; tone: StatusTone }[] = [
    { key: "ALL", label: "Total Lots", count: totalCount, icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />, tone: "purple" },
    { key: "Pending Evaluation", label: "Pending Evaluation", count: pendingCount, icon: <PendingActionsOutlinedIcon sx={{ fontSize: 20 }} />, tone: "pending" },
    { key: "Awaiting Approval", label: "Awaiting Approval", count: awaitingApprovalCount, icon: <HowToRegOutlinedIcon sx={{ fontSize: 20 }} />, tone: "action" },
    { key: "Released", label: "Released", count: releasedCount, icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />, tone: "notDetected" },
    { key: "Rejected", label: "Rejected", count: rejectedCount, icon: <BlockOutlinedIcon sx={{ fontSize: 20 }} />, tone: "detected" },
    { key: "Out of Stock", label: "Out of Stock", count: outOfStockCount, icon: <InventoryOutlinedIcon sx={{ fontSize: 20 }} />, tone: "pending" }
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
