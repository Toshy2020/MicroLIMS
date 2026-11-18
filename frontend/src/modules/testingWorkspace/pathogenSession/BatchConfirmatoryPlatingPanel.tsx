import { useState, useEffect, useMemo } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Radio,
  RadioGroup,
  FormControlLabel,
  MenuItem,
  TextField,
  Alert,
  Chip,
  CircularProgress,
  Tabs,
  Tab,
  Tooltip,
  Card,
  CardContent,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import {
  PathogenTestingSessionDto,
  GrowthObservation,
  ConfirmationResult,
  EligibleLocationForConfirmationDto,
  BatchConfirmatoryPlateReadingInput
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { brandColors } from "../../../theme";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  session: PathogenTestingSessionDto;
  onNext: () => void;
  onBack: () => void;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
}

interface PathogenSetupState {
  mediaLots: (number | "")[];
  incubatorId: number | "";
  startDateTime: string;
}

interface ReleasedMediaOption {
  id: number;
  materialId?: number | null;
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

export function BatchConfirmatoryPlatingPanel({ session, onNext, onBack, onUpdated }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const [phase, setPhase] = useState<"setup" | "readout">("setup");
  const [activeTab, setActiveTab] = useState(0);

  // Eligible locations from backend gating
  const [eligibleLocations, setEligibleLocations] = useState<EligibleLocationForConfirmationDto[]>([]);
  const [loadingEligible, setLoadingEligible] = useState(true);

  // Lookups
  const [releasedMedia, setReleasedMedia] = useState<ReleasedMediaOption[]>([]);
  const [incubators, setIncubators] = useState<IncubatorOption[]>([]);
  const [loadingLookups, setLoadingLookups] = useState(false);

  // Media Setup state keyed by testCode
  const [setupStateByPathogen, setSetupStateByPathogen] = useState<Record<string, PathogenSetupState>>({});

  // Plate readings state keyed by `${locationId}_${mediumIndex}`
  const [plateReadings, setPlateReadings] = useState<Record<string, GrowthObservation>>({});

  // Optional biochemical observation comment for Detected (+) results
  const [biochemicalComment, setBiochemicalComment] = useState<string>("");

  // Action status
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  // Load eligible locations from backend
  useEffect(() => {
    let active = true;
    setLoadingEligible(true);
    setError(null);

    PathogenSessionService.getEligibleConfirmations(session.sampleId)
      .then((locations) => {
        if (!active) return;
        setEligibleLocations(locations ?? []);

        // Initialize readings from existing session matrix if available
        const initialReadings: Record<string, GrowthObservation> = {};
        for (const loc of locations ?? []) {
          const matrixCell = session.resultMatrix.find(
            (c) => c.sampleLocationId === loc.locationId && c.testCode === loc.testCode
          );
          if (matrixCell?.confirmatoryPlates && matrixCell.confirmatoryPlates.length > 0) {
            matrixCell.confirmatoryPlates.forEach((plate) => {
              initialReadings[`${loc.locationId}_${plate.mediumIndex}`] = plate.observation as GrowthObservation;
            });
          }
        }
        setPlateReadings(initialReadings);

        // If any locations already have confirmatory plates recorded or in progress, start in readout phase
        const hasExistingPlates = Object.keys(initialReadings).length > 0;
        if (hasExistingPlates) {
          setPhase("readout");
        }
      })
      .catch((e: unknown) => {
        if (!active) return;
        setError(getErrorMessage(e, t("pathogenSession.confirmatory.loadEligibleError")));
      })
      .finally(() => {
        if (active) setLoadingEligible(false);
      });

    return () => {
      active = false;
    };
  }, [session.sampleId, session.resultMatrix, t]);

  // Load released media lots and incubators
  useEffect(() => {
    setLoadingLookups(true);
    Promise.all([
      masterDataOptions.getReleasedMedia().catch(() => [] as ReleasedMediaOption[]),
      masterDataOptions.getEquipment("Incubator").catch(() => [] as IncubatorOption[])
    ])
      .then(([m, inc]) => {
        setReleasedMedia(m ?? []);
        setIncubators(inc ?? []);
      })
      .finally(() => setLoadingLookups(false));
  }, []);

  // Distinct pathogens that have eligible locations
  const eligiblePathogens = useMemo(() => {
    const map = new Map<string, {
      testCode: string;
      testDisplayName: string;
      testOrderId: number;
      requiredMediaCount: number;
      locations: EligibleLocationForConfirmationDto[];
    }>();

    for (const loc of eligibleLocations) {
      if (!map.has(loc.testCode)) {
        map.set(loc.testCode, {
          testCode: loc.testCode,
          testDisplayName: loc.testDisplayName,
          testOrderId: loc.testOrderId,
          requiredMediaCount: loc.requiredConfirmatoryMediaCount || 1, // from Test Master; never by organism (CLAUDE.md Rule 3.2)
          locations: []
        });
      }
      map.get(loc.testCode)!.locations.push(loc);
    }

    return Array.from(map.values());
  }, [eligibleLocations]);

  const currentPathogen = eligiblePathogens[activeTab] ?? eligiblePathogens[0];

  // The confirmatory step window the server resolved from this test's own
  // media in Test Master (0 when none is configured) - never a default.
  const confirmatoryStepWindow = session.assignedTests
    .find((testItem) => testItem.testOrderId === currentPathogen?.testOrderId)
    ?.steps.find((s) => s.stepType === "ConfirmatoryPlating");

  // Initialize setup state for current pathogen
  useEffect(() => {
    if (!currentPathogen) return;

    setSetupStateByPathogen((prev) => {
      if (prev[currentPathogen.testCode]) return prev;

      return {
        ...prev,
        [currentPathogen.testCode]: {
          mediaLots: Array.from({ length: currentPathogen.requiredMediaCount }).map((_, idx) => {
            if (releasedMedia.length > idx) return releasedMedia[idx].id;
            return "";
          }),
          incubatorId: incubators.length > 0 ? incubators[0].id : "",
          startDateTime: new Date().toISOString().slice(0, 16)
        }
      };
    });
  }, [currentPathogen, releasedMedia, incubators]);

  // Handle media lot selection change
  const handleMediaLotChange = (testCode: string, mediumIndex: number, mediaLotId: number | "") => {
    setSetupStateByPathogen((prev) => {
      const current = prev[testCode] ?? {
        mediaLots: [],
        incubatorId: "",
        startDateTime: new Date().toISOString().slice(0, 16)
      };
      const lots = [...current.mediaLots];
      lots[mediumIndex] = mediaLotId;
      return {
        ...prev,
        [testCode]: { ...current, mediaLots: lots }
      };
    });
  };

  const handleIncubatorChange = (testCode: string, incubatorId: number | "") => {
    setSetupStateByPathogen((prev) => {
      const current = prev[testCode] ?? {
        mediaLots: [],
        incubatorId: "",
        startDateTime: new Date().toISOString().slice(0, 16)
      };
      return {
        ...prev,
        [testCode]: { ...current, incubatorId }
      };
    });
  };

  const handleStartTimeChange = (testCode: string, startDateTime: string) => {
    setSetupStateByPathogen((prev) => {
      const current = prev[testCode] ?? {
        mediaLots: [],
        incubatorId: "",
        startDateTime: new Date().toISOString().slice(0, 16)
      };
      return {
        ...prev,
        [testCode]: { ...current, startDateTime }
      };
    });
  };

  // Start Shared Confirmatory Setup
  const handleStartConfirmatorySetup = async () => {
    if (!currentPathogen) return;
    const setup = setupStateByPathogen[currentPathogen.testCode];

    if (!setup || setup.mediaLots.some((lot) => !lot) || !setup.incubatorId) {
      setError(t("pathogenSession.confirmatory.selectMediaValidation", { count: currentPathogen.requiredMediaCount }));
      return;
    }

    setError(null);
    setSubmitting(true);

    try {
      const mediaLotsSelected = setup.mediaLots.map((id) => Number(id));
      const materialIds = mediaLotsSelected.map((lotId) => {
        const lot = releasedMedia.find((m) => m.id === lotId);
        return lot?.materialId ?? lotId;
      });

      const updated = await PathogenSessionService.startConfirmatorySetup(session.sampleId, {
        testOrderId: currentPathogen.testOrderId,
        locationIds: currentPathogen.locations.map((l) => l.locationId),
        mediaMaterialIds: materialIds,
        mediaLotIds: mediaLotsSelected,
        incubatorEquipmentId: Number(setup.incubatorId),
        incubationStartUtc: setup.startDateTime ? new Date(setup.startDateTime).toISOString() : new Date().toISOString()
      });

      onUpdated(updated);
      setPhase("readout");
      setSaveSuccess(true);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("pathogenSession.confirmatory.startSetupError")));
    } finally {
      setSubmitting(false);
    }
  };

  // Handle Plate Reading change
  const handleObservationChange = (locationId: number, mediumIndex: number, obs: GrowthObservation) => {
    const key = `${locationId}_${mediumIndex}`;
    setPlateReadings((prev) => ({
      ...prev,
      [key]: obs
    }));
    setSaveSuccess(false);
  };

  // Calculate real-time agreement for a location
  const getAgreementForLocation = (location: EligibleLocationForConfirmationDto): {
    result: ConfirmationResult | null;
    isComplete: boolean;
    conformingCount: number;
    nonConformingCount: number;
    noGrowthCount: number;
  } => {
    const count = location.requiredConfirmatoryMediaCount || 1;
    const obsList: GrowthObservation[] = [];

    for (let idx = 0; idx < count; idx++) {
      const val = plateReadings[`${location.locationId}_${idx}`];
      if (val) obsList.push(val);
    }

    const isComplete = obsList.length === count;
    if (!isComplete) {
      return { result: null, isComplete: false, conformingCount: 0, nonConformingCount: 0, noGrowthCount: 0 };
    }

    const conforming = obsList.filter((o) => o === GrowthObservation.GrowthConforming).length;
    const nonConforming = obsList.filter((o) => o === GrowthObservation.GrowthNonConforming).length;
    const noGrowth = obsList.filter((o) => o === GrowthObservation.NoGrowth).length;

    let result: ConfirmationResult;
    if (conforming === count) {
      result = ConfirmationResult.Detected;
    } else if (nonConforming + noGrowth === count) {
      result = ConfirmationResult.NotDetected;
    } else {
      result = ConfirmationResult.Inconclusive;
    }

    return {
      result,
      isComplete: true,
      conformingCount: conforming,
      nonConformingCount: nonConforming,
      noGrowthCount: noGrowth
    };
  };

  // Agreement summary statistics across all eligible locations
  const stats = useMemo(() => {
    let detected = 0;
    let notDetected = 0;
    let inconclusive = 0;
    let pending = 0;

    for (const loc of eligibleLocations) {
      const agr = getAgreementForLocation(loc);
      if (!agr.isComplete) {
        pending++;
      } else if (agr.result === ConfirmationResult.Detected) {
        detected++;
      } else if (agr.result === ConfirmationResult.NotDetected) {
        notDetected++;
      } else if (agr.result === ConfirmationResult.Inconclusive) {
        inconclusive++;
      }
    }

    return {
      total: eligibleLocations.length,
      detected,
      notDetected,
      inconclusive,
      pending,
      allComplete: pending === 0 && eligibleLocations.length > 0
    };
  }, [eligibleLocations, plateReadings]);

  // Submit all batch confirmatory plate readings
  const handleSaveConfirmatoryReadings = async () => {
    if (!stats.allComplete) {
      setError(t("pathogenSession.confirmatory.completePendingError", { count: stats.pending }));
      return;
    }

    setError(null);
    setSubmitting(true);

    try {
      const readings: BatchConfirmatoryPlateReadingInput[] = [];

      for (const loc of eligibleLocations) {
        const count = loc.requiredConfirmatoryMediaCount || 1;
        for (let idx = 0; idx < count; idx++) {
          const obs = plateReadings[`${loc.locationId}_${idx}`];
          if (obs) {
            // Find media lot / materialId from setup or session
            const setup = setupStateByPathogen[loc.testCode];
            const lotId = setup?.mediaLots[idx];
            const lot = releasedMedia.find((m) => m.id === lotId);
            const materialId = lot?.materialId ?? (Number(lotId) > 0 ? Number(lotId) : 1);

            readings.push({
              locationPathogenObservationId: loc.primaryObservationId,
              mediumIndex: idx,
              materialId,
              observation: obs
            });
          }
        }
      }

      const updated = await PathogenSessionService.saveConfirmatoryReadings(session.sampleId, {
        readings,
        biochemicalComment: biochemicalComment.trim() || undefined
      });

      onUpdated(updated);
      setSaveSuccess(true);

      // Advance to Review & Complete (Step 5)
      onNext();
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("pathogenSession.confirmatory.saveReadingsError")));
    } finally {
      setSubmitting(false);
    }
  };

  // If no locations are eligible for confirmation (100% negative primary observations)
  if (!loadingEligible && eligibleLocations.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <CheckCircleOutlineIcon sx={{ fontSize: 48, color: theme.custom.status.notDetected.text, mb: 1.5 }} />
        <Typography sx={{ fontSize: 18, fontWeight: 800, color: "text.primary", mb: 1 }}>
          {t("pathogenSession.confirmatory.notRequiredTitle")}
        </Typography>
        <Typography sx={{ fontSize: 13, color: "text.secondary", maxWidth: 600, mx: "auto", mb: 3 }}>
          <Trans
            i18nKey="pathogenSession.confirmatory.notRequiredDesc"
            ns="testingWorkspace"
            components={{ strong: <strong /> }}
          />
        </Typography>
        <Stack direction="row" spacing={2} sx={{
          justifyContent: "center"
        }}>
          <Button variant="outlined" onClick={onBack}>
            {t("pathogenSession.confirmatory.backToPrimaryMatrix")}
          </Button>
          <Button variant="contained" color="success" endIcon={<ArrowForwardIcon />} onClick={onNext}>
            {t("pathogenSession.confirmatory.proceedToReview")}
          </Button>
        </Stack>
      </Paper>
    );
  }

  return (
    <Stack spacing={2.5}>
      {/* Top Banner */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: theme.custom.status.purple.border, bgcolor: theme.custom.status.purple.bg }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            justifyContent: "space-between",
            alignItems: { sm: "center" }
          }}>
          <Box>
            <Stack
              direction="row"
              spacing={1.5}
              sx={{
                alignItems: "center",
                mb: 0.75
              }}>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: "text.primary" }}>
                {t("pathogenSession.confirmatory.title")}
              </Typography>
              <Chip
                label={
                  <Trans
                    i18nKey="pathogenSession.confirmatory.flaggedLocationsChip"
                    ns="testingWorkspace"
                    values={{ count: eligibleLocations.length }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                }
                size="small"
                sx={{ bgcolor: theme.custom.status.detected.bg, color: theme.custom.status.detected.text, fontWeight: 700 }}
              />
            </Stack>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("pathogenSession.confirmatory.subtitle")}
            </Typography>
          </Box>

          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Button
              variant={phase === "setup" ? "contained" : "outlined"}
              size="small"
              onClick={() => setPhase("setup")}
              sx={{ fontWeight: 700 }}
            >
              {t("pathogenSession.confirmatory.phaseSetup")}
            </Button>
            <Button
              variant={phase === "readout" ? "contained" : "outlined"}
              size="small"
              onClick={() => setPhase("readout")}
              sx={{ fontWeight: 700 }}
            >
              {t("pathogenSession.confirmatory.phaseReadout")}
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {saveSuccess && (
        <Alert severity="success" onClose={() => setSaveSuccess(false)}>
          {t("pathogenSession.confirmatory.saveSuccess")}
        </Alert>
      )}

      {/* Pathogen Tabs (if multiple pathogens have growth) */}
      {eligiblePathogens.length > 1 && (
        <Tabs
          value={activeTab}
          onChange={(_, val) => setActiveTab(val)}
          sx={{
            borderBottom: "1px solid",
            borderBottomColor: "divider",
            "& .MuiTab-root": { fontWeight: 700, textTransform: "none", fontSize: 13 }
          }}
        >
          {eligiblePathogens.map((p) => (
            <Tab
              key={p.testCode}
              label={
                <Stack direction="row" spacing={1} sx={{
                  alignItems: "center"
                }}>
                  <span>{p.testDisplayName} (<Ltr>{p.testCode}</Ltr>)</span>
                  <Chip
                    label={
                      <Trans
                        i18nKey="pathogenSession.confirmatory.tabLocationCount"
                        ns="testingWorkspace"
                        values={{ count: p.locations.length }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    }
                    size="small"
                    sx={{ fontSize: 12, height: 20 }}
                  />
                </Stack>
              }
            />
          ))}
        </Tabs>
      )}

      {/* PHASE 1: SHARED MEDIA SETUP FORM */}
      {phase === "setup" && currentPathogen && (
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
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
                {t("pathogenSession.confirmatory.setupTitle", { testName: currentPathogen.testDisplayName })}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Trans
                  i18nKey="pathogenSession.confirmatory.setupSubtitle"
                  ns="testingWorkspace"
                  values={{ count: currentPathogen.requiredMediaCount, testCode: currentPathogen.testCode }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>
          </Stack>

          <Box sx={{ p: 2, mb: 3, bgcolor: "background.default", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 1 }}>
              <Trans
                i18nKey="pathogenSession.confirmatory.flaggedSamplingLocationsHeading"
                ns="testingWorkspace"
                values={{ count: currentPathogen.locations.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap sx={{
              flexWrap: "wrap"
            }}>
              {currentPathogen.locations.map((loc) => (
                <Chip
                  key={loc.locationId}
                  label={`${loc.locationName} (${loc.growthObservationDisplay})`}
                  size="small"
                  sx={{
                    bgcolor: loc.growthObservation === GrowthObservation.GrowthConforming ? theme.custom.status.detected.bg : theme.custom.status.action.bg,
                    color: loc.growthObservation === GrowthObservation.GrowthConforming ? theme.custom.status.detected.text : theme.custom.status.action.text,
                    fontWeight: 600
                  }}
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
              {/* Media Lot Selectors for N media */}
              {Array.from({ length: currentPathogen.requiredMediaCount }).map((_, idx) => {
                const setup = setupStateByPathogen[currentPathogen.testCode];
                const selectedLotId = setup?.mediaLots[idx] ?? "";

                return (
                  <TextField
                    key={idx}
                    select
                    label={t("pathogenSession.confirmatory.mediumLotLabel", { index: idx + 1 })}
                    value={selectedLotId}
                    onChange={(e) => handleMediaLotChange(currentPathogen.testCode, idx, Number(e.target.value))}
                    helperText={t("pathogenSession.confirmatory.mediumLotHelper", { index: idx + 1 })}
                    fullWidth
                    size="small"
                  >
                    {releasedMedia.map((m) => (
                      <MenuItem key={m.id} value={m.id}>
                        {t("pathogenSession.confirmatory.mediaLotOption", {
                          lotNumber: m.lotNumber ?? "—",
                          material: m.materialName ?? m.mediaTypeName ?? t("pathogenSession.confirmatory.mediaLotFallback"),
                          expiry: formatLabCalendarDate(m.expiryDate ?? "")
                        })}
                      </MenuItem>
                    ))}
                  </TextField>
                );
              })}

              {/* Incubator Selector */}
              <TextField
                select
                label={t("pathogenSession.confirmatory.incubatorLabel")}
                value={setupStateByPathogen[currentPathogen.testCode]?.incubatorId ?? ""}
                onChange={(e) => handleIncubatorChange(currentPathogen.testCode, Number(e.target.value))}
                helperText={t("pathogenSession.confirmatory.incubatorHelper")}
                fullWidth
                size="small"
              >
                {incubators.map((inc) => (
                  <MenuItem key={inc.id} value={inc.id}>
                    {t("pathogenSession.confirmatory.incubatorOption", {
                      code: inc.code ?? "—",
                      name: inc.name ?? t("pathogenSession.confirmatory.incubatorFallback"),
                      location: inc.location ?? t("pathogenSession.confirmatory.labFallback")
                    })}
                  </MenuItem>
                ))}
              </TextField>

              {/* Incubation Start Time */}
              <TextField
                label={t("pathogenSession.confirmatory.startTimeLabel")}
                type="datetime-local"
                value={setupStateByPathogen[currentPathogen.testCode]?.startDateTime ?? ""}
                onChange={(e) => handleStartTimeChange(currentPathogen.testCode, e.target.value)}
                helperText={t("pathogenSession.confirmatory.startTimeHelper")}
                fullWidth
                size="small"
                slotProps={{
                  inputLabel: { shrink: true },
                  htmlInput: { dir: "ltr" }
                }}
              />

              {/* Read-only Incubation Duration from Test Master */}
              <Box sx={{ p: 1.5, backgroundColor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider", gridColumn: { xs: "1", sm: "span 2" } }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("pathogenSession.confirmatory.incubationDurationTitle")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 700, color: confirmatoryStepWindow && confirmatoryStepWindow.incubationMaxHours > 0 ? "text.primary" : "error.main" }}>
                  {confirmatoryStepWindow && confirmatoryStepWindow.incubationMaxHours > 0 ? (
                    <Trans
                      i18nKey="pathogenSession.confirmatory.incubationDurationRange"
                      ns="testingWorkspace"
                      values={{
                        min: confirmatoryStepWindow.incubationMinHours,
                        max: confirmatoryStepWindow.incubationMaxHours
                      }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  ) : (
                    t("pathogenSession.confirmatory.notConfigured")
                  )}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {t("pathogenSession.confirmatory.incubationDurationHelper")}
                </Typography>
              </Box>
            </Box>
          )}

          <Stack
            direction="row"
            spacing={2}
            sx={{
              justifyContent: "flex-end",
              mt: 3
            }}>
            <Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={onBack}>
              {t("pathogenSession.confirmatory.backToPrimaryMatrix")}
            </Button>
            <Button
              variant="contained"
              color="primary"
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <PlayArrowIcon />}
              onClick={handleStartConfirmatorySetup}
              disabled={submitting || loadingLookups}
              sx={{ px: 3, fontWeight: 700 }}
            >
              {t("pathogenSession.confirmatory.startIncubationButton")}
            </Button>
          </Stack>
        </Paper>
      )}

      {/* PHASE 2: PLATE OBSERVATION READOUT GRID */}
      {phase === "readout" && (
        <Stack spacing={2.5}>
          {/* Agreement Stats Bar */}
          <Paper sx={{ p: 2, borderRadius: 2, border: "1px solid", borderColor: theme.custom.status.purple.border, bgcolor: theme.custom.status.purple.bg }}>
            <Stack
              direction={{ xs: "column", md: "row" }}
              spacing={2}
              sx={{
                justifyContent: "space-between",
                alignItems: { md: "center" }
              }}>
              <Stack useFlexGap
                direction="row"
                spacing={1.5}
                sx={{
                  alignItems: "center",
                  flexWrap: "wrap"
                }}>
                <Typography sx={{ fontSize: 14, fontWeight: 800, color: "text.primary" }}>
                  {t("pathogenSession.confirmatory.agreementStatusTitle")}
                </Typography>
                <Chip
                  label={
                    <Trans
                      i18nKey="pathogenSession.confirmatory.detectedChip"
                      ns="testingWorkspace"
                      values={{ count: stats.detected }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  }
                  size="small"
                  sx={{ bgcolor: stats.detected > 0 ? theme.custom.status.detected.bg : theme.custom.status.pending.bg, color: stats.detected > 0 ? theme.custom.status.detected.text : theme.custom.status.pending.text, fontWeight: 700 }}
                />
                <Chip
                  label={
                    <Trans
                      i18nKey="pathogenSession.confirmatory.notDetectedChip"
                      ns="testingWorkspace"
                      values={{ count: stats.notDetected }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  }
                  size="small"
                  sx={{ bgcolor: stats.notDetected > 0 ? theme.custom.status.notDetected.bg : theme.custom.status.pending.bg, color: stats.notDetected > 0 ? theme.custom.status.notDetected.text : theme.custom.status.pending.text, fontWeight: 700 }}
                />
                {stats.inconclusive > 0 && (
                  <Chip
                    icon={<WarningAmberOutlinedIcon sx={{ fontSize: 13 }} />}
                    label={
                      <Trans
                        i18nKey="pathogenSession.confirmatory.inconclusiveChip"
                        ns="testingWorkspace"
                        values={{ count: stats.inconclusive }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    }
                    size="small"
                    sx={{ bgcolor: theme.custom.status.action.bg, color: theme.custom.status.action.text, fontWeight: 700 }}
                  />
                )}
                {stats.pending > 0 && (
                  <Chip
                    label={
                      <Trans
                        i18nKey="pathogenSession.confirmatory.pendingReadsChip"
                        ns="testingWorkspace"
                        values={{ count: stats.pending }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    }
                    size="small"
                    sx={{ bgcolor: theme.custom.status.pending.bg, color: theme.custom.status.pending.text, fontWeight: 600 }}
                  />
                )}
              </Stack>

              <Stack direction="row" spacing={1.5}>
                <Button variant="outlined" size="small" onClick={() => setPhase("setup")}>
                  {t("pathogenSession.confirmatory.modifySetup")}
                </Button>
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <SaveOutlinedIcon />}
                  onClick={handleSaveConfirmatoryReadings}
                  disabled={submitting || !stats.allComplete}
                  sx={{ px: 2.5, fontWeight: 700 }}
                >
                  {t("pathogenSession.confirmatory.saveReadingsContinue")}
                </Button>
              </Stack>
            </Stack>
          </Paper>

          {/* Plate Observation Table */}
          <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
            <Box sx={{ maxHeight: 520, overflow: "auto" }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ position: "sticky", insetInlineStart: 0, zIndex: 3, bgcolor: theme.custom.chrome.tableHeaderBg, color: theme.custom.chrome.tableHeaderText, fontWeight: 800, minWidth: 220, borderInlineEnd: "2px solid", borderInlineEndColor: "divider" }}>
                      <Trans
                        i18nKey="pathogenSession.confirmatory.samplingLocationHeader"
                        ns="testingWorkspace"
                        values={{ count: eligibleLocations.length }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    </TableCell>
                    <TableCell align="center" sx={{ bgcolor: "background.default", fontWeight: 800, minWidth: 140 }}>
                      {t("pathogenSession.confirmatory.primaryReadingHeader")}
                    </TableCell>

                    {/* Medium Columns based on max required media count */}
                    {Array.from({ length: currentPathogen?.requiredMediaCount || 1 }).map((_, medIdx) => (
                      <TableCell
                        key={medIdx}
                        align="center"
                        sx={{ bgcolor: theme.custom.status.purple.bg, fontWeight: 800, minWidth: 260, borderInlineEnd: "1px solid", borderInlineEndColor: "divider" }}
                      >
                        <Typography sx={{ fontSize: 13, fontWeight: 800, color: theme.palette.primary.main }}>
                          <Trans
                            i18nKey="pathogenSession.confirmatory.mediumColTitle"
                            ns="testingWorkspace"
                            values={{ index: medIdx + 1 }}
                            components={{ ltr: <Ltr>{null}</Ltr> }}
                          />
                        </Typography>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                          {t("pathogenSession.confirmatory.mediumColSubtitle")}
                        </Typography>
                      </TableCell>
                    ))}

                    <TableCell align="center" sx={{ bgcolor: "background.default", fontWeight: 800, minWidth: 160 }}>
                      {t("pathogenSession.confirmatory.agreementEvaluationHeader")}
                    </TableCell>
                  </TableRow>
                </TableHead>

                <TableBody>
                  {eligibleLocations.map((loc, idx) => {
                    const agr = getAgreementForLocation(loc);
                    const reqCount = loc.requiredConfirmatoryMediaCount || 1;

                    return (
                      <TableRow key={loc.locationId} hover sx={{ bgcolor: idx % 2 === 0 ? "background.paper" : "background.default" }}>
                        {/* Sticky Location Name Column */}
                        <TableCell sx={{ position: "sticky", insetInlineStart: 0, zIndex: 2, bgcolor: idx % 2 === 0 ? "background.paper" : "background.default", fontWeight: 700, borderInlineEnd: "2px solid", borderInlineEndColor: "divider" }}>
                          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                            {loc.locationName}
                          </Typography>
                          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                            {loc.testDisplayName} (<Ltr>{loc.testCode}</Ltr>)
                          </Typography>
                        </TableCell>

                        {/* Primary Reading Badge */}
                        <TableCell align="center">
                          <Chip
                            label={loc.growthObservationDisplay}
                            size="small"
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              bgcolor: loc.growthObservation === GrowthObservation.GrowthConforming ? theme.custom.status.detected.bg : theme.custom.status.action.bg,
                              color: loc.growthObservation === GrowthObservation.GrowthConforming ? theme.custom.status.detected.text : theme.custom.status.action.text
                            }}
                          />
                        </TableCell>

                        {/* Medium Observation Radios */}
                        {Array.from({ length: reqCount }).map((_, medIdx) => {
                          const key = `${loc.locationId}_${medIdx}`;
                          const val = plateReadings[key] ?? "";

                          return (
                            <TableCell key={medIdx} align="center" sx={{ borderInlineEnd: "1px solid", borderInlineEndColor: "divider" }}>
                              <RadioGroup
                                row
                                value={val}
                                onChange={(e) => handleObservationChange(loc.locationId, medIdx, e.target.value as GrowthObservation)}
                                sx={{ justifyContent: "center", gap: 1 }}
                              >
                                <FormControlLabel
                                  value={GrowthObservation.NoGrowth}
                                  control={<Radio size="small" sx={{ color: theme.custom.status.notDetected.text, "&.Mui-checked": { color: theme.custom.status.notDetected.text } }} />}
                                  label={<Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.notDetected.text }}>{t("pathogenSession.confirmatory.radioNoGrowth")}</Typography>}
                                />
                                <FormControlLabel
                                  value={GrowthObservation.GrowthNonConforming}
                                  control={<Radio size="small" sx={{ color: theme.custom.status.inconclusive.text, "&.Mui-checked": { color: theme.custom.status.inconclusive.text } }} />}
                                  label={<Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.inconclusive.text }}>{t("pathogenSession.confirmatory.radioNonConf")}</Typography>}
                                />
                                <FormControlLabel
                                  value={GrowthObservation.GrowthConforming}
                                  control={<Radio size="small" sx={{ color: theme.custom.status.detected.text, "&.Mui-checked": { color: theme.custom.status.detected.text } }} />}
                                  label={<Typography sx={{ fontSize: 12, fontWeight: 800, color: theme.custom.status.detected.text }}>{t("pathogenSession.confirmatory.radioConf")}</Typography>}
                                />
                              </RadioGroup>
                            </TableCell>
                          );
                        })}

                        {/* Real-time Agreement Badge */}
                        <TableCell align="center">
                          {!agr.isComplete ? (
                            <Chip
                              label={t("pathogenSession.confirmatory.pendingReadBadge")}
                              size="small"
                              sx={{ fontSize: 12, fontWeight: 600, bgcolor: theme.custom.status.pending.bg, color: theme.custom.status.pending.text }}
                            />
                          ) : agr.result === ConfirmationResult.Detected ? (
                            <Chip
                              label={t("pathogenSession.confirmatory.detectedBadge")}
                              size="small"
                              sx={{ fontSize: 12, fontWeight: 800, bgcolor: theme.custom.status.detected.bg, color: theme.custom.status.detected.text }}
                            />
                          ) : agr.result === ConfirmationResult.NotDetected ? (
                            <Chip
                              label={t("pathogenSession.confirmatory.notDetectedBadge")}
                              size="small"
                              sx={{ fontSize: 12, fontWeight: 700, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                            />
                          ) : (
                            <Tooltip title={t("pathogenSession.confirmatory.inconclusiveTooltip")}>
                              <Chip
                                icon={<WarningAmberOutlinedIcon sx={{ fontSize: 12 }} />}
                                label={t("pathogenSession.confirmatory.inconclusiveBadge")}
                                size="small"
                                sx={{ fontSize: 12, fontWeight: 800, bgcolor: theme.custom.status.action.bg, color: theme.custom.status.action.text }}
                              />
                            </Tooltip>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </Box>
          </Paper>

          {/* Conditional Biochemical Supporting Observation Card */}
          {stats.detected > 0 && (
            <Card sx={{ bgcolor: theme.custom.status.inconclusive.bg, border: "1px solid", borderColor: theme.custom.status.inconclusive.border, borderRadius: 2 }}>
              <CardContent sx={{ p: 2.5 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 800, color: theme.custom.status.inconclusive.text, mb: 0.5, display: "flex", alignItems: "center", gap: 1 }}>
                  <span aria-hidden="true">🧪</span> {t("pathogenSession.confirmatory.biochemicalTitle")}
                </Typography>
                <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text, mb: 1.5, display: "block" }}>
                  <Trans
                    i18nKey="pathogenSession.confirmatory.biochemicalDesc"
                    ns="testingWorkspace"
                    values={{ count: stats.detected }}
                    components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
                  />
                </Typography>
                <TextField
                  fullWidth
                  multiline
                  rows={3}
                  placeholder={t("pathogenSession.confirmatory.biochemicalPlaceholder")}
                  slotProps={{ htmlInput: { "aria-label": t("pathogenSession.confirmatory.biochemicalAriaLabel") } }}
                  value={biochemicalComment}
                  onChange={(e) => setBiochemicalComment(e.target.value)}
                  size="small"
                  sx={{ bgcolor: "background.paper", borderRadius: 1 }}
                />
              </CardContent>
            </Card>
          )}

          {/* Action Footer */}
          <Stack direction="row" spacing={2} sx={{
            justifyContent: "flex-end"
          }}>
            <Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={() => setPhase("setup")}>
              {t("pathogenSession.confirmatory.backToSetup")}
            </Button>
            <Button
              variant="contained"
              color="primary"
              endIcon={<ArrowForwardIcon />}
              onClick={handleSaveConfirmatoryReadings}
              disabled={submitting || !stats.allComplete}
              sx={{ px: 3, fontWeight: 700 }}
            >
              {submitting ? <CircularProgress size={16} color="inherit" /> : t("pathogenSession.confirmatory.saveConfirmatoryContinue")}
            </Button>
          </Stack>
        </Stack>
      )}
    </Stack>
  );
}
