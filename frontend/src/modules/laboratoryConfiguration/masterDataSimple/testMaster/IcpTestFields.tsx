import {
  Box,
  Button,
  FormHelperText,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import type { IcpElementRow, IcpTestForm, IcpTestMode, IcpSampleForm } from "./icpTestConfig";

export interface IcpTestFieldsProps {
  form: IcpTestForm;
  onChange: (form: IcpTestForm) => void;
}

// The ICP part of the Test Master dialog: assay (% of target) or impurity (ppm against the item's limit),
// the sample basis for the assay formula, and the elements the test reports.
export function IcpTestFields({ form, onChange }: IcpTestFieldsProps) {
  const set = (patch: Partial<IcpTestForm>) => onChange({ ...form, ...patch });
  const assay = form.mode === "Assay";
  const liquid = form.sampleForm === "Liquid";
  const updateElement = (index: number, patch: Partial<IcpElementRow>) =>
    set({ elements: form.elements.map((e, i) => (i === index ? { ...e, ...patch } : e)) });

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>ICP Assay Configuration</Typography>
      <Stack spacing={2}>
        <Box>
          <ToggleButtonGroup
            size="small"
            exclusive
            value={form.mode}
            onChange={(_, v: IcpTestMode | null) => v && set({ mode: v })}
            aria-label="ICP test type"
          >
            <ToggleButton value="Assay" sx={{ textTransform: "none", px: 2.5 }}>Assay (% of target)</ToggleButton>
            <ToggleButton value="Impurity" sx={{ textTransform: "none", px: 2.5 }}>Elemental impurity (ppm)</ToggleButton>
          </ToggleButtonGroup>
          <FormHelperText>
            {assay
              ? "The instrument result is compared with each element's target concentration; the item specification sets the assay limits."
              : "The instrument result (ppm) is judged by the item specification: not more than a limit, where 0 means not detected."}
          </FormHelperText>
        </Box>

        {assay && (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "flex-start" } }}>
            <Box>
              <ToggleButtonGroup
                size="small"
                exclusive
                value={form.sampleForm}
                onChange={(_, v: IcpSampleForm | null) => v && set({ sampleForm: v })}
                aria-label="Sample form"
              >
                <ToggleButton value="Solid" sx={{ textTransform: "none", px: 2.5 }}>Solid</ToggleButton>
                <ToggleButton value="Liquid" sx={{ textTransform: "none", px: 2.5 }}>Liquid</ToggleButton>
              </ToggleButtonGroup>
            </Box>
            <TextField
              size="small"
              type="number"
              required
              label={liquid ? "Theoretical volume (mL)" : "Theoretical weight (g)"}
              value={form.theoreticalAmount}
              onChange={(e) => set({ theoreticalAmount: e.target.value })}
              helperText={liquid ? "Th.Vol" : "Th.Wt"}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              sx={{ width: 220 }}
            />
            {liquid && (
              <TextField
                size="small"
                type="number"
                required
                label="Specific gravity"
                value={form.specificGravity}
                onChange={(e) => set({ specificGravity: e.target.value })}
                helperText="Sp.gr"
                slotProps={{ htmlInput: { min: 0, step: "any" } }}
                sx={{ width: 180 }}
              />
            )}
          </Stack>
        )}

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>Elements</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ width: 120 }}>Symbol</TableCell>
                {assay && <TableCell>Target concentration (ppm)</TableCell>}
                <TableCell sx={{ width: 56 }} />
              </TableRow>
            </TableHead>
            <TableBody>
              {form.elements.map((el, index) => (
                <TableRow key={el.id ?? `new-${index}`}>
                  <TableCell>
                    <TextField
                      size="small"
                      value={el.symbol}
                      onChange={(e) => updateElement(index, { symbol: e.target.value })}
                      placeholder="Zn"
                      slotProps={{ htmlInput: { maxLength: 3, "aria-label": `Element ${index + 1} symbol` } }}
                      sx={{ width: 100 }}
                    />
                  </TableCell>
                  {assay && (
                    <TableCell>
                      <TextField
                        size="small"
                        type="number"
                        value={el.targetPpm}
                        onChange={(e) => updateElement(index, { targetPpm: e.target.value })}
                        placeholder="10"
                        slotProps={{ htmlInput: { min: 0, step: "any", "aria-label": `Element ${index + 1} target ppm` } }}
                        sx={{ width: 180 }}
                      />
                    </TableCell>
                  )}
                  <TableCell>
                    <IconButton
                      size="small"
                      aria-label={`Remove element ${index + 1}`}
                      disabled={form.elements.length === 1}
                      onClick={() => set({ elements: form.elements.filter((_, i) => i !== index) })}
                    >
                      <DeleteOutlinedIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => set({ elements: [...form.elements, { symbol: "", targetPpm: "" }] })}
            sx={{ mt: 1, textTransform: "none" }}
          >
            Add element
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
