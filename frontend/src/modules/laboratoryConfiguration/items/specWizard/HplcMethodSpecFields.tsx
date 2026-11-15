import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import type {
  HplcMethodAnalyteResponse,
  HplcMethodImpurityResponse
} from "../../masterDataSimple/services/HplcMethodService";
import type {
  RelatedSubstanceRow,
  ResultBasis
} from "../../specifications/services/SpecificationService";
import type { SpecFormModel } from "./specFormModel";
import { hplcParameterName } from "./specHelpers";

interface RelatedSubstanceProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  methodImpurities: HplcMethodImpurityResponse[];
  loadingMethodAnalytes: boolean;
}

/**
 * Related Substances (HPLC) row, impurity picker, and fixed basis fields.
 */
export function RelatedSubstanceMethodFields({
  model,
  set,
  errors,
  methodImpurities,
  loadingMethodAnalytes
}: RelatedSubstanceProps) {
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
        Related Substances (HPLC) Specification
      </Typography>
      <Stack spacing={2}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: model.relatedSubstanceRow === "Named" ? "1fr 1fr 1fr" : "1fr 1fr"
            },
            gap: 2
          }}
        >
          <FormControl size="small" fullWidth required error={Boolean(errors.relatedSubstanceRow)}>
            <InputLabel id="rs-row-label">Row *</InputLabel>
            <Select
              name="relatedSubstanceRow"
              labelId="rs-row-label"
              label="Row *"
              value={model.relatedSubstanceRow}
              onChange={(e) => {
                const val = e.target.value as RelatedSubstanceRow;
                const patch: Partial<SpecFormModel> = { relatedSubstanceRow: val };
                if (val === "UnspecifiedEach") {
                  patch.hplcMethodImpurityId = "";
                  patch.parameterName = "Any unspecified impurity";
                } else if (val === "Total") {
                  patch.hplcMethodImpurityId = "";
                  patch.parameterName = "Total impurities";
                } else if (val === "TotalOthers") {
                  patch.hplcMethodImpurityId = "";
                  patch.parameterName = "Total of others";
                } else if (val === "Named" && model.hplcMethodImpurityId) {
                  const imp = methodImpurities.find((i) => i.id === model.hplcMethodImpurityId);
                  if (imp) patch.parameterName = imp.name;
                }
                set(patch);
              }}
            >
              <MenuItem value="Named">Named impurity</MenuItem>
              <MenuItem value="UnspecifiedEach">Any unspecified impurity</MenuItem>
              <MenuItem value="Total">Total impurities</MenuItem>
              <MenuItem value="TotalOthers">Total of others</MenuItem>
            </Select>
            {errors.relatedSubstanceRow && (
              <FormHelperText>{errors.relatedSubstanceRow}</FormHelperText>
            )}
          </FormControl>

          {model.relatedSubstanceRow === "Named" && (
            <FormControl
              size="small"
              fullWidth
              required
              error={Boolean(errors.hplcMethodImpurityId)}
            >
              <InputLabel id="rs-impurity-picker-label">Impurity *</InputLabel>
              <Select
                name="hplcMethodImpurityId"
                labelId="rs-impurity-picker-label"
                label="Impurity *"
                value={model.hplcMethodImpurityId}
                onChange={(e) => {
                  const id = Number(e.target.value);
                  const imp = methodImpurities.find((i) => i.id === id);
                  set({
                    hplcMethodImpurityId: id,
                    ...(imp ? { parameterName: imp.name } : {})
                  });
                }}
                disabled={loadingMethodAnalytes}
              >
                {loadingMethodAnalytes ? (
                  <MenuItem disabled value="">
                    <em>Loading impurities...</em>
                  </MenuItem>
                ) : methodImpurities.length === 0 ? (
                  <MenuItem disabled value="">
                    <em>No impurities configured in HPLC method</em>
                  </MenuItem>
                ) : (
                  methodImpurities.map((imp) => (
                    <MenuItem key={imp.id} value={imp.id}>
                      {imp.name}
                      {imp.relativeRetention != null ? ` (RRT ${imp.relativeRetention})` : ""}
                    </MenuItem>
                  ))
                )}
              </Select>
              {errors.hplcMethodImpurityId && (
                <FormHelperText>{errors.hplcMethodImpurityId}</FormHelperText>
              )}
            </FormControl>
          )}

          <TextField
            size="small"
            label="Result Basis *"
            value="% as is"
            slotProps={{ input: { readOnly: true } }}
            helperText="Fixed basis for related substances"
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}

interface HplcAssayProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  methodAnalytes: HplcMethodAnalyteResponse[];
  loadingMethodAnalytes: boolean;
  assignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>;
}

/**
 * HPLC Method Assay analyte, result basis, and label claim fields.
 */
