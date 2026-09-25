import { useEffect, useMemo, useState } from "react";
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
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography
} from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import { SignatureDialog } from "../../components/SignatureDialog";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
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

interface QualitativeRowState {
  specificationId: number;
  parameterName: string;
  specLimitText: string;
  conforms: boolean;
  observation: string;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

export function QualitativePanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  current,
  onRecorded
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [rows, setRows] = useState<QualitativeRowState[]>([]);
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
        // 1. Resolve itemId if not provided
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

        // 2. Fetch specifications for this item and test
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

        // 3. Load FP section equipment
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

        const initialRows: QualitativeRowState[] = loadedSpecs.map((s) => {
          const limitText =
            s.specLimit ||
            s.expectedResultText ||
            s.expectedState ||
            (s.lowerLimit != null || s.upperLimit != null ? `${s.lowerLimit ?? ""} – ${s.upperLimit ?? ""}` : "Conforms");

          return {
            specificationId: s.id!,
            parameterName: s.parameterName || s.testCode,
            specLimitText: limitText,
            conforms: true,
            observation: ""
          };
        });

        if (active) {
          setSpecs(loadedSpecs);
          setFpEquipment(fpInstruments);
          setRows(initialRows);
        }
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load qualitative test configuration or specifications.";
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

  const handleRowChange = (index: number, patch: Partial<QualitativeRowState>) => {
    setRows((prev) =>
      prev.map((r, i) => (i === index ? { ...r, ...patch } : r))
    );
  };

  const isFormValid = useMemo(() => {
    if (rows.length === 0) return false;
    if (!analysedAt.trim()) return false;

    // Check analysis time is not more than 5 minutes in the future
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }

    // For every row: if !conforms, observation is required and <= 500 chars
    for (const r of rows) {
      if (!r.conforms) {
        if (!r.observation.trim()) return false;
      }
      if (r.observation.length > 500) return false;
    }

    return true;
  }, [rows, analysedAt]);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        parameters: rows.map((r) => ({
          specificationId: r.specificationId,
          conforms: r.conforms,
          observation: r.observation.trim() || null
        })),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordQualitativeResult(testOrderId, payload);
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
        "Failed to record qualitative result.";
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

        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.paper" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
            Qualitative Analysis Summary
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {specs.length} parameter{specs.length === 1 ? "" : "s"} evaluated and recorded.
          </Typography>
        </Box>
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
        {/* Section 1: Analysis Configuration */}
        <Box sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            1. Analysis Configuration
          </Typography>

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
              <InputLabel id="equipment-select-label">Equipment (Physicochemical)</InputLabel>
              <Select
                labelId="equipment-select-label"
                label="Equipment (Physicochemical)"
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

        {/* Section 2: Qualitative Parameters Rows */}
        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            2. Qualitative Observations ({rows.length} parameter{rows.length === 1 ? "" : "s"})
          </Typography>

          <Stack spacing={2}>
            {rows.map((row, idx) => {
              const observationRequired = !row.conforms;
              const hasObservationError = observationRequired && !row.observation.trim();

              return (
                <Box
                  key={row.specificationId}
                  sx={{
                    p: 2,
                    border: "1px solid",
                    borderColor: row.conforms ? "divider" : "error.light",
                    borderRadius: 1.5,
                    bgcolor: row.conforms ? "background.paper" : "error.lighter",
                    transition: "border-color 0.2s, background-color 0.2s"
                  }}
                >
                  <Box
                    sx={{
                      display: "flex",
                      flexWrap: "wrap",
                      justifyContent: "space-between",
                      alignItems: "center",
                      gap: 1.5,
                      mb: 1.5
                    }}
                  >
                    <Box>
                      <Typography sx={{ fontWeight: 700, fontSize: 14 }}>
                        {row.parameterName}
                      </Typography>
                      <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 12 }}>
                        Criteria: <strong>{row.specLimitText}</strong>
                      </Typography>
                    </Box>

                    {/* Complies / Does not comply toggle */}
                    <ToggleButtonGroup
                      value={row.conforms ? "complies" : "non-comply"}
                      exclusive
                      size="small"
                      onChange={(_, val) => {
                        if (val !== null) {
                          handleRowChange(idx, { conforms: val === "complies" });
                        }
                      }}
                    >
                      <ToggleButton
                        value="complies"
                        color="success"
                        sx={{
                          fontWeight: 600,
                          fontSize: 12,
                          px: 2,
                          "&.Mui-selected": {
                            bgcolor: "success.main",
                            color: "success.contrastText",
                            "&:hover": {
                              bgcolor: "success.dark"
                            }
                          }
                        }}
                      >
                        <CheckCircleOutlineIcon sx={{ fontSize: 16, mr: 0.5 }} />
                        Complies
                      </ToggleButton>
                      <ToggleButton
                        value="non-comply"
                        color="error"
                        sx={{
                          fontWeight: 600,
                          fontSize: 12,
                          px: 2,
                          "&.Mui-selected": {
                            bgcolor: "error.main",
                            color: "error.contrastText",
                            "&:hover": {
                              bgcolor: "error.dark"
                            }
                          }
                        }}
                      >
                        <HighlightOffIcon sx={{ fontSize: 16, mr: 0.5 }} />
                        Does not comply
                      </ToggleButton>
                    </ToggleButtonGroup>
                  </Box>

                  {/* Observation field */}
                  <TextField
                    size="small"
                    fullWidth
                    label={observationRequired ? "Observation (required) *" : "Observation (optional)"}
                    placeholder={observationRequired ? "State non-conformance details (required, max 500 chars)" : "Notes or visual description"}
                    value={row.observation}
                    onChange={(e) => handleRowChange(idx, { observation: e.target.value })}
                    required={observationRequired}
                    error={hasObservationError}
                    helperText={
                      hasObservationError
                        ? "Observation is required when non-compliant"
                        : row.observation.length > 450
                        ? `${row.observation.length}/500 chars`
                        : undefined
                    }
                    slotProps={{ htmlInput: { maxLength: 500 } }}
                  />
                </Box>
              );
            })}
          </Stack>
        </Box>

        {/* Section 3: Comment */}
        <TextField
          size="small"
          label="Comment (optional)"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          rows={2}
          fullWidth
        />

        {/* Section 4: Action button */}
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
        meaningStatement="I entered these qualitative observations and am recording this test result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
