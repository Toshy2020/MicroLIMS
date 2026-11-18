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
import { useTranslation } from "../../../../i18n";
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
  const { t } = useTranslation("workingStandards");
  return (
    <>
      <FormControl component="fieldset">
        <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
          {t("newDialog.sourceOption")}
        </FormLabel>
        <RadioGroup
          row
          value={sourceMode}
          onChange={(e) => onSourceModeChange(e.target.value as "Manual" | "Received")}
        >
          <FormControlLabel value="Manual" control={<Radio size="small" />} label={t("newDialog.sourceModeManual")} />
          <FormControlLabel value="Received" control={<Radio size="small" />} label={t("newDialog.sourceModeReceived")} />
        </RadioGroup>
      </FormControl>

      {sourceMode === "Received" ? (
        <Autocomplete
          size="small"
          options={sourceSamples}
          value={sourceSamples.find((s) => s.sampleId === sourceSampleId) ?? null}
          getOptionLabel={(o) => `${o.referenceNumber} - ${o.materialName} (${o.batchNumber ?? t("newDialog.noBatch")})`}
          onChange={(_, val) => onSourceSampleChange(val)}
          renderInput={(params) => (
            <TextField {...params} label={t("newDialog.sourceSampleLabel")} placeholder={t("newDialog.sourceSamplePlaceholder")} required />
          )}
        />
      ) : (
        <Stack direction="row" spacing={2}>
          <TextField
            fullWidth
            size="small"
            label={t("newDialog.nameLabel")}
            value={sourceMaterialName}
            onChange={(e) => onSourceMaterialNameChange(e.target.value)}
            required
          />
          <TextField
            fullWidth
            size="small"
            label={t("newDialog.batchLabel")}
            value={sourceBatchNumber}
            onChange={(e) => onSourceBatchNumberChange(e.target.value)}
            required
          />
        </Stack>
      )}

      <FormControl fullWidth size="small" required>
        <InputLabel id="ws-master-entry-label">{t("newDialog.masterEntryLabel")}</InputLabel>
        <Select
          labelId="ws-master-entry-label"
          id="ws-master-entry-select"
          label={t("newDialog.masterEntryLabel")}
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
          label={t("newDialog.quantityLabel")}
          value={quantityGrams}
          onChange={(e) => onQuantityGramsChange(e.target.value)}
          required
        />
        <TextField
          fullWidth
          size="small"
          label={t("newDialog.locationLabel")}
          value={location}
          onChange={(e) => onLocationChange(e.target.value)}
          required
        />
      </Stack>
    </>
  );
};
