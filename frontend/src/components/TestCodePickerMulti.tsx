import { Autocomplete, TextField, Chip, Tooltip, type SxProps, type Theme } from "@mui/material";
import { useTestDefinitions, TestDefinitionOption } from "../hooks/useTestDefinitions";

interface TestCodePickerMultiProps {
  value: string[];
  onChange: (codes: string[]) => void;
  label?: string;
  sx?: SxProps<Theme>;
  editableCodes?: Set<string>;
}

// Multi-select version of TestCodePicker - for screens that assign
// several tests at once (an Item's AssignedTests, a WaterSamplingPoint's
// AssignedTestCodes). Same Test Master source; it only chooses existing
// tests (tests are created on the Test Master page). Not freeSolo, so typed
// text can never be emitted as a code.
export function TestCodePickerMulti({ value, onChange, label = "Assigned Tests", sx, editableCodes }: TestCodePickerMultiProps) {
  const { options, activeOptions } = useTestDefinitions();
  // Looked up against the full list (not just activeOptions) so tests
  // already assigned before being frozen still render as tags.
  const selected = options.filter((o) => value.includes(o.code));

  // If editableCodes is passed, only offer options inside the set.
  const availableOptions = editableCodes
    ? activeOptions.filter((o) => editableCodes.has(o.code))
    : activeOptions;

  return (
    <Autocomplete<TestDefinitionOption, true, false, false>
      multiple
      size="small"
      sx={sx}
      options={availableOptions}
      value={selected}
      noOptionsText="No such test. Add it in Test Master first."
      getOptionLabel={(o) => `${o.code}: ${o.displayName}`}
      isOptionEqualToValue={(o, v) => o.code === v.code}
      onChange={(_e, newValue) => {
        const codes = newValue.map((item) => item.code);
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
        <li {...props} key={option.code}>
          {`${option.code}: ${option.displayName}`}
        </li>
      )}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => {
          const isLocked = editableCodes ? !editableCodes.has(option.code) : false;
          if (isLocked) {
            const { onDelete: _onDelete, ...chipProps } = getItemProps({ index });
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
