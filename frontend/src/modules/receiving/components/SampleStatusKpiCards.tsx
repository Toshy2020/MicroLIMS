import React from "react";
import { Box, Paper, Typography, Grid, useTheme } from "@mui/material";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import WaterDropOutlinedIcon from "@mui/icons-material/WaterDropOutlined";
import CleaningServicesOutlinedIcon from "@mui/icons-material/CleaningServicesOutlined";
import SensorsOutlinedIcon from "@mui/icons-material/SensorsOutlined";
import { SampleRecord } from "../types/receivingTypes";
import { StatusTone } from "../../../theme/statusTokens";

export type KpiFilterKey =
  | "ALL"
  | "Product"
  | "RM"
  | "PM"
  | "Water"
  | "Aftercleaning"
  | "EM";

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

  const productCount = samples.filter((s) => s.category === "FinishedProduct" || s.category === "Product").length;
  const rmCount = samples.filter((s) => s.category === "RawMaterial" || s.category === "RM").length;
  const pmCount = samples.filter((s) => s.category === "PackagingMaterial" || s.category === "PM").length;
  const waterCount = samples.filter((s) => s.category === "Water").length;
  const acCount = samples.filter(
    (s) => s.category === "AfterCleaning" || s.category === "Aftercleaning" || s.category === "AC"
  ).length;
  const emCount = samples.filter(
    (s) => s.category === "EnvironmentalMonitoring" || s.category === "EM"
  ).length;

  const cards: KpiCardConfig[] = [
    { key: "Product", label: "Product", count: productCount, icon: <Inventory2OutlinedIcon sx={{ fontSize: 20 }} />, tone: "purple" },
    { key: "RM", label: "RM", count: rmCount, icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />, tone: "info" },
    { key: "PM", label: "PM", count: pmCount, icon: <InventoryOutlinedIcon sx={{ fontSize: 20 }} />, tone: "action" },
    { key: "Water", label: "Water", count: waterCount, icon: <WaterDropOutlinedIcon sx={{ fontSize: 20 }} />, tone: "info" },
    { key: "Aftercleaning", label: "Aftercleaning", count: acCount, icon: <CleaningServicesOutlinedIcon sx={{ fontSize: 20 }} />, tone: "detected" },
    { key: "EM", label: "EM", count: emCount, icon: <SensorsOutlinedIcon sx={{ fontSize: 20 }} />, tone: "notDetected" }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {cards.map((card) => {
        const isActive = activeKpi === card.key;
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
