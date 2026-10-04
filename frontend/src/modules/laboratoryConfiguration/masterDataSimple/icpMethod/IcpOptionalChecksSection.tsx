import {
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
  Typography,
  Switch,
  FormControlLabel,
  Paper,
  Stack,
  Alert
} from "@mui/material";
import type { MaterialMasterEntry } from "../services/MaterialMasterService";
import type { IcpMethodErrors } from "./icpMethodValidation";

export interface IcpOptionalChecksSectionProps {
  requireBlank: boolean;
  blankMaxMgPerL: string | number;
  requireIcv: boolean;
  icvStandardEntryId: number | "";
  icvNominalMgPerL: string | number;
  icvRecoveryLowPercent: string | number;
  icvRecoveryHighPercent: string | number;
  requireCcv: boolean;
  ccvNominalMgPerL: string | number;
  ccvRecoveryLowPercent: string | number;
  ccvRecoveryHighPercent: string | number;
  calibrationStandardEntryId: number | "";
  availableStandards: MaterialMasterEntry[];
  errors: IcpMethodErrors;
  onRequireBlankChange: (val: boolean) => void;
  onBlankMaxChange: (val: string) => void;
  onRequireIcvChange: (val: boolean) => void;
  onIcvStandardChange: (val: number | "") => void;
  onIcvNominalChange: (val: string) => void;
  onIcvRecoveryLowChange: (val: string) => void;
  onIcvRecoveryHighChange: (val: string) => void;
  onRequireCcvChange: (val: boolean) => void;
  onCcvNominalChange: (val: string) => void;
  onCcvRecoveryLowChange: (val: string) => void;
  onCcvRecoveryHighChange: (val: string) => void;
}

