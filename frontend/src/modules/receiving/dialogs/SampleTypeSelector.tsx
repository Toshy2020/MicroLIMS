import { Box, Typography, Paper, Grid, useTheme } from "@mui/material";
import MedicationOutlinedIcon from "@mui/icons-material/MedicationOutlined";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import AllInboxOutlinedIcon from "@mui/icons-material/AllInboxOutlined";
import WaterDropOutlinedIcon from "@mui/icons-material/WaterDropOutlined";
import NatureOutlinedIcon from "@mui/icons-material/NatureOutlined";
import CleaningServicesOutlinedIcon from "@mui/icons-material/CleaningServicesOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { SampleCategoryKey } from "../types/receivingTypes";
import { RECEIVING_CATEGORIES } from "../constants/receivingConstants";
import { brandColors } from "../../../theme";

interface Props {
  selectedCategory: SampleCategoryKey | null;
  onSelectCategory: (cat: SampleCategoryKey) => void;
}

const CATEGORY_ICONS: Record<SampleCategoryKey, React.ReactNode> = {
  product: <MedicationOutlinedIcon sx={{ fontSize: 28 }} />,
  rm: <Inventory2OutlinedIcon sx={{ fontSize: 28 }} />,
  pm: <AllInboxOutlinedIcon sx={{ fontSize: 28 }} />,
  water: <WaterDropOutlinedIcon sx={{ fontSize: 28 }} />,
  em: <NatureOutlinedIcon sx={{ fontSize: 28 }} />,
  ac: <CleaningServicesOutlinedIcon sx={{ fontSize: 28 }} />
};

export function SampleTypeSelector({ selectedCategory, onSelectCategory }: Props) {
  const theme = useTheme();
  return (
    <Box sx={{ py: 1 }}>
      <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
        Select the type of sample you want to receive. The selected category will apply to all sample rows in this batch.
      </Typography>

      <Grid container spacing={2}>
        {RECEIVING_CATEGORIES.map((cat) => {
          const isSelected = selectedCategory === cat.key;
          return (
            <Grid item xs={12} sm={6} md={4} key={cat.key}>
              <Paper
                elevation={0}
                onClick={() => onSelectCategory(cat.key)}
                sx={{
                  p: 2.25,
                  borderRadius: 2,
                  cursor: "pointer",
                  border: isSelected
                    ? `2px solid ${brandColors.sectionTitle}`
                    : `1.5px solid ${theme.palette.divider}`,
                  bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
                  transition: "all 0.18s ease-in-out",
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "space-between",
                  position: "relative",
                  boxShadow: isSelected
                    ? theme.palette.mode === "dark"
                      ? "0 4px 14px rgba(192, 132, 200, 0.25)"
                      : "0 4px 14px rgba(123, 45, 142, 0.12)"
                    : "none",
                  "&:hover": {
                    borderColor: theme.palette.primary.main,
                    bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover",
                    transform: "translateY(-2px)",
                    boxShadow:
                      theme.palette.mode === "dark"
                        ? "0 4px 12px rgba(0,0,0,0.4)"
                        : "0 4px 12px rgba(0,0,0,0.06)"
                  }
                }}
              >
                {/* Header with Icon and Selection Check */}
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
                  <Box
                    sx={{
                      color: isSelected ? brandColors.sectionTitle : "text.secondary",
                      bgcolor: isSelected ? theme.custom.status.purple.bg : "background.default",
                      width: 44,
                      height: 44,
                      borderRadius: 2,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      transition: "all 0.15s ease"
                    }}
                  >
                    {CATEGORY_ICONS[cat.key]}
                  </Box>

                  {isSelected && (
                    <CheckCircleIcon sx={{ color: theme.palette.primary.main, fontSize: 22 }} />
                  )}
                </Box>

                {/* Title & Description */}
                <Box>
                  <Typography
                    sx={{
                      fontSize: 15,
                      fontWeight: 700,
                      color: isSelected ? brandColors.pageTitle : "text.primary",
                      mb: 0.5
                    }}
                  >
                    {cat.label}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", lineHeight: 1.4 }}>
                    {cat.description}
                  </Typography>
                </Box>
              </Paper>
            </Grid>
          );
        })}
      </Grid>
    </Box>
  );
}
