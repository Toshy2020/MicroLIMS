import {
  Card,
  CardContent,
  CardActions,
  Typography,
  Box,
  Button,
  Stack,
  Alert,
  useTheme
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import AddCircleOutlinedIcon from "@mui/icons-material/AddCircleOutlined";
import HistoryIcon from "@mui/icons-material/History";
import ScienceIcon from "@mui/icons-material/Science";
import PersonOutlinedIcon from "@mui/icons-material/PersonOutlined";
import BiotechIcon from "@mui/icons-material/Biotech";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpInstrumentDto } from "../types";

export interface IcpInstrumentCardProps {
  instrument: IcpInstrumentDto;
  canOperate: boolean;
  onOpenRun: (equipmentId: number, runId: number) => void;
  onStartRun: (equipmentId: number) => void;
  onViewHistory: (equipmentId: number) => void;
}

export function IcpInstrumentCard({
  instrument,
  canOperate,
  onOpenRun,
  onStartRun,
  onViewHistory
}: IcpInstrumentCardProps) {
  const { t } = useTranslation("icpWorkspace");
  const theme = useTheme();

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
          <IcpStatusBadge status={instrument.state} />
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
              <IcpStatusBadge
                status={instrument.activeRun.calibrationStatus}
                label={t("overview.card.calBadge", { status: instrument.activeRun.calibrationStatus })}
                size="small"
              />
            </Box>

            <Stack spacing={0.75}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <BiotechIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13 }}>
                  {t("overview.card.run")} <strong><Ltr>{instrument.activeRun.code}</Ltr></strong>
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
              textAlign: "center",
              borderRadius: 1.5,
              border: `1px dashed ${theme.palette.divider}`,
              backgroundColor: theme.palette.action.hover
            }}
          >
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("overview.card.ready")}
            </Typography>
          </Box>
        )}
      </CardContent>

      {/* Card Actions */}
      <CardActions sx={{ px: 2.5, pb: 2.5, pt: 0, justifyContent: "space-between" }}>
        <Button
          size="small"
          startIcon={<HistoryIcon />}
          onClick={() => onViewHistory(instrument.equipmentId)}
          sx={{ textTransform: "none", color: "text.secondary" }}
        >
          {t("overview.card.runHistory")}
        </Button>

        {isRunning && instrument.activeRun && (
          <Button
            size="small"
            variant="contained"
            color="primary"
            startIcon={<PlayArrowIcon />}
            onClick={() => onOpenRun(instrument.equipmentId, instrument.activeRun!.runId)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("overview.card.openRun")}
          </Button>
        )}

        {isAvailable && (
          <Button
            size="small"
            variant="contained"
            color="primary"
            startIcon={<AddCircleOutlinedIcon />}
            onClick={() => onStartRun(instrument.equipmentId)}
            disabled={!canOperate}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("overview.card.startRun")}
          </Button>
        )}
      </CardActions>
    </Card>
  );
}
