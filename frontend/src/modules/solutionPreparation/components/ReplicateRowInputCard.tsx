import {
  Box,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Paper
} from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { formatLabDate, formatLabCalendarDate } from "../../../utils/formatDate";
import type { LotOption, SolutionPreparationListItem } from "../types";

// A primary-standard lot can be picked only when usable and its purity is recorded.
export const isSelectableLot = (lot: LotOption): boolean => lot.usable && lot.purity != null;

export interface ReplicateRowState {
  standardMaterialId: number | null;
  standardWeightMg: string;
  referencePreparationId: number | null;
  referenceVolumeMl: string;
  titrantVolumeMl: string;
  blankMl: string;
}

interface Props {
  row: ReplicateRowState;
  index: number;
  isPrimary: boolean;
  blankRequired: boolean;
  standardLots: LotOption[];
  referenceOptions: SolutionPreparationListItem[];
  onChange: (patch: Partial<ReplicateRowState>) => void;
}

export function ReplicateRowInputCard({
  row,
  index,
  isPrimary,
  blankRequired,
  standardLots,
  referenceOptions,
  onChange
}: Props) {
  const { t } = useTranslation("solutionPreparation");
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", display: "block", mb: 1.5 }}>
        <Trans
          i18nKey="replicateCard.title"
          ns="solutionPreparation"
          values={{ index: index + 1 }}
          components={{ ltr: <Ltr>{null}</Ltr> }}
        />
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: blankRequired ? "repeat(4, 1fr)" : "repeat(3, 1fr)" }, gap: 1.5 }}>
        {isPrimary ? (
          <FormControl size="small" fullWidth required>
            <InputLabel>{t("replicateCard.standardLot")}</InputLabel>
            <Select
              label={t("replicateCard.standardLot")}
              value={row.standardMaterialId ?? ""}
              onChange={(e) => onChange({ standardMaterialId: Number(e.target.value) || null })}
              inputProps={{ "aria-label": t("replicateCard.standardLot") }}
            >
              {standardLots.map((lot) => (
                <MenuItem key={lot.materialId} value={lot.materialId} disabled={!isSelectableLot(lot)}>
                  {t("replicateCard.standardLotOption", {
                    batch: lot.batchNumber,
                    exp: lot.expiryDate ? formatLabCalendarDate(lot.expiryDate) : "—",
                    purity: lot.purity != null ? t("replicateCard.purityNote", { purity: lot.purity }) : "",
                    statusNote: !lot.usable
                      ? `, ${lot.reason}`
                      : lot.purity == null
                        ? t("replicateCard.noPurityNote")
                        : ""
                  })}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        ) : (
          <FormControl size="small" fullWidth required>
            <InputLabel>{t("replicateCard.referenceSolution")}</InputLabel>
            <Select
              label={t("replicateCard.referenceSolution")}
              value={row.referencePreparationId ?? ""}
              onChange={(e) => onChange({ referencePreparationId: Number(e.target.value) || null })}
              inputProps={{ "aria-label": t("replicateCard.referenceSolution") }}
            >
              {referenceOptions.map((ref) => (
                <MenuItem key={ref.id} value={ref.id}>
                  {t("replicateCard.referenceOption", {
                    code: ref.code || t("standardizationHistory.prepFallback", { id: ref.id }),
                    date: ref.preparedAt ? formatLabDate(ref.preparedAt) : "—"
                  })}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {isPrimary ? (
          <TextField
            size="small"
            label={t("replicateCard.weightMg")}
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.standardWeightMg}
            onChange={(e) => onChange({ standardWeightMg: e.target.value })}
            required
          />
        ) : (
          <TextField
            size="small"
            label={t("replicateCard.refVolMl")}
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.referenceVolumeMl}
            onChange={(e) => onChange({ referenceVolumeMl: e.target.value })}
            required
          />
        )}

        <TextField
          size="small"
          label={t("replicateCard.titrantVolMl")}
          type="number"
          slotProps={{ htmlInput: { step: "any", min: "0" } }}
          value={row.titrantVolumeMl}
          onChange={(e) => onChange({ titrantVolumeMl: e.target.value })}
          required
        />

        {blankRequired && (
          <TextField
            size="small"
            label={t("replicateCard.blankMl")}
            type="number"
            slotProps={{ htmlInput: { step: "any", min: "0" } }}
            value={row.blankMl}
            onChange={(e) => onChange({ blankMl: e.target.value })}
            required
          />
        )}
      </Box>
    </Paper>
  );
}
