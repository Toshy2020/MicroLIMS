import { Autocomplete, Chip, TextField } from "@mui/material";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";

// Multi-select limited to pathogen tests (Test Master workflow type
// "Observation"). Codes already configured stay visible even if the test
// has since been frozen.
export function PathogenPicker({ value, onChange }: { value: string[]; onChange: (codes: string[]) => void }) {
  const { options } = useTestDefinitions();
  const pathogens = options.filter((o) => o.workflowType === "Observation" && (o.isActive || value.includes(o.code)));
  const label = (code: string) => {
    const o = options.find((x) => x.code === code);
    return o?.displayName && o.displayName !== code ? `${o.displayName} (${code})` : code;
  };
  return (
    <Autocomplete
      multiple
      size="small"
      options={pathogens.map((o) => o.code)}
      value={value}
      onChange={(_, codes) => onChange(codes)}
      getOptionLabel={label}
      filterSelectedOptions
      renderValue={(codes, getItemProps) =>
        codes.map((code, index) => {
          const { key, ...itemProps } = getItemProps({ index });
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
