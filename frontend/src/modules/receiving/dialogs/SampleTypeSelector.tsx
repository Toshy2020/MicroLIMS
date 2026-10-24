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
import { clickable } from "../../../utils/clickable";

interface Props {
  selectedCategory: SampleCategoryKey | null;
  onSelectCategory: (cat: SampleCategoryKey) => void;
  // Which categories this dialog instance may offer - e.g. the main
  // Receiving page only offers FP/RM/PM, never Water/EM/After Cleaning.
  allowedCategories: SampleCategoryKey[];
}

const CATEGORY_ICONS: Record<SampleCategoryKey, React.ReactNode> = {
  product: <MedicationOutlinedIcon sx={{ fontSize: 22 }} />,
  rm: <Inventory2OutlinedIcon sx={{ fontSize: 22 }} />,
  pm: <AllInboxOutlinedIcon sx={{ fontSize: 22 }} />,
  water: <WaterDropOutlinedIcon sx={{ fontSize: 22 }} />,
  em: <NatureOutlinedIcon sx={{ fontSize: 22 }} />,
  ac: <CleaningServicesOutlinedIcon sx={{ fontSize: 22 }} />
};

export function SampleTypeSelector({ selectedCategory, onSelectCategory, allowedCategories }: Props) {
  const theme = useTheme();
  const availableCategories = RECEIVING_CATEGORIES.filter((c) => allowedCategories.includes(c.key));
  return (
    <Box sx={{ py: 1 }}>
      <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
        Select the type of sample you want to receive. The selected category will apply to all sample rows in this batch.
      </Typography>

      <Grid container spacing={1.5}>
        {availableCategories.map((cat) => {
          const isSelected = selectedCategory === cat.key;
          return (
            <Grid
              key={cat.key}
              size={{
                xs: 12,
                sm: 6,
                md: 4
              }}>
              {/* Compact horizontal card: six tall stacked cards pushed the
                  dialog's Next button below the fold on laptop screens. */}
              <Paper
                elevation={0}
                onClick={() => onSelectCategory(cat.key)}
                sx={{
                  p: 1.5,
                  borderRadius: 2,
                  cursor: "pointer",
                  border: isSelected
                    ? `2px solid ${brandColors.sectionTitle}`
                    : `1px solid ${theme.palette.divider}`,
                  // Keeps the text from shifting by the 1px border difference.
                  m: isSelected ? 0 : "1px",
                  bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
                  transition: "border-color 0.15s ease, background-color 0.15s ease",
                  height: "100%",
                  minHeight: 72,
                  display: "flex",
                  alignItems: "center",
                  gap: 1.5,
                  "&:hover": {
                    borderColor: theme.palette.primary.main,
                    bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover"
                  }
                }}
                {...clickable(() => onSelectCategory(cat.key), { pressed: isSelected })}
              >
                <Box
                  sx={{
                    color: isSelected ? brandColors.sectionTitle : "text.secondary",
                    bgcolor: isSelected ? "background.paper" : "background.default",
                    width: 40,
                    height: 40,
                    flexShrink: 0,
                    borderRadius: 1.5,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center"
                  }}
                >
                  {CATEGORY_ICONS[cat.key]}
                </Box>
                <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography
                    sx={{
                      fontSize: 14,
                      fontWeight: 700,
                      color: isSelected ? brandColors.pageTitle : "text.primary",
                      lineHeight: 1.3
                    }}
                  >
                    {cat.label}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", lineHeight: 1.35 }}>
                    {cat.description}
                  </Typography>
                </Box>
                {isSelected && (
                  <CheckCircleIcon sx={{ color: theme.palette.primary.main, fontSize: 20, flexShrink: 0 }} />
                )}
              </Paper>
            </Grid>
          );
        })}
      </Grid>
    </Box>
  );
}
