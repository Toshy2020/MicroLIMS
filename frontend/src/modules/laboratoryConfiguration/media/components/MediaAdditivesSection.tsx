import {
  Box,
  Button,
  IconButton,
  MenuItem,
  Select,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import { useTranslation } from "../../../../i18n";
import { AdditiveKind, AdditiveRow, newAdditiveRow } from "./mediaAdditives";

export interface UsableSolution {
  id: number;
  code: string | null;
  solutionMasterName: string;
  type: string;
  expiresAt: string | null;
}

interface Props {
  rows: AdditiveRow[];
  onChange: (rows: AdditiveRow[]) => void;
  stockLots: MaterialItem[];
  solutions: UsableSolution[];
  nextKey: () => number;
}

export function MediaAdditivesSection({ rows, onChange, stockLots, solutions, nextKey }: Props) {
  const { t } = useTranslation("labConfig");
  const theme = useTheme();
  const update = (key: number, patch: Partial<AdditiveRow>) =>
    onChange(rows.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  const changeKind = (row: AdditiveRow, kind: AdditiveKind) => {
    if (kind === row.kind) return;
    update(row.key, { kind, sourceId: "", unit: kind === "PreparedSolution" ? "mL" : "" });
  };

  const pickLot = (row: AdditiveRow, id: number | "") => {
    const lot = stockLots.find((l) => l.id === id);
    update(row.key, { sourceId: id, unit: lot ? lot.unit : "" });
  };

  return (
    <Box>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 0.5 }}>
        {t("media.additives.heading")}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
        {t("media.additives.helper")}
      </Typography>

      {rows.map((row, i) => (
        <Box
          key={row.key}
          sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "auto 1fr 110px 90px auto" }, gap: 1.5, alignItems: "center", mb: 1.5 }}
        >
          <ToggleButtonGroup
            size="small"
            exclusive
            value={row.kind}
            onChange={(_, v) => v && changeKind(row, v)}
            aria-label={t("media.additives.sourceAriaLabel", { index: i + 1 })}
          >
            <ToggleButton value="StockLot">{t("media.additives.stockLot")}</ToggleButton>
            <ToggleButton value="PreparedSolution">{t("media.additives.preparedSolution")}</ToggleButton>
          </ToggleButtonGroup>

          {row.kind === "StockLot" ? (
            <Select
              size="small"
              displayEmpty
              value={row.sourceId}
              onChange={(e) => pickLot(row, e.target.value as number | "")}
              inputProps={{ "aria-label": t("media.additives.stockLotAriaLabel", { index: i + 1 }) }}
            >
              <MenuItem value=""><em>{t("media.additives.selectStockLot")}</em></MenuItem>
              {stockLots.map((l) => (
                <MenuItem key={l.id} value={l.id}>
                  {t("media.additives.stockLotOption", {
                    prefix: l.stockId ? `${l.stockId}: ` : "",
                    name: l.materialName,
                    batch: l.batchNumber,
                    remaining: l.quantityRemaining,
                    unit: l.unit
                  })}
                </MenuItem>
              ))}
            </Select>
          ) : (
            <Select
              size="small"
              displayEmpty
              value={row.sourceId}
              onChange={(e) => update(row.key, { sourceId: e.target.value as number | "" })}
              inputProps={{ "aria-label": t("media.additives.preparedSolutionAriaLabel", { index: i + 1 }) }}
            >
              <MenuItem value=""><em>{t("media.additives.selectPreparedSolution")}</em></MenuItem>
              {solutions.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.code ?? `#${s.id}`}: {s.solutionMasterName}{s.expiresAt ? t("media.additives.expiresSuffix", { date: s.expiresAt.slice(0, 10) }) : ""}
                </MenuItem>
              ))}
            </Select>
          )}

          <TextField
            size="small"
            label={t("media.additives.quantityLabel")}
            type="number"
            value={row.quantity}
            onChange={(e) => update(row.key, { quantity: e.target.value })}
            slotProps={{ htmlInput: { step: "any", min: 0, "aria-label": t("media.additives.quantityAriaLabel", { index: i + 1 }) } }}
          />

          <TextField
            size="small"
            label={t("media.additives.unitLabel")}
            value={row.unit}
            onChange={(e) => update(row.key, { unit: e.target.value })}
            disabled={row.kind === "StockLot"}
            slotProps={{ htmlInput: { maxLength: 20, "aria-label": t("media.additives.unitAriaLabel", { index: i + 1 }) } }}
          />

          <IconButton aria-label={t("media.additives.removeAriaLabel", { index: i + 1 })} onClick={() => onChange(rows.filter((r) => r.key !== row.key))}>
            <DeleteOutlineIcon fontSize="small" />
          </IconButton>
        </Box>
      ))}

      <Button size="small" startIcon={<AddIcon />} onClick={() => onChange([...rows, newAdditiveRow(nextKey())])}>
        {t("media.additives.addButton")}
      </Button>
    </Box>
  );
}
