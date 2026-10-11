import { useEffect, useState } from "react";
import { Box, FormControl, InputLabel, MenuItem, Select, TextField, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { EquipmentInventoryService } from "../modules/inventory/equipment/services/EquipmentInventoryService";
import { useLaboratorySections } from "../hooks/useLaboratorySections";

// Same wording as a saved location, so a unit looks the same in the picker
// and in the lists: "Refrigerator LG · COD-… · Room".
export const equipmentLabel = (e: { instrumentType?: string; manufacturerName?: string; code?: string; location?: string }) =>
  [[e.instrumentType, e.manufacturerName].filter(Boolean).join(" ").replace(/\s+/g, " ").trim(), e.code, e.location]
    .filter(Boolean)
    .join(" · ");

export interface StorageLocationValue {
  equipmentId: number | "";
  locationText: string;
}

export const EMPTY_STORAGE_LOCATION: StorageLocationValue = { equipmentId: "", locationText: "" };

export const isStorageLocationValid = (v: StorageLocationValue) =>
  (v.equipmentId !== "") !== (v.locationText.trim() !== "");

// Request fields for the backend: exactly one of the two is sent.
export const storageLocationPayload = (v: StorageLocationValue) =>
  v.equipmentId !== "" ? { equipmentId: v.equipmentId, locationText: null } : { equipmentId: null, locationText: v.locationText.trim() };

const isColdStorage = (e: any) => {
  const type = (e.instrumentType || "").toLowerCase();
  const inService = e.status === "InService" || e.status === 0 || e.status === "0";
  return inService && (type.includes("refrigerator") || type.includes("freezer") || type.includes("fridge"));
};

// Media and cryovials are microbiology records, so only the Microbiology
// lab's own refrigerators/freezers are offered.
const MICRO_SECTION_CODE = "MICRO";

// Where something is kept: an in-service refrigerator/freezer from the
// equipment register, or a free-text area (e.g. the media storage area).
export function StorageLocationPicker({ value, onChange, disabled }: {
  value: StorageLocationValue;
  onChange: (v: StorageLocationValue) => void;
  disabled?: boolean;
}) {
  const [equipment, setEquipment] = useState<any[]>([]);
  const [failed, setFailed] = useState(false);
  const [mode, setMode] = useState<"equipment" | "area">(value.locationText ? "area" : "equipment");

  const { sections } = useLaboratorySections();
  const microSectionId = sections.find((s) => s.sectionCode === MICRO_SECTION_CODE)?.sectionId;

  useEffect(() => {
    if (microSectionId == null) return;
    EquipmentInventoryService.getAll()
      .then((data: any[]) => setEquipment((data || []).filter((e) => e.sectionId === microSectionId && isColdStorage(e))))
      .catch(() => setFailed(true));
  }, [microSectionId]);

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
      <ToggleButtonGroup
        size="small"
        exclusive
        value={mode}
        disabled={disabled}
        onChange={(_, m) => {
          if (!m) return;
          setMode(m);
          onChange(EMPTY_STORAGE_LOCATION);
        }}
      >
        <ToggleButton value="equipment">Refrigerator / Freezer</ToggleButton>
        <ToggleButton value="area">Other area</ToggleButton>
      </ToggleButtonGroup>

      {mode === "equipment" ? (
        <FormControl fullWidth size="small" disabled={disabled}>
          <InputLabel id="storage-equipment">Storage equipment *</InputLabel>
          <Select
            labelId="storage-equipment"
            label="Storage equipment *"
            value={value.equipmentId}
            onChange={(e) => onChange({ equipmentId: Number(e.target.value), locationText: "" })}
          >
            {equipment.map((e) => (
              <MenuItem key={e.id} value={e.id}>
                {equipmentLabel(e)}
              </MenuItem>
            ))}
          </Select>
          {failed && <Typography sx={{ fontSize: 12, color: "error.main", mt: 0.5 }}>Could not load storage equipment.</Typography>}
          {!failed && equipment.length === 0 && (
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>No in-service refrigerator or freezer found.</Typography>
          )}
        </FormControl>
      ) : (
        <TextField
          size="small"
          fullWidth
          disabled={disabled}
          label="Storage area *"
          placeholder="e.g. Media Storage Area - Shelf B"
          value={value.locationText}
          onChange={(e) => onChange({ equipmentId: "", locationText: e.target.value.slice(0, 200) })}
        />
      )}
    </Box>
  );
}
