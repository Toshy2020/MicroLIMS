import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Box,
  Radio,
  RadioGroup,
  FormControlLabel,
  FormControl,
  Alert,
  CircularProgress,
  Chip,
  Paper,
  Stack,
  useTheme
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import BiotechIcon from "@mui/icons-material/Biotech";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpMethodOptionDto, IcpRunDto } from "../types";

export interface StartIcpRunDialogProps {
  open: boolean;
  equipmentId: number;
  equipmentName?: string;
  equipmentCode?: string;
  onClose: () => void;
  onRunStarted: (run: IcpRunDto) => void;
}

export function StartIcpRunDialog({
  open,
  equipmentId,
  equipmentName,
  equipmentCode,
  onClose,
  onRunStarted
}: StartIcpRunDialogProps) {
  const theme = useTheme();
  const [methods, setMethods] = useState<IcpMethodOptionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedMethodId, setSelectedMethodId] = useState<number | null>(null);

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    setError(null);
    setSelectedMethodId(null);

    IcpWorkspaceService.getMethodOptions()
      .then((data) => {
        setMethods(data);
        if (data.length === 1) {
          setSelectedMethodId(data[0].id);
        }
      })
      .catch((err: unknown) => {
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        setError(e.response?.data?.message ?? e.message ?? "Could not load ICP methods.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [open]);

  const handleStart = async () => {
    if (!selectedMethodId) return;

    setStarting(true);
    setError(null);
    try {
      const newRun = await IcpWorkspaceService.startRun({
        equipmentId,
        icpMethodId: selectedMethodId
      });
      onRunStarted(newRun);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to start ICP run.");
    } finally {
      setStarting(false);
    }
  };

  return (
    <Dialog open={open} onClose={starting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <BiotechIcon color="primary" />
          <span>Start ICP Run</span>
        </Stack>
        {equipmentName && (
          <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 400, mt: 0.5 }}>
            Instrument: <strong>{equipmentName}</strong> ({equipmentCode})
          </Typography>
        )}
      </DialogTitle>

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Select the active ICP method for this analytical sequence. The run will initialize with a calibration record and method parameter snapshot.
        </Typography>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : methods.length === 0 ? (
          <Alert severity="warning">
            No active ICP methods configured for this laboratory section.
          </Alert>
        ) : (
          <FormControl component="fieldset" fullWidth>
            <RadioGroup
              value={selectedMethodId ?? ""}
              onChange={(e) => setSelectedMethodId(Number(e.target.value))}
            >
              <Stack spacing={1.5}>
                {methods.map((m) => {
                  const isSelected = selectedMethodId === m.id;
                  return (
                    <Paper
                      key={m.id}
                      elevation={0}
                      onClick={() => setSelectedMethodId(m.id)}
                      sx={{
                        p: 1.5,
                        borderRadius: 1.5,
                        border: `1px solid ${isSelected ? theme.palette.primary.main : theme.palette.divider}`,
                        backgroundColor: isSelected
                          ? theme.palette.mode === "dark"
                            ? "rgba(144, 202, 249, 0.08)"
                            : "rgba(25, 118, 210, 0.04)"
                          : theme.palette.background.paper,
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                        "&:hover": {
                          borderColor: theme.palette.primary.light
                        }
                      }}
                    >
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                        <FormControlLabel
                          value={m.id}
                          control={<Radio size="small" />}
                          label={
                            <Box sx={{ ml: 0.5 }}>
                              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                                {m.name} ({m.abbreviation})
                              </Typography>
                              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                                Mode: {m.mode === "MineralAssay" ? "Mineral Assay" : "Elemental Impurities"}
                              </Typography>
                            </Box>
                          }
                          sx={{ m: 0, alignItems: "flex-start" }}
                        />

                        <Chip
                          size="small"
                          label={`${m.eligibleTestOrderCount} Eligible Tests`}
                          color={m.eligibleTestOrderCount > 0 ? "primary" : "default"}
                          variant="outlined"
                          sx={{ fontWeight: 600, fontSize: 11 }}
                        />
                      </Box>
                    </Paper>
                  );
                })}
              </Stack>
            </RadioGroup>
          </FormControl>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={starting}>
          Cancel
        </Button>
        <Button
          variant="contained"
          startIcon={starting ? <CircularProgress size={16} color="inherit" /> : <PlayArrowIcon />}
          onClick={handleStart}
          disabled={!selectedMethodId || starting || loading}
          sx={{ textTransform: "none" }}
        >
          {starting ? "Starting Run..." : "Start Run"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
