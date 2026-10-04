import React from "react";
import { Box, FormControl, InputLabel, MenuItem, Select, TextField, Typography, Stack } from "@mui/material";
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
        Residual Solvent (GC) Specification
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
            <InputLabel id="rs-solvent-select-label">Solvent *</InputLabel>
            <Select
              labelId="rs-solvent-select-label"
              label="Solvent *"
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
                  <em>Loading solvents...</em>
                </MenuItem>
              ) : methodAnalytes.length === 0 ? (
                <MenuItem disabled value="">
                  <em>No solvents configured in GC method</em>
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
            label="Result Basis *"
            value="ppm"
            slotProps={{ input: { readOnly: true } }}
            helperText="Fixed basis for residual solvents"
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
