import { Box, Paper, Typography, Chip, Button } from "@mui/material";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import HelpOutlinedIcon from "@mui/icons-material/HelpOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";
import type { CurrentFactorDto, CurrentFactorState } from "../types";

interface CurrentFactorCardProps {
  currentFactor?: CurrentFactorDto | null;
  canStandardize: boolean;
  onStandardize: () => void;
}

interface StateConfig {
  labelKey: string;
  color: "success" | "warning" | "info" | "default";
  icon: React.ReactElement;
  textColor: string;
}

const STATE_CONFIGS: Record<CurrentFactorState, StateConfig> = {
  Valid: {
    labelKey: "currentFactor.states.Valid",
    color: "success",
    icon: <CheckCircleOutlinedIcon fontSize="small" />,
    textColor: "success.main"
  },
  Due: {
    labelKey: "currentFactor.states.Due",
    color: "warning",
    icon: <WarningAmberIcon fontSize="small" />,
    textColor: "warning.main"
  },
  BeforeEachUse: {
    labelKey: "currentFactor.states.BeforeEachUse",
    color: "info",
    icon: <InfoOutlinedIcon fontSize="small" />,
    textColor: "info.main"
  },
  NotStandardized: {
    labelKey: "currentFactor.states.NotStandardized",
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
  const { t } = useTranslation("solutionPreparation");
  const state: CurrentFactorState = currentFactor?.state ?? "NotStandardized";
  const config = STATE_CONFIGS[state] ?? STATE_CONFIGS.NotStandardized;
  const factor = currentFactor?.factor;

  const validUntilText = (() => {
    if (state === "BeforeEachUse") return t("currentFactor.beforeEachUse");
    if (currentFactor?.validUntil) return <Ltr>{formatLabCalendarDate(currentFactor.validUntil)}</Ltr>;
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
            {t("currentFactor.heading")}
          </Typography>
          <Chip
            label={t(config.labelKey)}
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
            {t("currentFactor.standardize")}
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
            {t("currentFactor.factorMean")}
          </Typography>
          <Typography
            variant="h5"
            sx={{
              fontFamily: "monospace",
              fontWeight: 700,
              color: factor != null ? config.textColor : "text.secondary"
            }}
          >
            {factor != null ? <Ltr>{factor.toFixed(4)}</Ltr> : "—"}
          </Typography>
        </Box>

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            {t("currentFactor.standardizedAt")}
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {currentFactor?.standardizedAt
              ? <Ltr>{formatDateTimeWithTimeZone(currentFactor.standardizedAt)}</Ltr>
              : "—"}
          </Typography>
        </Box>

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            {t("currentFactor.validUntil")}
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
