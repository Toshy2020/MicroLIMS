import {
  Box,
  Button,
  FormControl,
  FormHelperText,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import type { DosageForm } from "../../../specifications/services/SpecificationService";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Multi-stage and pharmacopoeial compendial stage/dosage limit fields
 * (MultiStage, DissolutionQ, DisintegrationTime, WeightVariation).
 */
export function StageLimits({ model, set, errors }: LimitGroupProps) {
  const handleAddStage = () => {
    set({
      stages: [
        ...model.stages,
        {
          stageNumber: model.stages.length + 1,
          stageLabel: `Stage ${model.stages.length + 1}`,
          acceptanceCriteriaText: ""
        }
      ]
    });
  };

  const handleRemoveStage = (index: number) => {
    const next = model.stages
      .filter((_, idx) => idx !== index)
      .map((s, idx) => ({ ...s, stageNumber: idx + 1 }));
    set({ stages: next });
  };

  const handleStageChange = (
    index: number,
    field: "stageLabel" | "acceptanceCriteriaText",
    val: string
  ) => {
    set({
      stages: model.stages.map((s, idx) => (idx === index ? { ...s, [field]: val } : s))
    });
  };

  if (model.limitType === "MultiStage") {
    return (
      <Stack spacing={1.5} data-field="stages">
        {model.stages.map((stage, idx) => (
          <Stack
            key={idx}
            direction="row"
            spacing={1}
            sx={{ alignItems: "flex-start" }}
          >
            <TextField
              size="small"
              label="Stage #"
              type="number"
              value={stage.stageNumber}
              disabled
              sx={{ width: 75 }}
            />
            <TextField
              size="small"
              label="Stage Label *"
              value={stage.stageLabel}
              placeholder="e.g. Stage 1 (S1, n=6)"
              onChange={(e) => handleStageChange(idx, "stageLabel", e.target.value)}
              sx={{ width: { xs: 140, sm: 200 } }}
            />
            <TextField
              size="small"
              label="Acceptance Criteria Text *"
              multiline
              minRows={1}
              value={stage.acceptanceCriteriaText}
              placeholder="e.g. Each unit ≥ Q + 5% (≥ 85%)"
              onChange={(e) => handleStageChange(idx, "acceptanceCriteriaText", e.target.value)}
              fullWidth
            />
            <IconButton
              aria-label={`Remove stage ${idx + 1}`}
              size="small"
              color="error"
              onClick={() => handleRemoveStage(idx)}
              disabled={model.stages.length <= 1}
              title="Remove Stage"
              sx={{ mt: 0.5 }}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Stack>
        ))}
        {errors.stages && <FormHelperText error>{errors.stages}</FormHelperText>}
        <Box>
          <Button
            type="button"
            size="small"
            startIcon={<AddIcon />}
            onClick={handleAddStage}
            sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
          >
            + Add Stage
          </Button>
        </Box>
      </Stack>
    );
  }

  if (model.limitType === "DissolutionQ") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          Dissolution Acceptance (USP &lt;711&gt; / EP 2.9.3)
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <TextField
            size="small"
            name="lowerLimit"
            type="number"
            label="Q (% dissolved) *"
            placeholder="e.g. 75"
            value={model.lowerLimit}
            onChange={(e) => set({ lowerLimit: e.target.value })}
            error={Boolean(errors.lowerLimit)}
            slotProps={{ htmlInput: { min: 0.01, max: 100, step: "any" } }}
            helperText={errors.lowerLimit || "Stored in lower limit (0 < Q ≤ 100 %)"}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            name="labelClaim"
            type="number"
            label="Label Claim *"
            placeholder="e.g. 100"
            value={model.labelClaim}
            onChange={(e) => set({ labelClaim: e.target.value })}
            error={Boolean(errors.labelClaim)}
            slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
            helperText={errors.labelClaim || "Active substance per dosage unit"}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            label="Unit"
            value="mg"
            disabled
            helperText="Fixed unit for dissolution claim"
            sx={{ width: 120 }}
          />
        </Stack>
        {model.lowerLimit.trim() !== "" && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Specification limit display: <strong>Q = {model.lowerLimit.trim()} %</strong>
          </Typography>
        )}
      </Box>
    );
  }

  if (model.limitType === "DisintegrationTime") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <TextField
            size="small"
            name="upperLimit"
            type="number"
            label="Time limit (min) *"
            placeholder="e.g. 15"
            value={model.upperLimit}
            onChange={(e) => set({ upperLimit: e.target.value })}
            error={Boolean(errors.upperLimit)}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            helperText={errors.upperLimit || "Stored in upper limit (NMT time in minutes)"}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            label="Unit"
            value="min"
            disabled
            helperText="Fixed unit for disintegration time"
            sx={{ width: 120 }}
          />
        </Stack>
        {model.upperLimit.trim() !== "" && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Specification limit display: <strong>NMT {model.upperLimit.trim()} min</strong>
          </Typography>
        )}
      </Box>
    );
  }

  if (model.limitType === "WeightVariation") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          Weight Variation Acceptance (USP &lt;2091&gt;)
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <FormControl size="small" sx={{ flex: 1 }} error={Boolean(errors.dosageForm)}>
            <InputLabel id="wv-dosage-form-label">Dosage Form *</InputLabel>
            <Select
              name="dosageForm"
              labelId="wv-dosage-form-label"
              label="Dosage Form *"
              value={model.dosageForm}
              onChange={(e) => set({ dosageForm: e.target.value as DosageForm })}
            >
              <MenuItem value="Tablet">Tablet</MenuItem>
              <MenuItem value="HardCapsule">Hard Capsule</MenuItem>
              <MenuItem value="SoftCapsule">Soft Capsule</MenuItem>
            </Select>
            {errors.dosageForm && <FormHelperText>{errors.dosageForm}</FormHelperText>}
          </FormControl>
          <TextField
            size="small"
            label="Unit"
            value="mg"
            disabled
            helperText="Fixed unit for weight variation"
            sx={{ width: 120 }}
          />
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Specification limit display:{" "}
          <strong>
            {model.dosageForm === "Tablet"
              ? "USP <2091>: tablets, limit by average weight"
              : "USP <2091>: net content 90-110 % of average"}
          </strong>
        </Typography>
      </Box>
    );
  }

  return null;
}
