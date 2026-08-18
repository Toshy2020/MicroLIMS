import { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  TextField,
  MenuItem,
  Alert,
  Paper,
  Chip,
  CircularProgress
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { SharedTsbStatusCard } from "./SharedTsbStatusCard";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { brandColors } from "../../../theme";

interface Props {
  session: PathogenTestingSessionDto;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
  onNext: () => void;
}

export function SharedTsbEnrichmentPanel({ session, onUpdated, onNext }: Props) {
  const [mediaLots, setMediaLots] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [loadingLookups, setLoadingLookups] = useState(false);

  const [selectedMediaId, setSelectedMediaId] = useState<number | "">("");
  const [selectedIncubatorId, setSelectedIncubatorId] = useState<number | "">("");
  const [startDateTime, setStartDateTime] = useState<string>(
    new Date().toISOString().slice(0, 16)
  );

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (session.sharedTsb.isStarted) return;

    setLoadingLookups(true);
    Promise.all([
      masterDataOptions.getReleasedMedia().catch(() => []),
      masterDataOptions.getEquipment("Incubator").catch(() => [])
    ])
      .then(([m, inc]) => {
        setMediaLots(m ?? []);
        setIncubators(inc ?? []);
        if (m && m.length > 0 && !selectedMediaId) {
          const tsbCandidate = m.find(
            (item: any) =>
              item.lotNumber?.includes("TSB") ||
              item.materialName?.includes("Tryptic") ||
              item.materialName?.includes("TSB") ||
              item.mediaTypeName?.includes("Broth")
          );
          if (tsbCandidate) setSelectedMediaId(tsbCandidate.id);
          else setSelectedMediaId(m[0].id);
        }
        if (inc && inc.length > 0 && !selectedIncubatorId) {
          setSelectedIncubatorId(inc[0].id);
        }
      })
      .finally(() => setLoadingLookups(false));
  }, [session.sharedTsb.isStarted]);

  const handleStartTsb = async () => {
    if (!selectedMediaId || !selectedIncubatorId) {
      setError("Please select a valid released TSB media lot and an in-service incubator.");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await PathogenSessionService.startSharedTsb(session.sampleId, {
        mediaLotId: Number(selectedMediaId),
        incubatorEquipmentId: Number(selectedIncubatorId),
        incubationStartUtc: startDateTime ? new Date(startDateTime).toISOString() : new Date().toISOString()
      });
      const updated = await PathogenSessionService.getSession(session.sampleId);
      onUpdated(updated);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to start shared TSB enrichment.");
    } finally {
      setSubmitting(false);
    }
  };

  const tsbTests = session.assignedTests.filter((t) => t.requiresTsb);

  if (tsbTests.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2 }}>
        <Alert severity="info" sx={{ mb: 3 }}>
          None of the assigned tests for this session require TSB broth enrichment. You may proceed directly to the test workflows and result matrix.
        </Alert>
        <Button variant="contained" endIcon={<ArrowForwardIcon />} onClick={onNext}>
          Proceed to Downstream Workflows
        </Button>
      </Paper>
    );
  }

  // If TSB is already started / incubating / completed, it is strictly LOCKED
  if (session.sharedTsb.isStarted) {
    return (
      <Stack spacing={3}>
        <SharedTsbStatusCard sharedTsb={session.sharedTsb} />

        <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#f8fafc" }}>
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 2 }}>
            <Box
              sx={{
                width: 36,
                height: 36,
                borderRadius: 1.5,
                bgcolor: "#64748b",
                color: "#ffffff",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <LockOutlinedIcon sx={{ fontSize: 20 }} />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: "#1e293b" }}>
                LOCKED — Execution in Progress
              </Typography>
              <Typography sx={{ fontSize: 13, color: "#64748b" }}>
                Shared TSB Enrichment parameters are locked and cannot be modified while execution is active.
              </Typography>
            </Box>
          </Stack>

          <Alert severity={session.sharedTsb.isCompleted ? "success" : "info"} sx={{ mb: 2.5 }}>
            {session.sharedTsb.isCompleted
              ? "TSB Enrichment incubation has elapsed and is complete. You may proceed to downstream testing."
              : "TSB Enrichment incubation is currently in progress. Downstream steps and final result entry remain locked until completion."}
          </Alert>

          <Stack direction="row" spacing={2} justifyContent="flex-end">
            <Button
              variant="contained"
              color="primary"
              endIcon={<ArrowForwardIcon />}
              onClick={onNext}
              sx={{ px: 3, fontWeight: 700 }}
            >
              Next: Test Workflows
            </Button>
          </Stack>
        </Paper>
      </Stack>
    );
  }

  return (
    <Stack spacing={3}>
      <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid #e5e7eb" }}>
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 2 }}>
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              bgcolor: brandColors.sectionTitle,
              color: "#ffffff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <ScienceOutlinedIcon sx={{ fontSize: 20 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: "#1f2937" }}>
              Start Shared TSB Enrichment
            </Typography>
            <Typography sx={{ fontSize: 13, color: "#6b7280" }}>
              Configure media and incubator once for the entire session. Required temperature and duration are governed by Test Master.
            </Typography>
          </Box>
        </Stack>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Box sx={{ mb: 2.5, p: 2, bgcolor: "#f8fafc", borderRadius: 2, border: "1px solid #e2e8f0" }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#475569", mb: 1 }}>
            APPLICABLE WORKFLOW SCOPE:
          </Typography>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip
              label={`${session.totalLocations} Sampling Locations`}
              size="small"
              sx={{ bgcolor: "#e0e7ff", color: "#3730a3", fontWeight: 600 }}
            />
            {tsbTests.map((t) => (
              <Chip
                key={t.testCode}
                label={`${t.testCode} (${t.displayName})`}
                size="small"
                sx={{ bgcolor: "#ede9fe", color: "#5b21b6", fontWeight: 600 }}
              />
            ))}
          </Stack>
        </Box>

        {loadingLookups ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" },
              gap: 2.5
            }}
          >
            <TextField
              select
              label="TSB Media Lot (GPT Passed & Released) *"
              value={selectedMediaId}
              onChange={(e) => setSelectedMediaId(Number(e.target.value))}
              helperText="Only active, unexpired, GPT-released lots are permitted"
              fullWidth
              size="small"
            >
              {mediaLots.map((m: any) => (
                <MenuItem key={m.id} value={m.id}>
                  {m.lotNumber} — {m.materialName ?? m.mediaTypeName ?? "Media Lot"} (Exp: {new Date(m.expiryDate).toLocaleDateString()})
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Incubator Equipment (In-Service) *"
              value={selectedIncubatorId}
              onChange={(e) => setSelectedIncubatorId(Number(e.target.value))}
              helperText="Select an operational, calibrated incubator"
              fullWidth
              size="small"
            >
              {incubators.map((inc: any) => (
                <MenuItem key={inc.id} value={inc.id}>
                  {inc.code} — {inc.name ?? "Incubator"} ({inc.location ?? "Lab"})
                </MenuItem>
              ))}
            </TextField>

            {/* Read-Only Controlled Parameters from Test Master */}
            <TextField
              label="Required Incubation Temperature *"
              value={session.sharedTsb.requiredTemperatureRange ?? "30.0 – 35.0 °C"}
              helperText="Controlled specification from approved Test Master workflow"
              fullWidth
              size="small"
              InputProps={{ readOnly: true }}
              sx={{ bgcolor: "#f1f5f9" }}
            />

            <TextField
              label="Required Incubation Time *"
              value={session.sharedTsb.requiredDurationRange ?? "18 – 24 h"}
              helperText="Controlled duration from approved Test Master workflow"
              fullWidth
              size="small"
              InputProps={{ readOnly: true }}
              sx={{ bgcolor: "#f1f5f9" }}
            />

            <TextField
              label="Actual Start Date & Time *"
              type="datetime-local"
              value={startDateTime}
              onChange={(e) => setStartDateTime(e.target.value)}
              helperText="Recorded contemporaneously under ALCOA+"
              fullWidth
              size="small"
              InputLabelProps={{ shrink: true }}
            />
          </Box>
        )}

        <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ mt: 3 }}>
          <Button
            variant="contained"
            color="primary"
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <PlayArrowIcon />}
            onClick={handleStartTsb}
            disabled={submitting || loadingLookups || !selectedMediaId || !selectedIncubatorId}
            sx={{ px: 3, fontWeight: 700 }}
          >
            Start TSB Enrichment
          </Button>

          <Button
            variant="outlined"
            endIcon={<ArrowForwardIcon />}
            onClick={onNext}
            sx={{ px: 3, fontWeight: 600 }}
          >
            Next: Test Workflows
          </Button>
        </Stack>
      </Paper>
    </Stack>
  );
}
