import { useMemo } from "react";
import {
  Box,
  Grid,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Typography,
  CircularProgress
} from "@mui/material";

export interface StandardMaterialOption {
  id: number;
  materialName: string;
  batchNumber: string;
  materialMasterEntryId?: number | null;
  materialMasterEntryCode?: string | null;
  materialType?: string | null;
  code?: string | null;
}

const lotDisplay = (lot: StandardMaterialOption) =>
  lot.materialType === "WorkingStandard" && lot.code
    ? `${lot.code} (working standard)`
    : `${lot.materialName} - Lot ${lot.batchNumber}`;

export interface IcpCalibrationLotPickersProps {
  calibrationEntryId: number;
  calibrationEntryCode?: string | null;
  selectedCalLotId: number | null;
  onSelectCalLotId: (id: number | null) => void;

  requireIcv: boolean;
  icvEntryId?: number | null;
  icvEntryCode?: string | null;
  selectedIcvLotId: number | null;
  onSelectIcvLotId: (id: number | null) => void;

  usableLots: StandardMaterialOption[];
  loadingLots: boolean;
  disabled?: boolean;
}

export function IcpCalibrationLotPickers({
  calibrationEntryId,
  calibrationEntryCode,
  selectedCalLotId,
  onSelectCalLotId,
  requireIcv,
  icvEntryId,
  icvEntryCode,
  selectedIcvLotId,
  onSelectIcvLotId,
  usableLots,
  loadingLots,
  disabled = false
}: IcpCalibrationLotPickersProps) {
  // Matching lots for Calibration Standard
  const calLotOptions = useMemo(() => {
    const matching = usableLots.filter((lot) => lot.materialMasterEntryId === calibrationEntryId);
    if (selectedCalLotId && !matching.some((l) => l.id === selectedCalLotId)) {
      const selectedFromAll = usableLots.find((l) => l.id === selectedCalLotId);
      if (selectedFromAll) {
        matching.unshift(selectedFromAll);
      }
    }
    return matching;
  }, [usableLots, calibrationEntryId, selectedCalLotId]);

  // Matching lots for ICV Standard
  const icvLotOptions = useMemo(() => {
    if (!requireIcv || !icvEntryId) return [];
    const matching = usableLots.filter((lot) => lot.materialMasterEntryId === icvEntryId);
    if (selectedIcvLotId && !matching.some((l) => l.id === selectedIcvLotId)) {
      const selectedFromAll = usableLots.find((l) => l.id === selectedIcvLotId);
      if (selectedFromAll) {
        matching.unshift(selectedFromAll);
      }
    }
    return matching;
  }, [usableLots, requireIcv, icvEntryId, selectedIcvLotId]);

  return (
    <Grid container spacing={2}>
      {/* Calibration Standard Lot */}
      <Grid size={{ xs: 12, sm: requireIcv ? 6 : 12 }}>
        <FormControl fullWidth size="small" disabled={disabled || loadingLots}>
          <InputLabel id="cal-lot-select-label">
            Calibration Standard Lot ({calibrationEntryCode || `Entry #${calibrationEntryId}`})
          </InputLabel>
          <Select
            labelId="cal-lot-select-label"
            label={`Calibration Standard Lot (${calibrationEntryCode || `Entry #${calibrationEntryId}`})`}
            value={selectedCalLotId ?? ""}
            onChange={(e) => {
              const val = e.target.value as number | string;
              onSelectCalLotId(val === "" ? null : Number(val));
            }}
          >
            <MenuItem value="">
              <em>Select Reference Standard Lot</em>
            </MenuItem>
            {calLotOptions.map((lot) => (
              <MenuItem key={lot.id} value={lot.id}>
                {lotDisplay(lot)}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        {calLotOptions.length === 0 && !loadingLots && (
          <Typography variant="caption" sx={{ color: "error.main", mt: 0.5, display: "block" }}>
            No usable reference standard lots found for {calibrationEntryCode || `Entry #${calibrationEntryId}`}.
          </Typography>
        )}
      </Grid>

      {/* ICV Standard Lot */}
      {requireIcv && (
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small" disabled={disabled || loadingLots}>
            <InputLabel id="icv-lot-select-label">
              ICV Standard Lot ({icvEntryCode || `Entry #${icvEntryId}`})
            </InputLabel>
            <Select
              labelId="icv-lot-select-label"
              label={`ICV Standard Lot (${icvEntryCode || `Entry #${icvEntryId}`})`}
              value={selectedIcvLotId ?? ""}
              onChange={(e) => {
                const val = e.target.value as number | string;
                onSelectIcvLotId(val === "" ? null : Number(val));
              }}
            >
              <MenuItem value="">
                <em>Select ICV Standard Lot</em>
              </MenuItem>
              {icvLotOptions.map((lot) => (
                <MenuItem key={lot.id} value={lot.id}>
                  {lotDisplay(lot)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          {icvLotOptions.length === 0 && !loadingLots && (
            <Typography variant="caption" sx={{ color: "error.main", mt: 0.5, display: "block" }}>
              No usable reference standard lots found for {icvEntryCode || `Entry #${icvEntryId}`}.
            </Typography>
          )}
        </Grid>
      )}

      {loadingLots && (
        <Grid size={{ xs: 12 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <CircularProgress size={16} />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Loading usable reference standard lots...
            </Typography>
          </Box>
        </Grid>
      )}
    </Grid>
  );
}
