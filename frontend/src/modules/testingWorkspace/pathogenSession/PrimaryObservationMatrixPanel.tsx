import { useState, useMemo, useEffect } from "react";
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
  TextField,
  MenuItem,
  InputAdornment,
  Alert,
  Chip,
  LinearProgress,
  Tooltip,
  CircularProgress,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import FilterListIcon from "@mui/icons-material/FilterList";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import {
  PathogenTestingSessionDto,
  GrowthObservation,
  PrimaryObservationInput,
  MatrixCellInput
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { brandColors } from "../../../theme";

interface Props {
  session: PathogenTestingSessionDto;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
  onNext: (skipToReview?: boolean) => void;
}

export function PrimaryObservationMatrixPanel({ session, onUpdated, onNext }: Props) {
  const theme = useTheme();
  // Key = `${sampleLocationId}_${testCode}`
  const [primaryObservations, setPrimaryObservations] = useState<Record<string, {
    sampleLocationId: number;
    testCode: string;
    observation: GrowthObservation | "";
    isQuantitative: boolean;
    numericValue: number | null;
  }>>({});

  const [searchQuery, setSearchQuery] = useState("");
  const [filterPendingOnly, setFilterPendingOnly] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [fillConfirmation, setFillConfirmation] = useState<{
    cells: { sampleLocationId: number; testCode: string }[];
    locationNames: string[];
  } | null>(null);

  // Initialize cell values from session matrix
  useEffect(() => {
    const initialMap: typeof primaryObservations = {};
    for (const loc of session.locations) {
      for (const test of session.assignedTests) {
        const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
        const key = `${slocId}_${test.testCode}`;
        const cell = session.resultMatrix.find(
          (c) => c.sampleLocationId === slocId && c.testCode === test.testCode
        );

        const isQuant = test.workflowType.toLowerCase().includes("count") ||
                        test.testCode.toLowerCase().includes("tamc") ||
                        test.testCode.toLowerCase().includes("tymc");

        let obs: GrowthObservation | "" = "";
        if (cell?.primaryObservation) {
          obs = cell.primaryObservation as GrowthObservation;
        } else if (cell?.resultCode === "NOT_DETECTED" || cell?.resultDisplay?.includes("Not Detected")) {
          obs = GrowthObservation.NoGrowth;
        } else if (cell?.resultCode === "DETECTED" || cell?.resultDisplay?.includes("Detected")) {
          obs = GrowthObservation.GrowthConforming;
        }

        initialMap[key] = {
          sampleLocationId: slocId,
          testCode: test.testCode,
          observation: obs,
          isQuantitative: isQuant,
          numericValue: cell?.numericValue ?? null
        };
      }
    }
    setPrimaryObservations(initialMap);
  }, [session]);

  const testStateMap = useMemo(() => {
    return new Map(session.assignedTests.map((t) => [t.testCode, t]));
  }, [session.assignedTests]);

  const isTsbIncubating = session.sharedTsb.isIncubating;
  const areAllTestsLocked = session.assignedTests.every((t) => !t.isResultEntryAllowed);

  const handleObservationChange = (sampleLocationId: number, testCode: string, value: GrowthObservation | "") => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    setPrimaryObservations((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, isQuantitative: false, numericValue: null }),
        observation: value
      }
    }));
    setSaveSuccess(false);
  };

  const handleQuantitativeChange = (sampleLocationId: number, testCode: string, numStr: string) => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    const num = numStr !== "" ? parseFloat(numStr) : null;
    setPrimaryObservations((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, isQuantitative: true, observation: "" }),
        numericValue: num !== null && !isNaN(num) ? num : null
      }
    }));
    setSaveSuccess(false);
  };

  // Computes the eligible-cell set once (same pass used to populate the
  // confirmation dialog's count/location list below is then reused as the
  // actual fill target on confirm - never re-filtered a second time, so the
  // dialog can never show a different set than what actually gets filled.
  const handleFillAllNoGrowthClick = () => {
    let lockedCount = 0;
    const cells: { sampleLocationId: number; testCode: string }[] = [];
    const locationNames = new Set<string>();

    for (const loc of session.locations) {
      for (const test of session.assignedTests) {
        if (!test.isResultEntryAllowed) {
          lockedCount++;
          continue;
        }
        const isQuant = test.workflowType.toLowerCase().includes("count") ||
                        test.testCode.toLowerCase().includes("tamc") ||
                        test.testCode.toLowerCase().includes("tymc");
        if (!isQuant) {
          const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
          const key = `${slocId}_${test.testCode}`;
          if (!primaryObservations[key]?.observation) {
            cells.push({ sampleLocationId: slocId, testCode: test.testCode });
            locationNames.add(loc.locationName);
          }
        }
      }
    }

    if (cells.length === 0) {
      setError(lockedCount > 0
        ? "No eligible empty cells to populate. Remaining empty cells are locked by upstream workflow prerequisites."
        : "All eligible qualitative cells already have primary observations.");
      return;
    }

    setFillConfirmation({ cells, locationNames: Array.from(locationNames) });
  };

  const handleConfirmFillAllNoGrowth = () => {
    if (!fillConfirmation) return;

    setPrimaryObservations((prev) => {
      const updated = { ...prev };
      for (const cell of fillConfirmation.cells) {
        const key = `${cell.sampleLocationId}_${cell.testCode}`;
        if (!updated[key]?.observation) {
          updated[key] = {
            sampleLocationId: cell.sampleLocationId,
            testCode: cell.testCode,
            observation: GrowthObservation.NoGrowth,
            isQuantitative: false,
            numericValue: null
          };
        }
      }
      return updated;
    });
    setSaveSuccess(false);
    setFillConfirmation(null);
  };

  // Calculate Dynamic Tri-State Counts
  const { completedCount, availableCount, lockedCount, totalCount, hasEligibleConfirmations } = useMemo(() => {
    let completed = 0;
    let available = 0;
    let locked = 0;
    let growthCount = 0;

    for (const loc of session.locations) {
      for (const test of session.assignedTests) {
        const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
        const key = `${slocId}_${test.testCode}`;
        const val = primaryObservations[key];

        const isFilled = val && (
          (val.isQuantitative && val.numericValue !== null) ||
          (!val.isQuantitative && val.observation !== "")
        );

        if (isFilled) {
          completed++;
          if (!val.isQuantitative && (val.observation === GrowthObservation.GrowthConforming || val.observation === GrowthObservation.GrowthNonConforming)) {
            growthCount++;
          }
        } else if (test.isResultEntryAllowed) {
          available++;
        } else {
          locked++;
        }
      }
    }

    const total = session.totalLocations * session.totalAssignedTests;
    return {
      completedCount: completed,
      availableCount: available,
      lockedCount: locked,
      totalCount: total,
      hasEligibleConfirmations: growthCount > 0
    };
  }, [session, primaryObservations]);

  const progressPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

  // Filtered locations
  const filteredLocations = useMemo(() => {
    return session.locations.filter((loc) => {
      const matchesSearch = !searchQuery || loc.locationName.toLowerCase().includes(searchQuery.toLowerCase());
      if (!matchesSearch) return false;

      if (filterPendingOnly) {
        const hasPending = session.assignedTests.some((t) => {
          if (!t.isResultEntryAllowed) return false;
          const slocId = loc.testLocationMap[t.testCode] ?? loc.primarySampleLocationId;
          const val = primaryObservations[`${slocId}_${t.testCode}`];
          return !val || (val.isQuantitative ? val.numericValue === null : !val.observation);
        });
        return hasPending;
      }

      return true;
    });
  }, [session, searchQuery, filterPendingOnly, primaryObservations]);

  const handleSaveObservations = async () => {
    setError(null);
    setSaving(true);
    try {
      const qualitativeObs: PrimaryObservationInput[] = [];
      const quantitativeCells: MatrixCellInput[] = [];

      for (const val of Object.values(primaryObservations)) {
        const testInfo = testStateMap.get(val.testCode);
        if (!testInfo?.isResultEntryAllowed) continue;

        if (val.isQuantitative && val.numericValue !== null) {
          quantitativeCells.push({
            sampleLocationId: val.sampleLocationId,
            testCode: val.testCode,
            resultCode: String(val.numericValue),
            resultDisplay: `${val.numericValue} CFU`,
            numericValue: val.numericValue,
            resultType: "Quantitative"
          });
        } else if (!val.isQuantitative && val.observation !== "") {
          qualitativeObs.push({
            sampleLocationId: val.sampleLocationId,
            testCode: val.testCode,
            observation: val.observation
          });
        }
      }

      if (qualitativeObs.length === 0 && quantitativeCells.length === 0) {
        setError("No observations or quantitative readings have been entered to save.");
        return;
      }

      let updatedSession = session;

      if (qualitativeObs.length > 0) {
        updatedSession = await PathogenSessionService.savePrimaryObservations(session.sampleId, {
          observations: qualitativeObs
        });
      }

      if (quantitativeCells.length > 0) {
        updatedSession = await PathogenSessionService.saveResultMatrix(session.sampleId, {
          cells: quantitativeCells
        });
      }

      onUpdated(updatedSession);
      setSaveSuccess(true);

      // Check if any locations require confirmatory testing
      const eligible = await PathogenSessionService.getEligibleConfirmations(session.sampleId);
      const needsConfirmation = eligible && eligible.length > 0;

      // Auto-route: if growth present, proceed to Confirmatory Plating (Step 4); otherwise skip to Review (Step 5)
      if (completedCount === totalCount) {
        onNext(!needsConfirmation);
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to save primary plate observations.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2.5}>
      {/* Gating Lock Alert Banner */}
      {isTsbIncubating && (
        <Alert severity="warning" icon={<LockOutlinedIcon />} sx={{ fontWeight: 600 }}>
          Primary plate observations are locked until required workflow steps complete. Shared TSB Enrichment is currently incubating.
        </Alert>
      )}

      {/* Completion & Tri-State Header Card */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: theme.custom.status.purple.border, bgcolor: theme.custom.status.purple.bg }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" alignItems={{ md: "center" }} spacing={2}>
          <Box sx={{ flex: 1 }}>
            <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" sx={{ mb: 1.25 }}>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: "text.primary" }}>
                Primary Observation Matrix (Panel A)
              </Typography>
              <Chip
                label={`Recorded: ${completedCount}`}
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: completedCount > 0 ? theme.custom.status.notDetected.bg : theme.custom.status.pending.bg,
                  color: completedCount > 0 ? theme.custom.status.notDetected.text : theme.custom.status.pending.text
                }}
              />
              <Chip
                label={`Available: ${availableCount}`}
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: availableCount > 0 ? theme.custom.status.info.bg : theme.custom.status.pending.bg,
                  color: availableCount > 0 ? theme.custom.status.info.text : theme.custom.status.pending.text
                }}
              />
              {lockedCount > 0 && (
                <Chip
                  icon={<LockOutlinedIcon sx={{ fontSize: 13 }} />}
                  label={`Locked: ${lockedCount}`}
                  size="small"
                  sx={{
                    fontWeight: 700,
                    bgcolor: theme.custom.status.detected.bg,
                    color: theme.custom.status.detected.text
                  }}
                />
              )}
            </Stack>

            <LinearProgress
              variant="determinate"
              value={progressPercent}
              sx={{
                height: 8,
                borderRadius: 4,
                bgcolor: "divider",
                "& .MuiLinearProgress-bar": {
                  bgcolor: completedCount === totalCount ? theme.palette.secondary.main : brandColors.sectionTitle
                }
              }}
            />
          </Box>

          <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
            <Button
              variant="outlined"
              size="small"
              startIcon={<DoneAllIcon />}
              onClick={handleFillAllNoGrowthClick}
              disabled={areAllTestsLocked || availableCount === 0}
              sx={{ fontSize: 12, fontWeight: 600, textTransform: "none" }}
            >
              Fill All Unset as No Growth (-)
            </Button>

            <Button
              variant="contained"
              color="primary"
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <SaveOutlinedIcon />}
              onClick={handleSaveObservations}
              disabled={saving || areAllTestsLocked}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              Save Observations
            </Button>

            <Button
              variant="contained"
              color="success"
              endIcon={<ArrowForwardIcon />}
              onClick={() => onNext(!hasEligibleConfirmations)}
              disabled={completedCount < totalCount || isTsbIncubating}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              {hasEligibleConfirmations ? "Proceed to Confirmation" : "Review & Complete"}
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {saveSuccess && (
        <Alert severity="success" onClose={() => setSaveSuccess(false)}>
          Primary plate observations saved successfully with ALCOA+ contemporaneous tracking!
        </Alert>
      )}

      {/* Filter and Search Bar */}
      <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
        <TextField
          size="small"
          placeholder="Filter by location name..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ fontSize: 18, color: "text.secondary" }} />
              </InputAdornment>
            )
          }}
          sx={{ width: { xs: "100%", sm: 300 } }}
        />

        <Button
          variant={filterPendingOnly ? "contained" : "outlined"}
          size="small"
          startIcon={<FilterListIcon />}
          onClick={() => setFilterPendingOnly(!filterPendingOnly)}
          sx={{ fontSize: 12, fontWeight: 600 }}
        >
          {filterPendingOnly ? "Showing Pending Only" : "Show Pending Only"}
        </Button>

        <Typography sx={{ fontSize: 12, color: "text.secondary", ml: "auto" }}>
          Showing {filteredLocations.length} of {session.totalLocations} locations
        </Typography>
      </Stack>

      {/* Dynamic Primary Observations Grid */}
      <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        <Box sx={{ maxHeight: 520, overflow: "auto" }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                {/* Sticky Sampling Location Column */}
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 0,
                    zIndex: 3,
                    bgcolor: "background.default",
                    fontWeight: 800,
                    color: "text.primary",
                    minWidth: 240,
                    borderRight: "2px solid",
                    borderRightColor: "divider"
                  }}
                >
                  Sampling Location ({filteredLocations.length})
                </TableCell>

                {/* Dynamic Test Columns */}
                {session.assignedTests.map((t) => {
                  const isLocked = !t.isResultEntryAllowed;
                  const confMediaCount = t.confirmatoryMediaCount ?? (t.testCode.toUpperCase().includes("SALMONELLA") ? 2 : 1);
                  const isQuant = t.workflowType.toLowerCase().includes("count") ||
                                  t.testCode.toLowerCase().includes("tamc") ||
                                  t.testCode.toLowerCase().includes("tymc");

                  return (
                    <TableCell
                      key={t.testCode}
                      align="center"
                      sx={{
                        bgcolor: isLocked ? "background.default" : theme.custom.status.purple.bg,
                        fontWeight: 800,
                        color: isLocked ? "text.secondary" : brandColors.sectionTitle,
                        minWidth: 190,
                        borderRight: "1px solid",
                        borderRightColor: "divider"
                      }}
                    >
                      <Stack direction="row" spacing={0.5} justifyContent="center" alignItems="center">
                        <Typography sx={{ fontSize: 13, fontWeight: 800 }}>
                          {t.testCode}
                        </Typography>
                        {isLocked && <LockOutlinedIcon sx={{ fontSize: 14, color: theme.custom.status.detected.text }} />}
                      </Stack>

                      <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 500 }}>
                        {t.displayName}
                      </Typography>

                      <Stack direction="row" spacing={0.5} justifyContent="center" alignItems="center" sx={{ mt: 0.5 }}>
                        <Chip
                          label={t.testSessionStateDisplay}
                          size="small"
                          sx={{
                            fontSize: 10,
                            height: 18,
                            bgcolor: isLocked ? theme.custom.status.detected.bg : theme.custom.status.info.bg,
                            color: isLocked ? theme.custom.status.detected.text : theme.custom.status.info.text,
                            fontWeight: 700
                          }}
                        />
                        {!isQuant && confMediaCount > 1 && (
                          <Chip
                            label={`${confMediaCount}x Media`}
                            size="small"
                            sx={{
                              fontSize: 10,
                              height: 18,
                              bgcolor: theme.custom.status.purple.bg,
                              color: theme.custom.status.purple.text,
                              fontWeight: 700,
                              border: "1px solid",
                              borderColor: theme.custom.status.purple.border
                            }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                  );
                })}
              </TableRow>
            </TableHead>

            <TableBody>
              {filteredLocations.map((loc, idx) => (
                <TableRow key={loc.id} hover sx={{ bgcolor: idx % 2 === 0 ? "background.paper" : "background.default" }}>
                  {/* Sticky Location Name Column */}
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 2,
                      bgcolor: idx % 2 === 0 ? "background.paper" : "background.default",
                      fontWeight: 700,
                      borderRight: "2px solid",
                      borderRightColor: "divider"
                    }}
                  >
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                      {loc.locationName}
                    </Typography>
                    <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mt: 0.25 }}>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        {loc.locationType}
                      </Typography>
                      {loc.gradeClassification && (
                        <Chip
                          label={`Grade ${loc.gradeClassification}`}
                          size="small"
                          sx={{ fontSize: 9, height: 16, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                        />
                      )}
                    </Stack>
                  </TableCell>

                  {/* Primary Observation Matrix Cells */}
                  {session.assignedTests.map((t) => {
                    const isQuant = t.workflowType.toLowerCase().includes("count") ||
                                    t.testCode.toLowerCase().includes("tamc") ||
                                    t.testCode.toLowerCase().includes("tymc");

                    const slocId = loc.testLocationMap[t.testCode] ?? loc.primarySampleLocationId;
                    const key = `${slocId}_${t.testCode}`;
                    const val = primaryObservations[key];
                    const isCellLocked = !t.isResultEntryAllowed;

                    // If cell is locked by workflow prerequisite, render clean disabled lock badge
                    if (isCellLocked) {
                      return (
                        <TableCell
                          key={t.testCode}
                          align="center"
                          sx={{
                            p: 1,
                            borderRight: "1px solid",
                            borderRightColor: "divider",
                            bgcolor: "background.default"
                          }}
                        >
                          <Tooltip title={t.lockReason ?? "Locked until upstream workflow step completes"}>
                            <Box
                              sx={{
                                py: 0.75,
                                px: 1.5,
                                bgcolor: theme.custom.countdown.locked.bg,
                                borderRadius: 1.5,
                                border: "1px dashed",
                                borderColor: "divider",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 0.75,
                                color: theme.custom.countdown.locked.text,
                                fontSize: 11,
                                fontWeight: 700,
                                cursor: "not-allowed"
                              }}
                            >
                              <LockOutlinedIcon sx={{ fontSize: 14, color: theme.custom.countdown.locked.text }} />
                              <span>Locked</span>
                            </Box>
                          </Tooltip>
                        </TableCell>
                      );
                    }

                    const isCellFilled = val && (
                      (isQuant && val.numericValue !== null) ||
                      (!isQuant && val.observation !== "")
                    );

                    const countResult = t.countResult;
                    const hasRecordedCountResult = countResult?.reportedResult != null;

                    if (isQuant && hasRecordedCountResult && countResult) {
                      const statusToneKey =
                        countResult.status === "OutOfSpecification"
                          ? "detected"
                          : countResult.status === "ActionLimitExceeded"
                          ? "action"
                          : countResult.status === "AlertLimitExceeded" || countResult.hasNonNumericReading
                          ? "inconclusive"
                          : "notDetected";

                      const toneTokens = theme.custom.status[statusToneKey];

                      return (
                        <TableCell
                          key={t.testCode}
                          align="center"
                          sx={{
                            p: 1,
                            borderRight: "1px solid",
                            borderRightColor: "divider"
                          }}
                        >
                          <Box
                            sx={{
                              p: 1,
                              borderRadius: 1,
                              minWidth: 110,
                              backgroundColor: toneTokens.bg,
                              border: `1px solid ${toneTokens.border}`
                            }}
                          >
                            <Typography variant="body2" sx={{ fontWeight: 700, color: toneTokens.text }}>
                              {countResult.reportedResult}
                            </Typography>
                            {!countResult.hasNonNumericReading && countResult.status && (
                              <Typography variant="caption" sx={{ color: toneTokens.text, display: "block" }}>
                                {countResult.status.replace(/([A-Z])/g, " $1").trim()}
                              </Typography>
                            )}
                            {countResult.requiresReview && (
                              <Typography variant="caption" sx={{ color: theme.custom.status.action.text, display: "block", fontWeight: 600 }}>
                                ⚠ Review required
                              </Typography>
                            )}
                          </Box>
                        </TableCell>
                      );
                    }

                    return (
                      <TableCell
                        key={t.testCode}
                        align="center"
                        sx={{
                          p: 1,
                          borderRight: "1px solid",
                          borderRightColor: "divider",
                          bgcolor: !isCellFilled ? theme.custom.status.pending.bg : "inherit"
                        }}
                      >
                        {isQuant ? (
                          <TextField
                            size="small"
                            type="number"
                            placeholder="CFU"
                            value={val?.numericValue ?? ""}
                            onChange={(e) => handleQuantitativeChange(slocId, t.testCode, e.target.value)}
                            InputProps={{
                              endAdornment: <InputAdornment position="end">CFU</InputAdornment>
                            }}
                            inputProps={{ min: 0, max: 10000, step: 1 }}
                            sx={{
                              width: 120,
                              "& .MuiInputBase-root": {
                                fontSize: 12,
                                fontWeight: 700,
                                bgcolor: "background.paper"
                              }
                            }}
                          />
                        ) : (
                          <TextField
                            select
                            size="small"
                            value={val?.observation ?? ""}
                            onChange={(e) => handleObservationChange(slocId, t.testCode, e.target.value as GrowthObservation)}
                            sx={{
                              width: 175,
                              "& .MuiInputBase-root": {
                                fontSize: 12,
                                fontWeight: 700,
                                bgcolor: val?.observation === GrowthObservation.GrowthConforming
                                  ? theme.custom.status.detected.bg
                                  : val?.observation === GrowthObservation.GrowthNonConforming
                                  ? theme.custom.status.action.bg
                                  : val?.observation === GrowthObservation.NoGrowth
                                  ? theme.custom.status.notDetected.bg
                                  : "background.paper",
                                color: val?.observation === GrowthObservation.GrowthConforming
                                  ? theme.custom.status.detected.text
                                  : val?.observation === GrowthObservation.GrowthNonConforming
                                  ? theme.custom.status.action.text
                                  : val?.observation === GrowthObservation.NoGrowth
                                  ? theme.custom.status.notDetected.text
                                  : "inherit"
                              }
                            }}
                          >
                            <MenuItem value="">
                              <em>Select Observation</em>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.NoGrowth}>
                              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
                                No Growth (-)
                              </Typography>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.GrowthNonConforming}>
                              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.action.text }}>
                                Growth Non-Conforming
                              </Typography>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.GrowthConforming}>
                              <Typography sx={{ fontSize: 12, fontWeight: 800, color: theme.custom.status.detected.text }}>
                                Growth Conforming (+)
                              </Typography>
                            </MenuItem>
                          </TextField>
                        )}
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </Paper>

      <ConfirmationDialog
        open={fillConfirmation !== null}
        message={fillConfirmation
          ? `This will set ${fillConfirmation.cells.length} unset result${fillConfirmation.cells.length === 1 ? "" : "s"} as No Growth (-) across ${fillConfirmation.locationNames.length} location${fillConfirmation.locationNames.length === 1 ? "" : "s"}: ${fillConfirmation.locationNames.join(", ")}.`
          : ""}
        onConfirm={handleConfirmFillAllNoGrowth}
        onCancel={() => setFillConfirmation(null)}
      />
    </Stack>
  );
}
