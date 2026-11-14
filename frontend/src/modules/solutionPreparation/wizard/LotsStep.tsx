import { useState, useEffect, useMemo, useCallback, useRef } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  Alert,
  CircularProgress,
  Stack,
  Divider,
  Stepper,
  Step,
  StepButton
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { ComponentLotPicker } from "../components/ComponentLotPicker";
import { SourcePreparationPicker } from "../components/SourcePreparationPicker";
import { AutoclaveSection, type AutoclaveFormState } from "../components/AutoclaveSection";
import { useLastChoiceDefault } from "../../../components/entry/LastChoiceField";
import { entryDefaultsService } from "../../../services/entryDefaultsService";
import { NumericEntryField } from "../../../components/bench/NumericEntryField";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { classifySaveFailure } from "../../testingWorkspace/benchSave";
import { SolutionPreparationService } from "../services/SolutionPreparationService";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot,
  LotOption,
  SourcePreparationOption,
  PreparationComponentInput,
  AutoclaveRecordInput
} from "../types";

export interface LotsStepDraftState {
  components: Record<number, { materialId: number | null; sourcePreparationId: number | null; quantityUsed: number | null }>;
  finalVolumeText: string;
  measuredPhText: string;
  autoclave: AutoclaveFormState;
}

export type SolutionFieldKey =
  | "finalVolumeMl"
  | "measuredPh"
  | "autoclaveEquipmentId"
  | "autoclaveProgram"
  | "autoclaveCycleNumber";

interface Props {
  preparation: SolutionPreparationResponse;
  onSave: (
    components: PreparationComponentInput[],
    finalVolumeMl: number | null,
    measuredPh: number | null,
    autoclave: AutoclaveRecordInput | null
  ) => Promise<void>;
  onNext: () => void;
  saving?: boolean;
  restoredDraft?: Partial<LotsStepDraftState> | null;
  onDraftChange?: (state: LotsStepDraftState) => void;
  fieldErrors?: Partial<Record<SolutionFieldKey, string>>;
  onClearFieldError?: (key: SolutionFieldKey) => void;
}

