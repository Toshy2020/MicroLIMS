import React, { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { SignatureDialog } from "../../components/SignatureDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import {
  AnalysisDetail,
  ParameterResultDetail,
  ResultReadingDetail,
  TestOrderSummaryDetail
} from "./types/sampleSummaryTypes";

interface Props {
  testOrderId: number;
  displayName: string;
  testCode?: string;
  itemId?: number | null;
  sampleId?: number | null;
  current: {
    workflowType?: string;
    testName?: string;
    sampleContext?: {
      sampleName?: string;
      [key: string]: unknown;
    };
    allStepsComplete?: boolean;
    finalResult?: string | null;
    returnInfo?: { reason?: string | null } | null;
  };
  onRecorded: () => Promise<void> | void;
  onClose?: () => void;
}

interface UnitRowInput {
  minutes: string;
  notDisintegrated: boolean;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const errorMessage = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

export function DisintegrationPanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  sampleId,
  current,
  onRecorded,
  onClose
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Configuration
  const [testDef, setTestDef] = useState<TestDefinitionOption | null>(null);
  const [spec, setSpec] = useState<SpecificationDto | null>(null);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  // Stage 1 form
  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [conditions, setConditions] = useState<Record<string, string>>({});
  const [stage1Units, setStage1Units] = useState<UnitRowInput[]>(() =>
    Array.from({ length: 6 }, () => ({ minutes: "", notDisintegrated: false }))
  );

  // Active analysis / pending stage
  const [activeAnalysis, setActiveAnalysis] = useState<AnalysisDetail | null>(null);
  const [activeOrder, setActiveOrder] = useState<TestOrderSummaryDetail | null>(null);
  const [stage2Units, setStage2Units] = useState<UnitRowInput[]>(() =>
    Array.from({ length: 12 }, () => ({ minutes: "", notDisintegrated: false }))
  );

  // Signature & submission
  const [signing, setSigning] = useState(false);
  const [signingStage, setSigningStage] = useState<1 | 2>(1);
  const [comment, setComment] = useState("");

  // Outcomes & banners
  const [outcome, setOutcome] = useState<{ text: string; status?: string } | null>(null);
  const [outcomeBanner, setOutcomeBanner] = useState<{
    message: string;
    severity: "success" | "info" | "error" | "warning";
  } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  const loadData = async () => {
    setLoading(true);
    setError(null);

    try {
      // 1. Fetch test definitions
      const defs: TestDefinitionOption[] = await masterDataOptions.getTestDefinitions();
      const matchedDef = defs.find((d) => d.code === effectiveTestCode) ?? null;

      const s1UnitsCount = matchedDef?.disintegrationStage1Units ?? 6;
      const s2UnitsCount = matchedDef?.disintegrationStage2Units ?? 12;

      // 2. Resolve itemId if not provided
      let resolvedItemId = itemId;
      if (resolvedItemId == null) {
        try {
          const allItems = await ItemService.getAll();
          const matched = allItems.find(
            (i) =>
              i.name === current.sampleContext?.sampleName ||
              i.assignedTests?.some((t) => t.testCode === effectiveTestCode)
          );
          resolvedItemId = matched?.id ?? null;
        } catch {
          // fallback
        }
      }

      // 3. Fetch specifications for this item and test (DisintegrationTime limitType)
      let matchedSpec: SpecificationDto | null = null;
      if (resolvedItemId != null) {
        const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
        const disSpecs = itemSpecs.filter(
          (s) => s.testCode === effectiveTestCode && s.limitType === "DisintegrationTime"
        );
        matchedSpec = disSpecs[0] ?? null;
      }

      // 4. Load FP section equipment (DisintegrationTester prioritized)
      let fpInstruments: ConfiguredEquipmentSummary[] = [];
      try {
        const [sections, allEquip] = await Promise.all([
          getSections(),
          EquipmentConfigurationService.getConfiguredSummary()
        ]);
        const fpSec = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === "FP");
        fpInstruments = (allEquip ?? []).filter(
          (e) =>
            (fpSec && e.sectionId === fpSec.sectionId) ||
            e.section?.code === "FP" ||
            e.section?.sectionCode === "FP"
        );
        // Sort with DisintegrationTester first
        fpInstruments.sort((a, b) => {
          const aDis = a.type === "DisintegrationTester" ? 0 : 1;
          const bDis = b.type === "DisintegrationTester" ? 0 : 1;
          return aDis - bDis;
        });
      } catch {
        // fallback
      }

      // 5. Initialize condition fields
      const condLabels = matchedDef?.conditionFields
        ? matchedDef.conditionFields.split(",").map((s) => s.trim()).filter(Boolean)
        : [];
      setConditions((prev) => {
        const next: Record<string, string> = { ...prev };
        for (const l of condLabels) {
          if (next[l] === undefined) next[l] = "";
        }
        return next;
      });

      // 6. Check sample summary for existing testOrder analysis & pending stage
      if (sampleId != null) {
        try {
          const summary = await SampleSummaryService.getSummary(sampleId);
          const ord = summary.testOrders?.find((t) => t.testOrderId === testOrderId) ?? null;
          setActiveOrder(ord);

          if (ord?.analysis) {
            setActiveAnalysis(ord.analysis);
            const param = ord.analysis.parameterResults?.[0];
            if (param?.comparisonStatus === "NextStageRequired") {
              setStage2Units(Array.from({ length: s2UnitsCount }, () => ({ minutes: "", notDisintegrated: false })));
            }
          }
        } catch {
          // non-blocking
        }
      }

      setTestDef(matchedDef);
      setSpec(matchedSpec);
      setFpEquipment(fpInstruments);
      setStage1Units((prev) =>
        prev.length === s1UnitsCount
          ? prev
          : Array.from({ length: s1UnitsCount }, () => ({ minutes: "", notDisintegrated: false }))
      );
    } catch (err: unknown) {
      setError(errorMessage(err, "Could not load disintegration test configuration or specifications."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testOrderId, effectiveTestCode, itemId, sampleId]);

  const conditionLabels = useMemo(() => {
    if (!testDef?.conditionFields) return [];
    return testDef.conditionFields
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);
  }, [testDef?.conditionFields]);

