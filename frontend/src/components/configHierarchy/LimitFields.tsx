import { Alert, Box, TextField } from "@mui/material";

export interface LimitValues {
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
}

export const emptyLimits: LimitValues = { alertLimit: "", actionLimit: "", specLimit: "", unit: "" };

// Advisory only: limits are free text (e.g. "<1"), and the ordering rule
// belongs to the backend, so this never blocks a save - it just points
// out an order that is probably a typo when all three are plain numbers.
export function limitOrderWarning({ alertLimit, actionLimit, specLimit }: LimitValues): string | null {
  const nums = [alertLimit, actionLimit, specLimit].map((s) => (s.trim() === "" ? null : Number(s.trim())));
  if (nums.some((n) => n !== null && Number.isNaN(n))) return null;
  const [alert, action, spec] = nums;
  if (alert != null && action != null && alert > action) return "Alert limit is higher than the action limit.";
  if (action != null && spec != null && action > spec) return "Action limit is higher than the specification.";
  if (alert != null && spec != null && alert > spec) return "Alert limit is higher than the specification.";
  return null;
}

interface Props {
  idPrefix: string;
  value: LimitValues;
  onChange: (value: LimitValues) => void;
  unitRequired?: boolean;
  unitPlaceholder?: string;
}

// Labelled Alert / Action / Specification / Unit inputs.
export function LimitFields({ idPrefix, value, onChange, unitRequired, unitPlaceholder }: Props) {
  const set = (k: keyof LimitValues) => (e: React.ChangeEvent<HTMLInputElement>) => onChange({ ...value, [k]: e.target.value });
  const warning = limitOrderWarning(value);
  return (
    <Box>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 1.25 }}>
        <TextField id={`${idPrefix}-alert`} size="small" label="Alert" value={value.alertLimit} onChange={set("alertLimit")} />
        <TextField id={`${idPrefix}-action`} size="small" label="Action" value={value.actionLimit} onChange={set("actionLimit")} />
        <TextField id={`${idPrefix}-spec`} size="small" label="Specification" value={value.specLimit} onChange={set("specLimit")} />
        <TextField
          id={`${idPrefix}-unit`}
          size="small"
          label="Unit"
          required={unitRequired}
          placeholder={unitPlaceholder}
          value={value.unit}
          onChange={set("unit")}
        />
      </Box>
      {warning && (
        <Alert severity="warning" sx={{ mt: 1, py: 0 }}>
          {warning} Check the values before saving.
        </Alert>
      )}
    </Box>
  );
}
