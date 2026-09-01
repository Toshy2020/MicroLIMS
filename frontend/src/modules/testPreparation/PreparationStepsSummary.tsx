import { Box, Paper, Typography } from "@mui/material";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";

// Read-only rendering of a preparation protocol. Shared by the analyst's
// confirm dialogue and the Item Configuration tab so the two can never
// describe the same protocol differently.
export function PreparationStepsSummary({ config }: { config: ItemPreparationConfiguration }) {
  const fields: { label: string; value: string }[] = [
    { label: "Sample Amount", value: `${config.amount} ${config.unit}` },
    { label: "Technique", value: config.technique === "PourPlate" ? "Pour Plate" : config.technique },
    { label: "Diluent", value: config.diluentTypeName || "—" }
  ];

  if (config.technique === "Filtration") {
    fields.push(
      { label: "Filtration Volume", value: config.filtrationVolume != null ? `${config.filtrationVolume} ml` : "—" },
      { label: "Washing Volume", value: config.washingVolume != null ? `${config.washingVolume} ml` : "—" }
    );
  }

  if (config.diluentMediaLotNumber) {
    fields.push({ label: "Diluent Media Lot", value: config.diluentMediaLotNumber });
  }

  fields.push({ label: "Neutralizer", value: config.neutralizerName || "—" });

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5 }}>
      {fields.map((f) => (
        <Paper key={f.label} sx={{ p: 1.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
            {f.label}
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 700, mt: 0.5 }}>
            {f.value}
          </Typography>
        </Paper>
      ))}
    </Box>
  );
}
