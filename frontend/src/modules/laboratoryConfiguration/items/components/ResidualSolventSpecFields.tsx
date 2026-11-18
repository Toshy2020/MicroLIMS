import React from "react";
import { Box, FormControl, InputLabel, MenuItem, Select, TextField, Typography, Stack } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import type { HplcMethodAnalyteResponse } from "../../masterDataSimple/services/HplcMethodService";

interface ResidualSolventSpecFieldsProps {
  methodAnalytes: HplcMethodAnalyteResponse[];
  loadingMethodAnalytes: boolean;
  selectedAnalyteId: number | "";
  onAnalyteChange: (id: number, analyte: HplcMethodAnalyteResponse | undefined) => void;
}

export function ResidualSolventSpecFields({
  methodAnalytes,
  loadingMethodAnalytes,
  selectedAnalyteId,
  onAnalyteChange
}: ResidualSolventSpecFieldsProps) {
  const { t } = useTranslation("labConfig");

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
        {t("items.residualSolventFields.heading")}
      </Typography>

      <Stack spacing={2}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
            gap: 2
          }}
        >
          <FormControl size="small" fullWidth required>
            <InputLabel id="rs-solvent-select-label">{t("items.residualSolventFields.solventLabel")}</InputLabel>
            <Select
              labelId="rs-solvent-select-label"
              label={t("items.residualSolventFields.solventLabel")}
              value={selectedAnalyteId}
              onChange={(e) => {
                const id = Number(e.target.value);
                const chosen = methodAnalytes.find((a) => a.id === id);
                onAnalyteChange(id, chosen);
              }}
              disabled={loadingMethodAnalytes}
            >
              {loadingMethodAnalytes ? (
                <MenuItem disabled value="">
                  <em>{t("items.residualSolventFields.loading")}</em>
                </MenuItem>
              ) : methodAnalytes.length === 0 ? (
                <MenuItem disabled value="">
                  <em>{t("items.residualSolventFields.noSolvents")}</em>
                </MenuItem>
              ) : (
                methodAnalytes.map((a) => (
                  <MenuItem key={a.id} value={a.id}>
                    {a.name}
                  </MenuItem>
                ))
              )}
            </Select>
          </FormControl>

          <TextField
            size="small"
            label={t("items.residualSolventFields.resultBasisLabel")}
            value="ppm"
            slotProps={{ input: { readOnly: true } }}
            helperText={t("items.residualSolventFields.resultBasisHelper")}
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
