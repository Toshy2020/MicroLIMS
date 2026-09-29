import { Box, Paper, Typography, Chip, Button } from "@mui/material";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import HelpOutlinedIcon from "@mui/icons-material/HelpOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { formatLabDate, formatLabDateTime } from "../../../utils/formatDate";
import type { CurrentFactorDto, CurrentFactorState } from "../types";

interface CurrentFactorCardProps {
  currentFactor?: CurrentFactorDto | null;
  canStandardize: boolean;
  onStandardize: () => void;
}

interface StateConfig {
  label: string;
  color: "success" | "warning" | "info" | "default";
  icon: React.ReactElement;
  textColor: string;
}

const STATE_CONFIGS: Record<CurrentFactorState, StateConfig> = {
  Valid: {
    label: "Valid",
    color: "success",
    icon: <CheckCircleOutlinedIcon fontSize="small" />,
    textColor: "success.main"
  },
  Due: {
    label: "Due for Restandardization",
    color: "warning",
    icon: <WarningAmberIcon fontSize="small" />,
    textColor: "warning.main"
  },
  BeforeEachUse: {
    label: "Restandardize Before Each Use",
    color: "info",
    icon: <InfoOutlinedIcon fontSize="small" />,
    textColor: "info.main"
  },
  NotStandardized: {
    label: "Not Standardized",
    color: "default",
    icon: <HelpOutlinedIcon fontSize="small" />,
    textColor: "text.secondary"
  }
};

export function CurrentFactorCard({
  currentFactor,
  canStandardize,
  onStandardize
}: CurrentFactorCardProps) {
  const state: CurrentFactorState = currentFactor?.state ?? "NotStandardized";
  const config = STATE_CONFIGS[state] ?? STATE_CONFIGS.NotStandardized;
  const factor = currentFactor?.factor;

  const validUntilText = (() => {
    if (state === "BeforeEachUse") return "Restandardize before each use";
    if (currentFactor?.validUntil) return formatLabDate(currentFactor.validUntil);
    return "—";
  })();

  return (
    <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          mb: 2.5
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Current Factor
          </Typography>
          <Chip
            label={config.label}
            color={config.color}
            icon={config.icon}
            size="small"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        </Box>

        {canStandardize && (
          <Button
            variant="contained"
            size="small"
            startIcon={<ScienceOutlinedIcon />}
            onClick={onStandardize}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            Standardize
          </Button>
        )}
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
          gap: 2
        }}
      >
        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            Factor (Mean)
          </Typography>
          <Typography
            variant="h5"
            sx={{
              fontFamily: "monospace",
              fontWeight: 700,
              color: factor != null ? config.textColor : "text.secondary"
            }}
          >
            {factor != null ? factor.toFixed(4) : "—"}
          </Typography>
        </Box>

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            Standardized At
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {currentFactor?.standardizedAt
              ? formatLabDateTime(currentFactor.standardizedAt)
              : "—"}
          </Typography>
        </Box>

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            Valid Until
          </Typography>
          <Typography
            variant="body2"
            sx={{
              fontWeight: 600,
              color: state === "Due" ? "error.main" : "text.primary"
            }}
          >
            {validUntilText}
          </Typography>
        </Box>
      </Box>
    </Paper>
  );
}
