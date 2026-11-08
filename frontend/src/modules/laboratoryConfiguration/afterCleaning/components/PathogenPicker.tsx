import { Autocomplete, Chip, TextField, Tooltip } from "@mui/material";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";

interface PathogenPickerProps {
  value: string[];
  onChange: (codes: string[]) => void;
  editableCodes?: Set<string>;
}

// Multi-select limited to pathogen tests (Test Master workflow type
// "Observation"). Codes already configured stay visible even if the test
// has since been frozen.
export function PathogenPicker({ value, onChange, editableCodes }: PathogenPickerProps) {
  const { options } = useTestDefinitions();
  const pathogens = options.filter((o) => o.workflowType === "Observation" && (o.isActive || value.includes(o.code)));
  const availablePathogens = pathogens.filter((o) => !editableCodes || editableCodes.has(o.code));

  const label = (code: string) => {
    const o = options.find((x) => x.code === code);
    return o?.displayName && o.displayName !== code ? `${o.displayName} (${code})` : code;
  };

  return (
    <Autocomplete
      multiple
      size="small"
      options={availablePathogens.map((o) => o.code)}
      value={value}
      onChange={(_, codes) => {
        if (editableCodes) {
          const locked = value.filter((c) => !editableCodes.has(c));
          const merged = Array.from(new Set([...locked, ...codes]));
          onChange(merged);
        } else {
          onChange(codes);
        }
      }}
      getOptionLabel={label}
      filterSelectedOptions
      renderValue={(codes, getItemProps) =>
        codes.map((code, index) => {
          const { key, ...itemProps } = getItemProps({ index });
          const isLocked = editableCodes ? !editableCodes.has(code) : false;
          if (isLocked) {
            const { onDelete, ...chipProps } = itemProps;
            const opt = options.find((x) => x.code === code);
            const sectionName = opt?.section?.name || "Owned by another laboratory";
            return (
              <Tooltip key={key} title={sectionName}>
                <span>
                  <Chip size="small" label={label(code)} {...chipProps} />
                </span>
              </Tooltip>
            );
          }
          return <Chip key={key} size="small" label={label(code)} {...itemProps} />;
        })
      }
      renderInput={(params) => (
        <TextField {...params} label="Pathogen tests" placeholder={value.length === 0 ? "e.g. E. coli, Salmonella" : undefined} />
      )}
      noOptionsText="No pathogen tests in the Test Master"
    />
  );
}
