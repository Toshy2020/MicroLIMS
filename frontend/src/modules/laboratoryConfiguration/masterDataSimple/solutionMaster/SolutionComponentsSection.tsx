import {
  Paper,
  Typography,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Stack,
  IconButton,
  Tooltip
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { SolutionComponentUnit } from "../services/SolutionMasterService";
import { ComponentRowState, COMPONENT_UNIT_OPTIONS } from "./solutionForm";

export interface SolutionComponentsSectionProps {
  components: ComponentRowState[];
  availableMaterialEntries: MaterialMasterEntry[];
  onAddComponent: () => void;
  onRemoveComponent: (index: number) => void;
  onMoveComponent: (index: number, direction: "up" | "down") => void;
  onComponentChange: (
    index: number,
    field: keyof ComponentRowState,
    value: string | number
  ) => void;
}

export function SolutionComponentsSection({
  components,
  availableMaterialEntries,
  onAddComponent,
  onRemoveComponent,
  onMoveComponent,
  onComponentChange
}: SolutionComponentsSectionProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <div>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
            Recipe Components *
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Ordered list of active material master entries from the same laboratory section.
          </Typography>
        </div>
        <Button
          size="small"
          startIcon={<AddIcon />}
          onClick={onAddComponent}
          variant="outlined"
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          Add Component
        </Button>
      </Stack>

      <Stack spacing={1.5}>
        {components.map((comp, idx) => (
          <Paper
            key={idx}
            variant="outlined"
            sx={{
              p: 1.5,
              bgcolor: "background.paper",
              borderRadius: 1.5,
              display: "flex",
              alignItems: "center",
              gap: 1.5,
              flexWrap: { xs: "wrap", md: "nowrap" }
            }}
          >
            <Typography
              variant="caption"
              sx={{
                fontWeight: 700,
                width: 24,
                color: "text.secondary",
                textAlign: "center"
              }}
            >
              #{idx + 1}
            </Typography>

            <FormControl size="small" sx={{ flex: 2, minWidth: 200 }} required>
              <InputLabel id={`comp-entry-label-${idx}`}>Material Master Entry</InputLabel>
              <Select
                labelId={`comp-entry-label-${idx}`}
                label="Material Master Entry"
                value={comp.materialMasterEntryId}
                onChange={(e) =>
                  onComponentChange(idx, "materialMasterEntryId", e.target.value)
                }
              >
                {availableMaterialEntries.map((m) => (
                  <MenuItem key={m.id} value={m.id}>
                    {m.code} - {m.name} {!m.isActive ? "(Inactive)" : ""}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              label="Quantity"
              type="number"
              size="small"
              required
              value={comp.quantity}
              onChange={(e) => onComponentChange(idx, "quantity", e.target.value)}
              sx={{ width: { xs: "100%", sm: 120 } }}
              slotProps={{ htmlInput: { min: "0.0001", step: "any" } }}
            />

            <FormControl size="small" sx={{ width: { xs: "100%", sm: 160 } }} required>
              <InputLabel id={`comp-unit-label-${idx}`}>Unit</InputLabel>
              <Select
                labelId={`comp-unit-label-${idx}`}
                label="Unit"
                value={comp.unit}
                onChange={(e) =>
                  onComponentChange(idx, "unit", e.target.value as SolutionComponentUnit)
                }
              >
                {COMPONENT_UNIT_OPTIONS.map((opt) => (
                  <MenuItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Stack direction="row" spacing={0.5} sx={{ ml: "auto" }}>
              <Tooltip title="Move Up">
                <span>
                  <IconButton aria-label="Move Up"
                    size="small"
                    disabled={idx === 0}
                    onClick={() => onMoveComponent(idx, "up")}
                  >
                    <ArrowUpwardIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
              <Tooltip title="Move Down">
                <span>
                  <IconButton aria-label="Move Down"
                    size="small"
                    disabled={idx === components.length - 1}
                    onClick={() => onMoveComponent(idx, "down")}
                  >
                    <ArrowDownwardIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
              <Tooltip title="Remove Component">
                <span>
                  <IconButton aria-label="Remove Component"
                    size="small"
                    color="error"
                    disabled={components.length <= 1}
                    onClick={() => onRemoveComponent(idx)}
                  >
                    <DeleteOutlinedIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
            </Stack>
          </Paper>
        ))}
      </Stack>
    </Paper>
  );
}
