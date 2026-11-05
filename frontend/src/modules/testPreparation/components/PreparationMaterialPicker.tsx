import { useEffect, useMemo, useState } from "react";
import { Box, MenuItem, TextField } from "@mui/material";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { SolutionMasterService } from "../../laboratoryConfiguration/masterDataSimple/services/SolutionMasterService";
import { MaterialMasterService } from "../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import type { PreparationMaterialSource } from "../services/ItemPreparationConfigurationService";
import {
  SOURCE_LABELS,
  type PreparationMaterialSelection
} from "../preparationMaterial";

interface MasterOption {
  id: number;
  label: string;
}

// Solution recipes a diluent or neutralizer can be made from.
const SOLUTION_TYPES = ["Diluent", "Reagent", "Neutralizer"];

interface Props {
  // "Diluent" or "Neutralizer" - used for the field labels.
  label: string;
  value: PreparationMaterialSelection;
  onChange: (next: PreparationMaterialSelection) => void;
  // The neutralizer may be contained in the medium (nothing to pick).
  allowContainedInMedium?: boolean;
  disabled?: boolean;
  // Name of the currently saved master, shown if it is no longer in the active list.
  currentName?: string | null;
}

async function loadMasters(source: PreparationMaterialSource): Promise<MasterOption[]> {
  if (source === "MediaProduct") {
    const rows: any[] = (await masterDataOptions.getMediaProducts()) ?? [];
    return rows.filter((p) => p.isActive).map((p) => ({ id: p.id, label: `${p.name} (${p.code})` }));
  }
  if (source === "SolutionRecipe") {
    const rows = await SolutionMasterService.getAll(undefined, true);
    return rows
      .filter((s) => s.isActive && SOLUTION_TYPES.includes(s.type))
      .map((s) => ({ id: s.id, label: `${s.name} (${s.type})` }));
  }
  if (source === "MaterialEntry") {
    const rows = await MaterialMasterService.getAll(undefined, true);
    return rows.filter((m) => m.isActive).map((m) => ({ id: m.id, label: `${m.name} (${m.code})` }));
  }
  return [];
}

// Source select plus the master picker for that source. Used by the Item
// master and the manual first entry; the lot itself is picked later by
// PreparationLotPicker.
export function PreparationMaterialPicker({
  label, value, onChange, allowContainedInMedium = false, disabled, currentName
}: Props) {
  const [options, setOptions] = useState<MasterOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState(false);

  const source = value.source;
  const needsMaster = source !== "" && source !== "ContainedInMedium";

  useEffect(() => {
    if (!needsMaster) {
      setOptions([]);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setLoadError(false);
    loadMasters(source as PreparationMaterialSource)
      .then((o) => { if (!cancelled) setOptions(o); })
      .catch(() => { if (!cancelled) { setOptions([]); setLoadError(true); } })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [source, needsMaster]);

  // Keep a saved-but-now-inactive master selectable so the value never dangles.
  const shown = useMemo(() => {
    if (value.masterId != null && !options.some((o) => o.id === value.masterId)) {
      return [...options, { id: value.masterId, label: currentName ?? `#${value.masterId}` }];
    }
    return options;
  }, [options, value.masterId, currentName]);

  const sources = (Object.keys(SOURCE_LABELS) as PreparationMaterialSource[])
    .filter((s) => s !== "ContainedInMedium" || allowContainedInMedium);

  return (
    <Box sx={{ display: "grid", gap: 1.5 }}>
      <TextField
        select
        required
        size="small"
        label={`${label} source`}
        value={source}
        disabled={disabled}
        onChange={(e) => onChange({ source: e.target.value as PreparationMaterialSource, masterId: null })}
      >
        {sources.map((s) => (
          <MenuItem key={s} value={s}>{SOURCE_LABELS[s]}</MenuItem>
        ))}
      </TextField>

      {needsMaster && (
        <TextField
          select
          required
          size="small"
          label={label}
          value={value.masterId ?? ""}
          disabled={disabled || loading}
          error={loadError}
          helperText={loadError ? "Could not load the list." : loading ? "Loading…" : undefined}
          onChange={(e) => onChange({ source, masterId: Number(e.target.value) })}
        >
          {shown.map((o) => (
            <MenuItem key={o.id} value={o.id}>{o.label}</MenuItem>
          ))}
        </TextField>
      )}
    </Box>
  );
}
