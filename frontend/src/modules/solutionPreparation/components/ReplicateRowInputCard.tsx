import {
  Box,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Paper
} from "@mui/material";
import { formatLabDate } from "../../../utils/formatDate";
import type { LotOption, SolutionPreparationListItem } from "../types";

export interface ReplicateRowState {
  standardMaterialId: number | null;
  standardWeightMg: string;
  referencePreparationId: number | null;
  referenceVolumeMl: string;
  titrantVolumeMl: string;
  blankMl: string;
}

interface Props {
  row: ReplicateRowState;
  index: number;
  isPrimary: boolean;
  blankRequired: boolean;
  standardLots: LotOption[];
  referenceOptions: SolutionPreparationListItem[];
  onChange: (patch: Partial<ReplicateRowState>) => void;
}

export function ReplicateRowInputCard({
  row,
  index,
  isPrimary,
  blankRequired,
  standardLots,
  referenceOptions,
  onChange
}: Props) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", display: "block", mb: 1.5 }}>
        Replicate {index + 1}
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: blankRequired ? "repeat(4, 1fr)" : "repeat(3, 1fr)" }, gap: 1.5 }}>
        {isPrimary ? (
          <FormControl size="small" fullWidth required>
            <InputLabel>Standard Lot</InputLabel>
            <Select
              label="Standard Lot"
              value={row.standardMaterialId ?? ""}
              onChange={(e) => onChange({ standardMaterialId: Number(e.target.value) || null })}
              inputProps={{ "aria-label": "Standard Lot" }}
            >
              {standardLots.map((lot) => (
                <MenuItem key={lot.materialId} value={lot.materialId} disabled={!lot.usable}>
                  {lot.batchNumber} (Exp: {lot.expiryDate ? formatLabDate(lot.expiryDate) : "—"}){!lot.usable ? ` — ${lot.reason}` : ""}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        ) : (
          <FormControl size="small" fullWidth required>
            <InputLabel>Reference Solution</InputLabel>
            <Select
              label="Reference Solution"
              value={row.referencePreparationId ?? ""}
              onChange={(e) => onChange({ referencePreparationId: Number(e.target.value) || null })}
              inputProps={{ "aria-label": "Reference Solution" }}
            >
              {referenceOptions.map((ref) => (
                <MenuItem key={ref.id} value={ref.id}>
                  {ref.code || `Prep #${ref.id}`} ({ref.preparedAt ? formatLabDate(ref.preparedAt) : "—"})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {isPrimary ? (
          <TextField
            size="small"
            label="Weight (mg)"
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.standardWeightMg}
            onChange={(e) => onChange({ standardWeightMg: e.target.value })}
            required
          />
        ) : (
          <TextField
            size="small"
            label="Ref Vol (mL)"
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.referenceVolumeMl}
            onChange={(e) => onChange({ referenceVolumeMl: e.target.value })}
            required
          />
        )}

        <TextField
          size="small"
          label="Titrant Vol (mL)"
          type="number"
          slotProps={{ htmlInput: { step: "any", min: "0" } }}
          value={row.titrantVolumeMl}
          onChange={(e) => onChange({ titrantVolumeMl: e.target.value })}
          required
        />

        {blankRequired && (
          <TextField
            size="small"
            label="Blank (mL)"
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.blankMl}
            onChange={(e) => onChange({ blankMl: e.target.value })}
            required
          />
        )}
      </Box>
    </Paper>
  );
}
