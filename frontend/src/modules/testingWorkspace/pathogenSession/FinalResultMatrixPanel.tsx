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
import {
  PathogenTestingSessionDto,
  MatrixCellInput
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { brandColors } from "../../../theme";

interface Props {
  session: PathogenTestingSessionDto;
  onUpdated: (updatedSession: PathogenTestingSessionDto) => void;
  onNext: () => void;
}

export function FinalResultMatrixPanel({ session, onUpdated, onNext }: Props) {
  const theme = useTheme();
  const [cellValues, setCellValues] = useState<Record<string, {
    resultCode: string;
    resultDisplay: string;
    numericValue: number | null;
    resultType: string;
    sampleLocationId: number;
    testCode: string;
  }>>({});

  const [searchQuery, setSearchQuery] = useState("");
  const [filterPendingOnly, setFilterPendingOnly] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  // Initialize cell values from session matrix
  useEffect(() => {
    const initialMap: typeof cellValues = {};
    for (const cell of session.resultMatrix) {
      const key = `${cell.sampleLocationId}_${cell.testCode}`;
      initialMap[key] = {
        resultCode: cell.resultCode ?? "",
        resultDisplay: cell.resultDisplay ?? "",
        numericValue: cell.numericValue,
        resultType: cell.resultType,
        sampleLocationId: cell.sampleLocationId,
        testCode: cell.testCode
      };
    }
    setCellValues(initialMap);
  }, [session]);

  const testStateMap = useMemo(() => {
    return new Map(session.assignedTests.map((t) => [t.testCode, t]));
  }, [session.assignedTests]);

  const isTsbIncubating = session.sharedTsb.isIncubating;
  const areAllTestsLocked = session.assignedTests.every((t) => !t.isResultEntryAllowed);

  const handleQualitativeChange = (sampleLocationId: number, testCode: string, value: string) => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    const display = value === "DETECTED" ? "Detected (+)" : value === "NOT_DETECTED" ? "Not Detected (-)" : "";
    setCellValues((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, resultType: "Qualitative", numericValue: null }),
        resultCode: value,
        resultDisplay: display
      }
    }));
    setSaveSuccess(false);
  };

  const handleQuantitativeChange = (sampleLocationId: number, testCode: string, numStr: string) => {
    const testInfo = testStateMap.get(testCode);
    if (testInfo && !testInfo.isResultEntryAllowed) return;

    const key = `${sampleLocationId}_${testCode}`;
    const num = numStr !== "" ? parseFloat(numStr) : null;
    const display = num !== null && !isNaN(num) ? `${num} CFU` : "";
    setCellValues((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { sampleLocationId, testCode, resultType: "Quantitative", resultCode: "" }),
        resultCode: numStr,
        resultDisplay: display,
        numericValue: num !== null && !isNaN(num) ? num : null
      }
    }));
    setSaveSuccess(false);
  };

  const handleFillAllNotDetected = () => {
    let eligibleCount = 0;
    let lockedCount = 0;

    for (const loc of session.locations) {
      for (const test of session.assignedTests) {
        if (!test.isResultEntryAllowed) {
          lockedCount++;
          continue;
        }
        const isQuantitative = test.workflowType.toLowerCase().includes("count") ||
                              test.testCode.toLowerCase().includes("tamc") ||
                              test.testCode.toLowerCase().includes("tymc");
        if (!isQuantitative) {
          const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
          const key = `${slocId}_${test.testCode}`;
          if (!cellValues[key]?.resultCode) {
            eligibleCount++;
          }
        }
      }
    }

    if (eligibleCount === 0) {
      setError(lockedCount > 0
        ? "No eligible empty cells to populate. All remaining empty cells are locked by workflow prerequisites."
        : "All eligible qualitative cells are already filled.");
      return;
    }

    setCellValues((prev) => {
      const updated = { ...prev };
      for (const loc of session.locations) {
        for (const test of session.assignedTests) {
          if (!test.isResultEntryAllowed) continue; // Strictly skip locked cells!

          const isQuantitative = test.workflowType.toLowerCase().includes("count") ||
                                test.testCode.toLowerCase().includes("tamc") ||
                                test.testCode.toLowerCase().includes("tymc");
          if (!isQuantitative) {
            const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
            const key = `${slocId}_${test.testCode}`;
            if (!updated[key]?.resultCode) {
              updated[key] = {
                sampleLocationId: slocId,
                testCode: test.testCode,
                resultCode: "NOT_DETECTED",
                resultDisplay: "Not Detected (-)",
                numericValue: null,
                resultType: "Qualitative"
              };
            }
          }
        }
      }
      return updated;
    });
    setSaveSuccess(false);
  };

  // Calculate Dynamic Tri-State Counts
  const { completedCount, availableCount, lockedCount, totalCount } = useMemo(() => {
    let completed = 0;
    let available = 0;
    let locked = 0;

    for (const loc of session.locations) {
      for (const test of session.assignedTests) {
        const slocId = loc.testLocationMap[test.testCode] ?? loc.primarySampleLocationId;
        const key = `${slocId}_${test.testCode}`;
        const val = cellValues[key];

        const isFilled = val && (
          (val.resultType === "Quantitative" && val.numericValue !== null) ||
          (val.resultType !== "Quantitative" && (val.resultCode === "NOT_DETECTED" || val.resultCode === "DETECTED"))
        );

        if (isFilled) {
          completed++;
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
      totalCount: total
    };
  }, [session, cellValues]);

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
          const val = cellValues[`${slocId}_${t.testCode}`];
          return !val || (val.resultType === "Quantitative" ? val.numericValue === null : !val.resultCode);
        });
        return hasPending;
      }

      return true;
    });
  }, [session, searchQuery, filterPendingOnly, cellValues]);

  const handleSaveMatrix = async () => {
    setError(null);
    setSaving(true);
    try {
      const cells: MatrixCellInput[] = Object.values(cellValues)
        .filter((c) => {
          const testInfo = testStateMap.get(c.testCode);
          return testInfo?.isResultEntryAllowed && (c.resultCode || c.numericValue !== null);
        })
        .map((c) => ({
          sampleLocationId: c.sampleLocationId,
          testCode: c.testCode,
          resultCode: c.resultCode,
          resultDisplay: c.resultDisplay,
          numericValue: c.numericValue,
          resultType: c.resultType
        }));

      if (cells.length === 0) {
        setError("No eligible result cells are available to save.");
        return;
      }

      const updated = await PathogenSessionService.saveResultMatrix(session.sampleId, { cells });
      onUpdated(updated);
      setSaveSuccess(true);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to save result matrix.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2.5}>
      {/* Gating Lock Alert Banner */}
      {isTsbIncubating && (
        <Alert severity="warning" icon={<LockOutlinedIcon />} sx={{ fontWeight: 600 }}>
          Results are locked until the required pathogen workflow steps are completed. Shared TSB Enrichment is currently incubating.
        </Alert>
      )}

      {/* Top Completion & Tri-State Stats Header */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#fcfaff" }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" alignItems={{ md: "center" }} spacing={2}>
          <Box sx={{ flex: 1 }}>
            <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" sx={{ mb: 1.25 }}>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: "#1f2937" }}>
                Final Result Matrix
              </Typography>
              <Chip
                label={`Completed: ${completedCount}`}
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: completedCount > 0 ? "#d1fae5" : "#f3f4f6",
                  color: completedCount > 0 ? "#065f46" : "#374151"
                }}
              />
              <Chip
                label={`Available: ${availableCount}`}
                size="small"
                sx={{
                  fontWeight: 700,
                  bgcolor: availableCount > 0 ? "#eff6ff" : "#f3f4f6",
                  color: availableCount > 0 ? "#1e40af" : "#374151"
                }}
              />
              {lockedCount > 0 && (
                <Chip
                  icon={<LockOutlinedIcon sx={{ fontSize: 13 }} />}
                  label={`Locked: ${lockedCount}`}
                  size="small"
                  sx={{
                    fontWeight: 700,
                    bgcolor: "#fee2e2",
                    color: "#991b1b"
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
                bgcolor: "#e5e7eb",
                "& .MuiLinearProgress-bar": {
                  bgcolor: completedCount === totalCount ? "#059669" : brandColors.sectionTitle
                }
              }}
            />
          </Box>

          <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
            <Button
              variant="outlined"
              size="small"
              startIcon={<DoneAllIcon />}
              onClick={handleFillAllNotDetected}
              disabled={areAllTestsLocked || availableCount === 0}
              sx={{ fontSize: 12, fontWeight: 600, textTransform: "none" }}
            >
              Fill All Unset as Not Detected (-)
            </Button>

            <Button
              variant="contained"
              color="primary"
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <SaveOutlinedIcon />}
              onClick={handleSaveMatrix}
              disabled={saving || areAllTestsLocked}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              Save Results
            </Button>

            <Button
              variant="contained"
              color="success"
              endIcon={<ArrowForwardIcon />}
              onClick={onNext}
              disabled={completedCount < totalCount || isTsbIncubating}
              sx={{ px: 2.5, fontWeight: 700 }}
            >
              Review & Complete
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {saveSuccess && (
        <Alert severity="success" onClose={() => setSaveSuccess(false)}>
          Matrix results saved successfully with ALCOA+ audit attribution!
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
                <SearchIcon sx={{ fontSize: 18, color: "#9ca3af" }} />
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
          {filterPendingOnly ? "Showing Available Only" : "Show Available Only"}
        </Button>

        <Typography sx={{ fontSize: 12, color: "#6b7280", ml: "auto" }}>
          Showing {filteredLocations.length} of {session.totalLocations} locations
        </Typography>
      </Stack>

      {/* Dynamic Result Matrix Grid */}
      <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        <Box sx={{ maxHeight: 520, overflow: "auto" }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                {/* Sticky First Column for Sampling Location */}
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

                  return (
                    <TableCell
                      key={t.testCode}
                      align="center"
                      sx={{
                        bgcolor: isLocked ? theme.custom.countdown.locked.bg : theme.custom.countdown.active.bg,
                        fontWeight: 800,
                        color: isLocked ? theme.custom.countdown.locked.text : theme.custom.countdown.active.text,
                        minWidth: 180,
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
                      <Chip
                        label={t.testSessionStateDisplay}
                        size="small"
                        sx={{
                          fontSize: 10,
                          height: 18,
                          bgcolor: isLocked ? theme.custom.status.detected.bg : theme.custom.status.info.bg,
                          color: isLocked ? theme.custom.status.detected.text : theme.custom.status.info.text,
                          mt: 0.5,
                          fontWeight: 700
                        }}
                      />
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
                          sx={{ fontSize: 9, height: 16, bgcolor: "#ecfdf5", color: "#065f46" }}
                        />
                      )}
                    </Stack>
                  </TableCell>

                  {/* Matrix Cells */}
                  {session.assignedTests.map((t) => {
                    const isQuantitative = t.workflowType.toLowerCase().includes("count") ||
                                          t.testCode.toLowerCase().includes("tamc") ||
                                          t.testCode.toLowerCase().includes("tymc");

                    const slocId = loc.testLocationMap[t.testCode] ?? loc.primarySampleLocationId;
                    const key = `${slocId}_${t.testCode}`;
                    const val = cellValues[key];
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
                          <Tooltip title={t.lockReason ?? "Locked until workflow prerequisite is complete"}>
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
                      (isQuantitative && val.numericValue !== null) ||
                      (!isQuantitative && (val.resultCode === "NOT_DETECTED" || val.resultCode === "DETECTED"))
                    );

                    return (
                      <TableCell
                        key={t.testCode}
                        align="center"
                        sx={{
                          p: 1,
                          borderRight: "1px solid #f1f5f9",
                          bgcolor: !isCellFilled ? "#fffbeb" : "inherit"
                        }}
                      >
                        {isQuantitative ? (
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
                                bgcolor: "#ffffff"
                              }
                            }}
                          />
                        ) : (
                          <TextField
                            select
                            size="small"
                            value={val?.resultCode ?? ""}
                            onChange={(e) => handleQualitativeChange(slocId, t.testCode, e.target.value)}
                            sx={{
                              width: 150,
                              "& .MuiInputBase-root": {
                                fontSize: 12,
                                fontWeight: 700,
                                bgcolor: val?.resultCode === "DETECTED"
                                  ? "#fee2e2"
                                  : val?.resultCode === "NOT_DETECTED"
                                  ? "#d1fae5"
                                  : "#ffffff",
                                color: val?.resultCode === "DETECTED"
                                  ? "#991b1b"
                                  : val?.resultCode === "NOT_DETECTED"
                                  ? "#065f46"
                                  : "inherit"
                              }
                            }}
                          >
                            <MenuItem value="">
                              <em>Select Result</em>
                            </MenuItem>
                            <MenuItem value="NOT_DETECTED">
                              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#065f46" }}>
                                Not Detected (-)
                              </Typography>
                            </MenuItem>
                            <MenuItem value="DETECTED">
                              <Typography sx={{ fontSize: 12, fontWeight: 800, color: "#dc2626" }}>
                                Detected (+)
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
    </Stack>
  );
}
