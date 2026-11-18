import { Box, FormControl, InputLabel, ListSubheader, MenuItem, Paper, Select, Typography } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import {
  equipmentLocationTag,
  isEquipmentLocation,
  type StorageChoices,
  type StorageEquipmentOption
} from "./materialStorage";

interface StorageLocationFieldProps {
  location: string;
  onChange: (location: string) => void;
  choices: StorageChoices;
  loading: boolean;
  error: boolean;
}

const groupLabelSx = { fontWeight: 700, fontSize: 12, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" } as const;
const detailLabelSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" } as const;
const detailValueSx = { fontSize: 13, fontWeight: 600, color: "text.primary" } as const;

function equipmentGroup({ title, items, none, noneKey }: { title: string; items: StorageEquipmentOption[]; none: string; noneKey: string }) {
  return [
    <ListSubheader key={`${noneKey}-header`} sx={groupLabelSx}>
      {title}
    </ListSubheader>,
    ...(items.length > 0
      ? items.map((eq) => (
          <MenuItem key={eq.id} value={equipmentLocationTag(eq)}>
            {equipmentLocationTag(eq)}
          </MenuItem>
        ))
      : [
          <MenuItem key={noneKey} disabled value={noneKey} sx={{ fontSize: 12, fontStyle: "italic" }}>
            {none}
          </MenuItem>
        ])
  ];
}

function Detail({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <Box>
      <Typography sx={detailLabelSx}>{label}</Typography>
      <Typography sx={{ ...detailValueSx, ...(tone ? { color: tone } : {}) }}>{value}</Typography>
    </Box>
  );
}

const cardSx = { mt: 2, p: 1.5, bgcolor: "background.default", borderColor: "divider", borderRadius: 1 } as const;
const gridSx = (columns: number) =>
  ({ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: `repeat(${columns}, 1fr)` }, gap: 1.5 }) as const;

export function StorageLocationField({ location, onChange, choices, loading, error }: StorageLocationFieldProps) {
  const { t } = useTranslation("inventory");
  const { selectedEquipment, roomStorageOption, activeSection } = choices;
  return (
    <>
      <FormControl size="small" fullWidth required error={error} sx={{ gridColumn: { sm: "span 2" } }}>
        <InputLabel id="storage-location-label">{t("wizard.lot.storageLocation")}</InputLabel>
        <Select
          labelId="storage-location-label"
          label={t("wizard.lot.storageLocation")}
          name="location"
          value={selectedEquipment && isEquipmentLocation(location, selectedEquipment) ? equipmentLocationTag(selectedEquipment) : location}
          onChange={(e) => onChange(e.target.value)}
          disabled={loading}
        >
          <MenuItem value="">
            <em>{loading ? t("wizard.storage.loading") : t("wizard.storage.select")}</em>
          </MenuItem>
          {equipmentGroup({ title: t("wizard.storage.refrigerator"), items: choices.refrigerators, none: t("wizard.storage.noRefrigerator"), noneKey: "no-ref" })}
          {equipmentGroup({ title: t("wizard.storage.deepFreezer"), items: choices.deepFreezers, none: t("wizard.storage.noDeepFreezer"), noneKey: "no-df" })}
          {equipmentGroup({ title: t("wizard.storage.freezer"), items: choices.freezers, none: t("wizard.storage.noFreezer"), noneKey: "no-fr" })}
          {choices.hasOtherOptions && <ListSubheader sx={groupLabelSx}>{t("wizard.storage.other")}</ListSubheader>}
          {roomStorageOption && <MenuItem value={roomStorageOption}>{roomStorageOption}</MenuItem>}
          {/* Fallback for legacy material locations when editing */}
          {choices.isFallbackLocation && <MenuItem value={location}>{t("wizard.storage.currentLocation", { location })}</MenuItem>}
        </Select>
      </FormControl>

      {selectedEquipment && (
        <Paper variant="outlined" sx={{ ...cardSx, gridColumn: "1 / -1" }}>
          <Box sx={gridSx(4)}>
            <Detail label={t("wizard.storage.selected")} value={`${selectedEquipment.instrumentType}, ${selectedEquipment.manufacturerName || t("wizard.storage.asset")}`} tone="primary.main" />
            <Detail label={t("wizard.storage.code")} value={selectedEquipment.code} />
            <Detail label={t("wizard.storage.location")} value={selectedEquipment.location || "—"} />
            <Detail label={t("wizard.storage.status")} value={t("wizard.storage.inService")} tone="success.main" />
          </Box>
        </Paper>
      )}

      {roomStorageOption && location === roomStorageOption && (
        <Paper variant="outlined" sx={{ ...cardSx, gridColumn: "1 / -1" }}>
          <Box sx={gridSx(3)}>
            <Detail label={t("wizard.storage.selected")} value={roomStorageOption} tone="primary.main" />
            <Detail label={t("wizard.storage.type")} value={t("wizard.storage.ambient")} />
            <Detail label={t("wizard.storage.location")} value={activeSection?.sectionName ?? roomStorageOption} />
          </Box>
        </Paper>
      )}
    </>
  );
}
