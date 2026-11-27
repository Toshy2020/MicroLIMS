import { Box, Button, IconButton, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "../../../i18n";
import type { QualityActionKind } from "../../../services/qualityService";
import { LookupSelect, TEXT_MAX } from "./stepKit";
import type { Lookup } from "./lookups";

// One planned action of a CAPA or one task of a change control, as typed in a plan form.
export interface ActionRow {
  kind: QualityActionKind;
  description: string;
  responsibleUserId: number | "";
  dueDate: string;
}

export const emptyActionRow = (kind: QualityActionKind): ActionRow => ({ kind, description: "", responsibleUserId: "", dueDate: "" });

export const isCompleteRow = (row: ActionRow): boolean =>
  Boolean(row.description.trim()) && row.responsibleUserId !== "" && Boolean(row.dueDate);

interface ActionRowsEditorProps {
  rows: ActionRow[];
  onChange: (rows: ActionRow[]) => void;
  // How many rows already exist on the server: they can be changed but not removed.
  lockedCount: number;
  // "action" (CAPA: corrective or preventive) or "task" (change control).
  noun: "action" | "task";
  users: Lookup;
  disabled?: boolean;
}

export function ActionRowsEditor({ rows, onChange, lockedCount, noun, users, disabled = false }: ActionRowsEditorProps) {
  const { t } = useTranslation("quality");
  const update = (index: number, patch: Partial<ActionRow>) =>
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  const defaultKind: QualityActionKind = noun === "action" ? "Corrective" : "ChangeTask";

  return (
    <Stack spacing={1.5}>
      {rows.map((row, index) => {
        const label = t(`steps.rows.${noun}Label`, { number: index + 1 });
        return (
          <Box
            key={index}
            role="group"
            aria-label={label}
            sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 1.5, display: "flex", flexDirection: "column", gap: 1.5 }}
          >
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography variant="subtitle2" component="p">
                {label}
              </Typography>
              {index >= lockedCount && !disabled && (
                <IconButton size="small" aria-label={t("steps.rows.remove", { label })} onClick={() => onChange(rows.filter((_, i) => i !== index))}>
                  <DeleteOutlinedIcon fontSize="small" />
                </IconButton>
              )}
            </Box>
            {noun === "action" && (
              <TextField
                select
                required
                label={t("steps.rows.kind")}
                value={row.kind}
                disabled={disabled}
                onChange={(e) => update(index, { kind: e.target.value === "Preventive" ? "Preventive" : "Corrective" })}
              >
                <MenuItem value="Corrective">{t("actionKinds.Corrective")}</MenuItem>
                <MenuItem value="Preventive">{t("actionKinds.Preventive")}</MenuItem>
              </TextField>
            )}
            <TextField
              required
              multiline
              minRows={2}
              label={t("steps.rows.description")}
              value={row.description}
              disabled={disabled}
              onChange={(e) => update(index, { description: e.target.value })}
              slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
            />
            <LookupSelect
              required
              label={t("steps.rows.responsible")}
              value={row.responsibleUserId}
              onChange={(value) => update(index, { responsibleUserId: value })}
              lookup={users}
              disabled={disabled}
            />
            <TextField
              required
              type="date"
              label={t("steps.rows.dueDate")}
              value={row.dueDate}
              disabled={disabled}
              onChange={(e) => update(index, { dueDate: e.target.value })}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Box>
        );
      })}
      {!disabled && (
        <Box>
          <Button startIcon={<AddIcon />} onClick={() => onChange([...rows, emptyActionRow(defaultKind)])}>
            {t(`steps.rows.add_${noun}`)}
          </Button>
        </Box>
      )}
    </Stack>
  );
}