export function HplcAssayMethodFields({
  model,
  set,
  errors,
  methodAnalytes,
  loadingMethodAnalytes,
  assignedTests
}: HplcAssayProps) {
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
        HPLC Method Assay Specification
      </Typography>
      <Stack spacing={2}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <FormControl size="small" fullWidth required error={Boolean(errors.hplcMethodAnalyteId)}>
            <InputLabel id="hplc-method-analyte-label">Analyte *</InputLabel>
            <Select
              name="hplcMethodAnalyteId"
              labelId="hplc-method-analyte-label"
              label="Analyte *"
              value={model.hplcMethodAnalyteId}
              onChange={(e) => {
                const id = Number(e.target.value);
                const chosen = methodAnalytes.find((a) => a.id === id);
                const patch: Partial<SpecFormModel> = { hplcMethodAnalyteId: id };
                if (chosen) {
                  const prevMatchesAnalyte = methodAnalytes.some(
                    (a) =>
                      a.name === model.parameterName ||
                      hplcParameterName(a.name, "MgPerUnit") === model.parameterName
                  );
                  const prevMatchesTest = assignedTests.some(
                    (t) => t.displayName === model.parameterName || t.testCode === model.parameterName
                  );
                  if (!model.parameterName.trim() || prevMatchesAnalyte || prevMatchesTest) {
                    patch.parameterName = hplcParameterName(chosen.name, model.resultBasis);
                  }
                }
                set(patch);
              }}
              disabled={loadingMethodAnalytes}
            >
              {loadingMethodAnalytes ? (
                <MenuItem disabled value="">
                  <em>Loading method analytes...</em>
                </MenuItem>
              ) : methodAnalytes.length === 0 ? (
                <MenuItem disabled value="">
                  <em>No analytes found in HPLC method</em>
                </MenuItem>
              ) : (
                methodAnalytes.map((a) => (
                  <MenuItem key={a.id} value={a.id}>
                    {a.name}
                    {a.wavelengthNm != null ? ` (${a.wavelengthNm} nm)` : ""}
                  </MenuItem>
                ))
              )}
            </Select>
            {errors.hplcMethodAnalyteId && (
              <FormHelperText>{errors.hplcMethodAnalyteId}</FormHelperText>
            )}
          </FormControl>

          <FormControl size="small" fullWidth required error={Boolean(errors.resultBasis)}>
            <InputLabel id="hplc-result-basis-label">Basis *</InputLabel>
            <Select
              name="resultBasis"
              labelId="hplc-result-basis-label"
              label="Basis *"
              value={model.resultBasis}
              onChange={(e) => {
                const val = e.target.value as ResultBasis;
                const patch: Partial<SpecFormModel> = { resultBasis: val };
                const analyte = methodAnalytes.find((a) => a.id === model.hplcMethodAnalyteId);
                if (
                  analyte &&
                  (model.parameterName === analyte.name ||
                    model.parameterName === hplcParameterName(analyte.name, "MgPerUnit"))
                ) {
                  patch.parameterName = hplcParameterName(analyte.name, val);
                }
                if (val === "PercentLabelClaim") {
                  patch.unit = "%";
                } else if (val === "MgPerUnit" && model.labelClaimUnit) {
                  patch.unit = model.labelClaimUnit;
                }
                set(patch);
              }}
            >
              <MenuItem value="PercentLabelClaim">Assay %</MenuItem>
              <MenuItem value="MgPerUnit">Amount per unit</MenuItem>
              {model.itemCategory === "RawMaterial" && (
                <MenuItem value="PercentAsIs">% as is</MenuItem>
              )}
              {model.itemCategory === "RawMaterial" && (
                <MenuItem value="PercentDriedBasis">% dried basis</MenuItem>
              )}
              {model.itemCategory === "RawMaterial" && (
                <MenuItem value="PercentAnhydrousBasis">% anhydrous basis</MenuItem>
              )}
            </Select>
            {errors.resultBasis && <FormHelperText>{errors.resultBasis}</FormHelperText>}
          </FormControl>
        </Box>

        {model.resultBasis === "MgPerUnit" && (
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              name="labelClaim"
              label="Label Claim *"
              type="number"
              value={model.labelClaim}
              onChange={(e) => set({ labelClaim: e.target.value })}
              required
              error={Boolean(errors.labelClaim)}
              helperText={errors.labelClaim || "Target amount per unit"}
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              fullWidth
            />
            <TextField
              size="small"
              name="labelClaimUnit"
              label="Label Claim Unit *"
              value={model.labelClaimUnit}
              onChange={(e) => set({ labelClaimUnit: e.target.value, unit: e.target.value })}
              required
              error={Boolean(errors.labelClaimUnit)}
              placeholder="e.g. mg, g, mcg"
              helperText={errors.labelClaimUnit || "Unit for claim and result"}
              fullWidth
            />
          </Box>
        )}
      </Stack>
    </Box>
  );
}
