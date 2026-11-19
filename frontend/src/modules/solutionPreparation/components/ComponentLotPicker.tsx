import { useEffect, useMemo, useState } from "react";
import {
  Box,
  Card,
  CardContent,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
  Alert,
  CircularProgress,
  Stack,
  Chip
} from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { ScanField } from "../../../components/labels/ScanField";
import { NumericEntryField, parseDecimal } from "../../../components/bench/NumericEntryField";
import { useTranslation } from "../../../i18n";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import type { SolutionPreparationComponentResponse, LotOption } from "../types";

interface Props {
  component: SolutionPreparationComponentResponse;
  lotOptions: LotOption[];
  loadingLots?: boolean;
  selectedMaterialId: number | null;
  quantityUsed: number | null | string;
  onSelectLot: (materialId: number | null) => void;
  onChangeQuantity: (quantity: number | null) => void;
  disabled?: boolean;
}

export function ComponentLotPicker({
  component,
  lotOptions,
  loadingLots = false,
  selectedMaterialId,
  quantityUsed,
  onSelectLot,
  onChangeQuantity,
  disabled = false
}: Props) {
  const { t } = useTranslation("solutionPreparation");
  const selectedLot = useMemo(
    () => lotOptions.find((l) => l.materialId === selectedMaterialId),
    [lotOptions, selectedMaterialId]
  );

  const unitDisplay = selectedLot?.unit ?? component.lotUnit ?? "";
  const hasUsableLots = lotOptions.some((l) => l.usable);
  const externalText = quantityUsed == null ? "" : String(quantityUsed);
  const [qtyText, setQtyText] = useState(externalText);

  useEffect(() => {
    setQtyText((prev) => (parseDecimal(prev) === (quantityUsed ?? null) ? prev : externalText));
  }, [externalText, quantityUsed]);

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {component.order}. {component.entryName}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              <Trans
                i18nKey="lotPicker.code"
                ns="solutionPreparation"
                values={{ code: component.entryCode }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>
          <Chip
            size="small"
            variant="outlined"
            label={
              <Trans
                i18nKey="lotPicker.recipe"
                ns="solutionPreparation"
                values={{ qty: component.recipeQuantity ?? "", unit: component.recipeUnit ?? "" }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            }
            sx={{ fontWeight: 600, fontSize: 12 }}
          />
        </Box>

        <Stack spacing={2}>
          {loadingLots ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("lotPicker.loading")}
              </Typography>
            </Box>
          ) : lotOptions.length === 0 ? (
            <Alert severity="warning" sx={{ py: 0.5, fontSize: 12 }}>
              {t("lotPicker.empty", { name: component.entryName })}
            </Alert>
          ) : !hasUsableLots ? (
            <Alert severity="error" sx={{ py: 0.5, fontSize: 12 }}>
              {t("lotPicker.allBlocked")}
            </Alert>
          ) : null}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "3fr 2fr" }, gap: 2, alignItems: "start" }}>
            <Stack spacing={1}>
              <ScanField
                label={t("lotPicker.scanLabel", { name: component.entryName })}
                expectedType="MaterialStockLot"
                offeredIds={lotOptions.filter((l) => l.usable).map((l) => l.materialId)}
                onSelect={(id) => onSelectLot(id)}
                disabled={disabled || loadingLots}
              />
              <FormControl fullWidth size="small" disabled={disabled || loadingLots || lotOptions.length === 0}>
                <InputLabel id={`lot-select-label-${component.id}`}>{t("lotPicker.selectLot")}</InputLabel>
                <Select
                  labelId={`lot-select-label-${component.id}`}
                  id={`lot-select-${component.id}`}
                  value={selectedMaterialId ?? ""}
                  label={t("lotPicker.selectLot")}
                  onChange={(e) => {
                    const val = e.target.value;
                    onSelectLot(val ? Number(val) : null);
                  }}
                >
                  <MenuItem value="">
                    <em>{t("lotPicker.chooseLot")}</em>
                  </MenuItem>
                  {lotOptions.map((lot) => {
                    const expText = lot.expiryDate
                      ? t("lotPicker.expPrefix", { date: formatLabCalendarDate(lot.expiryDate) })
                      : "";
                    const blockedText = !lot.usable
                      ? t("lotPicker.blockedSuffix", { reason: lot.reason ?? t("lotPicker.notUsable") })
                      : "";
                    return (
                      <MenuItem key={lot.materialId} value={lot.materialId} disabled={!lot.usable}>
                        {t("lotPicker.optionLine", {
                          batch: lot.batchNumber,
                          remaining: lot.quantityRemaining,
                          unit: lot.unit,
                          exp: expText,
                          blocked: blockedText
                        })}
                      </MenuItem>
                    );
                  })}
                </Select>
                {selectedLot && (
                  <FormHelperText sx={{ fontSize: 12 }}>
                    {t("lotPicker.availableInLot", { remaining: selectedLot.quantityRemaining, unit: selectedLot.unit })}
                    {selectedLot.expiryDate && t("lotPicker.availableExpires", { date: formatLabCalendarDate(selectedLot.expiryDate) })}
                  </FormHelperText>
                )}
              </FormControl>
            </Stack>

            <NumericEntryField
              label={t("lotPicker.quantityUsed")}
              kind="decimal"
              value={qtyText}
              onChange={(val) => {
                setQtyText(val);
                if (val === "") {
                  onChangeQuantity(null);
                } else {
                  const parsed = parseDecimal(val);
                  if (parsed !== null) onChangeQuantity(parsed);
                }
              }}
              disabled={disabled || !selectedMaterialId}
              unit={unitDisplay || undefined}
              helperText={t("lotPicker.quantityHelper", { unit: unitDisplay || t("lotPicker.unitFallback") })}
            />
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
