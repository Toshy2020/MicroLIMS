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
  useTheme,
  useMediaQuery
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import FilterListIcon from "@mui/icons-material/FilterList";
import {
  PathogenTestingSessionDto,
  GrowthObservation,
  PrimaryObservationInput,
  MatrixCellInput
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { NumericEntryField, parseDecimal } from "../../../components/bench/NumericEntryField";
import { RegistryBenchKeypad } from "../../../components/bench/BenchKeypad";
import { useGridNavigation } from "../../../components/entry/useGridNavigation";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, serverMessage, useKeptOffline } from "../benchSave";
import { ObservationOptions } from "../pathogenSteps/StepLayout";
import { brandColors } from "../../../theme";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface PrimaryCellState {
  sampleLocationId: number;
  testCode: string;
  observation: GrowthObservation | "";
  isQuantitative: boolean;
  numericValue: number | null;
  numericText: string;
}

interface PrimaryMatrixDraftValues {
  entries: Record<string, { observation: GrowthObservation | ""; numericText: string }>;
}

function pickPrimaryMatrixDraft(v: PrimaryMatrixDraftValues): Partial<PrimaryMatrixDraftValues> {
  return { entries: v.entries };
}

function isQuantitativeTest(test: { workflowType: string; testCode: string }): boolean {
  return (
    test.workflowType.toLowerCase().includes("count") ||
    test.testCode.toLowerCase().includes("tamc") ||
    test.testCode.toLowerCase().includes("tymc")
  );
}

interface Props {
  session: PathogenTestingSessionDto;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
  onNext: (skipToReview?: boolean) => void;
}

