import {
  Card,
  CardContent,
  CardActions,
  Typography,
  Box,
  Button,
  Stack,
  Alert,
  Divider,
  useTheme,
  Tooltip
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import AddCircleOutlinedIcon from "@mui/icons-material/AddCircleOutlined";
import HistoryIcon from "@mui/icons-material/History";
import ScienceIcon from "@mui/icons-material/Science";
import PersonOutlinedIcon from "@mui/icons-material/PersonOutlined";
import BiotechIcon from "@mui/icons-material/Biotech";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { useTechnique } from "../useTechnique";
import type { HplcInstrumentDto, HplcTechnique } from "../types";

export interface HplcInstrumentCardProps {
  instrument: HplcInstrumentDto;
  canOperate: boolean;
  technique?: HplcTechnique;
  onOpenRun: (equipmentId: number, runId: number) => void;
  onStartRun: (equipmentId: number) => void;
  onViewHistory: (equipmentId: number) => void;
}

export function HplcInstrumentCard({
  instrument,
  canOperate,
  technique,
  onOpenRun,
  onStartRun,
  onViewHistory
}: HplcInstrumentCardProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const { technique: routeTechnique } = useTechnique();
  const activeTechnique = technique ?? routeTechnique;
  const activeLabel = activeTechnique === "Gc" ? "GC" : "HPLC";

  const isRunning = instrument.state.toLowerCase() === "running" && Boolean(instrument.activeRun);
  const isAvailable = instrument.state.toLowerCase() === "available";
  const isUnavailable = instrument.state.toLowerCase() === "unavailable";

  return (
    <Card
      elevation={0}
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        borderRadius: 2,
        border: `1px solid ${
          isRunning
            ? theme.palette.primary.main
            : theme.palette.divider
        }`,
        backgroundColor: theme.palette.background.paper,
        transition: "box-shadow 0.2s ease, border-color 0.2s ease",
        "&:hover": {
          boxShadow: theme.shadows[3]
        }
      }}
    >
      <CardContent sx={{ flexGrow: 1, p: 2.5 }}>
        {/* Header: Name, Code & State Badge */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
          <Box sx={{ pr: 1 }}>
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16, lineHeight: 1.3 }}>
              {instrument.name}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, fontSize: 12, fontFamily: monospaceFontFamily }}>
              <Ltr>{instrument.code}</Ltr>
            </Typography>
          </Box>
          <HplcStatusBadge status={instrument.state} />
        </Box>

        {/* Unavailable reason banner */}
        {isUnavailable && (
          <Alert severity="error" sx={{ mt: 1.5, py: 0.5, fontSize: 13 }}>
            {instrument.reason || t("overview.card.unavailableReasonFallback")}
          </Alert>
        )}

        {/* Running instrument active run summary */}
        {isRunning && instrument.activeRun && (
          <Box
            sx={{
              mt: 1.5,
              p: 1.5,
              borderRadius: 1.5,
              backgroundColor: theme.palette.action.hover,
              border: `1px solid ${theme.palette.divider}`
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "primary.main", fontFamily: monospaceFontFamily }}>
                <Ltr>{instrument.activeRun.code}</Ltr>
              </Typography>
              <HplcStatusBadge status={instrument.activeRun.sstStatus} label={t("overview.card.sstBadge", { status: instrument.activeRun.sstStatus })} size="small" />
            </Box>

            <Stack spacing={0.75}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <BiotechIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13 }}>
                  {t("overview.card.method")} <strong><Ltr>{instrument.activeRun.methodAbbreviation}</Ltr></strong>
                </Typography>
              </Box>

              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <PersonOutlinedIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13 }}>
                  {t("overview.card.analyst")} {instrument.activeRun.analystName}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <ScienceIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13, fontVariantNumeric: "tabular-nums" }}>
                  {t("overview.card.assignedSamples")} <strong><Ltr>{instrument.activeRun.sampleCount}</Ltr></strong>
                </Typography>
              </Box>
            </Stack>
          </Box>
        )}

        {/* Available instrument placeholder */}
        {isAvailable && (
          <Box
            sx={{
              mt: 2,
              p: 2,
              borderRadius: 1.5,
              textAlign: "center",
              backgroundColor: theme.palette.action.hover
            }}
          >
            <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
              {activeTechnique === "Gc"
                ? t("overview.card.readyGc")
                : t("overview.card.readyHplc")}
            </Typography>
          </Box>
        )}
      </CardContent>

      <Divider />

      {/* Card Actions */}
      <CardActions sx={{ px: 2, py: 1.5, justifyContent: "space-between" }}>
        <Button
          size="small"
          startIcon={<HistoryIcon />}
          onClick={() => onViewHistory(instrument.equipmentId)}
          sx={{ textTransform: "none", color: "text.secondary" }}
        >
          {t("overview.card.history")}
        </Button>

        {isRunning && instrument.activeRun && (
          <Button
            variant="contained"
            size="small"
            color="primary"
            startIcon={<PlayArrowIcon />}
            onClick={() => onOpenRun(instrument.equipmentId, instrument.activeRun!.runId)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("overview.card.openRun")}
          </Button>
        )}

        {isAvailable && (
          <Tooltip title={!canOperate ? (activeLabel === "GC" ? t("overview.card.noPermissionGc") : t("overview.card.noPermissionHplc")) : ""}>
            <span>
              <Button
                variant="contained"
                size="small"
                color="primary"
                disabled={!canOperate}
                startIcon={<AddCircleOutlinedIcon />}
                onClick={() => onStartRun(instrument.equipmentId)}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                {t("overview.card.startNewRun")}
              </Button>
            </span>
          </Tooltip>
        )}

        {isUnavailable && (
          <Button
            size="small"
            disabled
            variant="outlined"
          >
            {t("overview.card.unavailable")}
          </Button>
        )}
      </CardActions>
    </Card>
  );
}
