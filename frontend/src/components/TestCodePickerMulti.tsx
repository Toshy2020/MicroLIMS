import { Autocomplete, TextField, Chip, createFilterOptions } from "@mui/material";
import { useTestDefinitions, TestDefinitionOption } from "../hooks/useTestDefinitions";

interface TestCodePickerMultiProps {
  value: string[];
  onChange: (codes: string[]) => void;
  label?: string;
  sx?: any;
}

type Option = TestDefinitionOption | { inputValue: string; isNew: true };

const filter = createFilterOptions<Option>();

// Multi-select version of TestCodePicker - for screens that assign
// several tests at once (an Item's AssignedTests, a WaterSamplingPoint's
// AssignedTestCodes). Same Test Master source, same inline "add new"
// support.
export function TestCodePickerMulti({ value, onChange, label = "Assigned Tests", sx }: TestCodePickerMultiProps) {
  const { options, activeOptions, addNew } = useTestDefinitions();
  // Looked up against the full list (not just activeOptions) so tests
  // already assigned before being frozen still render as tags.
  const selected = options.filter((o) => value.includes(o.code));

  return (
    <Autocomplete<Option, true, false, false>
      multiple
      size="small"
      sx={sx}
      options={activeOptions}
      value={selected}
      getOptionLabel={(o) => (typeof o === "string" ? o : "inputValue" in o ? o.inputValue : `${o.code} — ${o.displayName}`)}
      filterOptions={(opts, params) => {
        const filtered = filter(opts, params);
        // Checked against the full Test Master so a frozen-but-existing
        // code doesn't show a misleading "Add ... to Test Master".
        const exists = options.some((o) => o.code.toLowerCase() === params.inputValue.toLowerCase());
        if (params.inputValue !== "" && !exists) {
          filtered.push({ inputValue: params.inputValue, isNew: true });
        }
        return filtered;
      }}
      onChange={async (_e, newValue) => {
        const codes: string[] = [];
        for (const item of newValue) {
          if (typeof item === "string") { codes.push(item); continue; }
          if ("isNew" in item) {
            const created = await addNew(item.inputValue, item.inputValue);
            codes.push(created.code);
          } else {
            codes.push(item.code);
          }
        }
        onChange(codes);
      }}
      renderOption={(props, option) => (
        <li {...props} key={"code" in option ? option.code : option.inputValue}>
          {"isNew" in option ? `+ Add "${option.inputValue}" to Test Master` : `${option.code} — ${option.displayName}`}
        </li>
      )}
      renderTags={(tagValue, getTagProps) =>
        tagValue.map((option, index) =>
          "code" in option ? <Chip label={option.code} size="small" {...getTagProps({ index })} key={option.code} /> : null
        )
      }
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. TAMC, PATHOGEN_ECOLI" />}
    />
  );
}
