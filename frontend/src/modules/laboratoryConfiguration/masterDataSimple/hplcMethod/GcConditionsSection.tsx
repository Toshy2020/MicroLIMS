import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  FormControlLabel,
  Switch,
  FormHelperText,
  Paper
} from "@mui/material";
import { CarrierGas, HplcDetectorType } from "../services/HplcMethodService";
import { useTranslation } from "react-i18next";
import { SolutionMaster } from "../services/SolutionMasterService";
import {
  CARRIER_GAS_OPTIONS,
  GC_DETECTOR_OPTIONS,
  GcOvenStepRowState
} from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";
import { GcOvenProgramTable } from "./GcOvenProgramTable";

export interface GcConditionsSectionProps {
  carrierGas: CarrierGas | "";
  splitRatio: string | number;
  flowRateMlPerMin: string | number;
  inletTemperatureC: string | number;
  detectorTemperatureC: string | number;
  detectorType: HplcDetectorType;
  injectionVolumeUl: string | number;
  runTimeMin: string | number;
  diluentSolutionId: number | "";
  availableDiluents: SolutionMaster[];
  headspaceEnabled: boolean;
  headspaceEquilibrationTemperatureC: string | number;
  headspaceEquilibrationMin: string | number;
  headspaceTransferLineTemperatureC: string | number;
  ovenSteps: GcOvenStepRowState[];
  errors: HplcMethodErrors;
  onCarrierGasChange: (val: CarrierGas | "") => void;
  onSplitRatioChange: (val: string) => void;
  onFlowRateMlPerMinChange: (val: string) => void;
  onInletTemperatureCChange: (val: string) => void;
  onDetectorTemperatureCChange: (val: string) => void;
  onDetectorTypeChange: (val: HplcDetectorType) => void;
  onInjectionVolumeUlChange: (val: string) => void;
  onRunTimeMinChange: (val: string) => void;
  onDiluentSolutionIdChange: (val: number | "") => void;
  onHeadspaceEnabledChange: (val: boolean) => void;
  onHeadspaceEquilibrationTemperatureCChange: (val: string) => void;
  onHeadspaceEquilibrationMinChange: (val: string) => void;
  onHeadspaceTransferLineTemperatureCChange: (val: string) => void;
  onOvenStepsChange: (steps: GcOvenStepRowState[]) => void;
}