export function PrimaryObservationMatrixPanel({ session, onUpdated, onNext }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const { benchMode } = useBenchMode();
  const matrixObservationOptions: { value: GrowthObservation; title: string }[] = useMemo(
    () => [
      { value: GrowthObservation.NoGrowth, title: t("pathogenSession.primaryMatrix.noGrowth") },
      { value: GrowthObservation.GrowthNonConforming, title: t("pathogenSession.primaryMatrix.growthNonConforming") },
      { value: GrowthObservation.GrowthConforming, title: t("pathogenSession.primaryMatrix.growthConforming") }
    ],
    [t]
  );
  const isCompactMatrix = useMediaQuery("(max-width:1099.95px)");
  const [keptOffline, setKeptOffline] = useKeptOffline();
  // Key = `${sampleLocationId}_${testCode}`
  const [primaryObservations, setPrimaryObservations] = useState<Record<string, PrimaryCellState>>({});

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

        const isQuant = isQuantitativeTest(test);

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
          numericValue: cell?.numericValue ?? null,
          numericText: cell?.numericValue != null ? String(cell.numericValue) : ""
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

  const draftEntries: PrimaryMatrixDraftValues["entries"] = {};
  for (const [k, v] of Object.entries(primaryObservations)) {
    if (v.observation !== "" || v.numericText !== "") {
      draftEntries[k] = { observation: v.observation, numericText: v.numericText };
    }
  }

  const draft = useBenchDraft<PrimaryMatrixDraftValues>({
    formKey: "pathogen-primary-matrix",
    recordKey: String(session.sampleId),
    recordVersion: null,
    title: t("pathogenSession.primaryMatrix.draftTitle", { sampleRef: session.sampleReferenceNumber }),
    values: { entries: draftEntries },
    pick: pickPrimaryMatrixDraft,
    enabled: benchMode && Object.keys(primaryObservations).length > 0
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved?.entries) return;
    setPrimaryObservations((prev) => {
      const next = { ...prev };
      for (const [k, entry] of Object.entries(saved.entries ?? {})) {
        if (!next[k] || !entry) continue;
        if (next[k].isQuantitative) {
          const text = typeof entry.numericText === "string" ? entry.numericText : "";
          next[k] = { ...next[k], numericText: text, numericValue: parseDecimal(text) };
        } else if (
          entry.observation === GrowthObservation.NoGrowth ||
          entry.observation === GrowthObservation.GrowthNonConforming ||
          entry.observation === GrowthObservation.GrowthConforming ||
          entry.observation === ""
        ) {
          next[k] = { ...next[k], observation: entry.observation };
        }
      }
      return next;
    });
    setSaveSuccess(false);
  };

  const handleObservationChange = (sampleLocationId: number, testCode: string, value: GrowthObservation | "") => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    setPrimaryObservations((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, isQuantitative: false, numericValue: null, numericText: "" }),
        observation: value
      }
    }));
    setSaveSuccess(false);
  };

  const handleQuantitativeChange = (sampleLocationId: number, testCode: string, numStr: string) => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    setPrimaryObservations((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, isQuantitative: true, observation: "" }),
        numericText: numStr,
        numericValue: parseDecimal(numStr)
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
        const isQuant = isQuantitativeTest(test);
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
        ? t("pathogenSession.primaryMatrix.noEligibleEmptyLocked")
        : t("pathogenSession.primaryMatrix.noEligibleEmptyDone"));
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
            numericValue: null,
            numericText: ""
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
        setError(t("pathogenSession.primaryMatrix.noneEnteredToSave"));
        return;
      }

      let updatedSession = session;
      const headerArgs = draftHeaderArgs(draft.submitHeaders());

      if (qualitativeObs.length > 0) {
        updatedSession = await PathogenSessionService.savePrimaryObservations(session.sampleId, {
          observations: qualitativeObs
        }, ...headerArgs);
      }

      if (quantitativeCells.length > 0) {
        updatedSession = await PathogenSessionService.saveResultMatrix(session.sampleId, {
          cells: quantitativeCells
        }, ...headerArgs);
      }

      await draft.onSubmitted();
      setKeptOffline(false);
      onUpdated(updatedSession);
      setSaveSuccess(true);

      // Check if any locations require confirmatory testing
      const eligible = await PathogenSessionService.getEligibleConfirmations(session.sampleId);
      const needsConfirmation = eligible && eligible.length > 0;

      // Auto-route: if growth present, proceed to Confirmatory Plating (Step 4); otherwise skip to Review (Step 5)
      if (completedCount === totalCount) {
        onNext(!needsConfirmation);
      }
    } catch (e: unknown) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(serverMessage(e) ?? (e instanceof Error ? e.message : t("pathogenSession.primaryMatrix.saveFailed")));
      }
    } finally {
      setSaving(false);
    }
  };

  const hasQuantTests = session.assignedTests.some(isQuantitativeTest);
  const saveDisabledReason = areAllTestsLocked
    ? t("pathogenSession.primaryMatrix.saveDisabledLocked")
    : undefined;
  const proceedDisabledReason = isTsbIncubating
    ? t("pathogenSession.primaryMatrix.proceedDisabledTsb")
    : completedCount < totalCount
    ? t("pathogenSession.primaryMatrix.proceedDisabledIncomplete")
    : undefined;

  const isTestColumnEditable = (colIdx: number): boolean => {
    const testItem = session.assignedTests[colIdx];
    if (!testItem || !testItem.isResultEntryAllowed) return false;
    const isQuant = isQuantitativeTest(testItem);
    const hasRecordedCountResult = testItem.countResult?.reportedResult != null;
    return !(isQuant && hasRecordedCountResult);
  };

  const { cellProps } = useGridNavigation({
    rows: filteredLocations.length,
    cols: session.assignedTests.length,
    capture: true,
    isEditable: (_r, c) => isTestColumnEditable(c),
    onSave: () => {
      if (!saving && !areAllTestsLocked) {
        void handleSaveObservations();
      }
    }
  });

  return (
    <Stack spacing={2.5}>
      {benchMode && (
        <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />
      )}
      {benchMode && keptOffline && <OfflineDraftNotice onTryAgain={() => void handleSaveObservations()} />}
      {/* Gating Lock Alert Banner */}
      {isTsbIncubating && (
        <Alert severity="warning" icon={<LockOutlinedIcon />} sx={{ fontWeight: 600 }}>
          {t("pathogenSession.primaryMatrix.tsbLockedBanner")}
        </Alert>
      )}

      {/* Completion & Tri-State Header Card */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: theme.custom.status.purple.border, bgcolor: theme.custom.status.purple.bg }}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={2}
          sx={{
            justifyContent: "space-between",
            alignItems: { md: "center" }
          }}>
          <Box sx={{ flex: 1 }}>
            <Stack useFlexGap
              direction="row"
              spacing={1.5}
              sx={{
                alignItems: "center",
                flexWrap: "wrap",
                mb: 1.25
              }}>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: "text.primary" }}>
                {t("pathogenSession.primaryMatrix.title")}
              </Typography>
              <Chip
                label={
                  <Trans
                    i18nKey="pathogenSession.primaryMatrix.recordedChip"
                    ns="testingWorkspace"
                    values={{ count: completedCount }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                }
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: completedCount > 0 ? theme.custom.status.notDetected.bg : theme.custom.status.pending.bg,
                  color: completedCount > 0 ? theme.custom.status.notDetected.text : theme.custom.status.pending.text
                }}
              />
              <Chip
                label={
                  <Trans
                    i18nKey="pathogenSession.primaryMatrix.availableChip"
                    ns="testingWorkspace"
                    values={{ count: availableCount }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                }
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
                  label={
                    <Trans
                      i18nKey="pathogenSession.primaryMatrix.lockedChip"
                      ns="testingWorkspace"
                      values={{ count: lockedCount }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  }
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

          <Stack useFlexGap
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <Button
              variant="outlined"
              size="small"
              startIcon={<DoneAllIcon />}
              onClick={handleFillAllNoGrowthClick}
              disabled={areAllTestsLocked || availableCount === 0}
              sx={{ fontSize: 12, fontWeight: 600, textTransform: "none" }}
            >
              {t("pathogenSession.primaryMatrix.fillAllNoGrowth")}
            </Button>

            <Button
              variant="contained"
              color="primary"
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <SaveOutlinedIcon />}
              onClick={handleSaveObservations}
              disabled={saving || areAllTestsLocked}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              {t("pathogenSession.primaryMatrix.saveObservations")}
            </Button>

            <Button
              variant="contained"
              color="success"
              endIcon={<ArrowForwardIcon />}
              onClick={() => onNext(!hasEligibleConfirmations)}
              disabled={completedCount < totalCount || isTsbIncubating}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              {hasEligibleConfirmations ? t("pathogenSession.primaryMatrix.proceedToConfirmation") : t("pathogenSession.primaryMatrix.reviewAndComplete")}
            </Button>
          </Stack>
          {(saveDisabledReason || proceedDisabledReason) && (
            <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: "block" }}>
              {saveDisabledReason ?? proceedDisabledReason}
            </Typography>
          )}
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {saveSuccess && (
        <Alert severity="success" onClose={() => setSaveSuccess(false)}>
          {t("pathogenSession.primaryMatrix.saveSuccess")}
        </Alert>
      )}

      {/* Filter and Search Bar */}
      <Stack useFlexGap
        direction="row"
        spacing={2}
        sx={{
          alignItems: "center",
          flexWrap: "wrap"
        }}>
        <TextField
          size="small"
          placeholder={t("pathogenSession.primaryMatrix.filterPlaceholder")}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          sx={{ width: { xs: "100%", sm: 300 } }}
          slotProps={{
            htmlInput: { "aria-label": t("pathogenSession.primaryMatrix.filterAriaLabel") },
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon sx={{ fontSize: 18, color: "text.secondary" }} />
                </InputAdornment>
              )
            }
          }}
        />

        <Button
          variant={filterPendingOnly ? "contained" : "outlined"}
          size="small"
          startIcon={<FilterListIcon />}
          onClick={() => setFilterPendingOnly(!filterPendingOnly)}
          sx={{ fontSize: 12, fontWeight: 600 }}
        >
          {filterPendingOnly ? t("pathogenSession.primaryMatrix.showingPendingOnly") : t("pathogenSession.primaryMatrix.showPendingOnly")}
        </Button>

        <Typography sx={{ fontSize: 12, color: "text.secondary", marginInlineStart: "auto" }}>
          <Trans
            i18nKey="pathogenSession.primaryMatrix.showingLocations"
            ns="testingWorkspace"
            values={{ filtered: filteredLocations.length, total: session.totalLocations }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Typography>
      </Stack>

      {/* Dynamic Primary Observations Grid */}
      {isCompactMatrix ? (
        <Stack spacing={2}>
          {filteredLocations.map((loc) => (
            <Paper key={loc.id} sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", mb: 1.5 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
                  {loc.locationName}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {loc.locationType}
                </Typography>
                {loc.gradeClassification && (
                  <Chip
                    label={t("pathogenSession.primaryMatrix.gradeChip", { grade: loc.gradeClassification })}
                    size="small"
                    sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                  />
                )}
              </Stack>
              <Stack spacing={1.5}>
                {session.assignedTests.map((testItem) => {
                  const isQuant = isQuantitativeTest(testItem);
                  const slocId = loc.testLocationMap[testItem.testCode] ?? loc.primarySampleLocationId;
                  const key = `${slocId}_${testItem.testCode}`;
                  const val = primaryObservations[key];
                  const isCellLocked = !testItem.isResultEntryAllowed;
                  const countResult = testItem.countResult;
                  const hasRecordedCountResult = countResult?.reportedResult != null;

                  return (
                    <Box key={testItem.testCode} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
                        {testItem.displayName} (<Ltr>{testItem.testCode}</Ltr>)
                      </Typography>
                      {isCellLocked ? (
                        <Stack direction="row" spacing={1} sx={{ alignItems: "center", color: theme.custom.countdown.locked.text }}>
                          <LockOutlinedIcon sx={{ fontSize: 16 }} />
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {testItem.lockReason ?? t("pathogenSession.primaryMatrix.lockedReasonFallback")}
                          </Typography>
                        </Stack>
                      ) : isQuant && hasRecordedCountResult && countResult ? (
                        <Typography variant="body2" sx={{ fontWeight: 700 }}>
                          {countResult.reportedResult}
                        </Typography>
                      ) : isQuant ? (
                        <NumericEntryField
                          kind="decimal"
                          label={t("pathogenSession.primaryMatrix.compactCountLabel", { testName: testItem.displayName })}
                          aria-label={t("pathogenSession.primaryMatrix.countAriaLabel", { testName: testItem.displayName, locationName: loc.locationName })}
                          unit={t("pathogenSession.primaryMatrix.cfuUnit")}
                          value={val?.numericText ?? ""}
                          onChange={(next) => handleQuantitativeChange(slocId, testItem.testCode, next)}
                        />
                      ) : benchMode ? (
                        <ObservationOptions
                          name={`compact-obs-${key}`}
                          value={val?.observation ?? ""}
                          onChange={(v) => handleObservationChange(slocId, testItem.testCode, v)}
                          options={matrixObservationOptions}
                        />
                      ) : (
                        <TextField
                          select
                          fullWidth
                          size="small"
                          value={val?.observation ?? ""}
                          onChange={(e) => handleObservationChange(slocId, testItem.testCode, e.target.value as GrowthObservation)}
                          slotProps={{ htmlInput: { "aria-label": t("pathogenSession.primaryMatrix.observationAriaLabel", { testName: testItem.displayName, locationName: loc.locationName }) } }}
                        >
                          <MenuItem value="">
                            <em>{t("pathogenSession.primaryMatrix.selectObservation")}</em>
                          </MenuItem>
                          <MenuItem value={GrowthObservation.NoGrowth}>{t("pathogenSession.primaryMatrix.noGrowth")}</MenuItem>
                          <MenuItem value={GrowthObservation.GrowthNonConforming}>{t("pathogenSession.primaryMatrix.growthNonConforming")}</MenuItem>
                          <MenuItem value={GrowthObservation.GrowthConforming}>{t("pathogenSession.primaryMatrix.growthConforming")}</MenuItem>
                        </TextField>
                      )}
                    </Box>
                  );
                })}
              </Stack>
            </Paper>
          ))}
        </Stack>
      ) : (
      <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        <Box sx={{ maxHeight: 520, overflow: "auto" }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                {/* Sticky Sampling Location Column */}
                <TableCell
                  sx={{
                    position: "sticky",
                    insetInlineStart: 0,
                    zIndex: 3,
                    bgcolor: "background.default",
                    fontWeight: 800,
                    color: "text.primary",
                    minWidth: 240,
                    borderInlineEnd: "2px solid",
                    borderInlineEndColor: "divider"
                  }}
                >
                  <Trans
                    i18nKey="pathogenSession.primaryMatrix.samplingLocationHeader"
                    ns="testingWorkspace"
                    values={{ count: filteredLocations.length }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </TableCell>

                {/* Dynamic Test Columns */}
                {session.assignedTests.map((testItem) => {
                  const isLocked = !testItem.isResultEntryAllowed;
                  const confMediaCount = testItem.confirmatoryMediaCount ?? 1; // from Test Master; never by organism (CLAUDE.md Rule 3.2)
                  const isQuant = isQuantitativeTest(testItem);

                  return (
                    <TableCell
                      key={testItem.testCode}
                      align="center"
                      sx={{
                        bgcolor: isLocked ? "background.default" : theme.custom.status.purple.bg,
                        fontWeight: 800,
                        color: isLocked ? "text.secondary" : brandColors.sectionTitle,
                        minWidth: 190,
                        borderInlineEnd: "1px solid",
                        borderInlineEndColor: "divider"
                      }}
                    >
                      <Stack
                        direction="row"
                        spacing={0.5}
                        sx={{
                          justifyContent: "center",
                          alignItems: "center"
                        }}>
                        <Typography sx={{ fontSize: 13, fontWeight: 800 }}>
                          <Ltr>{testItem.testCode}</Ltr>
                        </Typography>
                        {isLocked && <LockOutlinedIcon sx={{ fontSize: 14, color: theme.custom.status.detected.text }} />}
                      </Stack>

                      <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 500 }}>
                        {testItem.displayName}
                      </Typography>

                      <Stack
                        direction="row"
                        spacing={0.5}
                        sx={{
                          justifyContent: "center",
                          alignItems: "center",
                          mt: 0.5
                        }}>
                        <Chip
                          label={testItem.testSessionStateDisplay}
                          size="small"
                          sx={{
                            fontSize: 12,
                            height: 20,
                            bgcolor: isLocked ? theme.custom.status.detected.bg : theme.custom.status.info.bg,
                            color: isLocked ? theme.custom.status.detected.text : theme.custom.status.info.text,
                            fontWeight: 700
                          }}
                        />
                        {!isQuant && confMediaCount > 1 && (
                          <Chip
                            label={
                              <Trans
                                i18nKey="pathogenSession.primaryMatrix.confMediaChip"
                                ns="testingWorkspace"
                                values={{ count: confMediaCount }}
                                components={{ ltr: <Ltr>{null}</Ltr> }}
                              />
                            }
                            size="small"
                            sx={{
                              fontSize: 12,
                              height: 20,
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
                      insetInlineStart: 0,
                      zIndex: 2,
                      bgcolor: idx % 2 === 0 ? "background.paper" : "background.default",
                      fontWeight: 700,
                      borderInlineEnd: "2px solid",
                      borderInlineEndColor: "divider"
                    }}
                  >
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                      {loc.locationName}
                    </Typography>
                    <Stack
                      direction="row"
                      spacing={0.5}
                      sx={{
                        alignItems: "center",
                        mt: 0.25
                      }}>
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {loc.locationType}
                      </Typography>
                      {loc.gradeClassification && (
                        <Chip
                          label={t("pathogenSession.primaryMatrix.gradeChip", { grade: loc.gradeClassification })}
                          size="small"
                          sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                        />
                      )}
                    </Stack>
                  </TableCell>

                  {/* Primary Observation Matrix Cells */}
                  {session.assignedTests.map((testItem, testIdx) => {
                    const isQuant = isQuantitativeTest(testItem);

                    const slocId = loc.testLocationMap[testItem.testCode] ?? loc.primarySampleLocationId;
                    const key = `${slocId}_${testItem.testCode}`;
                    const val = primaryObservations[key];
                    const isCellLocked = !testItem.isResultEntryAllowed;

                    // If cell is locked by workflow prerequisite, render clean disabled lock badge
                    if (isCellLocked) {
                      return (
                        <TableCell
                          key={testItem.testCode}
                          align="center"
                          sx={{
                            p: 1,
                            borderInlineEnd: "1px solid",
                            borderInlineEndColor: "divider",
                            bgcolor: "background.default"
                          }}
                        >
                          <Tooltip title={testItem.lockReason ?? t("pathogenSession.primaryMatrix.lockedReasonFallback")}>
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
                                fontSize: 12,
                                fontWeight: 700,
                                cursor: "not-allowed"
                              }}
                            >
                              <LockOutlinedIcon sx={{ fontSize: 14, color: theme.custom.countdown.locked.text }} />
                              <span>{testItem.lockReason ?? t("pathogenSession.primaryMatrix.lockedReasonFallback")}</span>
                            </Box>
                          </Tooltip>
                        </TableCell>
                      );
                    }

                    const isCellFilled = val && (
                      (isQuant && val.numericValue !== null) ||
                      (!isQuant && val.observation !== "")
                    );

                    const countResult = testItem.countResult;
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
                          key={testItem.testCode}
                          align="center"
                          sx={{
                            p: 1,
                            borderInlineEnd: "1px solid",
                            borderInlineEndColor: "divider"
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
                                {t("pathogenSession.primaryMatrix.reviewRequired")}
                              </Typography>
                            )}
                          </Box>
                        </TableCell>
                      );
                    }

                    return (
                      <TableCell
                        key={testItem.testCode}
                        align="center"
                        {...cellProps(idx, testIdx)}
                        sx={{
                          p: 1,
                          borderInlineEnd: "1px solid",
                          borderInlineEndColor: "divider",
                          bgcolor: !isCellFilled ? theme.custom.status.pending.bg : "inherit"
                        }}
                      >
                        {isQuant ? (
                          <Box sx={{ minWidth: benchMode ? 150 : 120 }}>
                            <NumericEntryField
                              kind="decimal"
                              label=""
                              aria-label={t("pathogenSession.primaryMatrix.countAriaLabel", { testName: testItem.displayName, locationName: loc.locationName })}
                              unit={t("pathogenSession.primaryMatrix.cfuUnit")}
                              value={val?.numericText ?? ""}
                              onChange={(next) => handleQuantitativeChange(slocId, testItem.testCode, next)}
                            />
                          </Box>
                        ) : benchMode ? (
                          <ObservationOptions
                            name={`table-obs-${key}`}
                            value={val?.observation ?? ""}
                            onChange={(v) => handleObservationChange(slocId, testItem.testCode, v)}
                            options={matrixObservationOptions}
                          />
                        ) : (
                          <TextField
                            select
                            size="small"
                            value={val?.observation ?? ""}
                            onChange={(e) => handleObservationChange(slocId, testItem.testCode, e.target.value as GrowthObservation)}
                            slotProps={{ htmlInput: { "aria-label": t("pathogenSession.primaryMatrix.observationAriaLabel", { testName: testItem.displayName, locationName: loc.locationName }) } }}
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
                              <em>{t("pathogenSession.primaryMatrix.selectObservation")}</em>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.NoGrowth}>
                              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
                                {t("pathogenSession.primaryMatrix.noGrowth")}
                              </Typography>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.GrowthNonConforming}>
                              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.action.text }}>
                                {t("pathogenSession.primaryMatrix.growthNonConforming")}
                              </Typography>
                            </MenuItem>
                            <MenuItem value={GrowthObservation.GrowthConforming}>
                              <Typography sx={{ fontSize: 12, fontWeight: 800, color: theme.custom.status.detected.text }}>
                                {t("pathogenSession.primaryMatrix.growthConforming")}
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
      )}

      {benchMode && hasQuantTests && <RegistryBenchKeypad />}

      <ConfirmationDialog
        open={fillConfirmation !== null}
        message={fillConfirmation
          ? t("pathogenSession.primaryMatrix.fillConfirmationMessage", {
              cellCount: fillConfirmation.cells.length,
              locationCount: fillConfirmation.locationNames.length,
              locationNames: fillConfirmation.locationNames.join(", ")
            })
          : ""}
        onConfirm={handleConfirmFillAllNoGrowth}
        onCancel={() => setFillConfirmation(null)}
      />
    </Stack>
  );
}
