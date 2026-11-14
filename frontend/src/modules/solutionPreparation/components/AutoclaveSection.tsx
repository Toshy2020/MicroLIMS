import { useEffect, useState } from "react";
import { Box, FormControl, FormHelperText, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import {
  EquipmentConfigurationService,
  type AutoclaveProgram
} from "../../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { ScanField } from "../../../components/labels/ScanField";
import { LastChoiceField } from "../../../components/entry/LastChoiceField";
import { NumericEntryField } from "../../../components/bench/NumericEntryField";

// What the analyst records for a recipe that needs sterilization. Program, load type,
// temperature and cycle time come from the configured autoclave program (as in Media
// Preparation); the cycle number is typed in. The backend checks the record.
export interface AutoclaveFormState {
  equipmentId: number | "";
  program: string;
  loadType: string;
  temperature: number | "";
  cycleTime: number | "";
  cycleNumber: number | "";
}

export const EMPTY_AUTOCLAVE: AutoclaveFormState = {
  equipmentId: "",
  program: "",
  loadType: "",
  temperature: "",
  cycleTime: "",
  cycleNumber: ""
};

export const programLabel = (p: AutoclaveProgram) => `${p.programCode}: ${p.programName}`;

interface AutoclaveOption {
  id: number;
  code?: string;
  name: string;
}

interface Props {
  value: AutoclaveFormState;
  onChange: (next: AutoclaveFormState) => void;
  disabled?: boolean;
  fieldErrors?: Partial<Record<"autoclaveEquipmentId" | "autoclaveProgram" | "autoclaveCycleNumber", string>>;
  equipmentIsDefault?: boolean;
  onEquipmentChangedByUser?: () => void;
}

export function AutoclaveSection({
  value,
  onChange,
  disabled = false,
  fieldErrors,
  equipmentIsDefault = false,
  onEquipmentChangedByUser
}: Props) {
  const [autoclaves, setAutoclaves] = useState<AutoclaveOption[]>([]);
  const [programs, setPrograms] = useState<AutoclaveProgram[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    masterDataOptions
      .getEquipment("Autoclave")
      .then((list: AutoclaveOption[]) => {
        if (active) setAutoclaves(Array.isArray(list) ? list : []);
      })
      .catch(() => {
        if (active) setLoadError("Could not load the autoclave list.");
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    if (!value.equipmentId) {
      setPrograms([]);
      return;
    }
    EquipmentConfigurationService.getAutoclavePrograms(Number(value.equipmentId), true)
      .then((list) => {
        if (active) setPrograms(Array.isArray(list) ? list : []);
      })
      .catch(() => {
        if (active) setPrograms([]);
      });
    return () => {
      active = false;
    };
  }, [value.equipmentId]);

  const selectedProgramId = programs.find((p) => programLabel(p) === value.program)?.id ?? "";

  const handleEquipment = (id: number | "") => {
    onEquipmentChangedByUser?.();
    onChange({ ...EMPTY_AUTOCLAVE, cycleNumber: value.cycleNumber, equipmentId: id });
  };

  const handleProgram = (id: number | "") => {
    const prog = programs.find((p) => p.id === Number(id));
    onChange(
      prog
        ? {
            ...value,
            program: programLabel(prog),
            loadType: prog.loadType,
            temperature: prog.temperature,
            cycleTime: prog.cycleTimeMinutes
          }
        : { ...value, program: "", loadType: "", temperature: "", cycleTime: "" }
    );
  };

  const readOnly = { input: { readOnly: true } };

  return (
    <Box data-testid="autoclave-section">
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
        Autoclave
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
        This recipe is sterilized. Select the autoclave and its configured program; load type, temperature and
        cycle time come from Laboratory Configuration.
      </Typography>
      {loadError && (
        <Typography variant="caption" color="error" sx={{ display: "block", mb: 1 }}>
          {loadError}
        </Typography>
      )}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, alignItems: "start" }}>
        <Stack spacing={1}>
          <ScanField
            label="Scan autoclave"
            expectedType="EquipmentAsset"
            offeredIds={autoclaves.map((a) => a.id)}
            onSelect={(id) => handleEquipment(id)}
            disabled={disabled}
          />
          <LastChoiceField kind="equipment" isDefault={equipmentIsDefault}>
            <FormControl fullWidth size="small" error={Boolean(fieldErrors?.autoclaveEquipmentId)}>
              <Select<number | "">
                size="small"
                displayEmpty
                disabled={disabled}
                value={value.equipmentId}
                onChange={(e) => handleEquipment(e.target.value === "" ? "" : Number(e.target.value))}
                inputProps={{ "aria-label": "Autoclave" }}
              >
                <MenuItem value="">
                  <em>Select Autoclave *</em>
                </MenuItem>
                {autoclaves.map((a) => (
                  <MenuItem key={a.id} value={a.id}>
                    {a.code ? `${a.code}: ` : ""}
                    {a.name}
                  </MenuItem>
                ))}
              </Select>
              {fieldErrors?.autoclaveEquipmentId && <FormHelperText>{fieldErrors.autoclaveEquipmentId}</FormHelperText>}
            </FormControl>
          </LastChoiceField>
        </Stack>

        <FormControl fullWidth size="small" error={Boolean(fieldErrors?.autoclaveProgram)}>
          <Select<number | "">
            size="small"
            displayEmpty
            disabled={disabled || !value.equipmentId || programs.length === 0}
            value={selectedProgramId}
            onChange={(e) => handleProgram(e.target.value === "" ? "" : Number(e.target.value))}
            inputProps={{ "aria-label": "Autoclave program" }}
          >
            <MenuItem value="">
              <em>{programs.length === 0 ? "No active programs for autoclave" : "Select Program / Load *"}</em>
            </MenuItem>
            {programs.map((p) => (
              <MenuItem key={p.id} value={p.id}>
                {programLabel(p)} ({p.temperature}°C, {p.cycleTimeMinutes} min)
              </MenuItem>
            ))}
          </Select>
          {fieldErrors?.autoclaveProgram && <FormHelperText>{fieldErrors.autoclaveProgram}</FormHelperText>}
        </FormControl>

        <TextField size="small" label="Load Type" value={value.loadType} slotProps={readOnly} />
        <NumericEntryField
          label="Temperature (°C)"
          kind="decimal"
          value={value.temperature === "" ? "" : String(value.temperature)}
          onChange={() => {}}
          readOnly
          unit="°C"
        />
        <NumericEntryField
          label="Cycle Time (min)"
          kind="decimal"
          value={value.cycleTime === "" ? "" : String(value.cycleTime)}
          onChange={() => {}}
          readOnly
          unit="min"
        />
        <NumericEntryField
          label="Cycle Number *"
          kind="count"
          disabled={disabled}
          value={value.cycleNumber === "" ? "" : String(value.cycleNumber)}
          onChange={(v) => {
            onChange({ ...value, cycleNumber: v === "" ? "" : parseInt(v, 10) });
          }}
          error={fieldErrors?.autoclaveCycleNumber}
        />
      </Box>
    </Box>
  );
}
