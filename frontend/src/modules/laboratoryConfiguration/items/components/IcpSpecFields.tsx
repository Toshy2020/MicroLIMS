import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Typography
} from "@mui/material";

export interface IcpSpecElementOption {
  id: number;
  symbol: string;
  targetPpm?: number | null;
}

export interface IcpSpecFieldsProps {
  // What the test reports, from the Test Master.
  mode: "Assay" | "Impurity" | null;
  elements: IcpSpecElementOption[];
  selectedElementId: number | "";
  onElementChange: (id: number, element: IcpSpecElementOption | undefined) => void;
}

// The ICP part of an item specification: pick one of the test's elements. The test already defines the
// target concentration (assay) and the basis, so the item only needs the element and its limits.
export function IcpSpecFields({ mode, elements, selectedElementId, onElementChange }: IcpSpecFieldsProps) {
  const isImpurity = mode === "Impurity";

  return (
    <Box
      sx={{
        border: "1px solid",
        borderColor: "primary.main",
        borderRadius: 1,
        p: 2,
        bgcolor: "action.hover"
      }}
    >
      <Typography
        variant="caption"
        sx={{
          fontWeight: 700,
          letterSpacing: "0.5px",
          color: "primary.main",
          textTransform: "uppercase",
          display: "block",
          mb: 1.5
        }}
      >
        {isImpurity ? "Elemental Impurity (ICP) Specification" : "Assay (ICP) Specification"}
      </Typography>

      <FormControl size="small" fullWidth required>
        <InputLabel id="icp-spec-element-label">Element *</InputLabel>
        <Select
          labelId="icp-spec-element-label"
          label="Element *"
          value={selectedElementId}
          onChange={(e) => {
            const id = Number(e.target.value);
            onElementChange(id, elements.find((el) => el.id === id));
          }}
          inputProps={{ "aria-label": "Element" }}
        >
          {elements.length === 0 ? (
            <MenuItem disabled value="">
              <em>No elements configured on this test</em>
            </MenuItem>
          ) : (
            elements.map((el) => (
              <MenuItem key={el.id} value={el.id}>
                {el.symbol}
                {!isImpurity && el.targetPpm != null ? ` (target ${el.targetPpm} ppm)` : ""}
              </MenuItem>
            ))
          )}
        </Select>
        <FormHelperText>
          {isImpurity
            ? "Limit is not more than the value below in ppm; 0 means not detected."
            : "Limits apply to the assay % of the test's target concentration."}
        </FormHelperText>
      </FormControl>
    </Box>
  );
}
