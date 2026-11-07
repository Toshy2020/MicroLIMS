import { Autocomplete, TextField, Chip, createFilterOptions, Tooltip } from "@mui/material";
import { useTestDefinitions, TestDefinitionOption } from "../hooks/useTestDefinitions";

interface TestCodePickerMultiProps {
  value: string[];
  onChange: (codes: string[]) => void;
  label?: string;
  sx?: any;
  editableCodes?: Set<string>;
}

type Option = TestDefinitionOption | { inputValue: string; isNew: true };

const filter = createFilterOptions<Option>();

// Multi-select version of TestCodePicker - for screens that assign
// several tests at once (an Item's AssignedTests, a WaterSamplingPoint's
// AssignedTestCodes). Same Test Master source, same inline "add new"
// support.
export function TestCodePickerMulti({ value, onChange, label = "Assigned Tests", sx, editableCodes }: TestCodePickerMultiProps) {
  const { options, activeOptions, addNew } = useTestDefinitions();
  // Looked up against the full list (not just activeOptions) so tests
  // already assigned before being frozen still render as tags.
  const selected = options.filter((o) => value.includes(o.code));

  // If editableCodes is passed, only offer options inside the set.
  const availableOptions = editableCodes
    ? activeOptions.filter((o) => editableCodes.has(o.code))
    : activeOptions;

  return (
    <Autocomplete<Option, true, false, false>
      multiple
      size="small"
      sx={sx}
      options={availableOptions}
      value={selected}
      getOptionLabel={(o) => (typeof o === "string" ? o : "inputValue" in o ? o.inputValue : `${o.code}: ${o.displayName}`)}
      filterOptions={(opts, params) => {
        const filtered = filter(opts, params);
        // Checked against the full Test Master so a frozen-but-existing
        // code doesn't show a misleading "Add ... to Test Master".
        const exists = options.some((o) => o.code.toLowerCase() === params.inputValue.toLowerCase());
        if (!editableCodes && params.inputValue !== "" && !exists) {
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
        // Preserve locked codes from other labs that cannot be removed
        if (editableCodes) {
          for (const code of value) {
            if (!editableCodes.has(code) && !codes.includes(code)) {
              codes.push(code);
            }
          }
        }
        onChange(codes);
      }}
      renderOption={(props, option) => (
        <li {...props} key={"code" in option ? option.code : option.inputValue}>
          {"isNew" in option ? `+ Add "${option.inputValue}" to Test Master` : `${option.code}: ${option.displayName}`}
        </li>
      )}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => {
          if (!("code" in option)) return null;
          const isLocked = editableCodes ? !editableCodes.has(option.code) : false;
          if (isLocked) {
            const { onDelete, ...chipProps } = getItemProps({ index });
            const sectionName = option.section?.name || "Owned by another laboratory";
            return (
              <Tooltip title={sectionName} key={option.code}>
                <span>
                  <Chip label={option.code} size="small" {...chipProps} />
                </span>
              </Tooltip>
            );
          }
          return <Chip label={option.code} size="small" {...getItemProps({ index })} key={option.code} />;
        })
      }
      renderInput={(params) => <TextField {...params} label={label} placeholder="e.g. TAMC, PATHOGEN_ECOLI" />}
    />
  );
}