export function GcConditionsSection({
  carrierGas,
  splitRatio,
  flowRateMlPerMin,
  inletTemperatureC,
  detectorTemperatureC,
  detectorType,
  injectionVolumeUl,
  runTimeMin,
  diluentSolutionId,
  availableDiluents,
  headspaceEnabled,
  headspaceEquilibrationTemperatureC,
  headspaceEquilibrationMin,
  headspaceTransferLineTemperatureC,
  ovenSteps,
  errors,
  onCarrierGasChange,
  onSplitRatioChange,
  onFlowRateMlPerMinChange,
  onInletTemperatureCChange,
  onDetectorTemperatureCChange,
  onDetectorTypeChange,
  onInjectionVolumeUlChange,
  onRunTimeMinChange,
  onDiluentSolutionIdChange,
  onHeadspaceEnabledChange,
  onHeadspaceEquilibrationTemperatureCChange,
  onHeadspaceEquilibrationMinChange,
  onHeadspaceTransferLineTemperatureCChange,
  onOvenStepsChange
}: GcConditionsSectionProps) {
  const { t } = useTranslation("masterData");
  const carrierGasError = errors["conditions.carrierGas"] ?? errors["carrier.carrierGas"];
  const flowRateError = errors["conditions.flowRateMlPerMin"] ?? errors["elution.flowRateMlPerMin"];

  return (
    <Stack spacing={2.5}>
      {/* Carrier Gas, Inlet & Split */}
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
          {t("hplcMethod.gc.inletTitle")}
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
          {t("hplcMethod.gc.inletSubtitle")}
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required error={Boolean(carrierGasError)}>
            <InputLabel id="gc-carrier-gas-label">{t("hplcMethod.gc.carrierGasLabel")}</InputLabel>
            <Select
              labelId="gc-carrier-gas-label"
              label={t("hplcMethod.gc.carrierGasLabel")}
              value={carrierGas}
              onChange={(e) => onCarrierGasChange(e.target.value as CarrierGas)}
            >
              {CARRIER_GAS_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {t(`hplcMethod.options.carrierGas.${opt.value}`, { defaultValue: opt.label })}
                </MenuItem>
              ))}
            </Select>
            {carrierGasError && <FormHelperText>{carrierGasError}</FormHelperText>}
          </FormControl>

          <TextField
            size="small"
            label={t("hplcMethod.gc.inletTempLabel")}
            type="number"
            value={inletTemperatureC}
            onChange={(e) => onInletTemperatureCChange(e.target.value)}
            required
            placeholder={t("hplcMethod.gc.inletTempPlaceholder")}
            error={Boolean(errors["conditions.inletTemperatureC"])}
            helperText={errors["conditions.inletTemperatureC"] ?? t("hplcMethod.gc.inletTempHelper")}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label={t("hplcMethod.gc.flowRateLabel")}
            type="number"
            value={flowRateMlPerMin}
            onChange={(e) => onFlowRateMlPerMinChange(e.target.value)}
            required
            placeholder={t("hplcMethod.gc.flowRatePlaceholder")}
            error={Boolean(flowRateError)}
            helperText={flowRateError ?? t("hplcMethod.gc.flowRateHelper")}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label={t("hplcMethod.gc.splitRatioLabel")}
            type="number"
            value={splitRatio}
            onChange={(e) => onSplitRatioChange(e.target.value)}
            placeholder={t("hplcMethod.gc.splitRatioPlaceholder")}
            error={Boolean(errors["conditions.splitRatio"])}
            helperText={errors["conditions.splitRatio"] ?? t("hplcMethod.gc.splitRatioHelper")}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />
        </Box>
      </Box>

      {/* Detection & Injection */}
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
          {t("hplcMethod.gc.detectionTitle")}
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
          {t("hplcMethod.gc.detectionSubtitle")}
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required error={Boolean(errors["detection.detectorType"])}>
            <InputLabel id="gc-detector-type-label">{t("hplcMethod.gc.detectorTypeLabel")}</InputLabel>
            <Select
              labelId="gc-detector-type-label"
              label={t("hplcMethod.gc.detectorTypeLabel")}
              value={detectorType}
              onChange={(e) => onDetectorTypeChange(e.target.value as HplcDetectorType)}
            >
              {GC_DETECTOR_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {t(`hplcMethod.options.gcDetector.${opt.value}`, { defaultValue: opt.label })}
                </MenuItem>
              ))}
            </Select>
            {errors["detection.detectorType"] && (
              <FormHelperText>{errors["detection.detectorType"]}</FormHelperText>
            )}
          </FormControl>

          <TextField
            size="small"
            label={t("hplcMethod.gc.detectorTempLabel")}
            type="number"
            value={detectorTemperatureC}
            onChange={(e) => onDetectorTemperatureCChange(e.target.value)}
            required
            placeholder={t("hplcMethod.gc.detectorTempPlaceholder")}
            error={Boolean(errors["conditions.detectorTemperatureC"])}
            helperText={errors["conditions.detectorTemperatureC"] ?? t("hplcMethod.gc.detectorTempHelper")}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label={t("hplcMethod.gc.injectionVolumeLabel")}
            type="number"
            value={injectionVolumeUl}
            onChange={(e) => onInjectionVolumeUlChange(e.target.value)}
            required
            placeholder={t("hplcMethod.gc.injectionVolumePlaceholder")}
            error={Boolean(errors["detection.injectionVolumeUl"])}
            helperText={errors["detection.injectionVolumeUl"] ?? t("hplcMethod.gc.injectionVolumeHelper")}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label={t("hplcMethod.gc.runTimeLabel")}
            type="number"
            value={runTimeMin}
            onChange={(e) => onRunTimeMinChange(e.target.value)}
            required
            placeholder={t("hplcMethod.gc.runTimePlaceholder")}
            error={Boolean(errors["detection.runTimeMin"])}
            helperText={errors["detection.runTimeMin"] ?? t("hplcMethod.gc.runTimeHelper")}
            slotProps={{ htmlInput: { min: 0.5, step: "any" } }}
            fullWidth
          />

          <FormControl size="small" fullWidth required error={Boolean(errors["solutions.diluent"])} sx={{ gridColumn: { xs: "1fr", sm: "1 / -1" } }}>
            <InputLabel id="gc-diluent-solution-label">{t("hplcMethod.gc.diluentLabel")}</InputLabel>
            <Select
              labelId="gc-diluent-solution-label"
              label={t("hplcMethod.gc.diluentLabel")}
              value={diluentSolutionId}
              onChange={(e) => onDiluentSolutionIdChange(Number(e.target.value) || "")}
            >
              {availableDiluents.length === 0 ? (
                <MenuItem disabled value="">
                  <em>{t("hplcMethod.gc.noDiluents")}</em>
                </MenuItem>
              ) : (
                availableDiluents.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.name} {!s.isActive && t("hplcMethod.solutions.inactiveTag")}
                  </MenuItem>
                ))
              )}
            </Select>
            {errors["solutions.diluent"] && <FormHelperText>{errors["solutions.diluent"]}</FormHelperText>}
          </FormControl>
        </Box>
      </Box>

      {/* Oven Program Table */}
      <GcOvenProgramTable steps={ovenSteps} errors={errors} onChange={onOvenStepsChange} />

      {/* Headspace Sampler Parameters */}
      <Paper
        elevation={0}
        sx={{
          p: 2,
          borderRadius: 1.5,
          border: "1px solid",
          borderColor: headspaceEnabled ? "primary.main" : "divider",
          bgcolor: headspaceEnabled ? "action.hover" : "transparent"
        }}
      >
        <FormControlLabel
          control={
            <Switch
              checked={headspaceEnabled}
              onChange={(e) => onHeadspaceEnabledChange(e.target.checked)}
              color="primary"
            />
          }
          label={
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {t("hplcMethod.gc.headspaceTitle")}
            </Typography>
          }
        />
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: headspaceEnabled ? 2 : 0 }}>
          {t("hplcMethod.gc.headspaceSubtitle")}
        </Typography>

        {headspaceEnabled && (
          <Box sx={{ mt: 1, display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              label={t("hplcMethod.gc.eqTempLabel")}
              type="number"
              value={headspaceEquilibrationTemperatureC}
              onChange={(e) => onHeadspaceEquilibrationTemperatureCChange(e.target.value)}
              required
              placeholder={t("hplcMethod.gc.eqTempPlaceholder")}
              error={Boolean(errors["headspace.equilibrationTemperatureC"])}
              helperText={errors["headspace.equilibrationTemperatureC"] ?? t("hplcMethod.gc.eqTempHelper")}
              slotProps={{ htmlInput: { min: 1, step: "any" } }}
              fullWidth
            />
            <TextField
              size="small"
              label={t("hplcMethod.gc.eqTimeLabel")}
              type="number"
              value={headspaceEquilibrationMin}
              onChange={(e) => onHeadspaceEquilibrationMinChange(e.target.value)}
              required
              placeholder={t("hplcMethod.gc.eqTimePlaceholder")}
              error={Boolean(errors["headspace.equilibrationMin"])}
              helperText={errors["headspace.equilibrationMin"] ?? t("hplcMethod.gc.eqTimeHelper")}
              slotProps={{ htmlInput: { min: 0.1, step: "any" } }}
              fullWidth
            />
            <TextField
              size="small"
              label={t("hplcMethod.gc.transferLineLabel")}
              type="number"
              value={headspaceTransferLineTemperatureC}
              onChange={(e) => onHeadspaceTransferLineTemperatureCChange(e.target.value)}
              required
              placeholder={t("hplcMethod.gc.transferLinePlaceholder")}
              error={Boolean(errors["headspace.transferLineTemperatureC"])}
              helperText={errors["headspace.transferLineTemperatureC"] ?? t("hplcMethod.gc.transferLineHelper")}
              slotProps={{ htmlInput: { min: 1, step: "any" } }}
              fullWidth
            />
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