  const activeParam: ParameterResultDetail | null = useMemo(() => {
    return activeAnalysis?.parameterResults?.[0] ?? null;
  }, [activeAnalysis]);

  const isNextStageRequired = activeParam?.comparisonStatus === "NextStageRequired";
  const existingReadings = activeParam?.readings ?? [];

  // Group existing readings by stage for read-only presentation
  const readingsByStage = useMemo(() => {
    const map = new Map<number, ResultReadingDetail[]>();
    for (const r of existingReadings) {
      const stageNum = r.stage ?? 1;
      if (!map.has(stageNum)) map.set(stageNum, []);
      map.get(stageNum)!.push(r);
    }
    return map;
  }, [existingReadings]);

  const limitMinutes = spec?.upperLimit != null ? Number(spec.upperLimit) : null;
  const specLimitText = spec?.specLimit || (limitMinutes != null ? `NMT ${limitMinutes} min` : "Not specified");

  const s1UnitsCount = testDef?.disintegrationStage1Units ?? 6;
  const s2UnitsCount = testDef?.disintegrationStage2Units ?? 12;
  const maxS1Failures = testDef?.disintegrationMaxStage1Failures ?? 2;
  const minPassTotal = testDef?.disintegrationMinPassTotal ?? 16;

  // Validation: Stage 1
  const isStage1Valid = useMemo(() => {
    if (!spec) return false;
    if (!analysedAt.trim()) return false;
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }
    for (const label of conditionLabels) {
      if (!conditions[label] || conditions[label].trim() === "") {
        return false;
      }
    }
    if (stage1Units.length !== s1UnitsCount) return false;
    for (const u of stage1Units) {
      if (u.notDisintegrated) continue;
      const m = Number(u.minutes);
      if (!u.minutes || u.minutes.trim() === "" || isNaN(m) || m <= 0) {
        return false;
      }
    }
    return true;
  }, [spec, analysedAt, conditionLabels, conditions, s1UnitsCount, stage1Units]);

  // Validation: Stage 2
  const isStage2Valid = useMemo(() => {
    if (stage2Units.length !== s2UnitsCount) return false;
    for (const u of stage2Units) {
      if (u.notDisintegrated) continue;
      const m = Number(u.minutes);
      if (!u.minutes || u.minutes.trim() === "" || isNaN(m) || m <= 0) {
        return false;
      }
    }
    return true;
  }, [s2UnitsCount, stage2Units]);

  // Submission: Stage 1
  const submitStage1 = async (password: string) => {
    setError(null);
    try {
      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        conditions,
        unitMinutes: stage1Units.map((u) => (u.notDisintegrated ? null : Number(u.minutes))),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordDisintegrationResult(testOrderId, payload);
      setSigning(false);

      let outcomeText = "";
      let severity: "success" | "info" | "error" = "success";
      if (res?.status === "NextStageRequired") {
        outcomeText = "Stage 2 required";
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = "Complies";
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = "Does not comply";
        severity = "error";
      } else {
        outcomeText = res?.outcomeSummary ?? res?.status ?? "Recorded";
      }

      setOutcome({ text: outcomeText, status: res?.status ?? undefined });
      setOutcomeBanner({ message: outcomeText, severity });

      await onRecorded();
      await loadData();
    } catch (err: unknown) {
      setSigning(false);
      setError(errorMessage(err, "Failed to record disintegration result."));
    }
  };

  // Submission: Stage 2
  const submitStage2 = async (password: string) => {
    setError(null);
    try {
      const payload = {
        unitMinutes: stage2Units.map((u) => (u.notDisintegrated ? null : Number(u.minutes))),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordDisintegrationStage(testOrderId, payload);
      setSigning(false);

      let outcomeText = "";
      let severity: "success" | "info" | "error" = "success";
      if (res?.status === "NextStageRequired") {
        outcomeText = "Stage 2 required";
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = "Complies";
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = "Does not comply";
        severity = "error";
      } else {
        outcomeText = res?.outcomeSummary ?? res?.status ?? "Recorded";
      }

      setOutcome({ text: outcomeText, status: res?.status ?? undefined });
      setOutcomeBanner({ message: outcomeText, severity });

      await onRecorded();
      await loadData();
    } catch (err: unknown) {
      setSigning(false);
      setError(errorMessage(err, "Failed to record disintegration stage."));
    }
  };

  const renderLiveHint = (row: UnitRowInput) => {
    if (row.notDisintegrated) {
      return <Chip size="small" color="error" label="Fail" sx={{ height: 20, fontSize: 10 }} />;
    }
    if (!row.minutes || row.minutes.trim() === "") {
      return "—";
    }
    const val = Number(row.minutes);
    if (isNaN(val) || val <= 0) {
      return <Chip size="small" color="warning" label="Invalid" sx={{ height: 20, fontSize: 10 }} />;
    }
    if (limitMinutes != null) {
      return val <= limitMinutes ? (
        <Chip size="small" color="success" label="Pass" sx={{ height: 20, fontSize: 10 }} />
      ) : (
        <Chip size="small" color="error" label="Fail" sx={{ height: 20, fontSize: 10 }} />
      );
    }
    return "—";
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, textAlign: "center" }}>
        <CircularProgress size={32} />
        <Typography sx={{ mt: 1, fontSize: 13, color: "text.secondary" }}>
          Loading disintegration configuration and readings…
        </Typography>
      </Box>
    );
  }

  // Finalized view
  const isFinalized =
    current.allStepsComplete ||
    (activeOrder?.analysis &&
      activeOrder.analysis.parameterResults?.[0]?.comparisonStatus !== "NextStageRequired" &&
      activeOrder.status === "Completed") ||
    (outcome && outcome.status !== "NextStageRequired");

  if (isFinalized && !isNextStageRequired) {
    const finalStatus = outcome?.status ?? activeParam?.comparisonStatus;
    const isOos = finalStatus === "OutOfSpecification" || (outcome?.text ?? "").includes("Does not comply");
    const summaryText = outcome?.text ?? activeParam?.reportedDisplay ?? current.finalResult ?? "Analysis Complete";

    return (
      <Box>
        <Alert severity={isOos ? "error" : "success"} sx={{ mb: 2 }}>
          {displayName}: <strong>{summaryText}</strong>
          {finalStatus && ` (${finalStatus})`}
        </Alert>

        {activeAnalysis && (
          <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
              Disintegration Analysis Summary
            </Typography>
            <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", mb: 2 }}>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Specification
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {specLimitText}
                </Typography>
              </Box>
              {activeParam?.reportedDisplay && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    Reported Result
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {activeParam.reportedDisplay}
                  </Typography>
                </Box>
              )}
              {activeParam?.reportedValue != null && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    Longest Time
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {activeParam.reportedValue} min
                  </Typography>
                </Box>
              )}
              {activeAnalysis.equipmentCode && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    Apparatus
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {activeAnalysis.equipmentCode}
                  </Typography>
                </Box>
              )}
            </Stack>

            {Array.from(readingsByStage.entries()).map(([stg, rList]) => (
              <Box key={stg} sx={{ mt: 2 }}>
                <Typography sx={{ fontWeight: 600, fontSize: 12, mb: 0.5 }}>
                  Stage {stg} ({rList.length} units)
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Unit</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Time (min)</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Unit Mark</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rList.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell sx={{ fontSize: 12 }}>Unit {r.index}</TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                          {r.value1 != null ? `${r.value1} min` : (r.text || "Not disintegrated")}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {r.passed === null ? "—" : r.passed ? (
                            <Chip size="small" color="success" label="Pass" sx={{ height: 20, fontSize: 10 }} />
                          ) : (
                            <Chip size="small" color="error" label="Fail" sx={{ height: 20, fontSize: 10 }} />
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            ))}
          </Paper>
        )}

        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded — Pending Review" />
          {onClose && (
            <Button variant="contained" onClick={onClose}>
              Done / Close
            </Button>
          )}
        </Box>
      </Box>
    );
  }

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? `Returned for revision: ${current.returnInfo.reason}`
            : "Returned by reviewer for revision"}
        </Alert>
      )}

      {outcomeBanner && (
        <Alert severity={outcomeBanner.severity} sx={{ mb: 2 }} onClose={() => setOutcomeBanner(null)}>
          Outcome: <strong>{outcomeBanner.message}</strong>
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {!spec && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          No Disintegration specification configured for this item and test. Please configure a specification with limit type &ldquo;Disintegration Time&rdquo; in Item Specifications.
        </Alert>
      )}

      {/* PENDING STAGE (Stage 2) */}
      {isNextStageRequired && activeParam ? (
        <Box sx={{ mt: 2 }}>
          <Alert severity="warning" sx={{ mb: 2 }}>
            <strong>Stage 2 required:</strong> Results from Stage 1 did not meet acceptance criteria. Staged testing continues to Stage 2.
          </Alert>

          {/* Grouped read-only previous stage(s) */}
          <Typography sx={{ fontWeight: 700, fontSize: 14, mb: 1 }}>
            1. Previously Recorded Stage 1 Units
          </Typography>

          <Stack spacing={2} sx={{ mb: 3 }}>
            {Array.from(readingsByStage.entries()).map(([stg, rList]) => (
              <Paper key={stg} variant="outlined" sx={{ p: 2, bgcolor: "background.paper" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
                  Stage {stg} Units ({rList.length} units)
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Unit</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Time (min)</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Stage Criterion</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rList.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell sx={{ fontSize: 12 }}>Unit {r.index}</TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                          {r.value1 != null ? `${r.value1} min` : (r.text || "Not disintegrated")}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {r.passed === null ? "—" : r.passed ? (
                            <Chip size="small" color="success" label="Pass" sx={{ height: 20, fontSize: 10 }} />
                          ) : (
                            <Chip size="small" color="error" label="Fail" sx={{ height: 20, fontSize: 10 }} />
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            ))}
          </Stack>

          {/* Stage 2 Entry */}
          <Paper variant="outlined" sx={{ p: 2, bgcolor: "action.hover" }}>
            <Typography sx={{ fontWeight: 700, fontSize: 14, mb: 1 }}>
              2. Stage 2 Unit Entry ({s2UnitsCount} additional units, Units {s1UnitsCount + 1}–{s1UnitsCount + s2UnitsCount})
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
              Stage 2 acceptance: At least {minPassTotal} of the total {s1UnitsCount + s2UnitsCount} units must disintegrate within the specification limit ({specLimitText}).
            </Typography>

            <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 100 }}>Unit</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 180 }}>Time (min)</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 180 }}>Status</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 100 }}>Live Hint</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {stage2Units.map((u, i) => (
                  <TableRow key={i}>
                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                      Unit {s1UnitsCount + i + 1}
                    </TableCell>
                    <TableCell>
                      <TextField
                        size="small"
                        type="number"
                        placeholder="Minutes"
                        value={u.notDisintegrated ? "" : u.minutes}
                        disabled={u.notDisintegrated}
                        onChange={(e) => {
                          const val = e.target.value;
                          setStage2Units((prev) =>
                            prev.map((row, idx) => (idx === i ? { ...row, minutes: val } : row))
                          );
                        }}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        sx={{ width: 140 }}
                      />
                    </TableCell>
                    <TableCell>
                      <FormControlLabel
                        control={
                          <Checkbox
                            size="small"
                            checked={u.notDisintegrated}
                            onChange={(e) => {
                              const checked = e.target.checked;
                              setStage2Units((prev) =>
                                prev.map((row, idx) =>
                                  idx === i ? { minutes: checked ? "" : row.minutes, notDisintegrated: checked } : row
                                )
                              );
                            }}
                          />
                        }
                        label={<Typography sx={{ fontSize: 12 }}>Not disintegrated</Typography>}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {renderLiveHint(u)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <Typography variant="caption" sx={{ color: "text.secondary", mt: 1, display: "block" }}>
              * Live pass/fail hint is based on {specLimitText}; the server&apos;s staged evaluation verdict is authoritative.
            </Typography>

            <Box sx={{ mt: 3, display: "flex", justifyContent: "flex-end" }}>
              <Button
                variant="contained"
                disabled={!isStage2Valid}
                onClick={() => {
                  setSigningStage(2);
                  setSigning(true);
                }}
              >
                Sign & Record Stage 2 Result
              </Button>
            </Box>
          </Paper>
        </Box>
      ) : (
        /* STAGE 1 INITIAL FORM */
        <Box sx={{ mt: 2 }}>
          <Typography sx={{ fontWeight: 700, mb: 1.5 }}>
            1. Analysis parameters & conditions
          </Typography>

          <Stack spacing={2} sx={{ mb: 2.5 }}>
            {/* Equipment and DateTime */}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <FormControl size="small" sx={{ flex: 1.5 }}>
                <InputLabel id="disintegration-equipment-label">Disintegration Apparatus</InputLabel>
                <Select
                  labelId="disintegration-equipment-label"
                  label="Disintegration Apparatus"
                  value={selectedEquipmentId}
                  onChange={(e) => setSelectedEquipmentId(e.target.value as number | "")}
                >
                  <MenuItem value=""><em>Select apparatus (optional)</em></MenuItem>
                  {fpEquipment.map((eq) => (
                    <MenuItem key={eq.id} value={eq.id}>
                      {eq.code} — {eq.name} ({eq.type})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <TextField
                size="small"
                type="datetime-local"
                label="Analysis Time (Local) *"
                value={analysedAt}
                onChange={(e) => setAnalysedAt(e.target.value)}
                slotProps={{ inputLabel: { shrink: true } }}
                required
                sx={{ flex: 1 }}
              />
            </Stack>

            {/* Conditions Form */}
            {conditionLabels.length > 0 && (
              <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                  Test Conditions
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
                  {conditionLabels.map((label) => (
                    <TextField
                      key={label}
                      size="small"
                      label={label}
                      placeholder={`Enter ${label}`}
                      value={conditions[label] || ""}
                      onChange={(e) =>
                        setConditions((prev) => ({ ...prev, [label]: e.target.value }))
                      }
                      required
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                  ))}
                </Stack>
              </Box>
            )}
          </Stack>

          {/* Stage 1 Units */}
          <Typography sx={{ fontWeight: 700, mb: 1 }}>
            2. Stage 1 Unit Times ({s1UnitsCount} units)
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
            Stage 1 acceptance criteria: All {s1UnitsCount} units must disintegrate within the specification limit ({specLimitText}). If 1–{maxS1Failures} fail, testing proceeds to Stage 2; more than {maxS1Failures} failures result in immediate failure.
          </Typography>

          <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 100 }}>Unit</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 180 }}>Time (min)</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 180 }}>Status</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 11, width: 100 }}>Live Hint</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {stage1Units.map((u, i) => (
                <TableRow key={i}>
                  <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                    Unit {i + 1}
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      placeholder="Minutes"
                      value={u.notDisintegrated ? "" : u.minutes}
                      disabled={u.notDisintegrated}
                      onChange={(e) => {
                        const val = e.target.value;
                        setStage1Units((prev) =>
                          prev.map((row, idx) => (idx === i ? { ...row, minutes: val } : row))
                        );
                      }}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                      sx={{ width: 140 }}
                    />
                  </TableCell>
                  <TableCell>
                    <FormControlLabel
                      control={
                        <Checkbox
                          size="small"
                          checked={u.notDisintegrated}
                          onChange={(e) => {
                            const checked = e.target.checked;
                            setStage1Units((prev) =>
                              prev.map((row, idx) =>
                                idx === i ? { minutes: checked ? "" : row.minutes, notDisintegrated: checked } : row
                              )
                            );
                          }}
                        />
                      }
                      label={<Typography sx={{ fontSize: 12 }}>Not disintegrated</Typography>}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {renderLiveHint(u)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Typography variant="caption" sx={{ color: "text.secondary", mt: 1, display: "block" }}>
            * Live pass/fail hint is based on {specLimitText}; the server&apos;s staged evaluation verdict is authoritative.
          </Typography>

          <Box sx={{ mt: 3, display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="contained"
              disabled={!isStage1Valid}
              onClick={() => {
                setSigningStage(1);
                setSigning(true);
              }}
            >
              Sign & Record Stage 1 Result
            </Button>
          </Box>
        </Box>
      )}

      {/* Signature Dialog */}
      <SignatureDialog
        open={signing}
        meaningStatement={
          signingStage === 1
            ? "I entered these stage 1 disintegration unit times and am recording this test result."
            : "I entered these stage 2 disintegration unit times and am recording them."
        }
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => setSigning(false)}
        onConfirm={signingStage === 1 ? submitStage1 : submitStage2}
      />
    </Box>
  );
}
