import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography
} from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { NumericEntryField, parseDecimal } from "../../../components/bench/NumericEntryField";
import { useTranslation } from "../../../i18n";
import { formatLabDate, formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { approvalStatusLabel } from "../releaseState";
import type { SolutionPreparationComponentResponse, SourcePreparationOption } from "../types";

interface Props {
  component: SolutionPreparationComponentResponse;
  options: SourcePreparationOption[];
  loading?: boolean;
  selectedPreparationId: number | null;
  quantityUsed: number | null | string;
  onSelect: (preparationId: number | null) => void;
  onChangeQuantity: (quantity: number | null) => void;
  disabled?: boolean;
}

// A prepared-solution recipe line: the analyst picks the prepared bottle of the source
// recipe that was used. Nothing is deducted, so the quantity is optional. Unusable bottles
// stay listed but disabled, with the server's reason.
export function SourcePreparationPicker({
  component,
  options,
  loading = false,
  selectedPreparationId,
  quantityUsed,
  onSelect,
  onChangeQuantity,
  disabled = false
}: Props) {
  const { t } = useTranslation("solutionPreparation");
  const selected = useMemo(
    () => options.find((o) => o.preparationId === selectedPreparationId),
    [options, selectedPreparationId]
  );
  const hasUsable = options.some((o) => o.usable);
  const recipeUnit = component.recipeUnit ?? "";
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
              {t("sourcePicker.preparedSolution")}
            </Typography>
          </Box>
          {component.recipeQuantity != null && (
            <Chip
              size="small"
              variant="outlined"
              label={
                <Trans
                  i18nKey="sourcePicker.recipe"
                  ns="solutionPreparation"
                  values={{ qty: component.recipeQuantity, unit: recipeUnit }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              }
              sx={{ fontWeight: 600, fontSize: 12 }}
            />
          )}
        </Box>

        <Stack spacing={2}>
          {loading ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("sourcePicker.loading")}
              </Typography>
            </Box>
          ) : options.length === 0 ? (
            <Alert severity="warning" sx={{ py: 0.5, fontSize: 12 }}>
              {t("sourcePicker.empty", { name: component.entryName })}
            </Alert>
          ) : !hasUsable ? (
            <Alert severity="error" sx={{ py: 0.5, fontSize: 12 }}>
              {t("sourcePicker.noneUsable")}
            </Alert>
          ) : null}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "3fr 2fr" }, gap: 2 }}>
            <FormControl fullWidth size="small" disabled={disabled || loading || options.length === 0}>
              <InputLabel id={`source-select-label-${component.id}`}>{t("sourcePicker.selectLabel")}</InputLabel>
              <Select
                labelId={`source-select-label-${component.id}`}
                id={`source-select-${component.id}`}
                value={selectedPreparationId ?? ""}
                label={t("sourcePicker.selectLabel")}
                onChange={(e) => {
                  const val = e.target.value;
                  onSelect(val ? Number(val) : null);
                }}
              >
                <MenuItem value="">
                  <em>{t("sourcePicker.chooseOption")}</em>
                </MenuItem>
                {options.map((o) => {
                  const dates = [
                    o.preparedAt ? t("sourcePicker.preparedDate", { date: formatLabDate(o.preparedAt) }) : null,
                    o.expiresAt ? t("sourcePicker.expDate", { date: formatDateTimeWithTimeZone(o.expiresAt) }) : null
                  ].filter(Boolean);
                  const release = approvalStatusLabel(o.approvalStatus, t);
                  return (
                    <MenuItem key={o.preparationId} value={o.preparationId} disabled={!o.usable}>
                      {o.code ?? `#${o.preparationId}`}
                      {dates.length > 0 ? ` · ${dates.join(" · ")}` : ""}
                      {release ? ` · ${release}` : ""}
                      {!o.usable ? t("sourcePicker.blockedSuffix", { reason: o.reason ?? t("sourcePicker.notUsable") }) : ""}
                    </MenuItem>
                  );
                })}
              </Select>
              {selected && (
                <FormHelperText sx={{ fontSize: 12 }}>
                  {selected.expiresAt
                    ? t("sourcePicker.expiresHelper", { date: formatDateTimeWithTimeZone(selected.expiresAt) })
                    : t("sourcePicker.noExpiryDate")}
                </FormHelperText>
              )}
            </FormControl>

            <NumericEntryField
              label={t("sourcePicker.quantityUsedOptional")}
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
              disabled={disabled || !selectedPreparationId}
              unit={recipeUnit || undefined}
              helperText={t("sourcePicker.quantityHelper")}
            />
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
