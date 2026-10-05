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
              {instrument.code}
            </Typography>
          </Box>
          <IcpStatusBadge status={instrument.state} />
        </Box>

        {/* Unavailable reason banner */}
        {isUnavailable && (
          <Alert severity="error" sx={{ mt: 1.5, py: 0.5, fontSize: 13 }}>
            {instrument.reason || "Instrument is out of service or calibration is overdue."}
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
                {instrument.activeRun.code}
              </Typography>
              <IcpStatusBadge
                status={instrument.activeRun.calibrationStatus}
                label={`Cal: ${instrument.activeRun.calibrationStatus}`}
                size="small"
              />
            </Box>

            <Stack spacing={0.75}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <BiotechIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13 }}>
                  Method: <strong>{instrument.activeRun.methodAbbreviation}</strong>
                </Typography>
              </Box>

              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <PersonOutlinedIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13 }}>
                  Analyst: {instrument.activeRun.analystName}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <ScienceIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                <Typography variant="body2" sx={{ fontSize: 13, fontVariantNumeric: "tabular-nums" }}>
                  Assigned Samples: <strong>{instrument.activeRun.sampleCount}</strong>
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
              Instrument is ready. No analytical run currently active.
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
          Run History
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
            Open Run
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
            Start Run
          </Button>
        )}
      </CardActions>
    </Card>
  );
}
