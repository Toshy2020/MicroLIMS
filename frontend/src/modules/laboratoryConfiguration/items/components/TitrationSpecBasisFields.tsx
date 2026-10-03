import { Box, FormControl, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import type { ResultBasis } from "../../specifications/services/SpecificationService";

export const TITRATION_BASIS_OPTIONS: { value: ResultBasis; label: string }[] = [
  { value: "PercentAsIs", label: "% as is" },
  { value: "PercentDriedBasis", label: "% dried basis" },
  { value: "PercentAnhydrousBasis", label: "% anhydrous basis" },
  { value: "PercentLabelClaim", label: "% of label claim" },
  { value: "MgPerUnit", label: "mg per unit" }
];

// Label claim (mg per unit, finished products) is needed for these bases only.
export const titrationBasisNeedsLabelClaim = (basis: string): boolean =>
  basis === "PercentLabelClaim" || basis === "MgPerUnit";

// Client hint only; the server validates the specification.
export const validateTitrationSpecBasis = (basis: string, labelClaim: string): string | null => {
  if (!TITRATION_BASIS_OPTIONS.some((o) => o.value === basis)) return "Please select a result basis.";
  if (titrationBasisNeedsLabelClaim(basis)) {
    const n = Number(labelClaim);
    if (!labelClaim.trim() || isNaN(n) || n <= 0) return "Label claim (mg per unit) must be greater than 0 for this basis.";
  }
  return null;
};

interface Props {
  resultBasis: string;
  labelClaim: string;
  onBasisChange: (b: ResultBasis) => void;
  onLabelClaimChange: (v: string) => void;
}

export function TitrationSpecBasisFields({ resultBasis, labelClaim, onBasisChange, onLabelClaimChange }: Props) {
  const needsClaim = titrationBasisNeedsLabelClaim(resultBasis);
  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: needsClaim ? "1fr 1fr" : "1fr" }, gap: 2 }}>
      <FormControl size="small" fullWidth required>
        <InputLabel id="titration-basis-label">Result Basis *</InputLabel>
        <Select
          labelId="titration-basis-label"
          label="Result Basis *"
          value={resultBasis}
          onChange={(e) => onBasisChange(e.target.value as ResultBasis)}
        >
          {TITRATION_BASIS_OPTIONS.map((o) => (
            <MenuItem key={o.value} value={o.value}>
              {o.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      {needsClaim && (
        <TextField
          size="small"
          label="Label Claim (mg per unit) *"
          type="number"
          value={labelClaim}
          onChange={(e) => onLabelClaimChange(e.target.value)}
          required
          helperText="Finished products only"
          slotProps={{ htmlInput: { step: "any", min: "0" } }}
          fullWidth
        />
      )}
    </Box>
  );
}
