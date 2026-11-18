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
  CircularProgress,
  useTheme
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
import { FieldHint, describedByWithHint } from "../../../components/FieldHint";
import { hints } from "../hints";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  session: PathogenTestingSessionDto;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
  onNext: () => void;
}

interface ReleasedMediaOption {
  id: number;
  lotNumber?: string | null;
  materialName?: string | null;
  mediaTypeName?: string | null;
  expiryDate?: string | null;
}

interface IncubatorOption {
  id: number;
  code?: string | null;
  name?: string | null;
  location?: string | null;
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === "object") {
    const apiErr = err as { response?: { data?: { message?: string } }; message?: string };
    return apiErr.response?.data?.message ?? apiErr.message ?? fallback;
  }
  return fallback;
}

export function SharedTsbEnrichmentPanel({ session, onUpdated, onNext }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const [mediaLots, setMediaLots] = useState<ReleasedMediaOption[]>([]);
  const [incubators, setIncubators] = useState<IncubatorOption[]>([]);
  const [loadingLookups, setLoadingLookups] = useState(false);

  const [selectedMediaId, setSelectedMediaId] = useState<number | "">("");
  const [selectedIncubatorId, setSelectedIncubatorId] = useState<number | "">("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (session.sharedTsb.isStarted) return;

    setLoadingLookups(true);
    Promise.all([
      masterDataOptions.getReleasedMedia().catch(() => [] as ReleasedMediaOption[]),
      masterDataOptions.getEquipment("Incubator").catch(() => [] as IncubatorOption[])
    ])
      .then(([m, inc]) => {
        const mediaList: ReleasedMediaOption[] = m ?? [];
        const incList: IncubatorOption[] = inc ?? [];
        setMediaLots(mediaList);
        setIncubators(incList);
        if (mediaList.length > 0 && !selectedMediaId) {
          const tsbCandidate = mediaList.find(
            (item) =>
              item.lotNumber?.includes("TSB") ||
              item.materialName?.includes("Tryptic") ||
              item.materialName?.includes("TSB") ||
              item.mediaTypeName?.includes("Broth")
          );
          if (tsbCandidate) setSelectedMediaId(tsbCandidate.id);
          else setSelectedMediaId(mediaList[0].id);
        }
        if (incList.length > 0 && !selectedIncubatorId) {
          setSelectedIncubatorId(incList[0].id);
        }
      })
      .finally(() => setLoadingLookups(false));
  }, [session.sharedTsb.isStarted]);

  const handleStartTsb = async () => {
    if (!selectedMediaId || !selectedIncubatorId) {
      setError(t("pathogenSession.tsbSetup.validationError"));
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await PathogenSessionService.startSharedTsb(session.sampleId, {
        mediaLotId: Number(selectedMediaId),
        incubatorEquipmentId: Number(selectedIncubatorId),
        incubationStartUtc: new Date().toISOString()
      });
      const updated = await PathogenSessionService.getSession(session.sampleId);
      onUpdated(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("pathogenSession.tsbSetup.startError")));
    } finally {
      setSubmitting(false);
    }
  };

  const tsbTests = session.assignedTests.filter((testItem) => testItem.requiresTsb);

  if (tsbTests.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2 }}>
        <Alert severity="info" sx={{ mb: 3 }}>
          {t("pathogenSession.tsbSetup.noTsbInfo")}
        </Alert>
        <Button variant="contained" endIcon={<ArrowForwardIcon />} onClick={onNext}>
          {t("pathogenSession.tsbSetup.proceedDownstream")}
        </Button>
      </Paper>
    );
  }

  // If TSB is already started / incubating / completed, it is strictly LOCKED
  if (session.sharedTsb.isStarted) {
    return (
      <Stack spacing={3}>
        <SharedTsbStatusCard sharedTsb={session.sharedTsb} />

        <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid", borderColor: "divider", bgcolor: "background.default" }}>
          <Stack
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: "center",
              mb: 2
            }}>
            <Box
              sx={{
                width: 36,
                height: 36,
                borderRadius: 1.5,
                // done/locked stay solid (mode-invariant) fills - matches the
                // intentional pattern in DownstreamWorkflowsPanel.tsx.
                bgcolor: "#64748b",
                color: "common.white",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <LockOutlinedIcon sx={{ fontSize: 20 }} />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
                {t("pathogenSession.tsbSetup.lockedTitle")}
              </Typography>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {t("pathogenSession.tsbSetup.lockedDesc")}
              </Typography>
            </Box>
          </Stack>

          <Alert severity={session.sharedTsb.isCompleted ? "success" : "info"} sx={{ mb: 2.5 }}>
            {session.sharedTsb.isCompleted
              ? t("pathogenSession.tsbSetup.lockedCompleteAlert")
              : t("pathogenSession.tsbSetup.lockedInProgressAlert")}
          </Alert>

          <Stack direction="row" spacing={2} sx={{
            justifyContent: "flex-end"
          }}>
            <Button
              variant="contained"
              color="primary"
              endIcon={<ArrowForwardIcon />}
              onClick={onNext}
              sx={{ px: 3, fontWeight: 700 }}
            >
              {t("pathogenSession.tsbSetup.nextTestWorkflows")}
            </Button>
          </Stack>
        </Paper>
      </Stack>
    );
  }

  return (
    <Stack spacing={3}>
      <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack
          direction="row"
          spacing={1.5}
          sx={{
            alignItems: "center",
            mb: 2
          }}>
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              bgcolor: brandColors.sectionTitle,
              color: "common.white",
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <ScienceOutlinedIcon sx={{ fontSize: 20 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
              {t("pathogenSession.tsbSetup.startTitle")}
            </Typography>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("pathogenSession.tsbSetup.startDesc")}
            </Typography>
          </Box>
        </Stack>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Box sx={{ mb: 2.5, p: 2, bgcolor: "background.default", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mb: 1 }}>
            {t("pathogenSession.tsbSetup.applicableScope")}
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{
            flexWrap: "wrap"
          }}>
            <Chip
              label={
                <Trans
                  i18nKey="pathogenSession.tsbSetup.locationsChip"
                  ns="testingWorkspace"
                  values={{ count: session.totalLocations }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              }
              size="small"
              sx={{ bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 600 }}
            />
            {tsbTests.map((testItem) => (
              <Chip
                key={testItem.testCode}
                label={
                  <span>
                    <Ltr>{testItem.testCode}</Ltr> ({testItem.displayName})
                  </span>
                }
                size="small"
                sx={{ bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 600 }}
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
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
              <TextField
                id="tsb-media-lot"
                slotProps={{ select: { "aria-describedby": describedByWithHint("tsb-media-lot", "tsb-media-lot") } }}
                select
                label={t("pathogenSession.tsbSetup.mediaLotLabel")}
                value={selectedMediaId}
                onChange={(e) => setSelectedMediaId(Number(e.target.value))}
                helperText={t("pathogenSession.tsbSetup.mediaLotHelper")}
                fullWidth
                size="small"
              >
                {mediaLots.map((m) => (
                  <MenuItem key={m.id} value={m.id}>
                    {t("pathogenSession.tsbSetup.mediaLotOption", {
                      lotNumber: m.lotNumber ?? "—",
                      material: m.materialName ?? m.mediaTypeName ?? t("pathogenSession.tsbSetup.mediaLotFallback"),
                      expiry: formatLabCalendarDate(m.expiryDate ?? "")
                    })}
                  </MenuItem>
                ))}
              </TextField>
              <FieldHint id="tsb-media-lot" label={t("pathogenSession.tsbSetup.mediaLotHintLabel")} hint={hints.tsbMediaLot} />
            </Stack>

            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
              <TextField
                id="tsb-incubator"
                slotProps={{ select: { "aria-describedby": describedByWithHint("tsb-incubator", "tsb-incubator") } }}
                select
                label={t("pathogenSession.tsbSetup.incubatorLabel")}
                value={selectedIncubatorId}
                onChange={(e) => setSelectedIncubatorId(Number(e.target.value))}
                helperText={t("pathogenSession.tsbSetup.incubatorHelper")}
                fullWidth
                size="small"
              >
                {incubators.map((inc) => (
                  <MenuItem key={inc.id} value={inc.id}>
                    {t("pathogenSession.tsbSetup.incubatorOption", {
                      code: inc.code ?? "—",
                      name: inc.name ?? t("pathogenSession.tsbSetup.incubatorFallback"),
                      location: inc.location ?? t("pathogenSession.tsbSetup.labFallback")
                    })}
                  </MenuItem>
                ))}
              </TextField>
              <FieldHint id="tsb-incubator" label={t("pathogenSession.tsbSetup.incubatorHintLabel")} hint={hints.incubator} />
            </Stack>

            {/* Read-Only Controlled Parameters from Test Master */}
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
              <TextField
                id="tsb-required-temperature"
                label={t("pathogenSession.tsbSetup.requiredTempLabel")}
                value={session.sharedTsb.requiredTemperatureRange ?? t("pathogenSession.tsbSetup.notConfigured")}
                helperText={session.sharedTsb.windowNotConfigured
                  ? t("pathogenSession.tsbSetup.windowNotConfiguredHelper")
                  : t("pathogenSession.tsbSetup.requiredTempHelper")}
                error={Boolean(session.sharedTsb.windowNotConfigured)}
                fullWidth
                size="small"
                sx={{ bgcolor: "background.default" }}
                slotProps={{
                  input: { readOnly: true, "aria-describedby": describedByWithHint("tsb-required-temperature", "tsb-required-temperature") }
                }}
              />
              <FieldHint id="tsb-required-temperature" label={t("pathogenSession.tsbSetup.requiredTempHintLabel")} hint={hints.requiredTemperature} />
            </Stack>

            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
              <TextField
                id="tsb-required-time"
                label={t("pathogenSession.tsbSetup.requiredTimeLabel")}
                value={session.sharedTsb.requiredDurationRange ?? t("pathogenSession.tsbSetup.notConfigured")}
                helperText={t("pathogenSession.tsbSetup.requiredTimeHelper")}
                error={Boolean(session.sharedTsb.windowNotConfigured)}
                fullWidth
                size="small"
                sx={{ bgcolor: "background.default" }}
                slotProps={{
                  input: { readOnly: true, "aria-describedby": describedByWithHint("tsb-required-time", "tsb-required-time") }
                }}
              />
              <FieldHint id="tsb-required-time" label={t("pathogenSession.tsbSetup.requiredTimeHintLabel")} hint={hints.requiredTime} />
            </Stack>
          </Box>
        )}

        <Stack
          direction="row"
          spacing={2}
          sx={{
            justifyContent: "flex-end",
            mt: 3
          }}>
          <Button
            variant="contained"
            color="primary"
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <PlayArrowIcon />}
            onClick={handleStartTsb}
            disabled={submitting || loadingLookups || !selectedMediaId || !selectedIncubatorId}
            sx={{ px: 3, fontWeight: 700 }}
          >
            {t("pathogenSession.tsbSetup.startButton")}
          </Button>

          <Button
            variant="outlined"
            endIcon={<ArrowForwardIcon />}
            onClick={onNext}
            sx={{ px: 3, fontWeight: 600 }}
          >
            {t("pathogenSession.tsbSetup.nextTestWorkflows")}
          </Button>
        </Stack>
      </Paper>
    </Stack>
  );
}
