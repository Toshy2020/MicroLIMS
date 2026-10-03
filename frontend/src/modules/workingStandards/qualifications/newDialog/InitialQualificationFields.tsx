import React from "react";
import {
  FormControl,
  FormControlLabel,
  FormLabel,
  InputLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Select,
  Stack,
  TextField,
  Autocomplete
} from "@mui/material";
import type { MaterialMasterEntry } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import type { EligibleSourceSampleDto } from "../../types";

interface InitialQualificationFieldsProps {
  sourceMode: "Manual" | "Received";
  onSourceModeChange: (mode: "Manual" | "Received") => void;
  sourceSampleId: number | null;
  sourceSamples: EligibleSourceSampleDto[];
  onSourceSampleChange: (sample: EligibleSourceSampleDto | null) => void;
  sourceMaterialName: string;
  onSourceMaterialNameChange: (val: string) => void;
  sourceBatchNumber: string;
  onSourceBatchNumberChange: (val: string) => void;
  materialMasterEntryId: number | "";
  onMaterialMasterEntryIdChange: (val: number) => void;
  masterEntries: MaterialMasterEntry[];
  quantityGrams: string;
  onQuantityGramsChange: (val: string) => void;
  location: string;
  onLocationChange: (val: string) => void;
}

export const InitialQualificationFields: React.FC<InitialQualificationFieldsProps> = ({
  sourceMode,
  onSourceModeChange,
  sourceSampleId,
  sourceSamples,
  onSourceSampleChange,
  sourceMaterialName,
  onSourceMaterialNameChange,
  sourceBatchNumber,
  onSourceBatchNumberChange,
  materialMasterEntryId,
  onMaterialMasterEntryIdChange,
  masterEntries,
  quantityGrams,
  onQuantityGramsChange,
  location,
  onLocationChange
}) => {
  return (
    <>
      <FormControl component="fieldset">
        <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
          Source Option
        </FormLabel>
        <RadioGroup
          row
          value={sourceMode}
          onChange={(e) => onSourceModeChange(e.target.value as "Manual" | "Received")}
        >
          <FormControlLabel value="Manual" control={<Radio size="small" />} label="Manual" />
          <FormControlLabel value="Received" control={<Radio size="small" />} label="Received sample" />
        </RadioGroup>
      </FormControl>

      {sourceMode === "Received" ? (
        <Autocomplete
          size="small"
          options={sourceSamples}
          value={sourceSamples.find((s) => s.sampleId === sourceSampleId) ?? null}
          getOptionLabel={(o) => `${o.referenceNumber} - ${o.materialName} (${o.batchNumber ?? "No batch"})`}
          onChange={(_, val) => onSourceSampleChange(val)}
          renderInput={(params) => (
            <TextField {...params} label="Source Sample" placeholder="Search received samples..." required />
          )}
        />
      ) : (
        <Stack direction="row" spacing={2}>
          <TextField
            fullWidth
            size="small"
            label="Name"
            value={sourceMaterialName}
            onChange={(e) => onSourceMaterialNameChange(e.target.value)}
            required
          />
          <TextField
            fullWidth
            size="small"
            label="Batch"
            value={sourceBatchNumber}
            onChange={(e) => onSourceBatchNumberChange(e.target.value)}
            required
          />
        </Stack>
      )}

      <FormControl fullWidth size="small" required>
        <InputLabel id="ws-master-entry-label">Master Entry</InputLabel>
        <Select
          labelId="ws-master-entry-label"
          id="ws-master-entry-select"
          label="Master Entry"
          value={materialMasterEntryId}
          onChange={(e) => onMaterialMasterEntryIdChange(Number(e.target.value))}
        >
          {masterEntries.map((entry) => (
            <MenuItem key={entry.id} value={entry.id}>
              {entry.name} ({entry.code})
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <Stack direction="row" spacing={2}>
        <TextField
          fullWidth
          size="small"
          type="number"
          label="Quantity (g)"
          value={quantityGrams}
          onChange={(e) => onQuantityGramsChange(e.target.value)}
          required
        />
        <TextField
          fullWidth
          size="small"
          label="Location"
          value={location}
          onChange={(e) => onLocationChange(e.target.value)}
          required
        />
      </Stack>
    </>
  );
};
