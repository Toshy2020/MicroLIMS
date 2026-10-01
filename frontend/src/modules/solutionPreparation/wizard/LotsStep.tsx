import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Paper,
  Typography,
  TextField,
  InputAdornment,
  Button,
  Alert,
  CircularProgress,
  Stack,
  Divider
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { ComponentLotPicker } from "../components/ComponentLotPicker";
import { SolutionPreparationService } from "../services/SolutionPreparationService";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot,
  LotOption,
  PreparationComponentInput
} from "../types";

interface Props {
  preparation: SolutionPreparationResponse;
  onSave: (
    components: PreparationComponentInput[],
    finalVolumeMl: number | null,
    measuredPh: number | null
  ) => Promise<void>;
  onNext: () => void;
  saving?: boolean;
}

export function LotsStep({
  preparation,
  onSave,
  onNext,
  saving = false
}: Props) {
  const recipe = useMemo<RecipeSnapshot | null>(() => {
    try {
      return JSON.parse(preparation.recipeSnapshotJson) as RecipeSnapshot;
    } catch {
      return null;
    }
  }, [preparation.recipeSnapshotJson]);

  // Form State
  const [componentInputs, setComponentInputs] = useState<
    Record<number, { materialId: number | null; quantityUsed: number | null }>
  >(() => {
    const map: Record<number, { materialId: number | null; quantityUsed: number | null }> = {};
    for (const c of preparation.components) {
      map[c.id] = {
        materialId: c.materialId,
        quantityUsed: c.quantityUsed
      };
    }
    return map;
  });

  const [finalVolume, setFinalVolume] = useState<number | "">(
    preparation.finalVolumeMl ?? recipe?.finalVolumeMl ?? ""
  );
  const [measuredPh, setMeasuredPh] = useState<number | "">(
    preparation.measuredPh ?? ""
  );

  // Lots per component
  const [lotsMap, setLotsMap] = useState<Record<number, LotOption[]>>({});
  const [loadingLots, setLoadingLots] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load lot options for all components
  const loadLots = useCallback(async () => {
    setLoadingLots(true);
    setError(null);
    try {
      const results = await Promise.all(
        preparation.components.map(async (c) => {
          const lots = await SolutionPreparationService.getLotOptions(preparation.id, c.id);
          return { componentId: c.id, lots };
        })
      );
      const newMap: Record<number, LotOption[]> = {};
      for (const res of results) {
        newMap[res.componentId] = res.lots;
      }
      setLotsMap(newMap);
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
        // Reset quantity if lot changed
        quantityUsed: materialId ? prev[componentId]?.quantityUsed ?? null : null
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

  const getInputsList = (): PreparationComponentInput[] => {
    return preparation.components.map((c) => ({
      componentId: c.id,
      materialId: componentInputs[c.id]?.materialId ?? null,
      quantityUsed: componentInputs[c.id]?.quantityUsed ?? null
    }));
  };

  const handleSaveDraft = async () => {
    setError(null);
    try {
      const vol = finalVolume === "" ? null : Number(finalVolume);
      const ph = measuredPh === "" ? null : Number(measuredPh);
      await onSave(getInputsList(), vol, ph);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to save draft.");
    }
  };

  const handleContinue = async () => {
    setError(null);
    try {
      const vol = finalVolume === "" ? null : Number(finalVolume);
      const ph = measuredPh === "" ? null : Number(measuredPh);
      await onSave(getInputsList(), vol, ph);
      onNext();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to save draft.");
    }
  };

  // pH Tolerance Check
  const phOutOfTolerance = useMemo(() => {
    if (recipe?.phTarget == null || recipe?.phTolerance == null || measuredPh === "") {
      return false;
    }
    const val = Number(measuredPh);
    return Math.abs(val - recipe.phTarget) > recipe.phTolerance;
  }, [recipe, measuredPh]);

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

        {/* Components List */}
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
          Components ({preparation.components.length})
        </Typography>

        {preparation.components.map((c) => (
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
        ))}

        <Divider sx={{ my: 2.5 }} />

        {/* Volume & pH */}
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
          Final Preparation Details
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mb: 2 }}>
          <TextField
            size="small"
            label="Final Volume *"
            type="number"
            value={finalVolume}
            onChange={(e) => {
              const v = e.target.value;
              setFinalVolume(v === "" ? "" : parseFloat(v));
            }}
            slotProps={{
              input: {
                endAdornment: <InputAdornment position="end">mL</InputAdornment>,
                inputProps: { min: 0.1, step: "any" }
              }
            }}
            helperText={`Recipe target: ${recipe?.finalVolumeMl ?? "—"} mL`}
          />

          {recipe?.phTarget != null ? (
            <Box>
              <TextField
                fullWidth
                size="small"
                label="Measured pH *"
                type="number"
                value={measuredPh}
                onChange={(e) => {
                  const v = e.target.value;
                  setMeasuredPh(v === "" ? "" : parseFloat(v));
                }}
                error={phOutOfTolerance}
                slotProps={{
                  input: {
                    inputProps: { min: 0, max: 14, step: "0.01" }
                  }
                }}
                helperText={
                  phOutOfTolerance
                    ? `pH ${measuredPh} is outside target ${recipe.phTarget} ± ${recipe.phTolerance ?? 0}`
                    : `Target: ${recipe.phTarget}${recipe.phTolerance ? ` ± ${recipe.phTolerance}` : ""}`
                }
              />
            </Box>
          ) : (
            <TextField
              size="small"
              label="Measured pH (Optional)"
              type="number"
              value={measuredPh}
              onChange={(e) => {
                const v = e.target.value;
                setMeasuredPh(v === "" ? "" : parseFloat(v));
              }}
              slotProps={{
                input: {
                  inputProps: { min: 0, max: 14, step: "0.01" }
                }
              }}
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
