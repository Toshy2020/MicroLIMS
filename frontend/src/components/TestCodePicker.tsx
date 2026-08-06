import { Autocomplete, TextField, createFilterOptions } from "@mui/material";
import { useTestDefinitions, TestDefinitionOption } from "../hooks/useTestDefinitions";

interface TestCodePickerProps {
  value: string;
  onChange: (code: string) => void;
  label?: string;
  size?: "small" | "medium";
  sx?: any;
}

type Option = TestDefinitionOption | { inputValue: string; isNew: true };

const filter = createFilterOptions<Option>();

// Single TestCode picker sourced from the Test Master (GET
// /masterdata/test-definitions). Typing a code that doesn't exist yet
// offers "Add <code> to Test Master" - selecting it creates the
// TestDefinition immediately and picks it, so nothing here ever
// silently free-types an ungoverned string.
export function TestCodePicker({ value, onChange, label = "Test", size = "small", sx }: TestCodePickerProps) {
  const { options, activeOptions, addNew } = useTestDefinitions();
  // Looked up against the full list (not just activeOptions) so a value
  // already assigned to a since-frozen test still displays correctly.
  const selected = options.find((o) => o.code === value) ?? null;

  return (
    <Autocomplete<Option, false, false, false>
      size={size}
      sx={sx}
      options={activeOptions}
      value={selected}
      getOptionLabel={(o) => (typeof o === "string" ? o : "inputValue" in o ? o.inputValue : `${o.code} — ${o.displayName}`)}
      filterOptions={(opts, params) => {
        const filtered = filter(opts, params);
        // Checked against the full Test Master (not just the active
        // options offered above) so a frozen-but-existing code doesn't
        // show a misleading "Add ... to Test Master" for something that
        // already exists.
        const exists = options.some((o) => o.code.toLowerCase() === params.inputValue.toLowerCase());
        if (params.inputValue !== "" && !exists) {
          filtered.push({ inputValue: params.inputValue, isNew: true });
        }
        return filtered;
      }}
      onChange={async (_e, newValue) => {
        if (!newValue) { onChange(""); return; }
        if (typeof newValue !== "string" && "isNew" in newValue) {
          const created = await addNew(newValue.inputValue, newValue.inputValue);
          onChange(created.code);
        } else if (typeof newValue !== "string") {
          onChange(newValue.code);
        }
      }}
      renderOption={(props, option) => (
        <li {...props} key={"code" in option ? option.code : option.inputValue}>
          {"isNew" in option ? `+ Add "${option.inputValue}" to Test Master` : `${option.code} — ${option.displayName}`}
        </li>
      )}
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. TAMC, PATHOGEN_ECOLI" />}
    />
  );
}
