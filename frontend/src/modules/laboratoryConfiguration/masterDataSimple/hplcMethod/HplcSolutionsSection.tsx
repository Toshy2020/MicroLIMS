import {
  Box,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Button,
  TextField,
  IconButton,
  Tooltip,
  Chip,
  FormHelperText
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { ElutionMode } from "../services/HplcMethodService";
import { SolutionMaster } from "../services/SolutionMasterService";
import { MobilePhaseRowState, CHANNELS } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcSolutionsSectionProps {
  diluentSolutionId: number | "";
  mobilePhases: MobilePhaseRowState[];
  elutionMode: ElutionMode;
  availableDiluents: SolutionMaster[];
  availableMobilePhases: SolutionMaster[];
  errors: HplcMethodErrors;
  onDiluentSolutionIdChange: (val: number | "") => void;
  onMobilePhaseChange: (index: number, field: keyof MobilePhaseRowState, val: string | number) => void;
  onAddChannel: () => void;
  onRemoveChannel: (index: number) => void;
}

export function HplcSolutionsSection({
  diluentSolutionId,
  mobilePhases,
  elutionMode,
  availableDiluents,
  availableMobilePhases,
  errors,
  onDiluentSolutionIdChange,
  onMobilePhaseChange,
  onAddChannel,
  onRemoveChannel
}: HplcSolutionsSectionProps) {
  const isIsocratic = elutionMode === "Isocratic";
  const usedChannels = new Set(mobilePhases.map((p) => p.channel));
  const canAddChannel = mobilePhases.length < 4 && CHANNELS.some((c) => !usedChannels.has(c));

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        Solutions &amp; Solvent Channels
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Assign master diluent and mobile phase solutions for HPLC channels A through D.
      </Typography>

      <Stack spacing={2.5}>
        {/* Diluent Solution Picker */}
        <FormControl size="small" fullWidth required error={Boolean(errors["solutions.diluent"])}>
          <InputLabel id="diluent-solution-label">Diluent Solution *</InputLabel>
          <Select
            labelId="diluent-solution-label"
            label="Diluent Solution *"
            value={diluentSolutionId}
            onChange={(e) => onDiluentSolutionIdChange(Number(e.target.value) || "")}
          >
            {availableDiluents.length === 0 ? (
              <MenuItem disabled value="">
                <em>No diluent solution masters available in this section</em>
              </MenuItem>
            ) : (
              availableDiluents.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} {!s.isActive && "(Inactive)"} &middot; {s.shelfLifeValue} {s.shelfLifeUnit}
                </MenuItem>
              ))
            )}
          </Select>
          {errors["solutions.diluent"] && <FormHelperText>{errors["solutions.diluent"]}</FormHelperText>}
        </FormControl>

        {/* Mobile Phase Channels */}
        <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
            <div>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Mobile Phase Channels
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {isIsocratic
                  ? "Specify solvent masters for active channels. Ratios must sum to 100% if provided."
                  : "Assign solvent masters to channels A–D used in the gradient program."}
              </Typography>
            </div>
            <Button
              size="small"
              variant="outlined"
              startIcon={<AddIcon />}
              onClick={onAddChannel}
              disabled={!canAddChannel}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              Add Channel
            </Button>
          </Stack>

          <Stack spacing={1.5}>
            {mobilePhases.map((phase, idx) => {
              const rowError = errors[`solutions.${idx}.solutionId`] ?? errors[`solutions.${idx}.channel`];
              return (
              <Box
                key={idx}
                sx={{
                  display: "flex",
                  alignItems: "flex-start",
                  gap: 1.5,
                  p: 1.5,
                  bgcolor: "background.paper",
                  borderRadius: 1,
                  border: "1px solid",
                  borderColor: "divider",
                  flexWrap: { xs: "wrap", sm: "nowrap" }
                }}
              >
                <Chip
                  label={`Channel ${phase.channel}`}
                  color="primary"
                  variant="filled"
                  size="small"
                  sx={{ fontWeight: 700, minWidth: 90 }}
                />

                <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required error={Boolean(rowError)}>
                  <InputLabel id={`mp-solution-label-${idx}`}>Mobile Phase Solution *</InputLabel>
                  <Select
                    labelId={`mp-solution-label-${idx}`}
                    label="Mobile Phase Solution *"
                    value={phase.solutionMasterId}
                    onChange={(e) => onMobilePhaseChange(idx, "solutionMasterId", Number(e.target.value) || "")}
                  >
                    {availableMobilePhases.length === 0 ? (
                      <MenuItem disabled value="">
                        <em>No mobile phase solution masters available in this section</em>
                      </MenuItem>
                    ) : (
                      availableMobilePhases.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.name} {!s.isActive && "(Inactive)"}
                        </MenuItem>
                      ))
                    )}
                  </Select>
                  {rowError && <FormHelperText>{rowError}</FormHelperText>}
                </FormControl>

                {isIsocratic && (
                  <TextField
                    size="small"
                    label="Ratio %"
                    type="number"
                    value={phase.ratioPercent}
                    onChange={(e) => onMobilePhaseChange(idx, "ratioPercent", e.target.value)}
                    placeholder="e.g. 50"
                    error={Boolean(errors[`solutions.${idx}.ratioPercent`])}
                    helperText={errors[`solutions.${idx}.ratioPercent`]}
                    slotProps={{ htmlInput: { min: 0, max: 100, step: "any" } }}
                    sx={{ width: errors[`solutions.${idx}.ratioPercent`] ? 220 : 110 }}
                  />
                )}

                <Tooltip title="Remove channel">
                  <span>
                    <IconButton aria-label="Remove channel"
                      size="small"
                      onClick={() => onRemoveChannel(idx)}
                      disabled={mobilePhases.length <= 1}
                      color="error"
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              </Box>
              );
            })}
          </Stack>
        </Box>
      </Stack>
    </Box>
  );
}
