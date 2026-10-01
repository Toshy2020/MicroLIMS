import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  IconButton,
  Tooltip,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { ElutionMode } from "../services/HplcMethodService";
import {
  GradientStepRowState,
  ELUTION_MODE_OPTIONS
} from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcElutionSectionProps {
  elutionMode: ElutionMode;
  flowRateMlPerMin: string | number;
  equilibrationMin: string | number;
  gradientSteps: GradientStepRowState[];
  mobilePhaseChannels: string[];
  errors: HplcMethodErrors;
  onElutionModeChange: (val: ElutionMode) => void;
  onFlowRateMlPerMinChange: (val: string) => void;
  onEquilibrationMinChange: (val: string) => void;
  onGradientStepChange: (index: number, field: keyof GradientStepRowState, val: string) => void;
  onAddGradientStep: () => void;
  onRemoveGradientStep: (index: number) => void;
}

export function HplcElutionSection({
  elutionMode,
  flowRateMlPerMin,
  equilibrationMin,
  gradientSteps,
  mobilePhaseChannels,
  errors,
  onElutionModeChange,
  onFlowRateMlPerMinChange,
  onEquilibrationMinChange,
  onGradientStepChange,
  onAddGradientStep,
  onRemoveGradientStep
}: HplcElutionSectionProps) {
  const isGradient = elutionMode === "Gradient";
  const hasChannel = (ch: string) => mobilePhaseChannels.includes(ch);

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        Elution & Flow Parameters
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Configure elution mechanism, flow rate, equilibration time, and gradient profile if applicable.
      </Typography>

      <Stack spacing={2.5}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required>
            <InputLabel id="elution-mode-label">Elution Mode *</InputLabel>
            <Select
              labelId="elution-mode-label"
              label="Elution Mode *"
              value={elutionMode}
              onChange={(e) => onElutionModeChange(e.target.value as ElutionMode)}
            >
              {ELUTION_MODE_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            size="small"
            label="Flow Rate (mL/min) *"
            type="number"
            value={flowRateMlPerMin}
            onChange={(e) => onFlowRateMlPerMinChange(e.target.value)}
            required
            placeholder="e.g. 1.0"
            error={Boolean(errors["elution.flowRateMlPerMin"])}
            helperText={errors["elution.flowRateMlPerMin"] ?? "Mobile phase flow velocity"}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label="Equilibration (min)"
            type="number"
            value={equilibrationMin}
            onChange={(e) => onEquilibrationMinChange(e.target.value)}
            placeholder="e.g. 10"
            error={Boolean(errors["elution.equilibrationMin"])}
            helperText={errors["elution.equilibrationMin"] ?? "Column wash / equilibration (optional)"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
        </Box>

        {isGradient && (
          <Box sx={{ mt: 1, p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <div>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Gradient Time Program
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  Step 1 must begin at 0.0 min. Each row&rsquo;s percentages must sum to 100%.
                </Typography>
              </div>
              <Button
                size="small"
                variant="outlined"
                startIcon={<AddIcon />}
                onClick={onAddGradientStep}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                Add Step
              </Button>
            </Stack>

            {gradientSteps.length < 2 && (
              <Alert severity="warning" sx={{ mb: 1.5, py: 0.5, fontSize: 12 }}>
                A gradient program requires at least 2 steps.
              </Alert>
            )}

            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600, width: 60 }}>Step</TableCell>
                  <TableCell sx={{ fontWeight: 600, minWidth: 100 }}>Time (min)</TableCell>
                  <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>% A</TableCell>
                  <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>% B</TableCell>
                  <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>% C</TableCell>
                  <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>% D</TableCell>
                  <TableCell sx={{ fontWeight: 600, width: 80 }}>Total %</TableCell>
                  <TableCell align="right" sx={{ width: 60 }} />
                </TableRow>
              </TableHead>
              <TableBody>
                {gradientSteps.map((step, idx) => {
                  const pa = Number(step.percentA || 0);
                  const pb = Number(step.percentB || 0);
                  const pc = Number(step.percentC || 0);
                  const pd = Number(step.percentD || 0);
                  const sum = pa + pb + pc + pd;
                  const is100 = Math.abs(sum - 100) <= 0.01;

                  return (
                    <TableRow key={idx}>
                      <TableCell sx={{ fontWeight: 500 }}>#{idx + 1}</TableCell>
                      <TableCell>
                        <TextField
                          size="small"
                          type="number"
                          value={step.timeMin}
                          onChange={(e) => onGradientStepChange(idx, "timeMin", e.target.value)}
                          slotProps={{ htmlInput: { min: 0, step: "any" } }}
                          disabled={idx === 0}
                          error={Boolean(errors[`gradient.${idx}.timeMin`])}
                          helperText={errors[`gradient.${idx}.timeMin`] ?? (idx === 0 ? "Initial (0 min)" : undefined)}
                          sx={{ width: 110 }}
                        />
                      </TableCell>
                      <TableCell>
                        <TextField
                          size="small"
                          type="number"
                          value={step.percentA}
                          onChange={(e) => onGradientStepChange(idx, "percentA", e.target.value)}
                          disabled={!hasChannel("A")}
                          error={Boolean(errors[`gradient.${idx}.percentA`])}
                          helperText={errors[`gradient.${idx}.percentA`]}
                          slotProps={{ htmlInput: { min: 0, max: 100, step: "any" } }}
                          sx={{ width: 85 }}
                        />
                      </TableCell>
                      <TableCell>
                        <TextField
                          size="small"
                          type="number"
                          value={step.percentB}
                          onChange={(e) => onGradientStepChange(idx, "percentB", e.target.value)}
                          disabled={!hasChannel("B")}
                          error={Boolean(errors[`gradient.${idx}.percentB`])}
                          helperText={errors[`gradient.${idx}.percentB`]}
                          slotProps={{ htmlInput: { min: 0, max: 100, step: "any" } }}
                          sx={{ width: 85 }}
                        />
                      </TableCell>
                      <TableCell>
                        <TextField
                          size="small"
                          type="number"
                          value={step.percentC}
                          onChange={(e) => onGradientStepChange(idx, "percentC", e.target.value)}
                          disabled={!hasChannel("C")}
                          error={Boolean(errors[`gradient.${idx}.percentC`])}
                          helperText={errors[`gradient.${idx}.percentC`]}
                          slotProps={{ htmlInput: { min: 0, max: 100, step: "any" } }}
                          sx={{ width: 85 }}
                        />
                      </TableCell>
                      <TableCell>
                        <TextField
                          size="small"
                          type="number"
                          value={step.percentD}
                          onChange={(e) => onGradientStepChange(idx, "percentD", e.target.value)}
                          disabled={!hasChannel("D")}
                          error={Boolean(errors[`gradient.${idx}.percentD`])}
                          helperText={errors[`gradient.${idx}.percentD`]}
                          slotProps={{ htmlInput: { min: 0, max: 100, step: "any" } }}
                          sx={{ width: 85 }}
                        />
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600, color: is100 ? "success.main" : "error.main" }}>
                        {sum.toFixed(1)}%
                        {errors[`gradient.${idx}.total`] && (
                          <Typography variant="caption" color="error" sx={{ display: "block", fontWeight: 400 }}>
                            {errors[`gradient.${idx}.total`]}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">
                        <Tooltip title="Remove step">
                          <span>
                            <IconButton
                              size="small"
                              onClick={() => onRemoveGradientStep(idx)}
                              disabled={gradientSteps.length <= 2}
                              color="error"
                            >
                              <DeleteIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
        )}
      </Stack>
    </Box>
  );
}
