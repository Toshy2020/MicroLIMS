import { Box, FormControl, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../../i18n";
import type { ResultBasis } from "../../specifications/services/SpecificationService";

export function getTitrationBasisOptions(t: TFunction<"labConfig">): { value: ResultBasis; label: string }[] {
  return [
    { value: "PercentAsIs", label: t("items.titrationBasisFields.options.PercentAsIs") },
    { value: "PercentDriedBasis", label: t("items.titrationBasisFields.options.PercentDriedBasis") },
    { value: "PercentAnhydrousBasis", label: t("items.titrationBasisFields.options.PercentAnhydrousBasis") },
    { value: "PercentLabelClaim", label: t("items.titrationBasisFields.options.PercentLabelClaim") },
    { value: "MgPerUnit", label: t("items.titrationBasisFields.options.MgPerUnit") }
  ];
}

export const TITRATION_BASIS_OPTIONS: { value: ResultBasis; label: string }[] =
  getTitrationBasisOptions(i18n.getFixedT("en", "labConfig"));

// Label claim (mg per unit, finished products) is needed for these bases only.
export const titrationBasisNeedsLabelClaim = (basis: string): boolean =>
  basis === "PercentLabelClaim" || basis === "MgPerUnit";

// Client hint only; the server validates the specification.
export const validateTitrationSpecBasis = (
  basis: string,
  labelClaim: string,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): string | null => {
  if (!TITRATION_BASIS_OPTIONS.some((o) => o.value === basis)) {
    return t("items.titrationBasisFields.errors.selectBasis");
  }
  if (titrationBasisNeedsLabelClaim(basis)) {
    const n = Number(labelClaim);
    if (!labelClaim.trim() || isNaN(n) || n <= 0) {
      return t("items.titrationBasisFields.errors.labelClaimRequired");
    }
  }
  return null;
};

interface Props {
  // Label claim bases are offered only for finished products (the server enforces it).
  finishedProduct: boolean;
  resultBasis: string;
  labelClaim: string;
  onBasisChange: (b: ResultBasis) => void;
  onLabelClaimChange: (v: string) => void;
}

export function TitrationSpecBasisFields({ finishedProduct, resultBasis, labelClaim, onBasisChange, onLabelClaimChange }: Props) {
  const { t } = useTranslation("labConfig");
  const needsClaim = titrationBasisNeedsLabelClaim(resultBasis);
  const allOptions = getTitrationBasisOptions(t);
  const options = finishedProduct
    ? allOptions
    : allOptions.filter((o) => !titrationBasisNeedsLabelClaim(o.value));
  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: needsClaim ? "1fr 1fr" : "1fr" }, gap: 2 }}>
      <FormControl size="small" fullWidth required>
        <InputLabel id="titration-basis-label">{t("items.titrationBasisFields.resultBasisLabel")}</InputLabel>
        <Select
          labelId="titration-basis-label"
          label={t("items.titrationBasisFields.resultBasisLabel")}
          value={resultBasis}
          onChange={(e) => onBasisChange(e.target.value as ResultBasis)}
        >
          {options.map((o) => (
            <MenuItem key={o.value} value={o.value}>
              {o.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      {needsClaim && (
        <TextField
          size="small"
          label={t("items.titrationBasisFields.labelClaimLabel")}
          type="number"
          value={labelClaim}
          onChange={(e) => onLabelClaimChange(e.target.value)}
          required
          helperText={t("items.titrationBasisFields.finishedProductsOnly")}
          slotProps={{ htmlInput: { step: "any", min: "0" } }}
          fullWidth
        />
      )}
    </Box>
  );
}
