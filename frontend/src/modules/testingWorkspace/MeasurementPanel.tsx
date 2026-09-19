import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
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
  Typography
} from "@mui/material";
import { SignatureDialog } from "../../components/SignatureDialog";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";

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

export function MeasurementPanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  current,
  onRecorded
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [testDef, setTestDef] = useState<TestDefinitionOption | null>(null);
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [values, setValues] = useState<Record<string, string>[]>([]);
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);

  // Outcome / completion view
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  useEffect(() => {
    let active = true;

    const loadData = async () => {
      setLoading(true);
      setError(null);

      try {
        // 1. Fetch test definitions to get replicateCount & evaluationBasis
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

        // 3. Fetch specifications for this item and test
        let loadedSpecs: SpecificationDto[] = [];
        if (resolvedItemId != null) {
          const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
          loadedSpecs = itemSpecs
            .filter((s) => s.testCode === effectiveTestCode)
            .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
        }

        if (loadedSpecs.length === 0) {
          if (active) {
            setError(`No specifications configured for test ${effectiveTestCode}.`);
            setLoading(false);
          }
          return;
        }

        // 4. Load FP section equipment
        let fpInstruments: ConfiguredEquipmentSummary[] = [];
        try {
          const [sections, allEquip] = await Promise.all([
            getSections(),
            EquipmentConfigurationService.getConfiguredSummary()
          ]);
          const fpSec = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === "FP");
          fpInstruments = (allEquip ?? []).filter(
            (e) => (fpSec && e.sectionId === fpSec.sectionId) || e.section?.code === "FP" || e.section?.sectionCode === "FP"
          );
        } catch {
          // equipment is optional on backend
        }

        const repCount = matchedDef?.replicateCount && matchedDef.replicateCount >= 1 && matchedDef.replicateCount <= 30
          ? matchedDef.replicateCount
          : 1;

        if (active) {
          setTestDef(matchedDef);
          setSpecs(loadedSpecs);
          setFpEquipment(fpInstruments);
          setValues(Array.from({ length: repCount }, () => ({})));
        }
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load measurement test configuration or specifications.";
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    };

    loadData();

    return () => {
      active = false;
    };
  }, [effectiveTestCode, itemId, current.sampleContext?.sampleName]);

  const replicateCount = useMemo(() => {
    return testDef?.replicateCount && testDef.replicateCount >= 1 && testDef.replicateCount <= 30
      ? testDef.replicateCount
      : 1;
  }, [testDef?.replicateCount]);

  const columns: UnitEntryGridColumn[] = useMemo(() => {
    return specs.map((s) => ({
      key: String(s.id),
      label: s.parameterName || s.testCode || "Parameter",
      unit: s.unit || undefined
    }));
  }, [specs]);

  const isFormValid = useMemo(() => {
    if (specs.length === 0 || values.length !== replicateCount) return false;
    if (!analysedAt.trim()) return false;

    // Check analysis time is not more than 5 minutes in the future
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }

    // Every spec must have a valid number in every row
    for (const s of specs) {
      const key = String(s.id);
      for (let r = 0; r < replicateCount; r++) {
        const raw = values[r]?.[key];
        if (raw == null || raw.trim() === "" || isNaN(Number(raw))) {
          return false;
        }
      }
    }

    return true;
  }, [specs, values, replicateCount, analysedAt]);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        parameters: specs.map((s) => ({
          specificationId: s.id!,
          readings: values.map((row) => Number(row[String(s.id)]))
        })),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordMeasurementResult(testOrderId, payload);
      setSigning(false);
      setOutcome({
        outcomeSummary: res?.outcomeSummary ?? res?.finalResult,
        status: res?.status
      });

      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to record measurement result.";
      setError(msg);
    }
  };

  // Completion view
  if (current.allStepsComplete || outcome) {
    const isOos = (outcome?.status ?? "").includes("OutOfSpecification");
    const isReview = (outcome?.status ?? "").includes("RequiresReview");
    const alertSeverity = isOos ? "error" : isReview ? "warning" : "success";

    return (
      <Box>
        <Alert severity={alertSeverity} sx={{ mb: 2 }}>
          {displayName}: <strong>{outcome?.outcomeSummary ?? current.finalResult ?? "Results Recorded"}</strong>
          {outcome?.status && ` (${outcome.status})`}
        </Alert>

        {testDef && (
          <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.paper" }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
              Measurement Analysis Summary
            </Typography>
            <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap" }}>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Evaluation Basis</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{testDef.evaluationBasis ?? "Mean"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicates Recorded</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{replicateCount}</Typography>
              </Box>
            </Stack>
          </Box>
        )}
      </Box>
    );
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress size={32} />
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

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2.5}>
        {/* Section 1: Run metadata */}
        <Box sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, color: "text.primary" }}>
              1. Analysis Configuration
            </Typography>
            <Stack direction="row" spacing={1}>
              <Chip size="small" variant="outlined" label={`Basis: ${testDef?.evaluationBasis ?? "Mean"}`} />
              <Chip size="small" variant="outlined" label={`Replicates: ${replicateCount}`} />
            </Stack>
          </Box>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="datetime-local"
              label="Analysis time *"
              value={analysedAt}
              onChange={(e) => setAnalysedAt(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              required
              fullWidth
            />

            <FormControl size="small" fullWidth>
              <InputLabel id="equipment-select-label">Equipment (Finished Product)</InputLabel>
              <Select
                labelId="equipment-select-label"
                label="Equipment (Finished Product)"
                value={selectedEquipmentId}
                onChange={(e) => setSelectedEquipmentId(String(e.target.value) === "" ? "" : Number(e.target.value))}
              >
                <MenuItem value="">
                  <em>None (no instrument linked)</em>
                </MenuItem>
                {fpEquipment.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    {eq.name} ({eq.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Box>
        </Box>

        {/* Section 2: Specifications summary */}
        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            2. Specification Limits ({specs.length} parameter{specs.length === 1 ? "" : "s"})
          </Typography>

          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: "action.hover" }}>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Parameter</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Specification Limit</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Unit</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {specs.map((s) => (
                <TableRow key={s.id}>
                  <TableCell sx={{ fontSize: 13, fontWeight: 500 }}>
                    {s.parameterName || s.testCode}
                  </TableCell>
                  <TableCell sx={{ fontSize: 13 }}>
                    {s.specLimit || s.expectedResultText || (s.lowerLimit != null || s.upperLimit != null ? `${s.lowerLimit ?? ""} – ${s.upperLimit ?? ""}` : "—")}
                  </TableCell>
                  <TableCell sx={{ fontSize: 13, color: "text.secondary" }}>
                    {s.unit || "—"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>

        {/* Section 3: Replicate readings grid */}
        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            3. Replicate Readings ({replicateCount} replicate{replicateCount === 1 ? "" : "s"})
          </Typography>

          <UnitEntryGrid
            rowCount={replicateCount}
            rowLabel={(i) => `Rep ${i + 1}`}
            columns={columns}
            values={values}
            onChange={setValues}
          />
        </Box>

        {/* Section 4: Comment */}
        <TextField
          size="small"
          label="Comment (optional)"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          rows={2}
          fullWidth
        />

        {/* Section 5: Action button */}
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            disabled={!isFormValid}
            onClick={() => setSigning(true)}
            sx={{ minWidth: 140, fontWeight: 700, textTransform: "none" }}
          >
            Sign and record result
          </Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these measurement readings and am recording this test result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
