import React from "react";
import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  Typography,
  Stack
} from "@mui/material";
import type {
  IcpMethodResponse,
  IcpMethodElementResponse
} from "../../masterDataSimple/services/IcpMethodService";
import type { ResultBasis } from "../../specifications/services/SpecificationService";

export interface IcpSpecFieldsProps {
  method: IcpMethodResponse | null;
  loadingMethod: boolean;
  selectedElementId: number | "";
  onElementChange: (id: number, element: IcpMethodElementResponse | undefined) => void;
  resultBasis: ResultBasis | string | "";
  onBasisChange: (basis: ResultBasis) => void;
  labelClaim: string;
  onLabelClaimChange: (val: string) => void;
  labelClaimUnit: string;
  onLabelClaimUnitChange: (val: string) => void;
}

export const ICP_MINERAL_BASIS_OPTIONS: { value: ResultBasis; label: string }[] = [
  { value: "PercentLabelClaim", label: "% of label claim" },
  { value: "MgPerUnit", label: "mg per unit" }
];

export const icpParameterName = (symbol: string, basis: ResultBasis | string | ""): string =>
  basis === "MgPerUnit" ? `${symbol} (per unit)` : symbol;

export function IcpSpecFields({
  method,
  loadingMethod,
  selectedElementId,
  onElementChange,
  resultBasis,
  onBasisChange,
  labelClaim,
  onLabelClaimChange,
  labelClaimUnit,
  onLabelClaimUnitChange
}: IcpSpecFieldsProps) {
  const isImpurities = method?.mode === "ElementalImpurities";
  const elements = method?.elements ?? [];

  return (
    <Box
      sx={{
        border: "1px solid",
        borderColor: "primary.main",
        borderRadius: 1,
        p: 2,
        bgcolor: "action.hover"
      }}
    >
      <Typography
        variant="caption"
        sx={{
          fontWeight: 700,
          letterSpacing: "0.5px",
          color: "primary.main",
          textTransform: "uppercase",
          display: "block",
          mb: 1.5
        }}
      >
        {isImpurities ? "Elemental Impurities (ICP) Specification" : "Mineral Assay (ICP) Specification"}
      </Typography>

      <Stack spacing={2}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: isImpurities ? "1fr 1fr 1fr" : "1fr 1fr" },
            gap: 2
          }}
        >
          <FormControl size="small" fullWidth required>
            <InputLabel id="icp-spec-element-label">Element *</InputLabel>
            <Select
              labelId="icp-spec-element-label"
              label="Element *"
              value={selectedElementId}
              onChange={(e) => {
                const id = Number(e.target.value);
                const chosen = elements.find((el) => el.id === id);
                onElementChange(id, chosen);
              }}
              disabled={loadingMethod}
              inputProps={{ "aria-label": "Element" }}
            >
              {loadingMethod ? (
                <MenuItem disabled value="">
                  <em>Loading elements...</em>
                </MenuItem>
              ) : elements.length === 0 ? (
                <MenuItem disabled value="">
                  <em>{method ? "No elements configured in ICP method" : "No ICP method loaded"}</em>
                </MenuItem>
              ) : (
                elements.map((el) => (
                  <MenuItem key={el.id} value={el.id}>
                    {el.symbol} ({el.wavelengthNm} nm)
                  </MenuItem>
                ))
              )}
            </Select>
            <FormHelperText>Element and wavelength from ICP method</FormHelperText>
          </FormControl>

          {isImpurities ? (
            <>
              <TextField
                size="small"
                label="Result Basis *"
                value="µg/g"
                slotProps={{ input: { readOnly: true } }}
                helperText="Fixed basis for elemental impurities (MgPerKg)"
                fullWidth
              />
              <TextField
                size="small"
                label="Limit Type *"
                value="NMT (Not More Than)"
                slotProps={{ input: { readOnly: true } }}
                helperText="Fixed limit type for elemental impurities"
                fullWidth
              />
            </>
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="icp-spec-basis-label">Basis *</InputLabel>
              <Select
                labelId="icp-spec-basis-label"
                label="Basis *"
                value={resultBasis}
                onChange={(e) => onBasisChange(e.target.value as ResultBasis)}
                inputProps={{ "aria-label": "Basis" }}
              >
                {ICP_MINERAL_BASIS_OPTIONS.map((opt) => (
                  <MenuItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </MenuItem>
                ))}
              </Select>
              <FormHelperText>Assay basis for mineral elements</FormHelperText>
            </FormControl>
          )}
        </Box>

        {!isImpurities && (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
              gap: 2
            }}
          >
            <TextField
              size="small"
              label="Label Claim *"
              type="number"
              value={labelClaim}
              onChange={(e) => onLabelClaimChange(e.target.value)}
              required
              helperText="Target nominal claim (> 0)"
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              fullWidth
            />
            <TextField
              size="small"
              label="Label Claim Unit *"
              value={labelClaimUnit}
              onChange={(e) => onLabelClaimUnitChange(e.target.value)}
              required
              helperText="Unit for label claim (e.g. mg)"
              placeholder="e.g. mg"
              fullWidth
            />
          </Box>
        )}
      </Stack>
    </Box>
  );
}
