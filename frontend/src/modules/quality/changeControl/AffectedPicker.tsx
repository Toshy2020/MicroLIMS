import { useState } from "react";
import { Box, Button, FormControl, FormControlLabel, FormLabel, MenuItem, Radio, RadioGroup, Stack, TextField } from "@mui/material";
import { useTranslation } from "../../../i18n";
import type { QualityLinkTargetType } from "../../../services/qualityService";
import { RecordPicker } from "../steps/RecordPicker";

// An affected record of a change control as chosen in a form: an existing record found with the search, or a new
// record that does not exist yet and is named by its planned code.
export interface AffectedItem {
  targetType: QualityLinkTargetType;
  targetId: number | null;
  newRecordCode: string | null;
  label: string;
}

// What the system's search can find, and the link target it stands for.
const SEARCHABLE: { entityType: string; targetType: QualityLinkTargetType }[] = [
  { entityType: "Item", targetType: "Item" },
  { entityType: "Specification", targetType: "Specification" },
  { entityType: "TestDefinition", targetType: "TestDefinition" },
  { entityType: "Equipment", targetType: "Equipment" },
  { entityType: "DocumentMaster", targetType: "DocumentMaster" }
];

// A new record can be an item, a test, equipment or a controlled document. A new specification is listed by its
// item or test, so it is not offered here (the server refuses it without an id).
const NEW_TYPES: QualityLinkTargetType[] = ["Item", "TestDefinition", "Equipment", "DocumentMaster"];

const CODE_MAX = 100;

interface AffectedPickerProps {
  onAdd: (item: AffectedItem) => void;
  disabled?: boolean;
}

export function AffectedPicker({ onAdd, disabled = false }: AffectedPickerProps) {
  const { t } = useTranslation("quality");
  const [mode, setMode] = useState<"existing" | "new">("existing");
  const [searchType, setSearchType] = useState(SEARCHABLE[0].entityType);
  const [newType, setNewType] = useState<QualityLinkTargetType>("TestDefinition");
  const [code, setCode] = useState("");

  const target = SEARCHABLE.find((entry) => entry.entityType === searchType) ?? SEARCHABLE[0];

  return (
    <Stack spacing={1.5}>
      <FormControl component="fieldset" disabled={disabled}>
        <FormLabel component="legend">{t("steps.changeControl.affected.mode")}</FormLabel>
        <RadioGroup row value={mode} onChange={(e) => setMode(e.target.value === "new" ? "new" : "existing")}>
          <FormControlLabel value="existing" control={<Radio />} label={t("steps.changeControl.affected.existing")} />
          <FormControlLabel value="new" control={<Radio />} label={t("steps.changeControl.affected.new")} />
        </RadioGroup>
      </FormControl>

      {mode === "existing" ? (
        <>
          <TextField
            select
            size="small"
            label={t("steps.changeControl.affected.kind")}
            value={searchType}
            disabled={disabled}
            onChange={(e) => setSearchType(e.target.value)}
          >
            {SEARCHABLE.map((entry) => (
              <MenuItem key={entry.entityType} value={entry.entityType}>
                {t(`steps.changeControl.affected.kinds.${entry.targetType}`)}
              </MenuItem>
            ))}
          </TextField>
          <RecordPicker
            label={t("steps.changeControl.affected.search")}
            entityTypes={[target.entityType]}
            disabled={disabled}
            onPick={(hit) => onAdd({ targetType: target.targetType, targetId: hit.id, newRecordCode: null, label: hit.title })}
          />
        </>
      ) : (
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
          <TextField
            select
            size="small"
            label={t("steps.changeControl.affected.newKind")}
            value={newType}
            disabled={disabled}
            onChange={(e) => setNewType(e.target.value as QualityLinkTargetType)}
          >
            {NEW_TYPES.map((type) => (
              <MenuItem key={type} value={type}>
                {t(`steps.changeControl.affected.kinds.${type}`)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            size="small"
            label={t("steps.changeControl.affected.code")}
            helperText={t("steps.changeControl.affected.codeHelp")}
            value={code}
            disabled={disabled}
            onChange={(e) => setCode(e.target.value)}
            slotProps={{ htmlInput: { maxLength: CODE_MAX } }}
          />
          <Box>
            <Button
              variant="outlined"
              disabled={disabled || !code.trim()}
              onClick={() => {
                onAdd({ targetType: newType, targetId: null, newRecordCode: code.trim(), label: code.trim() });
                setCode("");
              }}
            >
              {t("steps.changeControl.affected.addNew")}
            </Button>
          </Box>
        </Stack>
      )}
    </Stack>
  );
}
