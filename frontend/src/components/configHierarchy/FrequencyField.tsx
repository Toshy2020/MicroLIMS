import { Autocomplete, TextField } from "@mui/material";

const FREQUENCIES = ["Daily", "Weekly", "Fortnightly", "Monthly", "Quarterly", "Annually"];

// Testing frequency is stored as free text; suggesting a fixed list keeps
// new entries consistent while still accepting any existing value.
export function FrequencyField({ id, value, onChange }: { id: string; value: string; onChange: (v: string) => void }) {
  return (
    <Autocomplete
      freeSolo
      options={FREQUENCIES}
      value={value}
      onInputChange={(_, v) => onChange(v)}
      renderInput={(params) => <TextField {...params} id={id} size="small" label="Testing frequency" placeholder="e.g. Weekly" />}
    />
  );
}