export function IcpOptionalChecksSection({
  requireBlank,
  blankMaxMgPerL,
  requireIcv,
  icvStandardEntryId,
  icvNominalMgPerL,
  icvRecoveryLowPercent,
  icvRecoveryHighPercent,
  requireCcv,
  ccvNominalMgPerL,
  ccvRecoveryLowPercent,
  ccvRecoveryHighPercent,
  calibrationStandardEntryId,
  availableStandards,
  errors,
  onRequireBlankChange,
  onBlankMaxChange,
  onRequireIcvChange,
  onIcvStandardChange,
  onIcvNominalChange,
  onIcvRecoveryLowChange,
  onIcvRecoveryHighChange,
  onRequireCcvChange,
  onCcvNominalChange,
  onCcvRecoveryLowChange,
  onCcvRecoveryHighChange
}: IcpOptionalChecksSectionProps) {
  // ICV must be a second source (cannot be the same standard as calibration)
  const icvAvailableStandards = availableStandards.filter(
    (s) => s.id !== calibrationStandardEntryId
  );

  return (
    <Stack spacing={2}>
      {/* 1. Method Blank Check */}
      <Paper
        variant="outlined"
        sx={{
          p: 2,
          borderRadius: 1.5,
          bgcolor: requireBlank ? "background.paper" : "action.hover"
        }}
      >
        <FormControlLabel
          control={
            <Switch
              checked={requireBlank}
              onChange={(e) => onRequireBlankChange(e.target.checked)}
              color="primary"
            />
          }
          label={
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Method Blank Check
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Enforce calibration blank evaluation prior to analytical sequence
              </Typography>
            </Box>
          }
        />

        {requireBlank && (
          <Box sx={{ mt: 2, maxWidth: 320 }}>
            <TextField
              size="small"
              label="Blank Limit (mg/L) *"
              placeholder="e.g. 0.05"
              type="number"
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              value={blankMaxMgPerL}
              onChange={(e) => onBlankMaxChange(e.target.value)}
              required
              error={Boolean(errors.blankMaxMgPerL)}
              helperText={errors.blankMaxMgPerL ?? "Maximum allowable concentration in blank solution"}
              fullWidth
            />
          </Box>
        )}
      </Paper>

      {/* 2. Initial Calibration Verification (ICV) Check */}
      <Paper
        variant="outlined"
        sx={{
          p: 2,
          borderRadius: 1.5,
          bgcolor: requireIcv ? "background.paper" : "action.hover"
        }}
      >
        <FormControlLabel
          control={
            <Switch
              checked={requireIcv}
              onChange={(e) => onRequireIcvChange(e.target.checked)}
              color="primary"
            />
          }
          label={
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Initial Calibration Verification (ICV / Second Source)
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Verify calibration curve accuracy against an independent second-source reference standard
              </Typography>
            </Box>
          }
        />

        {requireIcv && (
          <Box sx={{ mt: 2 }}>
            {errors.icvCheck && (
              <Alert severity="warning" sx={{ mb: 2, py: 0.5 }}>
                {errors.icvCheck}
              </Alert>
            )}

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr" },
                gap: 2,
                mb: 2
              }}
            >
              <FormControl size="small" fullWidth error={Boolean(errors.icvStandardEntryId)}>
                <InputLabel id="icv-std-picker-label">ICV Reference Standard (Second Source) *</InputLabel>
                <Select
                  labelId="icv-std-picker-label"
                  label="ICV Reference Standard (Second Source) *"
                  value={icvStandardEntryId}
                  onChange={(e) => onIcvStandardChange(Number(e.target.value) || "")}
                >
                  {icvAvailableStandards.length === 0 ? (
                    <MenuItem disabled value="">
                      <em>No alternate reference standards available</em>
                    </MenuItem>
                  ) : (
                    icvAvailableStandards.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.code} &middot; {s.name} {!s.isActive && "(Inactive)"}
                      </MenuItem>
                    ))
                  )}
                </Select>
                {errors.icvStandardEntryId && (
                  <FormHelperText>{errors.icvStandardEntryId}</FormHelperText>
                )}
              </FormControl>

              <TextField
                size="small"
                label="Nominal (mg/L) *"
                placeholder="e.g. 2.0"
                type="number"
                slotProps={{ htmlInput: { step: "any", min: "0" } }}
                value={icvNominalMgPerL}
                onChange={(e) => onIcvNominalChange(e.target.value)}
                required
                error={Boolean(errors.icvNominalMgPerL)}
                helperText={errors.icvNominalMgPerL ?? "Target concentration of ICV solution"}
                fullWidth
              />
            </Box>

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                gap: 2
              }}
            >
              <TextField
                size="small"
                label="Recovery Low Limit (%) *"
                placeholder="90"
                type="number"
                slotProps={{ htmlInput: { step: "0.1", min: "0" } }}
                value={icvRecoveryLowPercent}
                onChange={(e) => onIcvRecoveryLowChange(e.target.value)}
                required
                error={Boolean(errors.icvRecoveryLowPercent)}
                helperText={errors.icvRecoveryLowPercent ?? "Acceptance minimum % (e.g. 90%)"}
                fullWidth
              />

              <TextField
                size="small"
                label="Recovery High Limit (%) *"
                placeholder="110"
                type="number"
                slotProps={{ htmlInput: { step: "0.1", min: "0" } }}
                value={icvRecoveryHighPercent}
                onChange={(e) => onIcvRecoveryHighChange(e.target.value)}
                required
                error={Boolean(errors.icvRecoveryHighPercent)}
                helperText={errors.icvRecoveryHighPercent ?? "Acceptance maximum % (e.g. 110%)"}
                fullWidth
              />
            </Box>
          </Box>
        )}
      </Paper>

      {/* 3. Continuing Calibration Verification (CCV) Check */}
      <Paper
        variant="outlined"
        sx={{
          p: 2,
          borderRadius: 1.5,
          bgcolor: requireCcv ? "background.paper" : "action.hover"
        }}
      >
        <FormControlLabel
          control={
            <Switch
              checked={requireCcv}
              onChange={(e) => onRequireCcvChange(e.target.checked)}
              color="primary"
            />
          }
          label={
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Continuing Calibration Verification (CCV / Mid-check)
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Periodic drift check during and at the end of the analytical sequence
              </Typography>
            </Box>
          }
        />

        {requireCcv && (
          <Box sx={{ mt: 2 }}>
            {errors.ccvCheck && (
              <Alert severity="warning" sx={{ mb: 2, py: 0.5 }}>
                {errors.ccvCheck}
              </Alert>
            )}

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
                gap: 2
              }}
            >
              <TextField
                size="small"
                label="Nominal (mg/L) *"
                placeholder="e.g. 1.0"
                type="number"
                slotProps={{ htmlInput: { step: "any", min: "0" } }}
                value={ccvNominalMgPerL}
                onChange={(e) => onCcvNominalChange(e.target.value)}
                required
                error={Boolean(errors.ccvNominalMgPerL)}
                helperText={errors.ccvNominalMgPerL ?? "Target concentration"}
                fullWidth
              />

              <TextField
                size="small"
                label="Recovery Low Limit (%) *"
                placeholder="90"
                type="number"
                slotProps={{ htmlInput: { step: "0.1", min: "0" } }}
                value={ccvRecoveryLowPercent}
                onChange={(e) => onCcvRecoveryLowChange(e.target.value)}
                required
                error={Boolean(errors.ccvRecoveryLowPercent)}
                helperText={errors.ccvRecoveryLowPercent ?? "Acceptance min %"}
                fullWidth
              />

              <TextField
                size="small"
                label="Recovery High Limit (%) *"
                placeholder="110"
                type="number"
                slotProps={{ htmlInput: { step: "0.1", min: "0" } }}
                value={ccvRecoveryHighPercent}
                onChange={(e) => onCcvRecoveryHighChange(e.target.value)}
                required
                error={Boolean(errors.ccvRecoveryHighPercent)}
                helperText={errors.ccvRecoveryHighPercent ?? "Acceptance max %"}
                fullWidth
              />
            </Box>
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
