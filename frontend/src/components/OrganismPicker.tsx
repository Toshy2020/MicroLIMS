import { Autocomplete, TextField, createFilterOptions } from "@mui/material";
import { useOrganisms, OrganismOption } from "../hooks/useOrganisms";

interface OrganismPickerProps {
  value: number | null;
  onChange: (organismId: number | null) => void;
  label?: string;
  size?: "small" | "medium";
  sx?: any;
}

type Option = OrganismOption | { inputValue: string; isNew: true };

const filter = createFilterOptions<Option>();

const optionLabel = (o: OrganismOption) => (o.atccNumber ? `${o.scientificName} (ATCC: ${o.atccNumber})` : o.scientificName);

// Single Organism picker sourced from the canonical Organism master list
// (GET /masterdata/organisms). Typing a scientific name that doesn't
// exist yet offers "Add <name> to Organism list" - selecting it creates
// the Organism immediately and picks it, mirroring TestCodePicker.tsx's
// freeSolo pattern so analysts are never blocked by missing master data.
export function OrganismPicker({ value, onChange, label = "Organism", size = "small", sx }: OrganismPickerProps) {
  const { options, addNew } = useOrganisms();
  const selected = options.find((o) => o.id === value) ?? null;

  return (
    <Autocomplete<Option, false, false, false>
      size={size}
      sx={sx}
      options={options}
      value={selected}
      getOptionLabel={(o) => (typeof o === "string" ? o : "inputValue" in o ? o.inputValue : optionLabel(o))}
      isOptionEqualToValue={(o, v) => "id" in o && "id" in v && o.id === v.id}
      filterOptions={(opts, params) => {
        const filtered = filter(opts, params);
        const exists = options.some((o) => o.scientificName.toLowerCase() === params.inputValue.toLowerCase());
        if (params.inputValue !== "" && !exists) {
          filtered.push({ inputValue: params.inputValue, isNew: true });
        }
        return filtered;
      }}
      onChange={async (_e, newValue) => {
        if (!newValue) { onChange(null); return; }
        if (typeof newValue !== "string" && "isNew" in newValue) {
          const created = await addNew(newValue.inputValue);
          onChange(created.id);
        } else if (typeof newValue !== "string") {
          onChange(newValue.id);
        }
      }}
      renderOption={(props, option) => (
        <li {...props} key={"id" in option ? option.id : option.inputValue}>
          {"isNew" in option ? `+ Add "${option.inputValue}" to Organism list` : optionLabel(option)}
        </li>
      )}
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. Escherichia coli" />}
    />
  );
}
