import { useEffect, useState } from "react";
import { Box, MenuItem, Select, TextField, Typography } from "@mui/material";
import {
  EquipmentConfigurationService,
  type AutoclaveProgram
} from "../../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { masterDataOptions } from "../../../services/masterDataOptions";

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
}

export function AutoclaveSection({ value, onChange, disabled = false }: Props) {
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
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
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

        <TextField size="small" label="Load Type" value={value.loadType} slotProps={readOnly} />
        <TextField
          size="small"
          label="Temperature (°C)"
          value={value.temperature === "" ? "" : `${value.temperature} °C`}
          slotProps={readOnly}
        />
        <TextField
          size="small"
          label="Cycle Time (min)"
          value={value.cycleTime === "" ? "" : `${value.cycleTime} min`}
          slotProps={readOnly}
        />
        <TextField
          size="small"
          label="Cycle Number *"
          type="number"
          placeholder="e.g. 1"
          disabled={disabled}
          value={value.cycleNumber}
          onChange={(e) => {
            const v = e.target.value;
            onChange({ ...value, cycleNumber: v === "" ? "" : parseInt(v, 10) });
          }}
        />
      </Box>
    </Box>
  );
}
