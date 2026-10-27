import { useEffect, useMemo, useRef, useState } from "react";
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
import {
  buildContainerRows,
  buildQualitativePayload,
  getConclusionLine,
  isValidContainerCount
} from "./qualitativePerContainerHelper";

interface Props {
  testOrderId: number;
  displayName: string;
  testCode?: string;
  itemId?: number | null;
  sampleId?: number | null;
  current: {
    workflowType?: string;
    perContainer?: boolean;
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

interface QualitativeContainerRowState {
  index: number;
  conforms: boolean;
  observation: string;
}

interface QualitativeRowState {
  specificationId: number;
  parameterName: string;
  specLimitText: string;
  conforms: boolean;
  observation: string;
  containers?: QualitativeContainerRowState[];
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

  const isPerContainer = Boolean(current.perContainer);
  const [containerCountInput, setContainerCountInput] = useState<string>("");
  const [containerCountTouched, setContainerCountTouched] = useState(false);
  const containerMemoryRef = useRef<Map<number, Map<number, QualitativeContainerRowState>>>(new Map());

  // Outcome / completion view
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  useEffect(() => {
    let active = true;

    const loadData = async () => {
      containerMemoryRef.current.clear();
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
          const itemSpecs = await SpecificationService.getForTestOrder(testOrderId);
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
            observation: "",
            containers: []
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
  }, [effectiveTestCode, itemId, current.sampleContext?.sampleName, testOrderId]);

  const handleRowChange = (index: number, patch: Partial<QualitativeRowState>) => {
    setRows((prev) =>
      prev.map((r, i) => (i === index ? { ...r, ...patch } : r))
    );
  };

  const applyContainerCount = (count: number) => {
    setRows((prev) =>
      prev.map((r) => {
        let specMap = containerMemoryRef.current.get(r.specificationId);
        if (!specMap) {
          specMap = new Map();
          containerMemoryRef.current.set(r.specificationId, specMap);
        }
        for (const c of r.containers || []) {
          specMap.set(c.index, { ...c });
        }
        const updatedContainers = buildContainerRows(r.containers || [], count, specMap);
        for (const c of updatedContainers) {
          specMap.set(c.index, { ...c });
        }
        return {
          ...r,
          containers: updatedContainers,
          conforms: updatedContainers.every((c) => c.conforms)
        };
      })
    );
  };

  const handleContainerCountChange = (val: string) => {
    setContainerCountInput(val);
  };

  const handleContainerCountCommit = () => {
    setContainerCountTouched(true);
    const count = Number(containerCountInput);
    if (isValidContainerCount(count)) {
      applyContainerCount(count);
    }
  };

  const handleContainerChange = (
    paramIndex: number,
    containerIndex: number,
    patch: Partial<QualitativeContainerRowState>
  ) => {
    setRows((prev) =>
      prev.map((r, pIdx) => {
        if (pIdx !== paramIndex) return r;
        const nextContainers = (r.containers || []).map((c) => {
          if (c.index !== containerIndex) return c;
          const updated = { ...c, ...patch };
          if (patch.conforms === true) {
            updated.observation = "";
          }
          return updated;
        });

        let specMap = containerMemoryRef.current.get(r.specificationId);
        if (!specMap) {
          specMap = new Map();
          containerMemoryRef.current.set(r.specificationId, specMap);
        }
        for (const c of nextContainers) {
          specMap.set(c.index, { ...c });
        }

        return {
          ...r,
          containers: nextContainers,
          conforms: nextContainers.every((c) => c.conforms)
        };
      })
    );
  };

  const isCountValid = useMemo(() => {
    return isValidContainerCount(Number(containerCountInput));
  }, [containerCountInput]);

  const isFormValid = useMemo(() => {
    if (rows.length === 0) return false;
    if (!analysedAt.trim()) return false;

    // Check analysis time is not more than 5 minutes in the future
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }

    if (isPerContainer) {
      const count = Number(containerCountInput);
      if (!isValidContainerCount(count)) return false;

      for (const r of rows) {
        if (!r.containers || r.containers.length !== count) return false;
        for (const c of r.containers) {
          if (!c.conforms) {
            if (!c.observation.trim()) return false;
          }
          if (c.observation.length > 500) return false;
        }
      }
      return true;
    }

    // For every row: if !conforms, observation is required and <= 500 chars
    for (const r of rows) {
      if (!r.conforms) {
        if (!r.observation.trim()) return false;
      }
      if (r.observation.length > 500) return false;
    }

    return true;
  }, [rows, analysedAt, isPerContainer, containerCountInput]);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload = buildQualitativePayload({
        analysedAt,
        equipmentId: selectedEquipmentId,
        password,
        comment,
        isPerContainer,
        containerCount: isPerContainer ? Number(containerCountInput) : undefined,
        parameters: rows
      });

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

          <Stack spacing={2}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <TextField
                size="small"
                type="datetime-local"
                label="Analysis time"
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

            {isPerContainer && (
              <TextField
                size="small"
                type="number"
                label="Containers received *"
                placeholder="1–200"
                value={containerCountInput}
                onChange={(e) => handleContainerCountChange(e.target.value)}
                onBlur={handleContainerCountCommit}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    handleContainerCountCommit();
                  }
                }}
                slotProps={{ htmlInput: { min: 1, max: 200, step: 1 } }}
                helperText="Enter the number of containers received (1 to 200)."
                error={Boolean((containerCountTouched || containerCountInput !== "") && !isCountValid)}
                required
                sx={{ maxWidth: { xs: "100%", sm: 300 } }}
              />
            )}
          </Stack>
        </Box>

        {/* Section 2: Qualitative Parameters Rows */}
        <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5, color: "text.primary" }}>
            2. Qualitative Observations ({rows.length} parameter{rows.length === 1 ? "" : "s"})
          </Typography>

          {isPerContainer && !isCountValid && (
            <Alert severity="info">
              Enter the number of containers received (1 to 200) to record results.
            </Alert>
          )}

          <Stack spacing={2}>
            {rows.map((row, idx) => {
              if (isPerContainer) {
                if (!isCountValid) return null;

                const parsedCount = Number(containerCountInput);
                const conclusion = getConclusionLine(parsedCount, row.containers);
                const allComply = !row.containers?.some((c) => !c.conforms);

                return (
                  <Box
                    key={row.specificationId}
                    sx={{
                      p: 2,
                      border: "1px solid",
                      borderColor: allComply ? "divider" : "error.light",
                      borderRadius: 1.5,
                      bgcolor: allComply ? "background.paper" : "error.lighter",
                      transition: "border-color 0.2s, background-color 0.2s"
                    }}
                  >
                    <Box sx={{ mb: 2 }}>
                      <Typography sx={{ fontWeight: 700, fontSize: 14 }}>
                        {row.parameterName}
                      </Typography>
                      <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 12, mb: 1.5 }}>
                        Criteria: <strong>{row.specLimitText}</strong>
                      </Typography>

                      {/* Read-only conclusion line */}
                      <Box
                        role="status"
                        aria-live="polite"
                        sx={{
                          p: 1.5,
                          borderRadius: 1,
                          bgcolor: allComply ? "action.hover" : "background.paper",
                          border: "1px solid",
                          borderColor: allComply ? "divider" : "error.main",
                          display: "flex",
                          alignItems: "center",
                          gap: 1
                        }}
                      >
                        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.secondary" }}>
                          Conclusion:
                        </Typography>
                        <Typography
                          sx={{
                            fontSize: 13,
                            fontWeight: 700,
                            color: allComply ? "success.dark" : "error.dark"
                          }}
                        >
                          {conclusion}
                        </Typography>
                      </Box>
                    </Box>

                    {/* Container rows 1..n */}
                    <Stack spacing={1.5}>
                      {(row.containers || []).map((container) => {
                        const obsRequired = !container.conforms;
                        const obsMissing = obsRequired && !container.observation.trim();

                        return (
                          <Box
                            key={container.index}
                            sx={{
                              p: 1.5,
                              border: "1px solid",
                              borderColor: container.conforms ? "divider" : "error.light",
                              borderRadius: 1,
                              bgcolor: container.conforms ? "background.paper" : "rgba(211, 47, 47, 0.04)"
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
                              <Typography sx={{ fontWeight: 600, fontSize: 13 }}>
                                Container {container.index} of {parsedCount}
                              </Typography>

                              <ToggleButtonGroup
                                aria-label={`Container ${container.index} result`}
                                value={container.conforms ? "complies" : "non-comply"}
                                exclusive
                                size="small"
                                onChange={(_, val) => {
                                  if (val !== null) {
                                    handleContainerChange(idx, container.index, { conforms: val === "complies" });
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

                            <TextField
                              size="small"
                              fullWidth
                              label={obsRequired ? "Observation (required) *" : "Observation (optional)"}
                              placeholder={obsRequired ? `Describe what container ${container.index} shows.` : "Notes or visual description"}
                              value={container.observation}
                              onChange={(e) => handleContainerChange(idx, container.index, { observation: e.target.value })}
                              required={obsRequired}
                              error={obsMissing}
                              helperText={
                                obsMissing
                                  ? `Describe what container ${container.index} shows.`
                                  : container.observation.length > 450
                                  ? `${container.observation.length}/500 chars`
                                  : undefined
                              }
                              slotProps={{ htmlInput: { maxLength: 500 } }}
                            />
                          </Box>
                        );
                      })}
                    </Stack>
                  </Box>
                );
              }

              // Non-per-container: original rendering
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
                      aria-label={`Conformance for ${row.parameterName}`}
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
