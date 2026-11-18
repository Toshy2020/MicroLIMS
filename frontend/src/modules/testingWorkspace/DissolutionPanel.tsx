import React, { useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
  TableContainer
} from "@mui/material";
import { Trans } from "react-i18next";
import { SignatureDialog } from "../../components/SignatureDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { CriteriaCard, NumericCell, ResultSection, VerdictBanner } from "../../components/lab";
import type { CriteriaRow, Verdict } from "../../components/lab";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { HplcWorkspaceService } from "../hplcWorkspace/services/HplcWorkspaceService";
import type { HplcDissolutionStandardResult } from "../hplcWorkspace/types";
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
  const { t } = useTranslation("testingWorkspace");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Configuration
  const [testDef, setTestDef] = useState<TestDefinitionOption | null>(null);
  const [spec, setSpec] = useState<SpecificationDto | null>(null);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  // Standard from the HPLC workspace run this sample is assigned to.
  const [hplcStandard, setHplcStandard] = useState<HplcDissolutionStandardResult | null>(null);

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
        const itemSpecs = await SpecificationService.getForTestOrder(testOrderId);
        const disSpecs = itemSpecs.filter(
          (s) => s.testCode === effectiveTestCode && s.limitType === "DissolutionQ"
        );
        matchedSpec = disSpecs[0] ?? null;
      }

      // 4. Fetch the HPLC run standard
      try {
        setHplcStandard(await HplcWorkspaceService.getDissolutionStandard(testOrderId));
      } catch {
        setHplcStandard({ standard: null, problem: t("dissolution.hplcLoadFailed") });
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
      setError(errorMessage(err, t("dissolution.loadConfigFailed")));
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
  const existingVesselCount = existingReadings.length;
  const nextStage = existingVesselCount === 6 ? 2 : 3;

  const vesselColumns: UnitEntryGridColumn[] = useMemo(
    () => [{ key: "area", label: t("dissolution.peakAreaInputCol") }],
    [t]
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
    if (!hplcStandard?.standard) return false;
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
  }, [hplcStandard, spec, analysedAt, conditionLabels, conditions, mediumVolumeMl, dilutionFactor, vesselAreasS1]);

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
        outcomeText = t("dissolution.stageNeeded", { stage: 2 });
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = t("dissolution.compliesAtStage", { stage: 1 });
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = t("dissolution.doesNotComplyS3");
        severity = "error";
      } else {
        outcomeText = res?.outcomeSummary ?? res?.status ?? t("dissolution.recordedFallback");
      }

      setOutcome({ text: outcomeText, status: res?.status ?? undefined });
      setOutcomeBanner({ message: outcomeText, severity });

      await onRecorded();
      await loadData();
    } catch (err: unknown) {
      setSigning(false);
      setError(errorMessage(err, t("dissolution.recordFailed")));
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
        outcomeText = t("dissolution.stageNeeded", { stage: 3 });
        severity = "info";
      } else if (res?.status === "WithinLimits") {
        outcomeText = t("dissolution.compliesAtStage", { stage: currentStage });
        severity = "success";
      } else if (res?.status === "OutOfSpecification") {
        outcomeText = t("dissolution.doesNotComplyS3");
        severity = "error";
      } else {
        outcomeText = res?.outcomeSummary ?? res?.status ?? t("dissolution.recordedFallback");
      }

      setOutcome({ text: outcomeText, status: res?.status ?? undefined });
      setOutcomeBanner({ message: outcomeText, severity });

      await onRecorded();
      await loadData();
    } catch (err: unknown) {
      setSigning(false);
      setError(errorMessage(err, t("dissolution.recordStageFailed")));
    }
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, textAlign: "center" }}>
        <CircularProgress size={32} />
        <Typography sx={{ mt: 1, fontSize: 13, color: "text.secondary" }}>
          {t("dissolution.loading")}
        </Typography>
      </Box>
    );
  }

  const qVal = spec?.lowerLimit != null ? Number(spec.lowerLimit) : 0;
  const s1Offset = testDef?.dissolutionS1Offset ?? 5;
  const s2MinOffset = testDef?.dissolutionS2MinOffset ?? 15;
  const s3MinOffset = testDef?.dissolutionS3MinOffset ?? 25;
  const s3MaxBelow = testDef?.dissolutionS3MaxBelowS2Min ?? 2;

  // Acceptance criteria from the spec and stage offsets the panel already loads.
  const criteriaRows: CriteriaRow[] = [];
  if (spec) {
    criteriaRows.push({
      parameter: t("dissolution.qParameter"),
      criterion: t("dissolution.qCriterion", { qVal }),
      unit: "%",
      source: t("dissolution.sourceSpecification")
    });
    criteriaRows.push({
      parameter: t("dissolution.s1Parameter"),
      criterion: t("dissolution.s1Criterion", { offset: s1Offset, total: qVal + s1Offset }),
      unit: "%",
      source: t("dissolution.sourceStageCriteria")
    });
    criteriaRows.push({
      parameter: t("dissolution.s2Parameter"),
      criterion: t("dissolution.s2Criterion", { qVal, offset: s2MinOffset, minVal: qVal - s2MinOffset }),
      unit: "%",
      source: t("dissolution.sourceStageCriteria")
    });
    criteriaRows.push({
      parameter: t("dissolution.s3Parameter"),
      criterion: t("dissolution.s3Criterion", {
        qVal,
        maxBelow: s3MaxBelow,
        s2Offset: s2MinOffset,
        s2Min: qVal - s2MinOffset,
        s3Offset: s3MinOffset,
        s3Min: qVal - s3MinOffset
      }),
      unit: "%",
      source: t("dissolution.sourceStageCriteria")
    });
  }

  const readingStatusBadge = (passed: boolean | null | undefined) =>
    passed === null || passed === undefined ? "—" : <StatusBadge status={passed ? "Pass" : "BelowSpec"} />;

  const renderReadingsTable = (rList: ResultReadingDetail[], firstHeader: string, lastHeader: string) => (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{firstHeader}</TableCell>
            <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12 }}>{t("dissolution.peakAreaCol")}</TableCell>
            <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12 }}>{t("dissolution.dissolvedCol")}</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{lastHeader}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rList.map((r) => (
            <TableRow key={r.id}>
              <TableCell sx={{ fontSize: 12 }}>{t("dissolution.vesselLabel", { index: r.index })}</TableCell>
              <TableCell align="right" sx={{ fontSize: 12 }}><NumericCell value={r.value1 != null ? Number(r.value1) : null} /></TableCell>
              <TableCell align="right" sx={{ fontSize: 12, fontWeight: 600 }}>
                <NumericCell value={r.computedValue != null ? Number(r.computedValue) : null} unit="%" />
              </TableCell>
              <TableCell sx={{ fontSize: 12 }}>{readingStatusBadge(r.passed)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );

  // Finalized view
  const isFinalized =
    current.allStepsComplete ||
    (activeOrder?.analysis &&
      activeOrder.analysis.parameterResults?.[0]?.comparisonStatus !== "NextStageRequired" &&
      activeOrder.status === "Completed") ||
    (outcome && outcome.status !== "NextStageRequired");

  if (isFinalized && !isNextStageRequired) {
    const finalStatus = outcome?.status ?? activeParam?.comparisonStatus;
    const summaryText = outcome?.text ?? activeParam?.reportedDisplay ?? current.finalResult ?? t("dissolution.analysisCompleteFallback");
    // Verdict comes only from the server's status field.
    const verdict: Verdict =
      finalStatus === "WithinLimits" ? "Pass" : finalStatus === "OutOfSpecification" ? "Fail" : "Pending";

    return (
      <Stack spacing={2}>
        <VerdictBanner verdict={verdict} detail={`${displayName}: ${summaryText}${finalStatus ? ` (${finalStatus})` : ""}`} />

        {activeAnalysis && (
          <ResultSection title={t("dissolution.summaryTitle")}>
            <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap" }}>
              {spec?.lowerLimit && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("dissolution.specificationLabel")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}><Ltr>Q = {spec.lowerLimit} %</Ltr></Typography>
                </Box>
              )}
              {spec?.labelClaim && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("dissolution.labelClaimLabel")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}><Ltr>{spec.labelClaim} {spec.labelClaimUnit || "mg"}</Ltr></Typography>
                </Box>
              )}
              {activeParam?.reportedDisplay && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("dissolution.reportedResultLabel")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}><Ltr>{activeParam.reportedDisplay}</Ltr></Typography>
                </Box>
              )}
              {activeAnalysis.equipmentCode && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("dissolution.apparatusLabel")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}><Ltr>{activeAnalysis.equipmentCode}</Ltr></Typography>
                </Box>
              )}
            </Stack>
          </ResultSection>
        )}

        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        {activeAnalysis && readingsByStage.size > 0 && (
          <ResultSection title={t("dissolution.rawReadingsTitle")}>
            <Stack spacing={2}>
              {Array.from(readingsByStage.entries()).map(([stg, rList]) => (
                <Box key={stg}>
                  <Typography sx={{ fontWeight: 600, fontSize: 12, mb: 0.5 }}>
                    {t("dissolution.stageUnitsHeading", { stage: stg, count: rList.length })}
                  </Typography>
                  {renderReadingsTable(rList, t("dissolution.vesselHeader"), t("dissolution.unitMarkHeader"))}
                </Box>
              ))}
            </Stack>
          </ResultSection>
        )}

        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <StatusBadge status="ResultRecorded" label={t("dissolution.resultRecordedPending")} />
          {onClose && (
            <Button variant="contained" onClick={onClose}>
              {t("dissolution.doneClose")}
            </Button>
          )}
        </Box>
      </Stack>
    );
  }

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? t("dissolution.returnedWithReason", { reason: current.returnInfo.reason })
            : t("dissolution.returnedDefault")}
        </Alert>
      )}

      {outcomeBanner && (
        <Alert severity={outcomeBanner.severity} sx={{ mb: 2 }} onClose={() => setOutcomeBanner(null)}>
          <Trans
            i18nKey="dissolution.outcomeBanner"
            ns="testingWorkspace"
            values={{ message: outcomeBanner.message }}
            components={{ strong: <strong /> }}
          />
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {!spec && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {t("dissolution.noSpecWarning")}
        </Alert>
      )}

      {/* HPLC run standard block */}
      <Stack spacing={2}>
      <ResultSection step={1} title={t("dissolution.hplcRunStandardTitle")}>
        {hplcStandard?.standard ? (
          <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
            <Typography sx={{ fontWeight: 600, fontSize: 13 }}><Ltr>{hplcStandard.standard.runCode}</Ltr></Typography>
            <StatusBadge status="Passed" label={hplcStandard.standard.sstCode} />
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("dissolution.hplcSummaryDetails", {
                mean: hplcStandard.standard.meanResponse,
                weight: hplcStandard.standard.standardWeightMg,
                purity: hplcStandard.standard.purityPercent,
                moisture: hplcStandard.standard.moisturePercent,
                dilution: hplcStandard.standard.standardDilution,
                cs: hplcStandard.standard.cs.toFixed(6)
              })}
            </Typography>
            <Button size="small" component={RouterLink} to={`/hplc-workspace/${hplcStandard.standard.equipmentId}/run/${hplcStandard.standard.hplcRunId}/samples`}>
              {t("dissolution.openRunBtn")}
            </Button>
          </Stack>
        ) : (
          <Alert severity="info">
            {hplcStandard?.problem ?? t("dissolution.assignHplcDefault")} {t("dissolution.useHplcWorkspace")}
          </Alert>
        )}
      </ResultSection>

      {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

      {/* PENDING STAGE (Stage 2 or 3) */}
      {isNextStageRequired && activeParam ? (
        <Stack spacing={2}>
          <Alert severity="warning">
            <Trans
              i18nKey="dissolution.stageRequiredBanner"
              ns="testingWorkspace"
              values={{ stage: nextStage, prev: nextStage - 1 }}
              components={{ strong: <strong /> }}
            />
          </Alert>

          {/* Grouped read-only previous stages */}
          <ResultSection step={2} title={t("dissolution.prevStagesTitle")}>
            <Stack spacing={2}>
              {Array.from(readingsByStage.entries()).map(([stg, rList]) => (
                <Box key={stg}>
                  <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
                    {t("dissolution.stageVesselsHeading", { stage: stg, count: rList.length })}
                  </Typography>
                  {renderReadingsTable(rList, t("dissolution.unitHeader"), t("dissolution.stageCriterionHeader"))}
                </Box>
              ))}
            </Stack>
          </ResultSection>

          {/* Next Stage Entry */}
          <ResultSection
            step={3}
            title={nextStage === 2 ? t("dissolution.nextStageEntryTitle2") : t("dissolution.nextStageEntryTitle3")}
          >
            <UnitEntryGrid
              rowCount={nextStage === 2 ? 6 : 12}
              rowLabel={(i) => t("dissolution.vesselLabel", { index: i + (nextStage === 2 ? 7 : 13) })}
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
                {t("dissolution.signStageBtn", { stage: nextStage })}
              </Button>
            </Box>
          </ResultSection>
        </Stack>
      ) : (
        /* STAGE 1 INITIAL FORM */
        <>
          <ResultSection step={2} title={t("dissolution.paramsAndConditionsTitle")}>
          <Stack spacing={2}>
            {/* Equipment and DateTime */}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <FormControl size="small" sx={{ flex: 1.5 }}>
                <InputLabel id="dissolution-equipment-label">{t("dissolution.apparatusSelectLabel")}</InputLabel>
                <Select
                  labelId="dissolution-equipment-label"
                  label={t("dissolution.apparatusSelectLabel")}
                  value={selectedEquipmentId}
                  onChange={(e) => setSelectedEquipmentId(e.target.value as number | "")}
                >
                  <MenuItem value=""><em>{t("dissolution.selectApparatusOptional")}</em></MenuItem>
                  {fpEquipment.map((eq) => (
                    <MenuItem key={eq.id} value={eq.id}>
                      <Ltr>{eq.code}</Ltr>: {eq.name} ({eq.type})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <TextField
                size="small"
                type="datetime-local"
                label={t("dissolution.analysisTimeLocal")}
                value={analysedAt}
                onChange={(e) => setAnalysedAt(e.target.value)}
                slotProps={{ inputLabel: { shrink: true }, htmlInput: { dir: "ltr" } }}
                required
                sx={{ flex: 1 }}
              />
            </Stack>

            {/* Conditions Form */}
            {conditionLabels.length > 0 && (
              <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                  {t("dissolution.testConditionsHeading")}
                </Typography>
                <Stack useFlexGap direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
                  {conditionLabels.map((label) => (
                    <TextField
                      key={label}
                      size="small"
                      label={label}
                      placeholder={t("dissolution.enterConditionPlaceholder", { label })}
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
                label={t("dissolution.mediumVolumeLabel")}
                placeholder={t("dissolution.mediumVolumePlaceholder")}
                value={mediumVolumeMl}
                onChange={(e) => setMediumVolumeMl(e.target.value)}
                slotProps={{ htmlInput: { min: 1, step: "any", dir: "ltr" } }}
                required
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                label={t("dissolution.dilutionFactorLabel")}
                placeholder="1"
                value={dilutionFactor}
                onChange={(e) => setDilutionFactor(e.target.value)}
                slotProps={{ htmlInput: { min: 0.0001, step: "any", dir: "ltr" } }}
                helperText={t("dissolution.dilutionFactorHelper")}
                required
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>
          </ResultSection>

          {/* Stage 1 Vessels */}
          <ResultSection step={3} title={t("dissolution.stage1VesselsTitle")}>

          <UnitEntryGrid
            rowCount={6}
            rowLabel={(i) => t("dissolution.vesselLabel", { index: i + 1 })}
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
              {t("dissolution.signStageBtn", { stage: 1 })}
            </Button>
          </Box>
          </ResultSection>
        </>
      )}
      </Stack>

      {/* Signature Dialog */}
      <SignatureDialog
        open={signing}
        meaningStatement={signingStage === 1
          ? "I entered these stage 1 dissolution vessel results and am recording this test result."
          : `I entered these stage ${signingStage} dissolution vessel results and am recording them.`}
        meaningStatementTranslation={signingStage === 1
          ? t("dissolution.signatureMeaningStage1")
          : t("dissolution.signatureMeaningOtherStage", { stage: signingStage })}
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => setSigning(false)}
        onConfirm={signingStage === 1 ? submitStage1 : submitNextStage}
      />
    </Box>
  );
}
