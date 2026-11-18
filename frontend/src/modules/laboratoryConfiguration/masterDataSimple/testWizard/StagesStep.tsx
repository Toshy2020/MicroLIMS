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
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../../i18n";
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
  const { t } = useTranslation("masterData");
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
      setDialogError(t("testWizard.stages.errors.roleRequired"));
      return;
    }
    const smpStr = sampleReplicates.trim();
    if (!smpStr) {
      setDialogError(t("testWizard.stages.errors.replicatesRequired"));
      return;
    }
    const smp = Number(smpStr);
    if (!Number.isInteger(smp) || smp < 1) {
      setDialogError(t("testWizard.stages.errors.replicatesInteger"));
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
            {t("testWizard.stages.heading", { count: model.stages.length })}
          </Typography>
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={openAdd}
            disabled={availableRoles.length === 0}
          >
            {t("testWizard.stages.addButton")}
          </Button>
        </Stack>

        <TableContainer>
          <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>{t("testWizard.stages.columns.stageRole")}</TableCell>
                <TableCell>{t("testWizard.stages.columns.sampleReplicates")}</TableCell>
                <TableCell align="right">{t("testWizard.stages.columns.actions")}</TableCell>
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
                    <IconButton aria-label={t("testWizard.stages.editAria")} size="small" onClick={() => openEdit(idx, r)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      aria-label={t("testWizard.stages.deleteAria")}
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
                    {t("testWizard.stages.empty")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>

        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
          {t("testWizard.stages.footnote")}
        </Typography>
      </Box>

      <FloatingDialog
        open={dialogOpen}
        title={
          editingIndex !== null && model.stages[editingIndex]
            ? t("testWizard.stages.dialog.editTitle", { role: model.stages[editingIndex].role })
            : t("testWizard.stages.dialog.addTitle")
        }
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} variant="outlined">
              {t("testWizard.stages.dialog.cancel")}
            </Button>
            <Button variant="contained" onClick={handleSaveRow}>
              {editingIndex !== null ? t("testWizard.stages.dialog.save") : t("testWizard.stages.dialog.add")}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2} sx={{ pt: 1 }}>
          {editingIndex !== null ? (
            <TextField size="small" label={t("testWizard.stages.dialog.stageRoleLabel")} value={role} disabled fullWidth />
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="wizard-stage-rep-role-label">{t("testWizard.stages.dialog.stageRoleLabel")}</InputLabel>
              <Select<ProductionStageRole>
                labelId="wizard-stage-rep-role-label"
                label={t("testWizard.stages.dialog.stageRoleLabel")}
                value={role as ProductionStageRole}
                onChange={(e) => setRole(e.target.value as ProductionStageRole)}
                inputProps={{ "aria-label": t("testWizard.stages.dialog.stageRoleLabel") }}
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
            label={t("testWizard.stages.dialog.sampleReplicatesLabel")}
            placeholder={t("testWizard.stages.dialog.sampleReplicatesPlaceholder")}
            value={sampleReplicates}
            onChange={(e) => setSampleReplicates(e.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText={t("testWizard.stages.dialog.sampleReplicatesHelper")}
            required
            fullWidth
          />
        </Stack>
      </FloatingDialog>
    </Stack>
  );
}

export function createStagesStep(
  getT: () => TFunction = () => i18n.getFixedT("en", "masterData")
): WizardStep<TestFormModel> {
  return {
    key: "stages",
    title: getT()("testWizard.stages.title"),
    applies: (model) =>
      model.lab === "fp" &&
      model.workflowType !== "Titration" &&
      model.workflowType !== "UvSpectrophotometry",
    validate: (model) => validateStep("stages", model, getT()),
    render: ({ model, set, errors }) => <StagesStepView model={model} set={set} errors={errors} />,
    summary: (model) => {
      const t = getT();
      return [
        {
          label: t("testWizard.stages.summary.label"),
          value:
            model.stages.length > 0
              ? model.stages.map((s) => `${s.role}: ${s.sampleReplicates}`).join(", ")
              : t("testWizard.stages.summary.none"),
          field: "stages"
        }
      ];
    }
  };
}
