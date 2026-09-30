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
import type { HplcInstrumentDto } from "../types";

export interface HplcInstrumentCardProps {
  instrument: HplcInstrumentDto;
  canOperate: boolean;
  onOpenRun: (equipmentId: number, runId: number) => void;
  onStartRun: (equipmentId: number) => void;
  onViewHistory: (equipmentId: number) => void;
}

export function HplcInstrumentCard({
  instrument,
  canOperate,
  onOpenRun,
  onStartRun,
  onViewHistory
}: HplcInstrumentCardProps) {
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
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, fontSize: 12 }}>
              {instrument.code}
            </Typography>
          </Box>
          <HplcStatusBadge status={instrument.state} />
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
              <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "primary.main" }}>
                {instrument.activeRun.code}
              </Typography>
              <HplcStatusBadge status={instrument.activeRun.sstStatus} label={`SST: ${instrument.activeRun.sstStatus}`} size="small" />
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
                <Typography variant="body2" sx={{ fontSize: 13 }}>
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
              borderRadius: 1.5,
              textAlign: "center",
              backgroundColor: theme.palette.action.hover
            }}
          >
            <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 13 }}>
              Instrument is ready. Start a new run to set up method, column, and mobile phases.
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
          History
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
            Open Run
          </Button>
        )}

        {isAvailable && (
          <Tooltip title={!canOperate ? "You do not have permission to start an HPLC run." : ""}>
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
                Start New Run
              </Button>
            </span>
          </Tooltip>
        )}

        {isUnavailable && (
          <Button
            size="small"
            disabled
            variant="outlined"
            sx={{ textTransform: "none" }}
          >
            Unavailable
          </Button>
        )}
      </CardActions>
    </Card>
  );
}
