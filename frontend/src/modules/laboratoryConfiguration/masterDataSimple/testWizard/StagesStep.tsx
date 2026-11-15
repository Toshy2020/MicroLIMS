import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import type { WizardStep } from "../../../../components/config/useWizard";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import type { ProductionStageRole } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import { ALL_STAGE_ROLES } from "../testMaster/testMasterConstants";
import { DraftStageReplicate, TestFormModel, validateStep } from "./testFormModel";

interface StagesViewProps {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

function StagesStepView({ model, set, errors }: StagesViewProps) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [role, setRole] = useState<ProductionStageRole | "">("");
  const [sampleReplicates, setSampleReplicates] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);

  const configuredRoles = new Set(model.stages.map((r) => r.role));
  const availableRoles = ALL_STAGE_ROLES.filter((r) => !configuredRoles.has(r));

  const openAdd = () => {
    setEditingIndex(null);
    setRole(availableRoles[0] ?? "");
    setSampleReplicates("");
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (index: number, item: DraftStageReplicate) => {
    setEditingIndex(index);
    setRole(item.role);
    setSampleReplicates(item.sampleReplicates);
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSaveRow = () => {
    if (editingIndex === null && !role) {
      setDialogError("Production stage role is required.");
      return;
    }
    const smpStr = sampleReplicates.trim();
    if (!smpStr) {
      setDialogError("Sample replicates is required.");
      return;
    }
    const smp = Number(smpStr);
    if (!Number.isInteger(smp) || smp < 1) {
      setDialogError("Sample replicates must be an integer greater than or equal to 1.");
      return;
    }

    if (editingIndex !== null) {
      set({
        stages: model.stages.map((r, i) =>
          i === editingIndex ? { ...r, sampleReplicates: String(smp) } : r
        )
      });
    } else {
      set({
        stages: [...model.stages, { role: role as ProductionStageRole, sampleReplicates: String(smp) }]
      });
    }
    setDialogOpen(false);
  };

  const handleDeleteRow = (index: number) => {
    set({ stages: model.stages.filter((_, i) => i !== index) });
  };

  return (
    <Stack spacing={2}>
      {errors.stages && (
        <Alert severity="error" role="alert">
          {errors.stages}
        </Alert>
      )}
      <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
        <Stack sx={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
            Replicates per stage ({model.stages.length} configured)
          </Typography>
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={openAdd}
            disabled={availableRoles.length === 0}
          >
            Add Stage Replicate
          </Button>
        </Stack>

        <TableContainer>
          <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>Stage Role</TableCell>
                <TableCell>Sample Replicates</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {model.stages.map((r, idx) => (
                <TableRow key={r.id ?? `${r.role}-${idx}`}>
                  <TableCell>
                    <Chip size="small" label={r.role} variant="outlined" sx={{ fontWeight: 600 }} />
                  </TableCell>
                  <TableCell>{r.sampleReplicates}</TableCell>
                  <TableCell align="right">
                    <IconButton aria-label="Edit replicates" size="small" onClick={() => openEdit(idx, r)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      aria-label="Delete replicate rule"
                      size="small"
                      color="error"
                      onClick={() => handleDeleteRow(idx)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {model.stages.length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} align="center" sx={{ py: 2, color: "text.secondary" }}>
                    No stage replicates configured yet. Click &quot;Add Stage Replicate&quot; to configure replicate counts.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>

        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
          Production stages without a configured row are not configured for this test (not zero). Counts must be integers ≥ 1. HPLC standard injections are set on the method.
        </Typography>
      </Box>

      <FloatingDialog
        open={dialogOpen}
        title={
          editingIndex !== null && model.stages[editingIndex]
            ? `Edit Stage Replicates: ${model.stages[editingIndex].role}`
            : "Add Stage Replicate"
        }
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} variant="outlined">
              Cancel
            </Button>
            <Button variant="contained" onClick={handleSaveRow}>
              {editingIndex !== null ? "Save" : "Add"}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2} sx={{ pt: 1 }}>
          {editingIndex !== null ? (
            <TextField size="small" label="Stage Role" value={role} disabled fullWidth />
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="wizard-stage-rep-role-label">Stage Role</InputLabel>
              <Select<ProductionStageRole>
                labelId="wizard-stage-rep-role-label"
                label="Stage Role"
                value={role as ProductionStageRole}
                onChange={(e) => setRole(e.target.value as ProductionStageRole)}
                inputProps={{ "aria-label": "Stage Role" }}
              >
                {availableRoles.map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          <TextField
            size="small"
            type="number"
            label="Sample Replicates *"
            placeholder="e.g. 2"
            value={sampleReplicates}
            onChange={(e) => setSampleReplicates(e.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText="Integer ≥ 1"
            required
            fullWidth
          />
        </Stack>
      </FloatingDialog>
    </Stack>
  );
}

export function createStagesStep(): WizardStep<TestFormModel> {
  return {
    key: "stages",
    title: "Stages",
    applies: (model) =>
      model.lab === "fp" &&
      model.workflowType !== "Titration" &&
      model.workflowType !== "UvSpectrophotometry",
    validate: (model) => validateStep("stages", model),
    render: ({ model, set, errors }) => <StagesStepView model={model} set={set} errors={errors} />,
    summary: (model) => [
      {
        label: "Replicates per stage",
        value:
          model.stages.length > 0
            ? model.stages.map((s) => `${s.role}: ${s.sampleReplicates}`).join(", ")
            : "None",
        field: "stages"
      }
    ]
  };
}
