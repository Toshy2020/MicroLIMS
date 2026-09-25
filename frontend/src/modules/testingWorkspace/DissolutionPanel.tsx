import React, { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
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
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { SystemSuitabilityService, SystemSuitabilityRun } from "../systemSuitability/services/SystemSuitabilityService";
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

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const errorMessage = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

export function DissolutionPanel({
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

  // System suitability runs
  const [linkedRun, setLinkedRun] = useState<SystemSuitabilityRun | null>(null);
  const [selectableRuns, setSelectableRuns] = useState<SystemSuitabilityRun[]>([]);
  const [runChoice, setRunChoice] = useState<number | "">("");
  const [changingRun, setChangingRun] = useState(false);

  // Stage 1 form
  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [conditions, setConditions] = useState<Record<string, string>>({});
  const [mediumVolumeMl, setMediumVolumeMl] = useState("900");
  const [dilutionFactor, setDilutionFactor] = useState("1");
  const [vesselAreasS1, setVesselAreasS1] = useState<Record<string, string>[]>(() =>
    Array.from({ length: 6 }, () => ({ area: "" }))
  );

  // Active analysis / pending stage
  const [activeAnalysis, setActiveAnalysis] = useState<AnalysisDetail | null>(null);
  const [activeOrder, setActiveOrder] = useState<TestOrderSummaryDetail | null>(null);
  const [nextStageValues, setNextStageValues] = useState<Record<string, string>[]>([]);

  // Signature & submission
  const [signing, setSigning] = useState(false);
  const [signingStage, setSigningStage] = useState<1 | 2 | 3>(1);
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

      // 3. Fetch specifications for this item and test (DissolutionQ limitType)
      let matchedSpec: SpecificationDto | null = null;
      if (resolvedItemId != null) {
        const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
        const disSpecs = itemSpecs.filter(
          (s) => s.testCode === effectiveTestCode && s.limitType === "DissolutionQ"
        );
        matchedSpec = disSpecs[0] ?? null;
      }

      // 4. Fetch suitability runs
      try {
        const [l, s] = await Promise.all([
          SystemSuitabilityService.getLinkedForTestOrder(testOrderId),
          SystemSuitabilityService.getSelectableForTestOrder(testOrderId)
        ]);
        setLinkedRun(l);
        setSelectableRuns(s);
        setRunChoice("");
        setChangingRun(false);
      } catch {
        // non-blocking
      }

      // 5. Load FP section equipment (dissolution testers)
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
        // Sort with DissolutionTester first
        fpInstruments.sort((a, b) => {
          const aDis = a.type === "DissolutionTester" ? 0 : 1;
          const bDis = b.type === "DissolutionTester" ? 0 : 1;
          return aDis - bDis;
        });
      } catch {
        // fallback
      }

      // 6. Initialize condition fields
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

      // 7. Check sample summary for existing testOrder analysis & pending stage
      if (sampleId != null) {
        try {
          const summary = await SampleSummaryService.getSummary(sampleId);
          const ord = summary.testOrders?.find((t) => t.testOrderId === testOrderId) ?? null;
          setActiveOrder(ord);

          if (ord?.analysis) {
            setActiveAnalysis(ord.analysis);
            const param = ord.analysis.parameterResults?.[0];
            if (param?.comparisonStatus === "NextStageRequired") {
              const readingsCount = param.readings?.length ?? 6;
              const nextStageLength = readingsCount === 6 ? 6 : 12;
              setNextStageValues(Array.from({ length: nextStageLength }, () => ({ area: "" })));
            }
          }
        } catch {
          // non-blocking
        }
      }

      setTestDef(matchedDef);
      setSpec(matchedSpec);
      setFpEquipment(fpInstruments);
    } catch (err: unknown) {
      setError(errorMessage(err, "Could not load dissolution test configuration or specifications."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testOrderId, effectiveTestCode, itemId, sampleId]);

  // Suitability linking
  const linkRun = async () => {
    if (!runChoice) return;
    setError(null);
    try {
      await SystemSuitabilityService.linkTestOrders(Number(runChoice), [testOrderId]);
      const [l, s] = await Promise.all([
        SystemSuitabilityService.getLinkedForTestOrder(testOrderId),
        SystemSuitabilityService.getSelectableForTestOrder(testOrderId)
      ]);
      setLinkedRun(l);
      setSelectableRuns(s);
      setRunChoice("");
      setChangingRun(false);
    } catch (e) {
      setError(errorMessage(e, "Could not link this test to the system suitability run."));
    }
  };

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
  const existingVesselCount = existingReadings.length;
  const nextStage = existingVesselCount === 6 ? 2 : 3;

  const vesselColumns: UnitEntryGridColumn[] = useMemo(
    () => [{ key: "area", label: "Peak area" }],
    []
  );

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

  // Validation: Stage 1
  const isStage1Valid = useMemo(() => {
    if (!linkedRun) return false;
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
    const vol = Number(mediumVolumeMl);
    if (isNaN(vol) || vol <= 0) return false;
    const df = Number(dilutionFactor);
    if (isNaN(df) || df <= 0) return false;

    if (vesselAreasS1.length !== 6) return false;
    for (const row of vesselAreasS1) {
      const a = Number(row.area);
      if (!row.area || row.area.trim() === "" || isNaN(a) || a <= 0) {
        return false;
      }
    }
    return true;
  }, [linkedRun, spec, analysedAt, conditionLabels, conditions, mediumVolumeMl, dilutionFactor, vesselAreasS1]);

  // Validation: Next Stage (Stage 2 or 3)
  const isNextStageValid = useMemo(() => {
    const expectedLength = nextStage === 2 ? 6 : 12;
    if (nextStageValues.length !== expectedLength) return false;
    for (const row of nextStageValues) {
      const a = Number(row.area);
      if (!row.area || row.area.trim() === "" || isNaN(a) || a <= 0) {
        return false;
      }
    }
    return true;
  }, [nextStage, nextStageValues]);

  // Submission: Stage 1
  const submitStage1 = async (password: string) => {
    setError(null);
    try {
      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        conditions,
        mediumVolumeMl: Number(mediumVolumeMl),
        dilutionFactor: dilutionFactor.trim() !== "" ? Number(dilutionFactor) : 1,
        vesselAreas: vesselAreasS1.map((r) => Number(r.area)),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordDissolutionResult(testOrderId, payload);
      setSigning(false);

      let outcomeText = "";
      let severity: "success" | "info" | "error" = "success";
      if (res?.status === "NextStageRequired") {
        outcomeText = "Stage 2 needed";
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = "Complies at S1";
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = "Does not comply at S3";
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
      setError(errorMessage(err, "Failed to record dissolution result."));
    }
  };

  // Submission: Next Stage (Stage 2 or 3)
  const submitNextStage = async (password: string) => {
    setError(null);
    try {
      const currentStage = nextStage;
      const payload = {
        vesselAreas: nextStageValues.map((r) => Number(r.area)),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordDissolutionStage(testOrderId, payload);
      setSigning(false);

      let outcomeText = "";
      let severity: "success" | "info" | "error" = "success";
      if (res?.status === "NextStageRequired") {
        outcomeText = "Stage 3 needed";
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = `Complies at S${currentStage}`;
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = "Does not comply at S3";
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
      setError(errorMessage(err, "Failed to record dissolution stage."));
    }
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, textAlign: "center" }}>
        <CircularProgress size={32} />
        <Typography sx={{ mt: 1, fontSize: 13, color: "text.secondary" }}>
          Loading dissolution configuration and readings…
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
              Dissolution Analysis Summary
            </Typography>
            <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", mb: 2 }}>
              {spec?.lowerLimit && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Specification</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>Q = {spec.lowerLimit} %</Typography>
                </Box>
              )}
              {spec?.labelClaim && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Label Claim</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{spec.labelClaim} {spec.labelClaimUnit || "mg"}</Typography>
                </Box>
              )}
              {activeParam?.reportedDisplay && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Reported Result</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{activeParam.reportedDisplay}</Typography>
                </Box>
              )}
              {activeAnalysis.equipmentCode && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Apparatus</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{activeAnalysis.equipmentCode}</Typography>
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
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Vessel</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Peak Area</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>% Dissolved</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Unit Mark</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rList.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell sx={{ fontSize: 12 }}>Vessel {r.index}</TableCell>
                        <TableCell sx={{ fontSize: 12 }}>{r.value1 != null ? String(r.value1) : "—"}</TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                          {r.computedValue != null ? `${r.computedValue} %` : "—"}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {r.passed === null ? "—" : r.passed ? (
                            <Chip size="small" color="success" label="Pass" sx={{ height: 20, fontSize: 10 }} />
                          ) : (
                            <Chip size="small" color="error" label="Below Spec" sx={{ height: 20, fontSize: 10 }} />
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

  const qVal = spec?.lowerLimit != null ? Number(spec.lowerLimit) : 0;
  const s1Offset = testDef?.dissolutionS1Offset ?? 5;
  const s2MinOffset = testDef?.dissolutionS2MinOffset ?? 15;
  const s3MinOffset = testDef?.dissolutionS3MinOffset ?? 25;
  const s3MaxBelow = testDef?.dissolutionS3MaxBelowS2Min ?? 2;

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
          No Dissolution Q specification configured for this item and test. Please configure a specification with limit type &ldquo;Dissolution Q&rdquo; in Item Specifications.
        </Alert>
      )}

      {/* Suitability Run Link Block */}
      <Typography sx={{ fontWeight: 700, mb: 1 }}>1. System suitability run</Typography>
      {linkedRun && !changingRun ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2.5, flexWrap: "wrap" }}>
          <Chip color="success" label={`${linkedRun.code} · Passed`} />
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {linkedRun.equipmentCode} / {linkedRun.columnCode} · standard {linkedRun.referenceStandardName} ({linkedRun.standardPurityPercent}%)
          </Typography>
          {!isNextStageRequired && selectableRuns.length > 1 && (
            <Button size="small" onClick={() => setChangingRun(true)}>
              Change
            </Button>
          )}
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2.5, flexWrap: "wrap" }}>
          <Select
            size="small"
            displayEmpty
            value={runChoice}
            onChange={(e) => setRunChoice(e.target.value as number)}
            sx={{ minWidth: 320 }}
          >
            <MenuItem value="" disabled>
              {selectableRuns.length ? "Choose a passed run" : "No passed run for this method yet"}
            </MenuItem>
            {selectableRuns.map((r) => (
              <MenuItem key={r.id} value={r.id}>
                {r.code} — {r.equipmentCode}, {new Date(r.performedAt).toLocaleDateString()}
              </MenuItem>
            ))}
          </Select>
          <Button variant="contained" disabled={!runChoice} onClick={linkRun}>
            Link
          </Button>
          {changingRun && <Button onClick={() => setChangingRun(false)}>Cancel</Button>}
        </Stack>
      )}
      {!linkedRun && selectableRuns.length === 0 && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Perform a passing System Suitability run for this method first (Laboratory → System Suitability).
        </Alert>
      )}

      {/* PENDING STAGE (Stage 2 or 3) */}
      {isNextStageRequired && activeParam ? (
        <Box sx={{ mt: 2 }}>
          <Alert severity="warning" sx={{ mb: 2 }}>
            <strong>Stage {nextStage} required:</strong> Results from Stage {nextStage - 1} did not meet acceptance criteria. Staged testing continues to Stage {nextStage}.
          </Alert>

          {/* Grouped read-only previous stages */}
          <Typography sx={{ fontWeight: 700, fontSize: 14, mb: 1 }}>
            2. Previously Recorded Stages
          </Typography>

          <Stack spacing={2} sx={{ mb: 3 }}>
            {Array.from(readingsByStage.entries()).map(([stg, rList]) => (
              <Paper key={stg} variant="outlined" sx={{ p: 2, bgcolor: "background.paper" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
                  Stage {stg} Units ({rList.length} vessels)
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Unit</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Peak Area</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>% Dissolved</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Stage Criterion</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rList.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell sx={{ fontSize: 12 }}>Vessel {r.index}</TableCell>
                        <TableCell sx={{ fontSize: 12 }}>{r.value1 != null ? String(r.value1) : "—"}</TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                          {r.computedValue != null ? `${r.computedValue} %` : "—"}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {r.passed === null ? "—" : r.passed ? (
                            <Chip size="small" color="success" label="Pass" sx={{ height: 20, fontSize: 10 }} />
                          ) : (
                            <Chip size="small" color="error" label="Below Spec" sx={{ height: 20, fontSize: 10 }} />
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            ))}
          </Stack>

          {/* Next Stage Entry */}
          <Paper variant="outlined" sx={{ p: 2, bgcolor: "action.hover" }}>
            <Typography sx={{ fontWeight: 700, fontSize: 14, mb: 1 }}>
              3. Stage {nextStage} Vessel Area Entry ({nextStage === 2 ? "6 additional units, Vessels 7–12" : "12 additional units, Vessels 13–24"})
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
              {nextStage === 2
                ? `Stage 2 acceptance: Average of 12 units (S1+S2) ≥ Q (${qVal}%), and no unit < Q − ${s2MinOffset}% (${qVal - s2MinOffset}%).`
                : `Stage 3 acceptance: Average of 24 units (S1+S2+S3) ≥ Q (${qVal}%), not more than ${s3MaxBelow} units < Q − ${s2MinOffset}% (${qVal - s2MinOffset}%), and no unit < Q − ${s3MinOffset}% (${qVal - s3MinOffset}%).`}
            </Typography>

            <UnitEntryGrid
              rowCount={nextStage === 2 ? 6 : 12}
              rowLabel={(i) => `Vessel ${i + (nextStage === 2 ? 7 : 13)}`}
              columns={vesselColumns}
              values={nextStageValues}
              onChange={setNextStageValues}
            />

            <Box sx={{ mt: 3, display: "flex", justifyContent: "flex-end" }}>
              <Button
                variant="contained"
                disabled={!isNextStageValid}
                onClick={() => {
                  setSigningStage(nextStage as 2 | 3);
                  setSigning(true);
                }}
              >
                Sign & Record Stage {nextStage} Result
              </Button>
            </Box>
          </Paper>
        </Box>
      ) : (
        /* STAGE 1 INITIAL FORM */
        <Box sx={{ mt: 2 }}>
          <Typography sx={{ fontWeight: 700, mb: 1.5 }}>
            2. Analysis parameters & conditions
          </Typography>

          <Stack spacing={2} sx={{ mb: 2.5 }}>
            {/* Equipment and DateTime */}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <FormControl size="small" sx={{ flex: 1.5 }}>
                <InputLabel id="dissolution-equipment-label">Dissolution Apparatus</InputLabel>
                <Select
                  labelId="dissolution-equipment-label"
                  label="Dissolution Apparatus"
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

            {/* Medium Volume & Dilution Factor */}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                label="Medium Volume (mL) *"
                placeholder="e.g. 900"
                value={mediumVolumeMl}
                onChange={(e) => setMediumVolumeMl(e.target.value)}
                slotProps={{ htmlInput: { min: 1, step: "any" } }}
                required
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                label="Sample Dilution Factor *"
                placeholder="1"
                value={dilutionFactor}
                onChange={(e) => setDilutionFactor(e.target.value)}
                slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                helperText="Default: 1 (undiluted)"
                required
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>

          {/* Stage 1 Vessels */}
          <Typography sx={{ fontWeight: 700, mb: 1 }}>
            3. Stage 1 Vessel Peak Areas (6 units)
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
            Stage 1 acceptance criteria: Each of the 6 units must be ≥ Q + {s1Offset}% (≥ {qVal + s1Offset}%). If any unit is below, testing automatically proceeds to Stage 2.
          </Typography>

          <UnitEntryGrid
            rowCount={6}
            rowLabel={(i) => `Vessel ${i + 1}`}
            columns={vesselColumns}
            values={vesselAreasS1}
            onChange={setVesselAreasS1}
          />

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
        meaningStatement={signingStage === 1
          ? "I entered these stage 1 dissolution vessel results and am recording this test result."
          : `I entered these stage ${signingStage} dissolution vessel results and am recording them.`}
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => setSigning(false)}
        onConfirm={signingStage === 1 ? submitStage1 : submitNextStage}
      />
    </Box>
  );
}
