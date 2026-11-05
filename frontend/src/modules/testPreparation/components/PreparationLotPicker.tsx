import { Alert, MenuItem, TextField } from "@mui/material";
import { formatLabDate } from "../../../utils/formatDate";
import type { PreparationLotGroup } from "../services/SamplePreparationService";

interface Props {
  // "Diluent" or "Neutralizer".
  label: string;
  group: PreparationLotGroup;
  value: number | null;
  onChange: (lotId: number | null) => void;
  disabled?: boolean;
}

// Pick the actual lot of the configured diluent/neutralizer. Unusable lots
// stay visible but disabled, with the server's reason, so the analyst sees
// why a lot they hold is not offered. Renders nothing when no lot is needed.
export function PreparationLotPicker({ label, group, value, onChange, disabled }: Props) {
  if (!group.lotRequired) return null;

  const anyUsable = group.options.some((o) => o.usable);

  return (
    <>
      <TextField
        select
        required
        size="small"
        label={`${label} lot`}
        value={value ?? ""}
        disabled={disabled}
        helperText={group.name}
        onChange={(e) => onChange(e.target.value === "" ? null : Number(e.target.value))}
      >
        {group.options.map((o) => (
          <MenuItem key={o.id} value={o.id} disabled={!o.usable}>
            {o.code}
            {o.expiresAt ? ` · expires ${formatLabDate(o.expiresAt)}` : ""}
            {!o.usable && o.reason ? ` — ${o.reason}` : ""}
          </MenuItem>
        ))}
      </TextField>
      {!anyUsable && (
        <Alert severity="warning" sx={{ fontSize: 12 }}>
          No usable {label.toLowerCase()} lot of {group.name} is available.
        </Alert>
      )}
    </>
  );
}
