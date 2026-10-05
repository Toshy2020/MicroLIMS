import {
  Box,
  Typography,
  TextField,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  IconButton,
  Tooltip,
  Alert,
  TableContainer,
  Stack
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { GcOvenStepRowState } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface GcOvenProgramTableProps {
  steps: GcOvenStepRowState[];
  errors: HplcMethodErrors;
  onChange: (steps: GcOvenStepRowState[]) => void;
}

export function GcOvenProgramTable({ steps, errors, onChange }: GcOvenProgramTableProps) {
  const handleStepChange = (
    index: number,
    field: keyof GcOvenStepRowState,
    value: string | number
  ) => {
    const updated = steps.map((step, i) => {
      if (i === index) {
        return { ...step, [field]: value };
      }
      return step;
    });
    onChange(updated);
  };

  const handleAddStep = () => {
    const last = steps[steps.length - 1];
    const prevTemp = last ? Number(last.temperatureC || 0) : 100;
    const nextTemp = prevTemp > 0 ? prevTemp + 50 : 150;
    onChange([
      ...steps,
      {
        rateCPerMin: "10",
        temperatureC: String(nextTemp),
        holdMin: "2"
      }
    ]);
  };

  const handleRemoveStep = (index: number) => {
    if (steps.length <= 1) return;
    onChange(steps.filter((_, i) => i !== index));
  };

  return (
    <Box
      sx={{
        p: 2,
        bgcolor: "action.hover",
        borderRadius: 1.5,
        border: "1px solid",
        borderColor: "divider"
      }}
    >
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <div>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
            Column Oven Temperature Program
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Step 1 specifies the initial temperature (no ramp rate). Subsequent steps ramp at a set rate (°C/min) to target temperature with a dwell/hold time.
          </Typography>
        </div>
        <Button
          size="small"
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={handleAddStep}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          Add Ramp Step
        </Button>
      </Stack>

      {errors["form.ovenSteps"] && (
        <Alert severity="error" sx={{ mb: 1.5, py: 0.5, fontSize: 12 }}>
          {errors["form.ovenSteps"]}
        </Alert>
      )}

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 600, width: 70 }}>Step</TableCell>
              <TableCell sx={{ fontWeight: 600, minWidth: 140 }}>Ramp Rate (°C/min)</TableCell>
              <TableCell sx={{ fontWeight: 600, minWidth: 140 }}>Temperature (°C) *</TableCell>
              <TableCell sx={{ fontWeight: 600, minWidth: 140 }}>Hold Time (min) *</TableCell>
              <TableCell align="right" sx={{ width: 60 }} />
            </TableRow>
          </TableHead>
          <TableBody>
            {steps.map((step, idx) => {
              const isInitial = idx === 0;
              const rateErr = errors[`oven.${idx}.rateCPerMin`];
              const tempErr = errors[`oven.${idx}.temperatureC`];
              const holdErr = errors[`oven.${idx}.holdMin`];

              return (
                <TableRow key={idx}>
                  <TableCell sx={{ fontWeight: 600 }}>
                    {isInitial ? "1 (Initial)" : idx + 1}
                  </TableCell>
                  <TableCell>
                    {isInitial ? (
                      <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic", pl: 1 }}>
                        Initial (no ramp)
                      </Typography>
                    ) : (
                      <TextField
                        size="small"
                        type="number"
                        placeholder="e.g. 10"
                        value={step.rateCPerMin}
                        onChange={(e) => handleStepChange(idx, "rateCPerMin", e.target.value)}
                        error={Boolean(rateErr)}
                        helperText={rateErr}
                        slotProps={{ htmlInput: { min: 0.1, step: "any" } }}
                        sx={{ maxWidth: 130 }}
                      />
                    )}
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      placeholder={isInitial ? "e.g. 40" : "e.g. 240"}
                      value={step.temperatureC}
                      onChange={(e) => handleStepChange(idx, "temperatureC", e.target.value)}
                      error={Boolean(tempErr)}
                      helperText={tempErr}
                      slotProps={{ htmlInput: { min: 1, step: "any" } }}
                      sx={{ maxWidth: 130 }}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      placeholder="e.g. 5"
                      value={step.holdMin}
                      onChange={(e) => handleStepChange(idx, "holdMin", e.target.value)}
                      error={Boolean(holdErr)}
                      helperText={holdErr}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ maxWidth: 130 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title={isInitial ? "Cannot delete initial temperature step" : "Delete step"}>
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={isInitial || steps.length <= 1}
                          onClick={() => handleRemoveStep(idx)}
                          aria-label={`Delete oven step ${idx + 1}`}
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
  );
}
