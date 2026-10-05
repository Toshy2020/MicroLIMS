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
  const carrierGasError = errors["conditions.carrierGas"] ?? errors["carrier.carrierGas"];
  const flowRateError = errors["conditions.flowRateMlPerMin"] ?? errors["elution.flowRateMlPerMin"];

  return (
    <Stack spacing={2.5}>
      {/* Carrier Gas, Inlet & Split */}
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
          Inlet &amp; Carrier Gas Conditions
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
          Specify carrier gas, inlet temperature, column flow velocity, and injection split ratio.
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required error={Boolean(carrierGasError)}>
            <InputLabel id="gc-carrier-gas-label">Carrier Gas *</InputLabel>
            <Select
              labelId="gc-carrier-gas-label"
              label="Carrier Gas *"
              value={carrierGas}
              onChange={(e) => onCarrierGasChange(e.target.value as CarrierGas)}
            >
              {CARRIER_GAS_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
            {carrierGasError && <FormHelperText>{carrierGasError}</FormHelperText>}
          </FormControl>

          <TextField
            size="small"
            label="Inlet Temperature (°C) *"
            type="number"
            value={inletTemperatureC}
            onChange={(e) => onInletTemperatureCChange(e.target.value)}
            required
            placeholder="e.g. 200 or 250"
            error={Boolean(errors["conditions.inletTemperatureC"])}
            helperText={errors["conditions.inletTemperatureC"] ?? "Injector port temperature"}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label="Column Flow Rate (mL/min) *"
            type="number"
            value={flowRateMlPerMin}
            onChange={(e) => onFlowRateMlPerMinChange(e.target.value)}
            required
            placeholder="e.g. 2.0"
            error={Boolean(flowRateError)}
            helperText={flowRateError ?? "Carrier gas flow velocity"}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label="Split Ratio"
            type="number"
            value={splitRatio}
            onChange={(e) => onSplitRatioChange(e.target.value)}
            placeholder="Empty for splitless (e.g. 10 or 20 for 10:1, 20:1)"
            error={Boolean(errors["conditions.splitRatio"])}
            helperText={errors["conditions.splitRatio"] ?? "Leave blank for splitless injection"}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />
        </Box>
      </Box>

      {/* Detection & Injection */}
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
          Detection &amp; Run Parameters
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
          Specify detector type, detector temperature, autosampler injection volume, total run duration, and diluent.
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required error={Boolean(errors["detection.detectorType"])}>
            <InputLabel id="gc-detector-type-label">Detector Type *</InputLabel>
            <Select
              labelId="gc-detector-type-label"
              label="Detector Type *"
              value={detectorType}
              onChange={(e) => onDetectorTypeChange(e.target.value as HplcDetectorType)}
            >
              {GC_DETECTOR_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
            {errors["detection.detectorType"] && (
              <FormHelperText>{errors["detection.detectorType"]}</FormHelperText>
            )}
          </FormControl>

          <TextField
            size="small"
            label="Detector Temperature (°C) *"
            type="number"
            value={detectorTemperatureC}
            onChange={(e) => onDetectorTemperatureCChange(e.target.value)}
            required
            placeholder="e.g. 250 or 300"
            error={Boolean(errors["conditions.detectorTemperatureC"])}
            helperText={errors["conditions.detectorTemperatureC"] ?? "Detector cell/flame temperature"}
            slotProps={{ htmlInput: { min: 1, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label="Injection Volume (µL) *"
            type="number"
            value={injectionVolumeUl}
            onChange={(e) => onInjectionVolumeUlChange(e.target.value)}
            required
            placeholder="e.g. 1"
            error={Boolean(errors["detection.injectionVolumeUl"])}
            helperText={errors["detection.injectionVolumeUl"] ?? "Liquid or headspace gas volume"}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            fullWidth
          />

          <TextField
            size="small"
            label="Run Time (min) *"
            type="number"
            value={runTimeMin}
            onChange={(e) => onRunTimeMinChange(e.target.value)}
            required
            placeholder="e.g. 25"
            error={Boolean(errors["detection.runTimeMin"])}
            helperText={errors["detection.runTimeMin"] ?? "Total GC acquisition time"}
            slotProps={{ htmlInput: { min: 0.5, step: "any" } }}
            fullWidth
          />

          <FormControl size="small" fullWidth required error={Boolean(errors["solutions.diluent"])} sx={{ gridColumn: { xs: "1fr", sm: "1 / -1" } }}>
            <InputLabel id="gc-diluent-solution-label">Diluent Solution *</InputLabel>
            <Select
              labelId="gc-diluent-solution-label"
              label="Diluent Solution *"
              value={diluentSolutionId}
              onChange={(e) => onDiluentSolutionIdChange(Number(e.target.value) || "")}
            >
              {availableDiluents.length === 0 ? (
                <MenuItem disabled value="">
                  <em>No diluent solutions registered for this section</em>
                </MenuItem>
              ) : (
                availableDiluents.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.name} {!s.isActive && "(Inactive)"}
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
              Headspace Sampler (USP &lt;467&gt; Static Headspace)
            </Typography>
          }
        />
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: headspaceEnabled ? 2 : 0 }}>
          Enable if samples are incubated and sampled via automated headspace autosampler.
        </Typography>

        {headspaceEnabled && (
          <Box sx={{ mt: 1, display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              label="Equilibration Temp (°C) *"
              type="number"
              value={headspaceEquilibrationTemperatureC}
              onChange={(e) => onHeadspaceEquilibrationTemperatureCChange(e.target.value)}
              required
              placeholder="e.g. 80"
              error={Boolean(errors["headspace.equilibrationTemperatureC"])}
              helperText={errors["headspace.equilibrationTemperatureC"] ?? "Oven incubation temperature"}
              slotProps={{ htmlInput: { min: 1, step: "any" } }}
              fullWidth
            />
            <TextField
              size="small"
              label="Equilibration Time (min) *"
              type="number"
              value={headspaceEquilibrationMin}
              onChange={(e) => onHeadspaceEquilibrationMinChange(e.target.value)}
              required
              placeholder="e.g. 45 or 60"
              error={Boolean(errors["headspace.equilibrationMin"])}
              helperText={errors["headspace.equilibrationMin"] ?? "Vial incubation hold time"}
              slotProps={{ htmlInput: { min: 0.1, step: "any" } }}
              fullWidth
            />
            <TextField
              size="small"
              label="Transfer Line Temp (°C) *"
              type="number"
              value={headspaceTransferLineTemperatureC}
              onChange={(e) => onHeadspaceTransferLineTemperatureCChange(e.target.value)}
              required
              placeholder="e.g. 90 or 105"
              error={Boolean(errors["headspace.transferLineTemperatureC"])}
              helperText={errors["headspace.transferLineTemperatureC"] ?? "Sample transfer line temperature"}
              slotProps={{ htmlInput: { min: 1, step: "any" } }}
              fullWidth
            />
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
