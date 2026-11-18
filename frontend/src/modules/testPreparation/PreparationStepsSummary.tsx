import { Box, Paper, Typography } from "@mui/material";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";
import { sourceLabel } from "./preparationMaterial";

// Only the protocol fields, so the grouped confirm row can render a group's
// shared steps without inventing the identity/approval fields a full
// configuration carries.
export type PreparationSteps = Pick<
  ItemPreparationConfiguration,
  "amount" | "technique" | "filtrationVolume" | "washingVolume" | "diluent" | "neutralizer"
> & Partial<Pick<ItemPreparationConfiguration, "diluentSource" | "neutralizerSource">>;

// Read-only rendering of a preparation protocol. Shared by the analyst's
// confirm dialogue, the grouped confirm row and the Item Configuration tab
// so they can never describe the same protocol differently.
export function PreparationStepsSummary({ config }: { config: PreparationSteps }) {
  const { t } = useTranslation("testPreparation");
  const fields: { label: string; value: string; ltrValue?: boolean; note?: string | null }[] = [
    { label: t("fields.sampleAmount"), value: `${config.amount}`, ltrValue: true },
    {
      label: t("fields.technique"),
      value: t(`techniques.${config.technique}`, { defaultValue: config.technique })
    },
    { label: t("fields.diluent"), value: config.diluent || "—", note: sourceLabel(config.diluentSource, t) }
  ];

  if (config.technique === "Filtration") {
    fields.push(
      {
        label: t("fields.filtrationVolume"),
        value: config.filtrationVolume != null ? t("fields.unitMl", { value: config.filtrationVolume }) : "—",
        ltrValue: config.filtrationVolume != null
      },
      {
        label: t("fields.washingVolume"),
        value: config.washingVolume != null ? t("fields.unitMl", { value: config.washingVolume }) : "—",
        ltrValue: config.washingVolume != null
      }
    );
  }

  fields.push({
    label: t("fields.neutralizer"),
    value: config.neutralizer || "—",
    note: sourceLabel(config.neutralizerSource, t)
  });

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5 }}>
      {fields.map((f) => (
        <Paper key={f.label} sx={{ p: 1.5, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
            {f.label}
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 700, mt: 0.5 }}>
            {f.ltrValue ? <Ltr>{f.value}</Ltr> : f.value}
          </Typography>
          {f.note && (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {f.note}
            </Typography>
          )}
        </Paper>
      ))}
    </Box>
  );
}
