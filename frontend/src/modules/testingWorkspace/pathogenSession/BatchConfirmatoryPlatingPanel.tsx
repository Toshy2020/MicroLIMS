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
  FormControl,
  Select,
  MenuItem,
  TextField,
  Alert,
  Chip,
  CircularProgress,
  Divider,
  Tabs,
  Tab,
  Tooltip,
  Card,
  CardContent
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
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

export function BatchConfirmatoryPlatingPanel({ session, onNext, onBack, onUpdated }: Props) {
  const [phase, setPhase] = useState<"setup" | "readout">("setup");
  const [activeTab, setActiveTab] = useState(0);

  // Eligible locations from backend gating
  const [eligibleLocations, setEligibleLocations] = useState<EligibleLocationForConfirmationDto[]>([]);
  const [loadingEligible, setLoadingEligible] = useState(true);

  // Lookups
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
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
      .catch((e) => {
        if (!active) return;
        setError(e?.response?.data?.message ?? "Failed to load eligible locations for confirmation.");
      })
      .finally(() => {
        if (active) setLoadingEligible(false);
      });

    return () => {
      active = false;
    };
  }, [session.sampleId, session.resultMatrix]);

  // Load released media lots and incubators
  useEffect(() => {
    setLoadingLookups(true);
    Promise.all([
      masterDataOptions.getReleasedMedia().catch(() => []),
      masterDataOptions.getEquipment("Incubator").catch(() => [])
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
          requiredMediaCount: loc.requiredConfirmatoryMediaCount || (loc.testCode.toUpperCase().includes("SALMONELLA") ? 2 : 1),
          locations: []
        });
      }
      map.get(loc.testCode)!.locations.push(loc);
    }

    return Array.from(map.values());
  }, [eligibleLocations]);

  const currentPathogen = eligiblePathogens[activeTab] ?? eligiblePathogens[0];

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
      setError(`Please select all ${currentPathogen.requiredMediaCount} required media lot(s) and an incubator.`);
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
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to start confirmatory plating setup.");
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
      setError(`Please complete plate readings for all ${stats.pending} pending location(s).`);
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
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to save confirmatory plate readings.");
    } finally {
      setSubmitting(false);
    }
  };

  // If no locations are eligible for confirmation (100% negative primary observations)
  if (!loadingEligible && eligibleLocations.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2, border: "1px solid #e5e7eb" }}>
        <CheckCircleOutlineIcon sx={{ fontSize: 48, color: "#059669", mb: 1.5 }} />
        <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#1f2937", mb: 1 }}>
          Confirmatory Plating Not Required
        </Typography>
        <Typography sx={{ fontSize: 13, color: "#6b7280", maxWidth: 600, mx: "auto", mb: 3 }}>
          All sampling locations in this batch were recorded as <strong>No Growth (-)</strong> during primary plate observation.
          No confirmatory plating is required for this session.
        </Typography>
        <Stack direction="row" spacing={2} justifyContent="center">
          <Button variant="outlined" onClick={onBack}>
            Back to Primary Matrix
          </Button>
          <Button variant="contained" color="success" endIcon={<ArrowForwardIcon />} onClick={onNext}>
            Proceed to Review & Complete
          </Button>
        </Stack>
      </Paper>
    );
  }

  return (
    <Stack spacing={2.5}>
      {/* Top Banner */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#faf5ff" }}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ sm: "center" }} spacing={2}>
          <Box>
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 0.75 }}>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: "#1f2937" }}>
                Confirmatory Plating (Panel B)
              </Typography>
              <Chip
                label={`${eligibleLocations.length} Flagged Locations`}
                size="small"
                sx={{ bgcolor: "#fee2e2", color: "#991b1b", fontWeight: 700 }}
              />
            </Stack>
            <Typography sx={{ fontSize: 12, color: "#6b7280" }}>
              Shared confirmatory media plating, batch incubation, and multi-media agreement evaluation.
            </Typography>
          </Box>

          <Stack direction="row" spacing={1.5} alignItems="center">
            <Button
              variant={phase === "setup" ? "contained" : "outlined"}
              size="small"
              onClick={() => setPhase("setup")}
              sx={{ fontWeight: 700 }}
            >
              1. Shared Media Setup
            </Button>
            <Button
              variant={phase === "readout" ? "contained" : "outlined"}
              size="small"
              onClick={() => setPhase("readout")}
              sx={{ fontWeight: 700 }}
            >
              2. Plate Observations
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {saveSuccess && (
        <Alert severity="success" onClose={() => setSaveSuccess(false)}>
          Confirmatory records and agreement evaluation updated successfully!
        </Alert>
      )}

      {/* Pathogen Tabs (if multiple pathogens have growth) */}
      {eligiblePathogens.length > 1 && (
        <Tabs
          value={activeTab}
          onChange={(_, val) => setActiveTab(val)}
          sx={{
            borderBottom: "1px solid #e5e7eb",
            "& .MuiTab-root": { fontWeight: 700, textTransform: "none", fontSize: 13 }
          }}
        >
          {eligiblePathogens.map((p) => (
            <Tab
              key={p.testCode}
              label={
                <Stack direction="row" spacing={1} alignItems="center">
                  <span>{p.testDisplayName} ({p.testCode})</span>
                  <Chip label={`${p.locations.length} loc`} size="small" sx={{ fontSize: 10, height: 18 }} />
                </Stack>
              }
            />
          ))}
        </Tabs>
      )}

      {/* PHASE 1: SHARED MEDIA SETUP FORM */}
      {phase === "setup" && currentPathogen && (
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
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "#1f2937" }}>
                Shared Media & Incubation Setup — {currentPathogen.testDisplayName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "#6b7280" }}>
                {currentPathogen.requiredMediaCount} confirmatory media required by Test Master specification for {currentPathogen.testCode}.
              </Typography>
            </Box>
          </Stack>

          <Box sx={{ p: 2, mb: 3, bgcolor: "#f8fafc", borderRadius: 2, border: "1px solid #e2e8f0" }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#475569", mb: 1 }}>
              FLAGGED SAMPLING LOCATIONS ({currentPathogen.locations.length}):
            </Typography>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              {currentPathogen.locations.map((loc) => (
                <Chip
                  key={loc.locationId}
                  label={`${loc.locationName} (${loc.growthObservationDisplay})`}
                  size="small"
                  sx={{
                    bgcolor: loc.growthObservation === GrowthObservation.GrowthConforming ? "#fee2e2" : "#ffedd5",
                    color: loc.growthObservation === GrowthObservation.GrowthConforming ? "#991b1b" : "#9a3412",
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
                    label={`Confirmatory Medium ${idx + 1} Lot (GPT Passed & Released) *`}
                    value={selectedLotId}
                    onChange={(e) => handleMediaLotChange(currentPathogen.testCode, idx, Number(e.target.value))}
                    helperText={`Select media lot for medium #${idx + 1}`}
                    fullWidth
                    size="small"
                  >
                    {releasedMedia.map((m: any) => (
                      <MenuItem key={m.id} value={m.id}>
                        {m.lotNumber} — {m.materialName ?? m.mediaTypeName ?? "Media Lot"} (Exp: {new Date(m.expiryDate).toLocaleDateString()})
                      </MenuItem>
                    ))}
                  </TextField>
                );
              })}

              {/* Incubator Selector */}
              <TextField
                select
                label="Incubator Equipment (In-Service) *"
                value={setupStateByPathogen[currentPathogen.testCode]?.incubatorId ?? ""}
                onChange={(e) => handleIncubatorChange(currentPathogen.testCode, Number(e.target.value))}
                helperText="Select active, calibrated incubator"
                fullWidth
                size="small"
              >
                {incubators.map((inc: any) => (
                  <MenuItem key={inc.id} value={inc.id}>
                    {inc.code} — {inc.name ?? "Incubator"} ({inc.location ?? "Lab"})
                  </MenuItem>
                ))}
              </TextField>

              {/* Incubation Start Time */}
              <TextField
                label="Actual Incubation Start Time *"
                type="datetime-local"
                value={setupStateByPathogen[currentPathogen.testCode]?.startDateTime ?? ""}
                onChange={(e) => handleStartTimeChange(currentPathogen.testCode, e.target.value)}
                helperText="Contemporaneously recorded start timestamp"
                fullWidth
                size="small"
                InputLabelProps={{ shrink: true }}
              />
            </Box>
          )}

          <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ mt: 3 }}>
            <Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={onBack}>
              Back to Primary Matrix
            </Button>
            <Button
              variant="contained"
              color="primary"
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <PlayArrowIcon />}
              onClick={handleStartConfirmatorySetup}
              disabled={submitting || loadingLookups}
              sx={{ px: 3, fontWeight: 700 }}
            >
              Start Confirmatory Incubation
            </Button>
          </Stack>
        </Paper>
      )}

      {/* PHASE 2: PLATE OBSERVATION READOUT GRID */}
      {phase === "readout" && (
        <Stack spacing={2.5}>
          {/* Agreement Stats Bar */}
          <Paper sx={{ p: 2, borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#fcfaff" }}>
            <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" alignItems={{ md: "center" }} spacing={2}>
              <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
                <Typography sx={{ fontSize: 14, fontWeight: 800, color: "#1f2937" }}>
                  Multi-Media Agreement Status:
                </Typography>
                <Chip
                  label={`Detected (+): ${stats.detected}`}
                  size="small"
                  sx={{ bgcolor: stats.detected > 0 ? "#fee2e2" : "#f3f4f6", color: stats.detected > 0 ? "#991b1b" : "#374151", fontWeight: 700 }}
                />
                <Chip
                  label={`Not Detected (-): ${stats.notDetected}`}
                  size="small"
                  sx={{ bgcolor: stats.notDetected > 0 ? "#d1fae5" : "#f3f4f6", color: stats.notDetected > 0 ? "#065f46" : "#374151", fontWeight: 700 }}
                />
                {stats.inconclusive > 0 && (
                  <Chip
                    icon={<WarningAmberOutlinedIcon sx={{ fontSize: 13 }} />}
                    label={`Inconclusive (Retest): ${stats.inconclusive}`}
                    size="small"
                    sx={{ bgcolor: "#ffedd5", color: "#9a3412", fontWeight: 700 }}
                  />
                )}
                {stats.pending > 0 && (
                  <Chip
                    label={`Pending Reads: ${stats.pending}`}
                    size="small"
                    sx={{ bgcolor: "#f1f5f9", color: "#64748b", fontWeight: 600 }}
                  />
                )}
              </Stack>

              <Stack direction="row" spacing={1.5}>
                <Button variant="outlined" size="small" onClick={() => setPhase("setup")}>
                  Modify Setup
                </Button>
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <SaveOutlinedIcon />}
                  onClick={handleSaveConfirmatoryReadings}
                  disabled={submitting || !stats.allComplete}
                  sx={{ px: 2.5, fontWeight: 700 }}
                >
                  Save Readings & Continue
                </Button>
              </Stack>
            </Stack>
          </Paper>

          {/* Plate Observation Table */}
          <Paper sx={{ border: "1px solid #e5e7eb", borderRadius: 2, overflow: "hidden" }}>
            <Box sx={{ maxHeight: 520, overflow: "auto" }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ position: "sticky", left: 0, zIndex: 3, bgcolor: "#f8fafc", fontWeight: 800, minWidth: 220, borderRight: "2px solid #e2e8f0" }}>
                      Sampling Location ({eligibleLocations.length})
                    </TableCell>
                    <TableCell align="center" sx={{ bgcolor: "#f8fafc", fontWeight: 800, minWidth: 140 }}>
                      Primary Reading
                    </TableCell>

                    {/* Medium Columns based on max required media count */}
                    {Array.from({ length: currentPathogen?.requiredMediaCount || 1 }).map((_, medIdx) => (
                      <TableCell
                        key={medIdx}
                        align="center"
                        sx={{ bgcolor: "#fcfaff", fontWeight: 800, minWidth: 260, borderRight: "1px solid #f1f5f9" }}
                      >
                        <Typography sx={{ fontSize: 13, fontWeight: 800, color: brandColors.sectionTitle }}>
                          Confirmatory Medium #{medIdx + 1}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "#64748b" }}>
                          Colony appearance & growth observation
                        </Typography>
                      </TableCell>
                    ))}

                    <TableCell align="center" sx={{ bgcolor: "#f8fafc", fontWeight: 800, minWidth: 160 }}>
                      Agreement Evaluation
                    </TableCell>
                  </TableRow>
                </TableHead>

                <TableBody>
                  {eligibleLocations.map((loc, idx) => {
                    const agr = getAgreementForLocation(loc);
                    const reqCount = loc.requiredConfirmatoryMediaCount || 1;

                    return (
                      <TableRow key={loc.locationId} hover sx={{ bgcolor: idx % 2 === 0 ? "#ffffff" : "#fafafa" }}>
                        {/* Sticky Location Name Column */}
                        <TableCell sx={{ position: "sticky", left: 0, zIndex: 2, bgcolor: idx % 2 === 0 ? "#ffffff" : "#fafafa", fontWeight: 700, borderRight: "2px solid #e2e8f0" }}>
                          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
                            {loc.locationName}
                          </Typography>
                          <Typography sx={{ fontSize: 11, color: "#6b7280" }}>
                            {loc.testDisplayName} ({loc.testCode})
                          </Typography>
                        </TableCell>

                        {/* Primary Reading Badge */}
                        <TableCell align="center">
                          <Chip
                            label={loc.growthObservationDisplay}
                            size="small"
                            sx={{
                              fontSize: 11,
                              fontWeight: 700,
                              bgcolor: loc.growthObservation === GrowthObservation.GrowthConforming ? "#fee2e2" : "#ffedd5",
                              color: loc.growthObservation === GrowthObservation.GrowthConforming ? "#991b1b" : "#9a3412"
                            }}
                          />
                        </TableCell>

                        {/* Medium Observation Radios */}
                        {Array.from({ length: reqCount }).map((_, medIdx) => {
                          const key = `${loc.locationId}_${medIdx}`;
                          const val = plateReadings[key] ?? "";

                          return (
                            <TableCell key={medIdx} align="center" sx={{ borderRight: "1px solid #f1f5f9" }}>
                              <RadioGroup
                                row
                                value={val}
                                onChange={(e) => handleObservationChange(loc.locationId, medIdx, e.target.value as GrowthObservation)}
                                sx={{ justifyContent: "center", gap: 1 }}
                              >
                                <FormControlLabel
                                  value={GrowthObservation.NoGrowth}
                                  control={<Radio size="small" sx={{ color: "#059669", "&.Mui-checked": { color: "#059669" } }} />}
                                  label={<Typography sx={{ fontSize: 11, fontWeight: 600, color: "#065f46" }}>No Growth</Typography>}
                                />
                                <FormControlLabel
                                  value={GrowthObservation.GrowthNonConforming}
                                  control={<Radio size="small" sx={{ color: "#d97706", "&.Mui-checked": { color: "#d97706" } }} />}
                                  label={<Typography sx={{ fontSize: 11, fontWeight: 600, color: "#b45309" }}>Non-Conf</Typography>}
                                />
                                <FormControlLabel
                                  value={GrowthObservation.GrowthConforming}
                                  control={<Radio size="small" sx={{ color: "#dc2626", "&.Mui-checked": { color: "#dc2626" } }} />}
                                  label={<Typography sx={{ fontSize: 11, fontWeight: 800, color: "#dc2626" }}>Conf (+)</Typography>}
                                />
                              </RadioGroup>
                            </TableCell>
                          );
                        })}

                        {/* Real-time Agreement Badge */}
                        <TableCell align="center">
                          {!agr.isComplete ? (
                            <Chip
                              label="Pending Read"
                              size="small"
                              sx={{ fontSize: 11, fontWeight: 600, bgcolor: "#f1f5f9", color: "#64748b" }}
                            />
                          ) : agr.result === ConfirmationResult.Detected ? (
                            <Chip
                              label="Detected (+)"
                              size="small"
                              sx={{ fontSize: 11, fontWeight: 800, bgcolor: "#fee2e2", color: "#991b1b" }}
                            />
                          ) : agr.result === ConfirmationResult.NotDetected ? (
                            <Chip
                              label="Not Detected (-)"
                              size="small"
                              sx={{ fontSize: 11, fontWeight: 700, bgcolor: "#ecfdf5", color: "#065f46" }}
                            />
                          ) : (
                            <Tooltip title="Media observations disagree across plates. Retest is required.">
                              <Chip
                                icon={<WarningAmberOutlinedIcon sx={{ fontSize: 12 }} />}
                                label="Inconclusive (Retest)"
                                size="small"
                                sx={{ fontSize: 11, fontWeight: 800, bgcolor: "#ffedd5", color: "#9a3412" }}
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
            <Card sx={{ bgcolor: "#fffbeb", border: "1px solid #fcd34d", borderRadius: 2 }}>
              <CardContent sx={{ p: 2.5 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 800, color: "#92400e", mb: 0.5, display: "flex", alignItems: "center", gap: 1 }}>
                  🧪 Biochemical Supporting Observation (Optional)
                </Typography>
                <Typography sx={{ fontSize: 11.5, color: "#78350f", mb: 1.5, display: "block" }}>
                  Add any biochemical confirmation, species identification, or supporting remarks for the <strong>{stats.detected} Detected (+)</strong> location(s) above. This comment will be contemporaneously recorded in the audit trail.
                </Typography>
                <TextField
                  fullWidth
                  multiline
                  rows={3}
                  placeholder="E.g. 'Indole test positive; confirms E. coli detection' or 'Oxidase negative; gram-negative rod; consistent with Enterobacteriaceae family'"
                  value={biochemicalComment}
                  onChange={(e) => setBiochemicalComment(e.target.value)}
                  size="small"
                  sx={{ bgcolor: "#ffffff", borderRadius: 1 }}
                />
              </CardContent>
            </Card>
          )}

          {/* Action Footer */}
          <Stack direction="row" spacing={2} justifyContent="flex-end">
            <Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={() => setPhase("setup")}>
              Back to Setup
            </Button>
            <Button
              variant="contained"
              color="primary"
              endIcon={<ArrowForwardIcon />}
              onClick={handleSaveConfirmatoryReadings}
              disabled={submitting || !stats.allComplete}
              sx={{ px: 3, fontWeight: 700 }}
            >
              {submitting ? <CircularProgress size={16} color="inherit" /> : "Save Confirmatory Readings & Continue"}
            </Button>
          </Stack>
        </Stack>
      )}
    </Stack>
  );
}
