import { useState, useEffect } from "react";
import {
  Alert,
  Button,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
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
      setError("Enter the incubation hours and temperature range.");
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
        await masterDataOptions.updateMediaIncubationCondition(conditionToEdit.id, values);
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
      setError(msg ?? "Failed to save the incubation condition.");
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
          ? `Edit incubation condition - ${product.name}`
          : `Add incubation condition - ${product.name}`
      }
      actions={
        <>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button variant="contained" onClick={handleSave} disabled={saving}>
            {saving ? "Saving..." : conditionToEdit ? "Save Changes" : "Save Condition"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 0.5 }}>
        {error && <Alert severity="error">{error}</Alert>}

        <Stack direction="row" spacing={2}>
          <TextField
            label="Incubation Min (h)"
            type="number"
            size="small"
            required
            fullWidth
            value={incubationMinHours}
            onChange={(e) =>
              setIncubationMinHours(e.target.value === "" ? "" : Number(e.target.value))
            }
          />
          <TextField
            label="Incubation Max (h)"
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
            label="Temp Min (°C)"
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
            label="Temp Max (°C)"
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
          Once an evaluation configuration or a Test Master step uses this condition, it can't be changed.
        </Typography>
      </Stack>
    </FloatingDialog>
  );
}
