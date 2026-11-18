import { useState, useEffect } from "react";
import {
  Alert,
  Button,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { FieldHint, hintDescribedBy } from "../../../../components/FieldHint";
import { useTranslation } from "../../../../i18n";
import { getMediaHints } from "../hints";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import {
  MediaIncubationConditionOption,
  MediaProductOption,
} from "../types/mediaConfigurationTypes";

interface MediaIncubationConditionDialogProps {
  open: boolean;
  product: MediaProductOption;
  conditionToEdit: MediaIncubationConditionOption | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function MediaIncubationConditionDialog(props: MediaIncubationConditionDialogProps) {
  const { open, product, conditionToEdit, onClose, onSuccess } = props;

  const { t } = useTranslation("labConfig");
  const hints = getMediaHints(t);
  const [incubationMinHours, setIncubationMinHours] = useState<number | "">("");
  const [incubationMaxHours, setIncubationMaxHours] = useState<number | "">("");
  const [temperatureMin, setTemperatureMin] = useState<number | "">("");
  const [temperatureMax, setTemperatureMax] = useState<number | "">("");

  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      if (conditionToEdit) {
        setIncubationMinHours(conditionToEdit.incubationMinHours ?? "");
        setIncubationMaxHours(conditionToEdit.incubationMaxHours ?? "");
        setTemperatureMin(conditionToEdit.temperatureMin ?? "");
        setTemperatureMax(conditionToEdit.temperatureMax ?? "");
      } else {
        setIncubationMinHours("");
        setIncubationMaxHours("");
        setTemperatureMin("");
        setTemperatureMax("");
      }
      setError(null);
      setSaving(false);
    }
  }, [open, conditionToEdit]);

  const handleSave = async () => {
    setError(null);

    if (
      incubationMinHours === "" ||
      incubationMaxHours === "" ||
      temperatureMin === "" ||
      temperatureMax === ""
    ) {
      setError(t("media.incubationConditionDialog.requiredError"));
      return;
    }

    const values = {
      incubationMinHours: Number(incubationMinHours),
      incubationMaxHours: Number(incubationMaxHours),
      temperatureMin: Number(temperatureMin),
      temperatureMax: Number(temperatureMax),
    };

    setSaving(true);
    try {
      if (conditionToEdit) {
        await masterDataOptions.updateMediaIncubationCondition(conditionToEdit.id, values, conditionToEdit.version);
      } else {
        await masterDataOptions.createMediaIncubationCondition({
          mediaProductId: product.id,
          ...values,
        });
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? t("media.incubationConditionDialog.saveError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      titleSx={{ fontWeight: 700, fontSize: 16 }}
      title={
        conditionToEdit
          ? t("media.incubationConditionDialog.editTitle", { name: product.name })
          : t("media.incubationConditionDialog.addTitle", { name: product.name })
      }
      actions={
        <>
          <Button onClick={onClose} disabled={saving} color="inherit">
            {t("media.incubationConditionDialog.cancel")}
          </Button>
          <Button variant="contained" onClick={handleSave} disabled={saving}>
            {saving
              ? t("media.incubationConditionDialog.saving")
              : conditionToEdit
              ? t("media.incubationConditionDialog.saveChanges")
              : t("media.incubationConditionDialog.saveCondition")}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 0.5 }}>
        {error && <Alert severity="error">{error}</Alert>}

        <Stack direction="row" spacing={2}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", flex: 1 }}>
            <TextField
              label={t("media.incubationConditionDialog.incubationMinLabel")}
              type="number"
              size="small"
              required
              fullWidth
              value={incubationMinHours}
              onChange={(e) =>
                setIncubationMinHours(e.target.value === "" ? "" : Number(e.target.value))
              }
              slotProps={{ input: { "aria-describedby": hintDescribedBy("media-incubation-min") } }}
            />
            <FieldHint
              id="media-incubation-min"
              label={t("media.incubationConditionDialog.incubationMinLabel")}
              hint={hints.incubationMin}
            />
          </Stack>
          <TextField
            label={t("media.incubationConditionDialog.incubationMaxLabel")}
            type="number"
            size="small"
            required
            fullWidth
            value={incubationMaxHours}
            onChange={(e) =>
              setIncubationMaxHours(e.target.value === "" ? "" : Number(e.target.value))
            }
          />
        </Stack>

        <Stack direction="row" spacing={2}>
          <TextField
            label={t("media.incubationConditionDialog.tempMinLabel")}
            type="number"
            size="small"
            required
            fullWidth
            value={temperatureMin}
            onChange={(e) =>
              setTemperatureMin(e.target.value === "" ? "" : Number(e.target.value))
            }
          />
          <TextField
            label={t("media.incubationConditionDialog.tempMaxLabel")}
            type="number"
            size="small"
            required
            fullWidth
            value={temperatureMax}
            onChange={(e) =>
              setTemperatureMax(e.target.value === "" ? "" : Number(e.target.value))
            }
          />
        </Stack>

        <Typography variant="caption" color="text.secondary">
          {t("media.incubationConditionDialog.lockedNote")}
        </Typography>
      </Stack>
    </FloatingDialog>
  );
}
