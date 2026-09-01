import React from "react";
import {
  Box,
  Card,
  CardActionArea,
  CardContent,
  Checkbox,
  FormControlLabel,
  Typography,
  Chip,
  Button,
  Alert,
  useTheme
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import { brandColors } from "../../../theme";

export interface SamplingPointGridItem {
  id: number;
  title: string;
  subtitle?: string;
  assignedTests: string[];
  disabled?: boolean;
}

interface Props {
  items: SamplingPointGridItem[];
  selectedIds: Record<number, boolean>;
  onToggle: (id: number) => void;
  onSelectAll: (select: boolean) => void;
  onConfirm: () => void;
  confirmLabel?: string;
  loading?: boolean;
  errorMessage?: string | null;
  // Lets a caller block confirmation on its own prerequisites (e.g. Water's
  // storage condition) on top of the grid's own selection rule.
  confirmDisabled?: boolean;
}

export function SamplingPointGrid({
  items,
  selectedIds,
  onToggle,
  onSelectAll,
  onConfirm,
  confirmLabel = "Start Testing",
  loading = false,
  errorMessage = null,
  confirmDisabled = false
}: Props) {
  const theme = useTheme();

  const selectableItems = items.filter((item) => !item.disabled);
  const selectedCount = selectableItems.filter((item) => !!selectedIds[item.id]).length;
  const isAllSelected = selectableItems.length > 0 && selectedCount === selectableItems.length;
  const isIndeterminate = selectedCount > 0 && selectedCount < selectableItems.length;

  const handleSelectAllChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    onSelectAll(event.target.checked);
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      {errorMessage && (
        <Alert severity="error" sx={{ borderRadius: 1.5 }}>
          {errorMessage}
        </Alert>
      )}

      {/* Top Controls: Select All & Selection Count */}
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          px: 1,
          py: 0.5,
          bgcolor: "background.default",
          borderRadius: 1.5,
          border: "1px solid",
          borderColor: "divider"
        }}
      >
        <FormControlLabel
          control={
            <Checkbox
              checked={isAllSelected}
              indeterminate={isIndeterminate}
              onChange={handleSelectAllChange}
              disabled={selectableItems.length === 0}
              size="small"
              sx={{ color: theme.palette.primary.main }}
            />
          }
          label={
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
              Select All
            </Typography>
          }
          sx={{ m: 0 }}
        />

        <Chip
          size="small"
          label={`${selectedCount} selected`}
          variant={selectedCount > 0 ? "filled" : "outlined"}
          sx={{
            fontWeight: 700,
            fontSize: 12,
            bgcolor: selectedCount > 0 ? theme.custom.status.purple.bg : "inherit",
            color: selectedCount > 0 ? theme.palette.primary.main : "text.secondary",
            borderColor: selectedCount > 0 ? theme.custom.status.purple.border : "divider"
          }}
        />
      </Box>

      {/* Responsive Grid of Sampling Point Cards */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(3, 1fr)"
          },
          gap: 1.5,
          maxHeight: "52vh",
          overflowY: "auto",
          p: 0.5
        }}
      >
        {items.map((item) => {
          const isSelected = !!selectedIds[item.id];
          const isDisabled = !!item.disabled;

          return (
            <Card
              key={item.id}
              variant="outlined"
              sx={{
                borderRadius: 2,
                transition: "all 0.15s ease",
                bgcolor: isDisabled
                  ? "action.disabledBackground"
                  : isSelected
                  ? theme.custom.status.purple.bg
                  : "background.paper",
                borderColor: isSelected
                  ? theme.palette.primary.main
                  : "divider",
                boxShadow: isSelected
                  ? `0 0 0 1px ${theme.palette.primary.main}`
                  : "none",
                opacity: isDisabled ? 0.6 : 1,
                cursor: isDisabled ? "not-allowed" : "pointer"
              }}
            >
              <CardActionArea
                disabled={isDisabled}
                onClick={() => onToggle(item.id)}
                sx={{
                  p: 1.5,
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "stretch",
                  justifyContent: "space-between"
                }}
              >
                <CardContent sx={{ p: 0, "&:last-child": { pb: 0 }, width: "100%" }}>
                  {/* Header: Checkbox + Title */}
                  <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1, mb: 0.75 }}>
                    <Checkbox
                      checked={isSelected}
                      disabled={isDisabled}
                      size="small"
                      sx={{ p: 0, mt: 0.25 }}
                    />
                    <Box sx={{ minWidth: 0, flex: 1 }}>
                      <Typography
                        sx={{
                          fontSize: 13,
                          fontWeight: 700,
                          color: isSelected ? theme.palette.primary.main : "text.primary",
                          lineHeight: 1.3
                        }}
                      >
                        {item.title}
                      </Typography>
                      {item.subtitle && (
                        <Typography
                          sx={{
                            fontSize: 11,
                            color: "text.secondary",
                            lineHeight: 1.2,
                            mt: 0.25
                          }}
                        >
                          {item.subtitle}
                        </Typography>
                      )}
                    </Box>
                  </Box>

                  {/* Assigned Tests Section */}
                  <Box
                    sx={{
                      mt: 1,
                      pt: 1,
                      borderTop: "1px dashed",
                      borderColor: isSelected ? theme.custom.status.purple.border : "divider"
                    }}
                  >
                    <Typography
                      sx={{
                        fontSize: 10.5,
                        fontWeight: 700,
                        textTransform: "uppercase",
                        color: "text.secondary",
                        letterSpacing: 0.5,
                        mb: 0.5
                      }}
                    >
                      Assigned Tests
                    </Typography>
                    {item.assignedTests.length > 0 ? (
                      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                        {item.assignedTests.map((testCode) => (
                          <Chip
                            key={testCode}
                            size="small"
                            label={testCode}
                            sx={{
                              fontSize: 11,
                              fontWeight: 600,
                              height: 20,
                              bgcolor: isSelected ? "#ffffff" : "background.default",
                              border: "1px solid",
                              borderColor: "divider"
                            }}
                          />
                        ))}
                      </Box>
                    ) : (
                      <Typography sx={{ fontSize: 11, color: "text.disabled", fontStyle: "italic" }}>
                        No tests configured
                      </Typography>
                    )}
                  </Box>
                </CardContent>
              </CardActionArea>
            </Card>
          );
        })}
      </Box>

      {/* Bottom Actions: Start Testing Button */}
      <Box sx={{ display: "flex", justifyContent: "flex-end", alignItems: "center", pt: 1 }}>
        <Button
          variant="contained"
          onClick={onConfirm}
          disabled={selectedCount === 0 || loading || confirmDisabled}
          startIcon={<PlayArrowIcon />}
          sx={{
            bgcolor: brandColors.sectionTitle,
            fontSize: 13,
            fontWeight: 700,
            px: 3,
            py: 0.85,
            textTransform: "none",
            borderRadius: 1.5,
            "&:hover": { bgcolor: "#631f74" }
          }}
        >
          {loading ? "Starting..." : confirmLabel}
        </Button>
      </Box>
    </Box>
  );
}
