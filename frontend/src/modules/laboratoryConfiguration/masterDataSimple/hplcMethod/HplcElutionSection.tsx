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
  Alert,
  TableContainer
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation("masterData");
  const isGradient = elutionMode === "Gradient";
  const hasChannel = (ch: string) => mobilePhaseChannels.includes(ch);

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        {t("hplcMethod.elution.title")}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {t("hplcMethod.elution.subtitle")}
      </Typography>

      <Stack spacing={2.5}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required>
            <InputLabel id="elution-mode-label">{t("hplcMethod.elution.elutionModeLabel")}</InputLabel>
            <Select
              labelId="elution-mode-label"
              label={t("hplcMethod.elution.elutionModeLabel")}
              value={elutionMode}
              onChange={(e) => onElutionModeChange(e.target.value as ElutionMode)}
            >
              {ELUTION_MODE_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {t(`hplcMethod.options.elutionMode.${opt.value}`, { defaultValue: opt.label })}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            size="small"
            label={t("hplcMethod.elution.flowRateLabel")}
            type="number"
            value={flowRateMlPerMin}
            onChange={(e) => onFlowRateMlPerMinChange(e.target.value)}
            required
            placeholder={t("hplcMethod.elution.flowRatePlaceholder")}
            error={Boolean(errors["elution.flowRateMlPerMin"])}
            helperText={errors["elution.flowRateMlPerMin"] ?? t("hplcMethod.elution.flowRateHelper")}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label={t("hplcMethod.elution.equilibrationLabel")}
            type="number"
            value={equilibrationMin}
            onChange={(e) => onEquilibrationMinChange(e.target.value)}
            placeholder={t("hplcMethod.elution.equilibrationPlaceholder")}
            error={Boolean(errors["elution.equilibrationMin"])}
            helperText={errors["elution.equilibrationMin"] ?? t("hplcMethod.elution.equilibrationHelper")}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
        </Box>

        {isGradient && (
          <Box sx={{ mt: 1, p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <div>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  {t("hplcMethod.elution.gradientTitle")}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {t("hplcMethod.elution.gradientSubtitle")}
                </Typography>
              </div>
              <Button
                size="small"
                variant="outlined"
                startIcon={<AddIcon />}
                onClick={onAddGradientStep}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                {t("hplcMethod.elution.addStep")}
              </Button>
            </Stack>

            {gradientSteps.length < 2 && (
              <Alert severity="warning" sx={{ mb: 1.5, py: 0.5, fontSize: 12 }}>
                {t("hplcMethod.elution.minStepsWarning")}
              </Alert>
            )}

            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600, width: 60 }}>{t("hplcMethod.elution.columns.step")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, minWidth: 100 }}>{t("hplcMethod.elution.columns.time")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>{t("hplcMethod.elution.columns.percentA")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>{t("hplcMethod.elution.columns.percentB")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>{t("hplcMethod.elution.columns.percentC")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, minWidth: 80 }}>{t("hplcMethod.elution.columns.percentD")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, width: 80 }}>{t("hplcMethod.elution.columns.total")}</TableCell>
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
                            slotProps={{ htmlInput: { "aria-label": t("hplcMethod.elution.aria.time", { index: idx + 1 }), min: 0, step: "any" } }}
                            disabled={idx === 0}
                            error={Boolean(errors[`gradient.${idx}.timeMin`])}
                            helperText={errors[`gradient.${idx}.timeMin`] ?? (idx === 0 ? t("hplcMethod.elution.initialZeroMin") : undefined)}
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
                            slotProps={{ htmlInput: { "aria-label": t("hplcMethod.elution.aria.percentA", { index: idx + 1 }), min: 0, max: 100, step: "any" } }}
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
                            slotProps={{ htmlInput: { "aria-label": t("hplcMethod.elution.aria.percentB", { index: idx + 1 }), min: 0, max: 100, step: "any" } }}
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
                            slotProps={{ htmlInput: { "aria-label": t("hplcMethod.elution.aria.percentC", { index: idx + 1 }), min: 0, max: 100, step: "any" } }}
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
                            slotProps={{ htmlInput: { "aria-label": t("hplcMethod.elution.aria.percentD", { index: idx + 1 }), min: 0, max: 100, step: "any" } }}
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
                          <Tooltip title={t("hplcMethod.elution.removeStep")}>
                            <span>
                              <IconButton aria-label={t("hplcMethod.elution.removeStep")}
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
            </TableContainer>
          </Box>
        )}
      </Stack>
    </Box>
  );
}