export function LotsStep({
  preparation,
  onSave,
  onNext,
  saving = false,
  restoredDraft,
  onDraftChange,
  fieldErrors,
  onClearFieldError
}: Props) {
  const { benchMode } = useBenchMode();
  const [activeSection, setActiveSection] = useState(0);
  const sectionRefs = useRef<(HTMLDivElement | null)[]>([]);
  const recipe = useMemo<RecipeSnapshot | null>(() => {
    try {
      return JSON.parse(preparation.recipeSnapshotJson) as RecipeSnapshot;
    } catch {
      return null;
    }
  }, [preparation.recipeSnapshotJson]);

  // Form State
  const [componentInputs, setComponentInputs] = useState<
    Record<number, { materialId: number | null; sourcePreparationId: number | null; quantityUsed: number | null }>
  >(() => {
    if (restoredDraft?.components) return restoredDraft.components;
    const map: Record<number, { materialId: number | null; sourcePreparationId: number | null; quantityUsed: number | null }> = {};
    for (const c of preparation.components) {
      map[c.id] = {
        materialId: c.materialId,
        sourcePreparationId: c.sourcePreparationId ?? null,
        quantityUsed: c.quantityUsed
      };
    }
    return map;
  });

  const needsAutoclave = recipe?.sterilizationRequired === true;
  const autoclaveLastChoice = useLastChoiceDefault<number>("equipment");
  const [autoclave, setAutoclave] = useState<AutoclaveFormState>(() =>
    restoredDraft?.autoclave ?? {
      equipmentId: preparation.autoclaveEquipmentId ?? "",
      program: preparation.autoclaveProgram ?? "",
      loadType: preparation.loadType ?? "",
      temperature: preparation.autoclaveTemperature ?? "",
      cycleTime: preparation.autoclaveCycleTime ?? "",
      cycleNumber: preparation.autoclaveCycleNumber ?? ""
    }
  );

  const { applyDefault: applyAutoclaveDefault } = autoclaveLastChoice;
  const autoclaveRef = useRef(autoclave);
  autoclaveRef.current = autoclave;
  const hasSavedAutoclave = preparation.autoclaveEquipmentId != null;
  const solutionMasterId = preparation.solutionMasterId;

  useEffect(() => {
    if (!needsAutoclave || hasSavedAutoclave) return;
    const controller = new AbortController();
    entryDefaultsService
      .get({ context: "solutionPreparation", solutionMasterId }, controller.signal)
      .then((res) => {
        if (controller.signal.aborted) return;
        const def = res.defaults.find((d) => d.field === "autoclaveEquipmentId");
        // Never overwrite something the analyst already chose or a restored draft.
        if (def?.id == null || autoclaveRef.current.equipmentId !== "") return;
        setAutoclave({ ...autoclaveRef.current, equipmentId: def.id });
        applyAutoclaveDefault(def.id);
      })
      .catch(() => {
        // A missing default is not an error: the field simply stays empty.
      });
    return () => controller.abort();
  }, [needsAutoclave, hasSavedAutoclave, solutionMasterId, applyAutoclaveDefault]);

  const [finalVolumeText, setFinalVolumeText] = useState<string>(() => {
    if (restoredDraft?.finalVolumeText !== undefined) return restoredDraft.finalVolumeText;
    const v = preparation.finalVolumeMl ?? recipe?.finalVolumeMl;
    return v != null ? String(v) : "";
  });
  const [measuredPhText, setMeasuredPhText] = useState<string>(() => {
    if (restoredDraft?.measuredPhText !== undefined) return restoredDraft.measuredPhText;
    return preparation.measuredPh != null ? String(preparation.measuredPh) : "";
  });

  useEffect(() => {
    if (!restoredDraft) return;
    if (restoredDraft.components) setComponentInputs(restoredDraft.components);
    if (restoredDraft.autoclave) setAutoclave(restoredDraft.autoclave);
    if (restoredDraft.finalVolumeText !== undefined) setFinalVolumeText(restoredDraft.finalVolumeText);
    if (restoredDraft.measuredPhText !== undefined) setMeasuredPhText(restoredDraft.measuredPhText);
  }, [restoredDraft]);

  useEffect(() => {
    onDraftChange?.({
      components: componentInputs,
      finalVolumeText,
      measuredPhText,
      autoclave
    });
  }, [componentInputs, finalVolumeText, measuredPhText, autoclave, onDraftChange]);

  // Lots per component
  const [lotsMap, setLotsMap] = useState<Record<number, LotOption[]>>({});
  const [sourcesMap, setSourcesMap] = useState<Record<number, SourcePreparationOption[]>>({});
  const [loadingLots, setLoadingLots] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load lot options for all components
  const loadLots = useCallback(async () => {
    setLoadingLots(true);
    setError(null);
    try {
      const results = await Promise.all(
        preparation.components
          .filter((c) => (c.kind ?? "Material") !== "Text")
          .map(async (c) => {
            if (c.kind === "PreparedSolution") {
              const sources = await SolutionPreparationService.getSourcePreparations(preparation.id, c.id);
              return { componentId: c.id, lots: null, sources };
            }
            const lots = await SolutionPreparationService.getLotOptions(preparation.id, c.id);
            return { componentId: c.id, lots, sources: null };
          })
      );
      const newMap: Record<number, LotOption[]> = {};
      const newSources: Record<number, SourcePreparationOption[]> = {};
      for (const res of results) {
        if (res.lots) newMap[res.componentId] = res.lots;
        if (res.sources) newSources[res.componentId] = res.sources;
      }
      setLotsMap(newMap);
      setSourcesMap(newSources);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to load lot options.");
    } finally {
      setLoadingLots(false);
    }
  }, [preparation.id, preparation.components]);

  useEffect(() => {
    loadLots();
  }, [loadLots]);

  const handleSelectLot = (componentId: number, materialId: number | null) => {
    setComponentInputs((prev) => ({
      ...prev,
      [componentId]: {
        materialId,
        sourcePreparationId: null,
        // Reset quantity if lot changed
        quantityUsed: materialId ? prev[componentId]?.quantityUsed ?? null : null
      }
    }));
  };

  const handleSelectSource = (componentId: number, sourcePreparationId: number | null) => {
    setComponentInputs((prev) => ({
      ...prev,
      [componentId]: {
        materialId: null,
        sourcePreparationId,
        quantityUsed: sourcePreparationId ? prev[componentId]?.quantityUsed ?? null : null
      }
    }));
  };

  const handleChangeQuantity = (componentId: number, quantityUsed: number | null) => {
    setComponentInputs((prev) => ({
      ...prev,
      [componentId]: {
        ...prev[componentId],
        quantityUsed
      }
    }));
  };

  // Text lines send nothing; prepared-solution lines send the picked bottle.
  const getInputsList = (): PreparationComponentInput[] => {
    return preparation.components.map((c) =>
      c.kind === "Text"
        ? { componentId: c.id }
        : c.kind === "PreparedSolution"
          ? {
              componentId: c.id,
              sourcePreparationId: componentInputs[c.id]?.sourcePreparationId ?? null,
              quantityUsed: componentInputs[c.id]?.quantityUsed ?? null
            }
          : {
              componentId: c.id,
              materialId: componentInputs[c.id]?.materialId ?? null,
              quantityUsed: componentInputs[c.id]?.quantityUsed ?? null
            }
    );
  };

  // The backend checks the record; the form only sends what was entered.
  const getAutoclaveRecord = (): AutoclaveRecordInput | null =>
    needsAutoclave
      ? {
          autoclaveEquipmentId: autoclave.equipmentId === "" ? null : autoclave.equipmentId,
          autoclaveProgram: autoclave.program || null,
          loadType: autoclave.loadType || null,
          autoclaveTemperature: autoclave.temperature === "" ? null : Number(autoclave.temperature),
          autoclaveCycleTime: autoclave.cycleTime === "" ? null : Number(autoclave.cycleTime),
          autoclaveCycleNumber: autoclave.cycleNumber === "" ? null : Number(autoclave.cycleNumber)
        }
      : null;

  const handleSaveDraft = async () => {
    setError(null);
    try {
      const vol = finalVolumeText === "" ? null : Number(finalVolumeText);
      const ph = measuredPhText === "" ? null : Number(measuredPhText);
      await onSave(getInputsList(), vol, ph, getAutoclaveRecord());
    } catch (err: unknown) {
      const failure = classifySaveFailure(err);
      if (benchMode && (failure.kind === "network" || failure.kind === "conflict")) return;
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to save draft.");
    }
  };

  const handleContinue = async () => {
    setError(null);
    try {
      const vol = finalVolumeText === "" ? null : Number(finalVolumeText);
      const ph = measuredPhText === "" ? null : Number(measuredPhText);
      await onSave(getInputsList(), vol, ph, getAutoclaveRecord());
      onNext();
    } catch (err: unknown) {
      const failure = classifySaveFailure(err);
      if (benchMode && (failure.kind === "network" || failure.kind === "conflict")) return;
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to save draft.");
    }
  };

  // pH Tolerance Check
  const phOutOfTolerance = useMemo(() => {
    if (recipe?.phTarget == null || recipe?.phTolerance == null || measuredPhText === "") {
      return false;
    }
    const val = Number(measuredPhText);
    return !Number.isNaN(val) && Math.abs(val - recipe.phTarget) > recipe.phTolerance;
  }, [recipe, measuredPhText]);

  const benchSections = useMemo(
    () => (needsAutoclave ? ["Components", "Autoclave", "Final Details"] : ["Components", "Final Details"]),
    [needsAutoclave]
  );

  const goToSection = (index: number) => {
    setActiveSection(index);
    sectionRefs.current[index]?.scrollIntoView?.({ behavior: "smooth", block: "start" });
  };

  const sectionWrapperSx = benchMode
    ? { minHeight: "calc(100vh - 220px)", display: "flex", flexDirection: "column" as const, justifyContent: "flex-start" as const }
    : undefined;

  return (
    <Stack spacing={3}>
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              Step 2: Stock Lots & Quantities
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {preparation.solutionMasterName}
              {preparation.hplcMethodAbbreviation && ` · Method: ${preparation.hplcMethodAbbreviation}`}
            </Typography>
          </Box>
          <Button
            size="small"
            variant="outlined"
            onClick={handleSaveDraft}
            disabled={saving}
            startIcon={saving ? <CircularProgress size={14} /> : <SaveOutlinedIcon />}
          >
            Save Draft
          </Button>
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {benchMode && (
          <Stepper
            nonLinear
            activeStep={activeSection}
            aria-label="Lots step sections"
            data-testid="lots-step-indicator"
            sx={{ mb: 2 }}
          >
            {benchSections.map((label, index) => (
              <Step key={label}>
                <StepButton onClick={() => goToSection(index)}>{label}</StepButton>
              </Step>
            ))}
          </Stepper>
        )}

        {/* Components List */}
        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[0] = el; }}
          data-bench-section="0"
          sx={sectionWrapperSx}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
            Components ({preparation.components.length})
          </Typography>

          {preparation.components.map((c) => {
            if (c.kind === "Text") {
              return (
                <Typography
                  key={c.id}
                  variant="body2"
                  sx={{ mb: 2, px: 2, py: 1, bgcolor: "action.hover", borderRadius: 1, fontStyle: "italic" }}
                >
                  {c.order}. {c.text}
                </Typography>
              );
            }
            if (c.kind === "PreparedSolution") {
              return (
                <SourcePreparationPicker
                  key={c.id}
                  component={c}
                  options={sourcesMap[c.id] ?? []}
                  loading={loadingLots}
                  selectedPreparationId={componentInputs[c.id]?.sourcePreparationId ?? null}
                  quantityUsed={componentInputs[c.id]?.quantityUsed ?? null}
                  onSelect={(prepId) => handleSelectSource(c.id, prepId)}
                  onChangeQuantity={(qty) => handleChangeQuantity(c.id, qty)}
                  disabled={saving}
                />
              );
            }
            return (
              <ComponentLotPicker
                key={c.id}
                component={c}
                lotOptions={lotsMap[c.id] ?? []}
                loadingLots={loadingLots}
                selectedMaterialId={componentInputs[c.id]?.materialId ?? null}
                quantityUsed={componentInputs[c.id]?.quantityUsed ?? null}
                onSelectLot={(matId) => handleSelectLot(c.id, matId)}
                onChangeQuantity={(qty) => handleChangeQuantity(c.id, qty)}
                disabled={saving}
              />
            );
          })}
        </Box>

        {needsAutoclave && (
          <Box
            ref={(el: HTMLDivElement | null) => { sectionRefs.current[1] = el; }}
            data-bench-section="1"
            sx={sectionWrapperSx}
          >
            <Divider sx={{ my: 2.5 }} />
            <AutoclaveSection
              value={autoclave}
              equipmentIsDefault={autoclaveLastChoice.isDefault}
              onEquipmentChangedByUser={autoclaveLastChoice.notifyValueChanged}
              onChange={(next) => {
                setAutoclave(next);
                onClearFieldError?.("autoclaveEquipmentId");
                onClearFieldError?.("autoclaveProgram");
                onClearFieldError?.("autoclaveCycleNumber");
              }}
              disabled={saving}
              fieldErrors={fieldErrors}
            />
          </Box>
        )}

        <Divider sx={{ my: 2.5 }} />

        {/* Volume & pH */}
        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[needsAutoclave ? 2 : 1] = el; }}
          data-bench-section={needsAutoclave ? "2" : "1"}
          sx={sectionWrapperSx}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
            Final Preparation Details
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mb: 2, alignItems: "start" }}>
            <NumericEntryField
              label="Final Volume *"
              kind="decimal"
              value={finalVolumeText}
              onChange={(v) => {
                setFinalVolumeText(v);
                onClearFieldError?.("finalVolumeMl");
              }}
              unit="mL"
              error={fieldErrors?.finalVolumeMl}
              helperText={`Recipe target: ${recipe?.finalVolumeMl ?? "—"} mL`}
            />

            {recipe?.phTarget != null ? (
              <NumericEntryField
                label="Measured pH *"
                kind="decimal"
                value={measuredPhText}
                onChange={(v) => {
                  setMeasuredPhText(v);
                  onClearFieldError?.("measuredPh");
                }}
                error={
                  fieldErrors?.measuredPh ??
                  (phOutOfTolerance
                    ? `pH ${measuredPhText} is outside target ${recipe.phTarget} ± ${recipe.phTolerance ?? 0}`
                    : undefined)
                }
                helperText={`Target: ${recipe.phTarget}${recipe.phTolerance ? ` ± ${recipe.phTolerance}` : ""}`}
              />
            ) : (
              <NumericEntryField
                label="Measured pH (Optional)"
                kind="decimal"
                value={measuredPhText}
                onChange={(v) => {
                  setMeasuredPhText(v);
                  onClearFieldError?.("measuredPh");
                }}
                error={fieldErrors?.measuredPh}
                helperText="Recipe does not specify a target pH"
              />
            )}
          </Box>

          {recipe?.instructions && (
            <Box sx={{ p: 1.5, bgcolor: "action.hover", borderRadius: 1, mb: 3 }}>
              <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                Instructions:
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", whiteSpace: "pre-wrap" }}>
                {recipe.instructions}
              </Typography>
            </Box>
          )}
        </Box>

        <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 1 }}>
          <Button
            variant="contained"
            onClick={handleContinue}
            disabled={saving}
            endIcon={saving ? <CircularProgress size={16} /> : <ArrowForwardIcon />}
            sx={{ textTransform: "none", fontWeight: 700, px: 3 }}
          >
            {saving ? "Saving..." : "Continue to Review"}
          </Button>
        </Box>
      </Paper>
    </Stack>
  );
}
