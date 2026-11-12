import { Autocomplete, TextField, type SxProps, type Theme } from "@mui/material";
import { useOrganisms, OrganismOption } from "../hooks/useOrganisms";

interface OrganismPickerProps {
  value: number | null;
  onChange: (organismId: number | null) => void;
  label?: string;
  size?: "small" | "medium";
  disabled?: boolean;
  sx?: SxProps<Theme>;
}

const optionLabel = (o: OrganismOption) => (o.atccNumber ? `${o.scientificName} (ATCC: ${o.atccNumber})` : o.scientificName);

// Single Organism picker sourced from the canonical Organism master list
// (GET /masterdata/organisms). It only chooses existing organisms; a new
// organism is added on the Organisms page (Master Testing Materials), where
// its culture collection and strain details are recorded. Not freeSolo, so
// typed text can never be emitted as an organism.
export function OrganismPicker({ value, onChange, label = "Organism", size = "small", disabled = false, sx }: OrganismPickerProps) {
  const { options } = useOrganisms();
  const selected = options.find((o) => o.id === value) ?? null;

  return (
    <Autocomplete<OrganismOption, false, false, false>
      size={size}
      disabled={disabled}
      sx={sx}
      options={options}
      value={selected}
      noOptionsText="No such organism. Add it in Master Testing Materials first."
      getOptionLabel={optionLabel}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      onChange={(_e, newValue) => onChange(newValue ? newValue.id : null)}
      renderOption={(props, option) => (
        <li {...props} key={option.id}>
          {optionLabel(option)}
        </li>
      )}
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. Escherichia coli" />}
    />
  );
}
