import { Autocomplete, TextField, type SxProps, type Theme } from "@mui/material";
import { useTestDefinitions, TestDefinitionOption } from "../hooks/useTestDefinitions";

interface TestCodePickerProps {
  value: string;
  onChange: (code: string) => void;
  label?: string;
  size?: "small" | "medium";
  sx?: SxProps<Theme>;
  editableCodes?: Set<string>;
}

// Single TestCode picker sourced from the Test Master (GET
// /masterdata/test-definitions). It only chooses tests that already exist;
// tests are created on the Test Master page, so a mistyped code can never
// become a regulated master record from here. The Autocomplete is not
// freeSolo, so typed text can never be emitted as a code.
export function TestCodePicker({ value, onChange, label = "Test", size = "small", sx, editableCodes }: TestCodePickerProps) {
  const { options, activeOptions } = useTestDefinitions();
  // Looked up against the full list (not just activeOptions) so a value
  // already assigned to a since-frozen test still displays correctly.
  const selected = options.find((o) => o.code === value) ?? null;

  // If editableCodes is passed, only offer options inside the set.
  const availableOptions = editableCodes
    ? activeOptions.filter((o) => editableCodes.has(o.code))
    : activeOptions;

  return (
    <Autocomplete<TestDefinitionOption, false, false, false>
      size={size}
      sx={sx}
      options={availableOptions}
      value={selected}
      noOptionsText="No such test. Add it in Test Master first."
      getOptionLabel={(o) => `${o.code}: ${o.displayName}`}
      isOptionEqualToValue={(o, v) => o.code === v.code}
      onChange={(_e, newValue) => {
        onChange(newValue ? newValue.code : "");
      }}
      renderOption={(props, option) => (
        <li {...props} key={option.code}>
          {`${option.code}: ${option.displayName}`}
        </li>
      )}
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. TAMC, PATHOGEN_ECOLI" />}
    />
  );
}
