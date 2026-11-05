import { useMemo } from "react";
import { Box, FormControl, InputLabel, Select, MenuItem, Typography, CircularProgress } from "@mui/material";

export interface StandardMaterialOption {
  id: number;
  materialName: string;
  batchNumber: string;
  materialMasterEntryId?: number | null;
  materialMasterEntryCode?: string | null;
  materialType?: string | null;
  code?: string | null;
}

const lotDisplay = (lot: StandardMaterialOption) => `${lot.materialName} - Lot ${lot.batchNumber}`;

export interface IcpCalibrationLotPickerProps {
  selectedLotId: number | null;
  onSelectLotId: (id: number | null) => void;
  usableLots: StandardMaterialOption[];
  loadingLots: boolean;
  disabled?: boolean;
}

// The calibration standard is any usable reference standard lot of the laboratory (the method master that
// used to pin a specific standard is gone). The server enforces the same rule.
export function IcpCalibrationLotPicker({
  selectedLotId,
  onSelectLotId,
  usableLots,
  loadingLots,
  disabled = false
}: IcpCalibrationLotPickerProps) {
  const options = useMemo(
    () => usableLots.filter((lot) => !lot.materialType || lot.materialType === "ReferenceStandard" || lot.id === selectedLotId),
    [usableLots, selectedLotId]
  );

  return (
    <Box>
      <FormControl fullWidth size="small" disabled={disabled || loadingLots}>
        <InputLabel id="cal-lot-select-label">Calibration standard lot</InputLabel>
        <Select
          labelId="cal-lot-select-label"
          label="Calibration standard lot"
          value={selectedLotId ?? ""}
          onChange={(e) => {
            const val = e.target.value as number | string;
            onSelectLotId(val === "" ? null : Number(val));
          }}
        >
          <MenuItem value="">
            <em>Select reference standard lot</em>
          </MenuItem>
          {options.map((lot) => (
            <MenuItem key={lot.id} value={lot.id}>
              {lotDisplay(lot)}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      {loadingLots && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mt: 0.5 }}>
          <CircularProgress size={16} />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Loading usable reference standard lots...
          </Typography>
        </Box>
      )}
      {options.length === 0 && !loadingLots && (
        <Typography variant="caption" sx={{ color: "error.main", mt: 0.5, display: "block" }}>
          No usable reference standard lots found.
        </Typography>
      )}
    </Box>
  );
}
