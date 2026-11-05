import { useMemo } from "react";
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { formatLabDate } from "../../../utils/formatDate";
import { approvalStatusLabel } from "../releaseState";
import type { SolutionPreparationComponentResponse, SourcePreparationOption } from "../types";

interface Props {
  component: SolutionPreparationComponentResponse;
  options: SourcePreparationOption[];
  loading?: boolean;
  selectedPreparationId: number | null;
  quantityUsed: number | null | string;
  onSelect: (preparationId: number | null) => void;
  onChangeQuantity: (quantity: number | null) => void;
  disabled?: boolean;
}

// A prepared-solution recipe line: the analyst picks the prepared bottle of the source
// recipe that was used. Nothing is deducted, so the quantity is optional. Unusable bottles
// stay listed but disabled, with the server's reason.
export function SourcePreparationPicker({
  component,
  options,
  loading = false,
  selectedPreparationId,
  quantityUsed,
  onSelect,
  onChangeQuantity,
  disabled = false
}: Props) {
  const selected = useMemo(
    () => options.find((o) => o.preparationId === selectedPreparationId),
    [options, selectedPreparationId]
  );
  const hasUsable = options.some((o) => o.usable);
  const recipeUnit = component.recipeUnit ?? "";

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {component.order}. {component.entryName}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Prepared solution
            </Typography>
          </Box>
          {component.recipeQuantity != null && (
            <Chip
              size="small"
              variant="outlined"
              label={`Recipe: ${component.recipeQuantity} ${recipeUnit}`}
              sx={{ fontWeight: 600, fontSize: 12 }}
            />
          )}
        </Box>

        <Stack spacing={2}>
          {loading ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Loading prepared solutions...
              </Typography>
            </Box>
          ) : options.length === 0 ? (
            <Alert severity="warning" sx={{ py: 0.5, fontSize: 12 }}>
              No prepared solutions found for {component.entryName}.
            </Alert>
          ) : !hasUsable ? (
            <Alert severity="error" sx={{ py: 0.5, fontSize: 12 }}>
              None of the prepared solutions can be used right now.
            </Alert>
          ) : null}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "3fr 2fr" }, gap: 2 }}>
            <FormControl fullWidth size="small" disabled={disabled || loading || options.length === 0}>
              <InputLabel id={`source-select-label-${component.id}`}>Select Prepared Solution *</InputLabel>
              <Select
                labelId={`source-select-label-${component.id}`}
                id={`source-select-${component.id}`}
                value={selectedPreparationId ?? ""}
                label="Select Prepared Solution *"
                onChange={(e) => {
                  const val = e.target.value;
                  onSelect(val ? Number(val) : null);
                }}
              >
                <MenuItem value="">
                  <em>(Choose prepared solution)</em>
                </MenuItem>
                {options.map((o) => {
                  const dates = [
                    o.preparedAt ? `Prepared ${formatLabDate(o.preparedAt)}` : null,
                    o.expiresAt ? `Exp ${formatLabDate(o.expiresAt)}` : null
                  ].filter(Boolean);
                  const release = approvalStatusLabel(o.approvalStatus);
                  return (
                    <MenuItem key={o.preparationId} value={o.preparationId} disabled={!o.usable}>
                      {o.code ?? `#${o.preparationId}`}
                      {dates.length > 0 ? ` · ${dates.join(" · ")}` : ""}
                      {release ? ` · ${release}` : ""}
                      {!o.usable ? ` [Blocked: ${o.reason ?? "Not usable"}]` : ""}
                    </MenuItem>
                  );
                })}
              </Select>
              {selected && (
                <FormHelperText sx={{ fontSize: 12 }}>
                  {selected.expiresAt ? `Expires ${formatLabDate(selected.expiresAt)}` : "No expiry date"}
                </FormHelperText>
              )}
            </FormControl>

            <TextField
              size="small"
              fullWidth
              label="Quantity Used (optional)"
              type="number"
              value={quantityUsed ?? ""}
              onChange={(e) => {
                const val = e.target.value;
                if (val === "") {
                  onChangeQuantity(null);
                } else {
                  const parsed = parseFloat(val);
                  onChangeQuantity(isNaN(parsed) ? null : parsed);
                }
              }}
              disabled={disabled || !selectedPreparationId}
              slotProps={{
                input: {
                  endAdornment: recipeUnit ? <InputAdornment position="end">{recipeUnit}</InputAdornment> : undefined,
                  inputProps: { min: 0, step: "any" }
                }
              }}
              helperText="Recorded only; not deducted from the prepared bottle"
            />
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
