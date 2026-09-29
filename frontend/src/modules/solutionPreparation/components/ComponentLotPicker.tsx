import { useMemo } from "react";
import {
  Box,
  Card,
  CardContent,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  InputAdornment,
  FormHelperText,
  Alert,
  CircularProgress,
  Stack,
  Chip
} from "@mui/material";
import { formatLabDate } from "../../../utils/formatDate";
import type { SolutionPreparationComponentResponse, LotOption } from "../types";

interface Props {
  component: SolutionPreparationComponentResponse;
  lotOptions: LotOption[];
  loadingLots?: boolean;
  selectedMaterialId: number | null;
  quantityUsed: number | null | string;
  onSelectLot: (materialId: number | null) => void;
  onChangeQuantity: (quantity: number | null) => void;
  disabled?: boolean;
}

export function ComponentLotPicker({
  component,
  lotOptions,
  loadingLots = false,
  selectedMaterialId,
  quantityUsed,
  onSelectLot,
  onChangeQuantity,
  disabled = false
}: Props) {
  const selectedLot = useMemo(
    () => lotOptions.find((l) => l.materialId === selectedMaterialId),
    [lotOptions, selectedMaterialId]
  );

  const unitDisplay = selectedLot?.unit ?? component.lotUnit ?? "";
  const hasUsableLots = lotOptions.some((l) => l.usable);

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {component.order}. {component.entryName}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Code: {component.entryCode}
            </Typography>
          </Box>
          <Chip
            size="small"
            variant="outlined"
            label={`Recipe: ${component.recipeQuantity} ${component.recipeUnit}`}
            sx={{ fontWeight: 600, fontSize: 11 }}
          />
        </Box>

        <Stack spacing={2}>
          {loadingLots ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Loading lot options...
              </Typography>
            </Box>
          ) : lotOptions.length === 0 ? (
            <Alert severity="warning" sx={{ py: 0.5, fontSize: 12 }}>
              No stock lots found for {component.entryName}.
            </Alert>
          ) : !hasUsableLots ? (
            <Alert severity="error" sx={{ py: 0.5, fontSize: 12 }}>
              All available lots for this reagent are blocked.
            </Alert>
          ) : null}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "3fr 2fr" }, gap: 2 }}>
            <FormControl fullWidth size="small" disabled={disabled || loadingLots || lotOptions.length === 0}>
              <InputLabel id={`lot-select-label-${component.id}`}>Select Lot *</InputLabel>
              <Select
                labelId={`lot-select-label-${component.id}`}
                id={`lot-select-${component.id}`}
                value={selectedMaterialId ?? ""}
                label="Select Lot *"
                onChange={(e) => {
                  const val = e.target.value;
                  onSelectLot(val ? Number(val) : null);
                }}
              >
                <MenuItem value="">
                  <em>(Choose stock lot)</em>
                </MenuItem>
                {lotOptions.map((lot) => {
                  const expText = lot.expiryDate ? ` · Exp: ${formatLabDate(lot.expiryDate)}` : "";
                  const blockedText = !lot.usable ? ` [Blocked: ${lot.reason ?? "Not usable"}]` : "";
                  return (
                    <MenuItem key={lot.materialId} value={lot.materialId} disabled={!lot.usable}>
                      {lot.batchNumber} — {lot.quantityRemaining} {lot.unit} remaining{expText}{blockedText}
                    </MenuItem>
                  );
                })}
              </Select>
              {selectedLot && (
                <FormHelperText sx={{ fontSize: 11 }}>
                  Available in lot: {selectedLot.quantityRemaining} {selectedLot.unit}
                  {selectedLot.expiryDate && ` · Expires ${formatLabDate(selectedLot.expiryDate)}`}
                </FormHelperText>
              )}
            </FormControl>

            <TextField
              size="small"
              fullWidth
              label="Quantity Used"
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
              disabled={disabled || !selectedMaterialId}
              slotProps={{
                input: {
                  endAdornment: unitDisplay ? (
                    <InputAdornment position="end">{unitDisplay}</InputAdornment>
                  ) : undefined,
                  inputProps: { min: 0, step: "any" }
                }
              }}
              helperText={`Entered in lot's unit (${unitDisplay || "unit"})`}
            />
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
