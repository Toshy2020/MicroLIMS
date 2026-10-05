import { useEffect, useState } from "react";
import {
  TextField,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Stack,
  Alert,
  IconButton,
  Select,
  MenuItem,
  Box,
  Typography,
  Checkbox,
  FormControlLabel,
  Chip,
  Tooltip,
  FormControl,
  InputLabel,
  FormHelperText,
  Switch,
  TableContainer
} from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import EditIcon from "@mui/icons-material/Edit";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteIcon from "@mui/icons-material/Delete";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { LoadErrorAlert, LoadFailuresAlert } from "../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../hooks/useLoadFailures";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import {
  masterDataOptions,
  incubationConditionLabel,
  MediaIncubationConditionOption,
  CreateTestDefinitionPayload,
  UpdateTestDefinitionPayload,
  TestDefinitionStageReplicateDto,
  ProductionStageRole
} from "../../../services/masterDataOptions";
import { tableHeadSx } from "../../../theme";
import { HplcMethodService, HplcMethodListItem } from "./services/HplcMethodService";
import { IcpMethodService, IcpMethodListItem } from "./services/IcpMethodService";
import { TitrationConfigSection } from "./TitrationConfigSection";
import {
  TitrationFormState,
  createInitialTitrationForm,
  titrationFormFromDefinition,
  titrationPayloadFields,
  validateTitrationForm
} from "./titrationConfig";
import { PageArea, PhyschemArea, areaIncludes, defaultAreaFor } from "./testMasterArea";
import { TestAreaField } from "./TestAreaField";
import {
  TitrationChangeReasonField,
  hasTitrationSettingsChanged,
  titrationChangeReasonCheck
} from "./TitrationChangeReasonField";

// Microbiology and the Finished Product (chemistry) lab each have their own
// Test Master page: same component, filtered to the lab's section and
// workflow types. FP tests have no workflow steps or media.
export type TestMasterLab = "micro" | "fp";
const FP_SECTION_CODE = "FP";
const WORKFLOW_TYPES_BY_LAB: Record<TestMasterLab, string[]> = {
  micro: ["CountTest", "Observation"],
  fp: ["HplcMethodAssay", "IcpMethodAssay", "Measurement", "Gravimetric", "Qualitative", "Dissolution", "Disintegration", "WeightVariation", "Titration"]
};
const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  CountTest: "Count Test",
  Observation: "Observation",
  HplcMethodAssay: "HPLC method assay",
  IcpMethodAssay: "ICP method assay",
  Measurement: "Measurement",
  Gravimetric: "Gravimetric",
  Qualitative: "Qualitative",
  Dissolution: "Dissolution",
  Disintegration: "Disintegration",
  WeightVariation: "Weight Variation",
  Titration: "Titration (assay)"
};

const EQUATION_TYPES = [
  "None",
  "HplcMethodAssay",
  "IcpMethodAssay",
  "SystemSuitability",
  "Measurement",
  "GravimetricLoss",
  "GravimetricResidue",
  "Qualitative",
  "Dissolution",
  "Disintegration",
  "WeightVariation",
  "Titration"
];
const EQUATION_TYPE_LABELS: Record<string, string> = {
  None: "None",
  HplcMethodAssay: "HPLC method assay",
  IcpMethodAssay: "ICP method assay",
  SystemSuitability: "System Suitability",
  Measurement: "Measurement (pH, density…)",
  GravimetricLoss: "Loss on drying / Gravimetric loss",
  GravimetricResidue: "Ash / Gravimetric residue",
  Qualitative: "Qualitative (appearance, ID)",
  Dissolution: "Dissolution (HPLC finish, staged S1-S3)",
  Disintegration: "Disintegration (time per unit, staged)",
  WeightVariation: "Weight Variation (USP <2091>, staged)",
  Titration: "Titration (USP <541>)"
};
const STEP_TYPES = ["PlateCount", "BrothEnrichment", "SelectiveBroth", "SelectivePlating", "ConfirmatoryPlating", "BiochemicalTest"];
const STEP_TYPES_REQUIRING_ORGANISM = ["SelectivePlating", "ConfirmatoryPlating"];
const STEP_TYPES_WITH_NO_MEDIA = ["BiochemicalTest"];
// Not server-enforced for PlateCount, but Broth/SelectiveBroth/SelectivePlating
// each require exactly one medium (WorkflowTemplateValidator rules 1-2) - this
// caps the editor at one row so the analyst gets that feedback immediately
// instead of only on server rejection.
const STEP_TYPES_SINGLE_MEDIA = ["BrothEnrichment", "SelectiveBroth", "SelectivePlating"];

const PHENOTYPIC_TEST_TYPES = ["Gram", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "IdentificationKit"];
const PHENOTYPIC_TEST_TYPE_LABELS: Record<string, string> = {
  Gram: "Gram Stain", Catalase: "Catalase", Oxidase: "Oxidase", Coagulase: "Coagulase",
  Antibiogram: "Antibiogram", IdentificationKit: "Identification Kit"
};

// Each step medium picks one incubation condition of its material's media
// product. The server copies that condition's temperature and hours onto
// the step medium when it saves (MasterDataController.BuildStepMediaAsync).
type StepMediaRow = { materialId: number | ""; mediaIncubationConditionId: number | ""; isRequired: boolean; displayOrder: number };

interface StepFormState {
  stepName?: string;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId: number | null;
  stepMedia: StepMediaRow[];
  // PlateCount only (backend TestWorkflowStep.RequiresIncubationTransfer /
  // WorkflowTemplateValidator rule 7). Stage 2's own window is a separate
  // TestWorkflowStepIncubationStage row (StageNumber 2), not more columns
  // on this step - see stage2* fields below.
  requiresIncubationTransfer: boolean;
  stage2TempMin?: string | number;
  stage2TempMax?: string | number;
  stage2IncubationMinHours?: string | number;
  stage2IncubationMaxHours?: string | number;
  // Older single-value field, kept for backward compatibility with
  // existing chained-step templates - the Add Step form below now uses
  // phenotypicTestTypes (a bundle of one or more) instead, letting one
  // step cover e.g. Gram Stain + Oxidase + Identification Kit together.
  phenotypicTestType?: string | null;
  phenotypicTestTypes: string[];
}

// A function rather than a shared constant object, so every reset gets its
// own stepMedia array instead of every WorkflowStepsSection instance (one
// per expanded Test Master row) sharing a single mutable reference.
const defaultStepForm = (): StepFormState => ({
  isFinalStep: false, stepType: "PlateCount", targetOrganismId: null, stepMedia: [], requiresIncubationTransfer: false,
  phenotypicTestType: null, phenotypicTestTypes: []
});

// Mirrors WorkflowTemplateValidator's six structural rules (backend
// MicroLIMS.Application/Services/WorkflowTemplateValidator.cs) so the admin
// gets immediate feedback - the server is still authoritative and any
// rejection it returns is surfaced as-is.
function validateStepForm(form: StepFormState): string | null {
  const isBroth = form.stepType === "BrothEnrichment" || form.stepType === "SelectiveBroth";
  const isSelectivePlating = form.stepType === "SelectivePlating";
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  if (isBroth && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A broth step must have exactly one assigned medium, marked as required.";
  if (isBroth && form.targetOrganismId)
    return "A broth step must not target an organism.";
  if (isSelectivePlating && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A selective plating step must have exactly one assigned medium, marked as required.";
  if (isSelectivePlating && !form.targetOrganismId)
    return "A selective plating step must target an organism.";
  if (isConfirmatory && form.stepMedia.length === 0)
    return "A confirmatory plating step must have at least one permitted medium.";
  if (isConfirmatory && !form.targetOrganismId)
    return "A confirmatory plating step must target an organism.";
  if (isBiochemical && form.stepMedia.length > 0)
    return "A biochemical test step must have no assigned media.";
  if (isBiochemical && form.targetOrganismId)
    return "A biochemical test step must not target an organism.";
  if (isBiochemical && !form.phenotypicTestType && form.phenotypicTestTypes.length === 0)
    return "A biochemical test step must specify at least one phenotypic test type.";
  if (isBiochemical && new Set(form.phenotypicTestTypes).size !== form.phenotypicTestTypes.length)
    return "The same phenotypic test type cannot be assigned to this step more than once.";
  if (!isBiochemical && (form.phenotypicTestType || form.phenotypicTestTypes.length > 0))
    return "Only a biochemical test step may specify a phenotypic test type.";
  // Mirrors WorkflowTemplateValidator rule 8 - every non-biochemical step
  // needs at least one medium. Rules above already cover Broth/Selective/
  // Confirmatory; this is PlateCount's only media check.
  if (form.stepType === "PlateCount" && form.stepMedia.length === 0)
    return "At least one medium is required for this step type.";
  for (const m of form.stepMedia) {
    if (m.materialId === "") return "Every medium row needs a selected material.";
    if (m.mediaIncubationConditionId === "") return "Every medium row needs an incubation condition.";
  }
  const materialIds = form.stepMedia.map((m) => m.materialId);
  if (new Set(materialIds).size !== materialIds.length) return "The same medium cannot be assigned to this step more than once.";

  // Mirrors WorkflowTemplateValidator rule 7 - the server is still
  // authoritative, this only spares a round trip for the common case.
  if (form.stepType === "PlateCount" && form.requiresIncubationTransfer) {
    const { stage2TempMin, stage2TempMax, stage2IncubationMinHours, stage2IncubationMaxHours } = form;
    if (stage2TempMin === undefined || stage2TempMin === "" || stage2TempMax === undefined || stage2TempMax === "" ||
        stage2IncubationMinHours === undefined || stage2IncubationMinHours === "" ||
        stage2IncubationMaxHours === undefined || stage2IncubationMaxHours === "")
      return "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range.";
    if (Number(stage2TempMin) >= Number(stage2TempMax))
      return "Stage 2's minimum temperature must be below its maximum.";
    if (Number(stage2IncubationMinHours) <= 0 || Number(stage2IncubationMaxHours) < Number(stage2IncubationMinHours))
      return "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum.";
  }
  return null;
}

// True for rows the pathogen-workflow migration could not backfill
// (TargetOrganismId/StepMedia are per-step and the migration had no source
// data for them) - flags templates that will fail validation the first time
// an analyst tries to run them, so an admin can find them here instead.
// The step's own incubationMinHours/MaxHours/temperatureMin/Max are no
// longer authoritative (see TestWorkflowEngine.cs) - a step with more than
// one permitted medium can have genuinely different windows per medium
// (e.g. Confirmatory Plating's XLD vs TSI), so stage 1's display is built
// from the picked media's own ranges instead, joined when they differ.
interface WorkflowStepMediaItem {
  materialId: number;
  materialName?: string;
  mediaIncubationConditionId?: number | string | null;
  isRequired?: boolean;
  displayOrder?: number;
  incubationMinHours?: number;
  incubationMaxHours?: number;
  tempMin?: number;
  tempMax?: number;
  [key: string]: unknown;
}

function stage1Ranges(stepMedia: WorkflowStepMediaItem[] | undefined, min: string, max: string): string {
  if (!stepMedia?.length) return "—";
  const distinct = Array.from(new Set(stepMedia.map((m) => `${String(m[min])}-${String(m[max])}`)));
  return distinct.join("; ");
}

interface StepCheckItem {
  stepType?: string;
  targetOrganismId?: number | null;
  stepMedia?: unknown[];
  phenotypicTestType?: string | null;
  phenotypicTestTypes?: string[];
}

function stepNeedsConfiguration(s: StepCheckItem): boolean {
  if (s.stepType && STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (s.stepType !== "BiochemicalTest" && (s.stepMedia?.length ?? 0) === 0) return true;
  if (s.stepType === "BiochemicalTest" && !s.phenotypicTestType && (s.phenotypicTestTypes?.length ?? 0) === 0) return true;
  return false;
}


const ALL_STAGE_ROLES: ProductionStageRole[] = [
  "Bulk",
  "InProcess",
  "Finished",
  "Stability",
  "Other"
];

function TestStageReplicatesSection({ testDefinitionId }: { testDefinitionId: number }) {
  const [replicates, setReplicates] = useState<TestDefinitionStageReplicateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingReplicate, setEditingReplicate] = useState<TestDefinitionStageReplicateDto | null>(null);
  const [pendingDelete, setPendingDelete] = useState<TestDefinitionStageReplicateDto | null>(null);
  const [role, setRole] = useState<ProductionStageRole | "">("");
  const [sampleReplicates, setSampleReplicates] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadReplicates = () => {
    setLoading(true);
    setError(null);
    masterDataOptions
      .getTestDefinitionStageReplicates(testDefinitionId)
      .then(setReplicates)
      .catch((e: unknown) => {
        const errObj = e as { response?: { data?: { message?: string } }; message?: string };
        setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load stage replicates.");
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadReplicates();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testDefinitionId]);

  const configuredRoles = new Set(replicates.map((r) => r.role));
  const availableRoles = ALL_STAGE_ROLES.filter((r) => !configuredRoles.has(r));

  const openAdd = () => {
    setEditingReplicate(null);
    setRole(availableRoles[0] ?? "");
    setSampleReplicates("");
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (r: TestDefinitionStageReplicateDto) => {
    setEditingReplicate(r);
    setRole(r.role);
    setSampleReplicates(String(r.sampleReplicates));
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSave = async () => {
    if (!editingReplicate && !role) {
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

    setSaving(true);
    setDialogError(null);
    try {
      if (editingReplicate) {
        await masterDataOptions.updateTestDefinitionStageReplicate(testDefinitionId, editingReplicate.id, {
          sampleReplicates: smp
        }, editingReplicate.version);
      } else {
        await masterDataOptions.createTestDefinitionStageReplicate(testDefinitionId, {
          role: role as ProductionStageRole,
          sampleReplicates: smp
        });
      }
      setDialogOpen(false);
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not save stage replicate configuration.");
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    setPendingDelete(null);
    setError(null);
    try {
      await masterDataOptions.deleteTestDefinitionStageReplicate(testDefinitionId, target.id);
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? "Could not delete stage replicate configuration.");
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Stack sx={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          Replicates per stage ({replicates.length} configured)
        </Typography>
        <Button
          size="small"
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={openAdd}
          disabled={availableRoles.length === 0 || loading}
        >
          Add Stage Replicate
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

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
            {replicates.map((r) => (
              <TableRow key={r.id}>
                <TableCell>
                  <Chip size="small" label={r.role} variant="outlined" sx={{ fontWeight: 600 }} />
                </TableCell>
                <TableCell>{r.sampleReplicates}</TableCell>
                <TableCell align="right">
                  <IconButton aria-label="Edit replicates" size="small" onClick={() => openEdit(r)} title="Edit Replicates">
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton aria-label="Delete replicate rule"
                    size="small"
                    color="error"
                    onClick={() => setPendingDelete(r)}
                    title="Delete Replicate Configuration"
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {replicates.length === 0 && !loading && (
              <TableRow>
                <TableCell colSpan={3} align="center" sx={{ py: 2, color: "text.secondary" }}>
                  No stage replicates configured yet. Click "Add Stage Replicate" to configure replicate counts.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
        Production stages without a configured row are not configured for this test (not zero). Counts must be integers ≥ 1. HPLC standard injections are set on the method.
      </Typography>

      <FloatingDialog
        open={dialogOpen}
        title={editingReplicate ? `Edit Stage Replicates: ${editingReplicate.role}` : "Add Stage Replicate"}
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button variant="contained" onClick={handleSave} disabled={saving}>
              {saving ? "Saving…" : editingReplicate ? "Save Changes" : "Add"}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2} sx={{ pt: 1 }}>
          {editingReplicate ? (
            <TextField
              size="small"
              label="Stage Role"
              value={editingReplicate.role}
              disabled
              fullWidth
            />
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="stage-rep-role-select-label">Stage Role</InputLabel>
              <Select<ProductionStageRole>
                labelId="stage-rep-role-select-label"
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

      <ConfirmationDialog
        open={pendingDelete != null}
        title="Delete Stage Replicate Configuration"
        message={
          pendingDelete
            ? `Delete stage replicate configuration for role "${pendingDelete.role}"?`
            : ""
        }
        confirmText="Delete"
        destructive
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </Box>
  );
}

interface WorkflowStepIncubationStage {
  stageNumber: number;
  tempMin?: number;
  tempMax?: number;
  incubationMinHours?: number;
  incubationMaxHours?: number;
}

interface WorkflowStepItem {
  id: number;
  stepName: string;
  stepOrder: number;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId?: number | null;
  targetOrganism?: { name: string } | null;
  requiresIncubationTransfer?: boolean;
  version?: number;
  stepMedia?: WorkflowStepMediaItem[];
  incubationStages?: WorkflowStepIncubationStage[];
  phenotypicTestType?: string | null;
  phenotypicTestTypes?: string[];
}

interface MaterialOption {
  id: number;
  name?: string;
  materialName?: string;
  mediaProductId?: number;
  [key: string]: unknown;
}

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
function WorkflowStepsSection({ test, workflowTypes, onWorkflowTypeChanged }: { test: TestDefinitionOption; workflowTypes: string[]; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<WorkflowStepItem[]>([]);
  const [organisms, setOrganisms] = useState<Array<{ id: number; scientificName: string; commonName?: string }>>([]);
  const [materials, setMaterials] = useState<MaterialOption[]>([]);
  const [conditions, setConditions] = useState<MediaIncubationConditionOption[]>([]);
  const [form, setForm] = useState<StepFormState>(defaultStepForm);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [stepToDelete, setStepToDelete] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadSteps = () => masterDataOptions.getTestWorkflowSteps(test.id).then(setSteps);
  useEffect(() => {
    masterDataOptions.getOrganisms().then(setOrganisms);
    masterDataOptions.getMaterials("DehydratedMedia").then(setMaterials);
    masterDataOptions.getMediaIncubationConditions().then(setConditions);
    loadSteps();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [test.id]);

  const changeWorkflowType = async (workflowType: string) => {
    setError(null);
    try {
      await masterDataOptions.updateWorkflowType(test.id, workflowType);
      onWorkflowTypeChanged();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not update the workflow type.");
    }
  };

  const startEditStep = (s: WorkflowStepItem) => {
    setEditingStepId(s.id);
    const stage2 = (s.incubationStages ?? []).find((x) => x.stageNumber === 2);
    setForm({
      stepName: s.stepName, isFinalStep: s.isFinalStep, stepType: s.stepType,
      targetOrganismId: s.targetOrganismId ?? null,
      stepMedia: (s.stepMedia ?? []).map((m) => ({
        materialId: m.materialId,
        mediaIncubationConditionId: typeof m.mediaIncubationConditionId === "number" ? m.mediaIncubationConditionId : "",
        isRequired: !!m.isRequired,
        displayOrder: typeof m.displayOrder === "number" ? m.displayOrder : 0
      })),
      requiresIncubationTransfer: !!s.requiresIncubationTransfer,
      stage2TempMin: stage2 ? String(stage2.tempMin) : undefined,
      stage2TempMax: stage2 ? String(stage2.tempMax) : undefined,
      stage2IncubationMinHours: stage2 ? stage2.incubationMinHours : undefined,
      stage2IncubationMaxHours: stage2 ? stage2.incubationMaxHours : undefined,
      phenotypicTestType: s.phenotypicTestType ?? null,
      phenotypicTestTypes: s.phenotypicTestTypes ?? []
    });
    setError(null);
  };

  const cancelEditStep = () => { setEditingStepId(null); setForm(defaultStepForm()); };

  const needsOrganism = STEP_TYPES_REQUIRING_ORGANISM.includes(form.stepType);
  const hasNoMedia = STEP_TYPES_WITH_NO_MEDIA.includes(form.stepType);
  const isSingleMedia = STEP_TYPES_SINGLE_MEDIA.includes(form.stepType);
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  // Switching StepType clears media/organism rather than carrying over a
  // combination that likely no longer satisfies that type's rules (e.g. a
  // Broth's single required medium isn't valid as-is for ConfirmatoryPlating,
  // which forbids IsRequired on every row) - forces a deliberate re-pick
  // instead of silently submitting a stale, mismatched configuration.
  const changeStepType = (stepType: string) => setForm({
    ...form, stepType, targetOrganismId: null, stepMedia: [], phenotypicTestType: null, phenotypicTestTypes: [],
    requiresIncubationTransfer: false, stage2TempMin: undefined, stage2TempMax: undefined,
    stage2IncubationMinHours: undefined, stage2IncubationMaxHours: undefined
  });

  const [pendingPhenotypicTest, setPendingPhenotypicTest] = useState("");
  const addPhenotypicTest = () => {
    if (!pendingPhenotypicTest || form.phenotypicTestTypes.includes(pendingPhenotypicTest)) return;
    setForm({ ...form, phenotypicTestTypes: [...form.phenotypicTestTypes, pendingPhenotypicTest] });
    setPendingPhenotypicTest("");
  };
  const removePhenotypicTest = (type: string) =>
    setForm({ ...form, phenotypicTestTypes: form.phenotypicTestTypes.filter((t) => t !== type) });

  const addMediaRow = () => setForm({
    ...form,
    stepMedia: [...form.stepMedia, {
      materialId: "", mediaIncubationConditionId: "", isRequired: false, displayOrder: form.stepMedia.length
    }]
  });
  // A condition belongs to one media product, so changing the material
  // clears a condition that no longer fits.
  const updateMediaRow = (index: number, patch: Partial<StepMediaRow>) => setForm({
    ...form,
    stepMedia: form.stepMedia.map((m, i) => {
      if (i !== index) return m;
      const next = { ...m, ...patch };
      if (patch.materialId !== undefined) {
        const material = materials.find((mat) => mat.id === patch.materialId);
        const matchesProduct = conditions.some(
          (c) => c.id === next.mediaIncubationConditionId && material?.mediaProductId != null && c.mediaProductId === material.mediaProductId
        );
        if (!matchesProduct) {
          next.mediaIncubationConditionId = "";
        }
      }
      return next;
    })
  });
  const removeMediaRow = (index: number) => setForm({ ...form, stepMedia: form.stepMedia.filter((_, i) => i !== index) });

  const saveStep = async () => {
    setError(null);
    if (!form.stepName || (isBiochemical && !form.phenotypicTestType && form.phenotypicTestTypes.length === 0)) {
      setError(isBiochemical ? "Step Name and at least one Phenotypic Test Type are required." : "Step Name is required.");
      return;
    }
    const validationError = validateStepForm(form);
    if (validationError) {
      setError(validationError);
      return;
    }
    const payload = {
      stepName: form.stepName,
      phenotypicTestType: isBiochemical ? form.phenotypicTestType ?? null : null,
      phenotypicTestTypes: isBiochemical ? form.phenotypicTestTypes : [],
      // No longer read at execution time - the picked medium's own window
      // is authoritative (see TestWorkflowEngine.cs). Kept on the request
      // shape only because the column itself isn't dropped yet.
      incubationMinHours: 0, incubationMaxHours: 0, temperatureMin: 0, temperatureMax: 0,
      isFinalStep: !!form.isFinalStep, stepType: form.stepType, targetOrganismId: form.targetOrganismId,
      stepMedia: form.stepMedia.map((m, i) => ({
        materialId: Number(m.materialId),
        mediaIncubationConditionId: m.mediaIncubationConditionId === "" ? null : Number(m.mediaIncubationConditionId),
        isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired,
        displayOrder: i
      })),
      requiresIncubationTransfer: form.stepType === "PlateCount" && !!form.requiresIncubationTransfer,
      incubationStages: (form.stepType === "PlateCount" && form.requiresIncubationTransfer) ? [
        {
          stageNumber: 2,
          tempMin: Number(form.stage2TempMin),
          tempMax: Number(form.stage2TempMax),
          incubationMinHours: Number(form.stage2IncubationMinHours),
          incubationMaxHours: Number(form.stage2IncubationMaxHours)
        }
      ] : []
    };
    try {
      if (editingStepId) {
        await masterDataOptions.updateTestWorkflowStep(editingStepId, payload, steps.find((s) => s.id === editingStepId)?.version);
      } else {
        await masterDataOptions.createTestWorkflowStep(test.id, payload);
      }
      cancelEditStep();
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? `Could not ${editingStepId ? "update" : "add"} this step.`);
    }
  };

  const move = async (stepId: number, direction: "up" | "down") => {
    setError(null);
    try {
      await masterDataOptions.moveTestWorkflowStep(stepId, direction);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not reorder this step.");
    }
  };

  const remove = async (stepId: number) => {
    setError(null);
    try {
      await masterDataOptions.deleteTestWorkflowStep(stepId);
      await loadSteps();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not delete this step.");
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "background.default", borderTop: "1px solid", borderTopColor: "divider" }}>
      <Stack
        direction="row"
        spacing={1.5}
        sx={{
          alignItems: "center",
          mb: 1.5
        }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          {["Measurement", "Gravimetric", "Qualitative", "HplcMethodAssay", "IcpMethodAssay", "Titration"].includes(test.workflowType) ? "Workflow Type" : "Workflow Steps"}
        </Typography>
        {/* Only step-driven tests can switch; every other workflow is set by the test type. */}
        {["CountTest", "Observation"].includes(test.workflowType) && (
          <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)} inputProps={{ "aria-label": "Workflow type" }}>
            {workflowTypes.filter((w) => w === "CountTest" || w === "Observation").map((w) => <MenuItem key={w} value={w}>{WORKFLOW_TYPE_LABELS[w] ?? w}</MenuItem>)}
          </Select>
        )}
      </Stack>
      {test.workflowType === "HplcMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>HPLC Method Assay Configuration</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "HplcMethodAssay"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Method Abbreviation</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>Required (from HPLC Method Master)</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {test.workflowType === "IcpMethodAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>ICP Method Assay Configuration</Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "IcpMethodAssay"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Method Abbreviation</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {test.workflowType === "HplcMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          HPLC Method Assay tests have no workflow steps or local test analytes: parameters, analytes, standard weights, and system suitability criteria are configured centrally in HPLC Methods Master.
        </Typography>
      ) : test.workflowType === "IcpMethodAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          ICP Method Assay tests have no workflow steps or local test analytes: elements, calibration standards, and quality controls are configured centrally in ICP Methods Master.
        </Typography>
      ) : test.workflowType === "Measurement" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Measurement Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Measurement"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Evaluation Basis</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.evaluationBasis ?? "Mean"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicate Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Measurement tests have no workflow steps: readings are entered per specification parameter directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Gravimetric" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Gravimetric Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "GravimetricLoss"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicate Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.replicateCount ?? 1}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Uses Tare</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.usesTare ? "Yes" : "No"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Gravimetric tests have no workflow steps: container/sample weights and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Titration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Titration Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            {[
              ["Type", test.titrationType],
              ["Mode", test.titrationMode],
              ["Calculation", test.titrationCalculation],
              ["Endpoint", test.titrationEndpoint],
              ["Replicates", test.replicateCount]
            ].map(([label, value]) => (
              <Box key={String(label)}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{label}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{value ?? "-"}</Typography>
              </Box>
            ))}
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Titration tests have no workflow steps: replicate weights and titrant volumes are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Qualitative" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Qualitative Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Qualitative"] ?? test.equationType}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Qualitative tests have no workflow steps: compliance observation is entered per specification parameter directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Dissolution" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Dissolution Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Dissolution"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.requiresSystemSuitability ? `Required (${test.methodAbbreviation ?? "No abbr"})` : "Required"}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S1 Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>≥ Q + {test.dissolutionS1Offset ?? 5} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S2 Min Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS2MinOffset ?? 15} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S3 Min Offset</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>&lt; Q − {test.dissolutionS3MinOffset ?? 25} %</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>S3 Max Below S2</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.dissolutionS3MaxBelowS2Min ?? 2} units</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Dissolution tests have no workflow steps: staged vessel peak areas (S1: 6, S2: +6, S3: +12) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "Disintegration" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Disintegration Configuration
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "Disintegration"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Stage 1 Units</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage1Units ?? 6}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Stage 2 Units</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationStage2Units ?? 12}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max S1 Failures</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMaxStage1Failures ?? 2}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Min Pass Total</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.disintegrationMinPassTotal ?? 16}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Disintegration tests have no workflow steps: staged unit times (S1: {test.disintegrationStage1Units ?? 6}, S2: +{test.disintegrationStage2Units ?? 12}) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : test.workflowType === "WeightVariation" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Weight Variation Configuration (USP &lt;2091&gt;)
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "WeightVariation"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Unit Count</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.wvUnitCount ?? 20}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Tablets (Bands / Max Out)</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                &le;{test.wvTabletBand1MaxMg ?? 130}mg: {test.wvTabletBand1Percent ?? 10}% | &le;{test.wvTabletBand2MaxMg ?? 324}mg: {test.wvTabletBand2Percent ?? 7.5}% | &gt;324mg: {test.wvTabletBand3Percent ?? 5}% (max {test.wvTabletMaxOutside ?? 2} out)
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Capsules (Limits / Retest)</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                &plusmn;{test.wvCapsuleInnerPercent ?? 10}% / &plusmn;{test.wvCapsuleOuterPercent ?? 25}% (S1: &le;{test.wvCapsuleS1MaxOutside ?? 2} pass, &le;{test.wvCapsuleS1MaxForRetest ?? 6} retest +{test.wvCapsuleS2ExtraUnits ?? 40} units, S2: &le;{test.wvCapsuleS2MaxOutside ?? 6} out)
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Condition Fields</Typography>
              <Typography variant="body2">{test.conditionFields || "None configured"}</Typography>
            </Box>
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Weight Variation tests have no workflow steps: unit weights (or gross and shell weights) and condition fields are entered directly.
          </Typography>
        </Box>
      ) : (
      <>
      {steps.length > 0 ? (
        <TableContainer>
          <Table size="small" sx={{ mb: 1.5 }}>
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>#</TableCell><TableCell>Step</TableCell><TableCell>Incubation</TableCell>
                <TableCell>Temp °C</TableCell><TableCell>Step Type</TableCell><TableCell>Media</TableCell><TableCell>Organism</TableCell>
                <TableCell>Status</TableCell><TableCell>Final</TableCell><TableCell /></TableRow>
            </TableHead>
            <TableBody>
              {steps.map((s, i) => {
                const stage2 = (s.incubationStages ?? []).find((x) => x.stageNumber === 2);
                const isTwoStage = s.stepType === "PlateCount" && s.requiresIncubationTransfer;
                return (
                  <TableRow key={s.id}>
                    <TableCell>{s.stepOrder}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{
                        alignItems: "center"
                      }}>
                        <span>{s.stepName}</span>
                        {isTwoStage && (
                          <Chip
                            size="small"
                            color="primary"
                            variant="outlined"
                            label="2-Stage"
                            sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>Stage 1: {stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours")}h</div>
                          <div>Stage 2: {stage2.incubationMinHours}-{stage2.incubationMaxHours}h</div>
                        </Box>
                      ) : (
                        `${stage1Ranges(s.stepMedia, "incubationMinHours", "incubationMaxHours")}h`
                      )}
                    </TableCell>
                    <TableCell>
                      {isTwoStage && stage2 ? (
                        <Box sx={{ fontSize: "0.8rem", lineHeight: 1.3 }}>
                          <div>Stage 1: {stage1Ranges(s.stepMedia, "tempMin", "tempMax")}</div>
                          <div>Stage 2: {stage2.tempMin}-{stage2.tempMax}</div>
                        </Box>
                      ) : (
                        stage1Ranges(s.stepMedia, "tempMin", "tempMax")
                      )}
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} sx={{
                        alignItems: "center"
                      }}>
                        <span>{s.stepType}</span>
                        {isTwoStage && (
                          <Chip
                            size="small"
                            color="secondary"
                            label="Transfer"
                            sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                          />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      {s.stepType === "BiochemicalTest"
                        ? (s.phenotypicTestTypes && s.phenotypicTestTypes.length > 0
                            ? s.phenotypicTestTypes.map((t: string) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")
                            : s.phenotypicTestType ? PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType : <em>—</em>)
                        : (s.stepMedia && s.stepMedia.length > 0 ? s.stepMedia.map((m) => m.materialName).join(", ") : <em>—</em>)}
                    </TableCell>
                    <TableCell>{s.targetOrganism?.name ?? <em>—</em>}</TableCell>
                    <TableCell>
                      {stepNeedsConfiguration(s) && (
                        <Tooltip title="This template is missing a required organism or medium (likely inherited from the pre-refactor migration) and will fail validation the first time an analyst runs it. Edit it to complete the configuration.">
                          <Chip size="small" color="warning" icon={<WarningAmberIcon fontSize="small" />} label="Needs configuration" />
                        </Tooltip>
                      )}
                    </TableCell>
                    <TableCell>{s.isFinalStep ? "Yes" : "—"}</TableCell>
                    <TableCell align="right">
                      <Tooltip title="Move up">
                        <span>
                          <IconButton size="small" disabled={i === 0} onClick={() => move(s.id, "up")} aria-label="Move up">
                            <ArrowUpwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title="Move down">
                        <span>
                          <IconButton size="small" disabled={i === steps.length - 1} onClick={() => move(s.id, "down")} aria-label="Move down">
                            <ArrowDownwardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title="Edit step">
                        <IconButton size="small" onClick={() => startEditStep(s)} aria-label="Edit step">
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Delete step">
                        <IconButton size="small" color="error" onClick={() => setStepToDelete(s.id)} aria-label="Delete step">
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      ) : (
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            mb: 1.5
          }}>No workflow steps configured yet.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? "Edit Step" : "Add Step"}</Typography>
      <Stack useFlexGap
        direction="row"
        spacing={1.5}
        sx={{
          flexWrap: "wrap",
          alignItems: "center"
        }}>
        <TextField size="small" label="Step Name" placeholder="e.g. TSB" value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {isBiochemical && (
          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <Select size="small" displayEmpty value={pendingPhenotypicTest} onChange={(e) => setPendingPhenotypicTest(e.target.value as string)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": "Phenotypic Test Type" }}>
              <MenuItem value=""><em>Phenotypic Test Type</em></MenuItem>
              {PHENOTYPIC_TEST_TYPES
                .filter((t) => !form.phenotypicTestTypes.includes(t))
                .map((t) => <MenuItem key={t} value={t}>{PHENOTYPIC_TEST_TYPE_LABELS[t]}</MenuItem>)}
            </Select>
            <Button size="small" variant="outlined" disabled={!pendingPhenotypicTest} onClick={addPhenotypicTest}>Add</Button>
            {form.phenotypicTestTypes.map((t) => (
              <Chip key={t} size="small" label={PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t} onDelete={() => removePhenotypicTest(t)} />
            ))}
          </Stack>
        )}
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }} inputProps={{ "aria-label": "Step type" }}>
          {STEP_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </Select>
        <FormControlLabel
          control={<Checkbox checked={!!form.isFinalStep} onChange={(e) => setForm({ ...form, isFinalStep: e.target.checked })} />}
          label="Final Step"
        />
        {form.stepType === "PlateCount" && (
          <FormControlLabel
            control={
              <Checkbox
                checked={!!form.requiresIncubationTransfer}
                onChange={(e) =>
                  setForm({
                    ...form,
                    requiresIncubationTransfer: e.target.checked,
                    ...(e.target.checked
                      ? {}
                      : {
                          stage2TempMin: undefined,
                          stage2TempMax: undefined,
                          stage2IncubationMinHours: undefined,
                          stage2IncubationMaxHours: undefined
                        })
                  })
                }
              />
            }
            label="Requires incubation transfer"
          />
        )}
        {editingStepId && <Button onClick={cancelEditStep}>Cancel</Button>}
        <Button variant="contained" onClick={saveStep}>{editingStepId ? "Save Changes" : "Add Step"}</Button>
      </Stack>

      {form.stepType === "PlateCount" && form.requiresIncubationTransfer && (
        <Box sx={{ mt: 1.5, p: 1.5, bgcolor: "background.paper", border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "text.primary" }}>
            Stage 2 Incubation (Transfer)
          </Typography>
          <Stack useFlexGap
            direction="row"
            spacing={1.5}
            sx={{
              flexWrap: "wrap",
              alignItems: "center"
            }}>
            <TextField
              size="small"
              type="number"
              label="Stage 2 Temp Min"
              value={form.stage2TempMin ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMin: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Temp Max"
              value={form.stage2TempMax ?? ""}
              onChange={(e) => setForm({ ...form, stage2TempMax: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Min Hours"
              value={form.stage2IncubationMinHours ?? ""}
              onChange={(e) => setForm({ ...form, stage2IncubationMinHours: e.target.value })}
              sx={{ width: 140 }}
            />
            <TextField
              size="small"
              type="number"
              label="Stage 2 Max Hours"
              value={form.stage2IncubationMaxHours ?? ""}
              onChange={(e) => setForm({ ...form, stage2IncubationMaxHours: e.target.value })}
              sx={{ width: 140 }}
            />
          </Stack>
        </Box>
      )}

      {needsOrganism && (
        <Stack
          direction="row"
          spacing={1.5}
          sx={{
            alignItems: "center",
            mt: 1.5
          }}>
          <Select<number | ""> size="small" displayEmpty value={form.targetOrganismId ?? ""} onChange={(e) => setForm({ ...form, targetOrganismId: e.target.value === "" ? null : Number(e.target.value) })} sx={{ minWidth: 220 }} inputProps={{ "aria-label": "Target Organism (required)" }}>
            <MenuItem value=""><em>Target Organism (required)</em></MenuItem>
            {organisms.map((o) => <MenuItem key={o.id} value={o.id}>{o.scientificName}</MenuItem>)}
          </Select>
        </Stack>
      )}

      {!hasNoMedia && (
        <Box sx={{ mt: 1.5 }}>
          <Stack
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: "center",
              mb: 0.5
            }}>
            <Typography sx={{ fontWeight: 700, fontSize: 12 }}>Step Media</Typography>
            {isSingleMedia && <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>This step type allows exactly one medium.</Typography>}
          </Stack>
          {/* Update replaces the whole StepMedia set server-side (no merge) -
              flagged here so an admin editing an existing step isn't
              surprised that rows not shown in this list get removed. */}
          {editingStepId && (
            <Alert severity="info" sx={{ mb: 1, maxWidth: 520 }}>Saving replaces this step's entire medium list with what's shown below.</Alert>
          )}
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              mb: 1
            }}>
            Pick the medium, then one of its incubation conditions. The condition sets this medium's temperature and incubation hours. Conditions are added on the Media Configurations page.
          </Typography>
          <Stack spacing={1}>
            {form.stepMedia.map((row, idx) => {
              const material = materials.find((mat) => mat.id === row.materialId);
              const productConditions = material?.mediaProductId ? conditions.filter((c) => c.mediaProductId === material.mediaProductId) : [];
              return (
                <Stack useFlexGap
                  key={idx}
                  direction="row"
                  spacing={1.5}
                  sx={{
                    alignItems: "center",
                    flexWrap: "wrap"
                  }}>
                  <Select<number | ""> size="small" displayEmpty value={row.materialId} onChange={(e) => updateMediaRow(idx, { materialId: e.target.value === "" ? "" : Number(e.target.value) })} sx={{ minWidth: 200 }} inputProps={{ "aria-label": "Material" }}>
                    <MenuItem value=""><em>Material</em></MenuItem>
                    {materials.map((m) => <MenuItem key={m.id} value={m.id}>{m.materialName}</MenuItem>)}
                  </Select>
                  <Select<number | "">
                    size="small"
                    displayEmpty
                    value={row.mediaIncubationConditionId}
                    disabled={productConditions.length === 0}
                    onChange={(e) => updateMediaRow(idx, { mediaIncubationConditionId: e.target.value === "" ? "" : Number(e.target.value) })}
                    sx={{ minWidth: 240 }}
                    inputProps={{ "aria-label": "Incubation condition" }}>
                    <MenuItem value=""><em>Incubation condition</em></MenuItem>
                    {productConditions.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {incubationConditionLabel(c)}
                      </MenuItem>
                    ))}
                  </Select>
                  {row.materialId !== "" && (
                    !material?.mediaProductId ? (
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        This batch isn't linked to a media product.
                      </Typography>
                    ) : productConditions.length === 0 ? (
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        This medium has no incubation conditions yet.
                      </Typography>
                    ) : null
                  )}
                  {!isConfirmatory && (
                    <FormControlLabel
                      control={<Checkbox checked={row.isRequired} onChange={(e) => updateMediaRow(idx, { isRequired: e.target.checked })} />}
                      label="Required"
                    />
                  )}
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>Order {idx + 1}</Typography>
                  <Tooltip title="Remove medium">
                    <IconButton size="small" color="error" onClick={() => removeMediaRow(idx)} aria-label="Remove medium">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              );
            })}
          </Stack>
          <Button size="small" sx={{ mt: 1 }} disabled={isSingleMedia && form.stepMedia.length >= 1} onClick={addMediaRow}>Add Medium</Button>
        </Box>
      )}
      </>
      )}

      <ConfirmationDialog
        open={stepToDelete !== null}
        title="Delete Workflow Step"
        message="Are you sure you want to delete this workflow step? This action cannot be undone."
        confirmText="Delete Step"
        destructive
        onConfirm={async () => {
          if (stepToDelete !== null) {
            const id = stepToDelete;
            setStepToDelete(null);
            await remove(id);
          }
        }}
        onCancel={() => setStepToDelete(null)}
      />
    </Box>
  );
}



// The Test Master - one canonical Code/DisplayName per test, referenced
// everywhere a TestCode is assigned (Items, Water Sampling Points, Room
// Test Configurations, Machine Part Configurations) via TestCodePicker/
// TestCodePickerMulti. Those pickers can also add a new test inline,
// but this page is the place to see the whole list and add one
// deliberately (e.g. before configuring several items that will all
// need it).
//
// Freezing a test hides it from those pickers' dropdown for *new*
// selections without touching anything that already references its
// Code - see useTestDefinitions.activeOptions.
export function TestMasterPage({ lab = "micro", area }: { lab?: TestMasterLab; area?: PageArea }) {
  const { options: allOptions, addNew, update, setActive, reload } = useTestDefinitions();
  const isFp = lab === "fp";
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[lab];
  const defaultWorkflowType: string = isFp ? "HplcMethodAssay" : "Observation";
  const [fpSectionId, setFpSectionId] = useState<number | null>(null);
  // Separate from fpSectionId: null meant "still loading", "failed" and "no
  // FP section exists" alike, so a failure (or a site without an FP section)
  // left the register showing skeleton rows forever.
  const [sectionsState, setSectionsState] = useState<"loading" | "loaded" | "failed">("loading");
  const [sectionsReloadKey, setSectionsReloadKey] = useState(0);
  useEffect(() => {
    setSectionsState("loading");
    getSections()
      .then((secs) => {
        setFpSectionId(secs.find((s) => s.sectionCode === FP_SECTION_CODE)?.sectionId ?? null);
        setSectionsState("loaded");
      })
      .catch(() => setSectionsState("failed"));
  }, [sectionsReloadKey]);
  const inLab = (sid?: number | null) =>
    isFp ? fpSectionId !== null && sid === fpSectionId : sid !== fpSectionId;
  const options = allOptions.filter(
    (t) =>
      sectionsState === "loaded" &&
      inLab(t.sectionId) &&
      (!isFp || !area || areaIncludes(t.physchemArea, area))
  );
  const [physchemArea, setPhyschemArea] = useState<PhyschemArea | null>(null);
  const [changeReason, setChangeReason] = useState("");
  const [changeReasonError, setChangeReasonError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
  const [workflowType, setWorkflowType] = useState<string>(defaultWorkflowType);
  const [equationType, setEquationType] = useState<string>(isFp ? "HplcMethodAssay" : "None");
  const [requiresSystemSuitability, setRequiresSystemSuitability] = useState<boolean>(false);
  const [methodAbbreviation, setMethodAbbreviation] = useState<string>("");
  const [hplcMethodId, setHplcMethodId] = useState<number | "">("");
  const [hplcMethods, setHplcMethods] = useState<HplcMethodListItem[]>([]);
  const [icpMethodId, setIcpMethodId] = useState<number | "">("");
  const [icpMethods, setIcpMethods] = useState<IcpMethodListItem[]>([]);

  // Lists the Add/Edit Test dialog picks from; a failure is named in the
  // dialog instead of leaving an empty picker.
  const { failed: dialogListFailures, fail: failDialogList } = useLoadFailures();

  useEffect(() => {
    HplcMethodService.getAll(false)
      .then((data) => setHplcMethods(data))
      .catch(failDialogList("HPLC methods"));
    IcpMethodService.getAll(false)
      .then((data) => setIcpMethods(data))
      .catch(failDialogList("ICP methods"));
  }, [failDialogList]);


  const [replicateCount, setReplicateCount] = useState<string>("1");
  const [evaluationBasis, setEvaluationBasis] = useState<"Mean" | "EachValue" | "Min" | "Max">("Mean");
  const [conditionFields, setConditionFields] = useState<string>("");
  const [usesTare, setUsesTare] = useState<boolean>(false);
  const [titration, setTitration] = useState<TitrationFormState>(createInitialTitrationForm);

  const [dissolutionS1Offset, setDissolutionS1Offset] = useState<string>("5");
  const [dissolutionS2MinOffset, setDissolutionS2MinOffset] = useState<string>("15");
  const [dissolutionS3MinOffset, setDissolutionS3MinOffset] = useState<string>("25");
  const [dissolutionS3MaxBelowS2Min, setDissolutionS3MaxBelowS2Min] = useState<string>("2");

  const [disintegrationStage1Units, setDisintegrationStage1Units] = useState<string>("6");
  const [disintegrationStage2Units, setDisintegrationStage2Units] = useState<string>("12");
  const [disintegrationMaxStage1Failures, setDisintegrationMaxStage1Failures] = useState<string>("2");
  const [disintegrationMinPassTotal, setDisintegrationMinPassTotal] = useState<string>("16");

  const [wvUnitCount, setWvUnitCount] = useState<string>("20");
  const [wvTabletBand1MaxMg, setWvTabletBand1MaxMg] = useState<string>("130");
  const [wvTabletBand1Percent, setWvTabletBand1Percent] = useState<string>("10");
  const [wvTabletBand2MaxMg, setWvTabletBand2MaxMg] = useState<string>("324");
  const [wvTabletBand2Percent, setWvTabletBand2Percent] = useState<string>("7.5");
  const [wvTabletBand3Percent, setWvTabletBand3Percent] = useState<string>("5");
  const [wvTabletMaxOutside, setWvTabletMaxOutside] = useState<string>("2");
  const [wvCapsuleInnerPercent, setWvCapsuleInnerPercent] = useState<string>("10");
  const [wvCapsuleOuterPercent, setWvCapsuleOuterPercent] = useState<string>("25");
  const [wvCapsuleS1MaxOutside, setWvCapsuleS1MaxOutside] = useState<string>("2");
  const [wvCapsuleS1MaxForRetest, setWvCapsuleS1MaxForRetest] = useState<string>("6");
  const [wvCapsuleS2ExtraUnits, setWvCapsuleS2ExtraUnits] = useState<string>("40");
  const [wvCapsuleS2MaxOutside, setWvCapsuleS2MaxOutside] = useState<string>("6");


  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editingSectionId, setEditingSectionId] = useState<number | null>(null);
  const [editingTest, setEditingTest] = useState<TestDefinitionOption | null>(null);
  const [allMySections, setMySections] = useState<LaboratorySection[]>([]);
  const mySections = allMySections.filter((s) => sectionsState === "loaded" && inLab(s.sectionId));
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [search, setSearch] = useState("");

  useEffect(() => {
    getMySections()
      .then((secs) => setMySections(secs))
      .catch(failDialogList("your laboratory sections"));
  }, [failDialogList]);

  const openCreateDialog = () => {
    setEditingId(null);
    setEditingTest(null);
    setCode("");
    setDisplayName("");
    setSectionId(mySections.length === 1 ? mySections[0].sectionId : "");
    setEditingSectionId(null);
    setWorkflowType(defaultWorkflowType);
    setEquationType(isFp ? (defaultWorkflowType === "HplcMethodAssay" ? "HplcMethodAssay" : "None") : "None");
    setRequiresSystemSuitability(false);
    setMethodAbbreviation("");
    setHplcMethodId("");
    setIcpMethodId("");
    setReplicateCount("1");
    setEvaluationBasis("Mean");
    setConditionFields("");
    setUsesTare(false);
    setTitration(createInitialTitrationForm());
    setDissolutionS1Offset("5");
    setDissolutionS2MinOffset("15");
    setDissolutionS3MinOffset("25");
    setDissolutionS3MaxBelowS2Min("2");
    setDisintegrationStage1Units("6");
    setDisintegrationStage2Units("12");
    setDisintegrationMaxStage1Failures("2");
    setDisintegrationMinPassTotal("16");
    setWvUnitCount("20");
    setWvTabletBand1MaxMg("130");
    setWvTabletBand1Percent("10");
    setWvTabletBand2MaxMg("324");
    setWvTabletBand2Percent("7.5");
    setWvTabletBand3Percent("5");
    setWvTabletMaxOutside("2");
    setWvCapsuleInnerPercent("10");
    setWvCapsuleOuterPercent("25");
    setWvCapsuleS1MaxOutside("2");
    setWvCapsuleS1MaxForRetest("6");
    setWvCapsuleS2ExtraUnits("40");
    setWvCapsuleS2MaxOutside("6");
    setPhyschemArea(area ? defaultAreaFor(area) : null);
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const startEdit = (t: TestDefinitionOption) => {
    setEditingId(t.id);
    setEditingTest(t);
    setCode(t.code);
    setDisplayName(t.displayName);
    setSectionId(t.sectionId ?? (mySections.length === 1 ? mySections[0].sectionId : ""));
    setEditingSectionId(t.sectionId ?? null);
    setWorkflowType(t.workflowType || defaultWorkflowType);
    setEquationType(t.equationType || (t.workflowType === "HplcMethodAssay" ? "HplcMethodAssay" : t.workflowType === "IcpMethodAssay" ? "IcpMethodAssay" : t.workflowType === "Measurement" ? "Measurement" : t.workflowType === "Gravimetric" ? "GravimetricLoss" : t.workflowType === "Qualitative" ? "Qualitative" : t.workflowType === "Dissolution" ? "Dissolution" : t.workflowType === "Disintegration" ? "Disintegration" : t.workflowType === "WeightVariation" ? "WeightVariation" : t.workflowType === "Titration" ? "Titration" : "None"));
    setRequiresSystemSuitability((t.workflowType === "Dissolution" || t.workflowType === "HplcMethodAssay") ? true : (t.workflowType === "Disintegration" || t.workflowType === "WeightVariation" || t.workflowType === "IcpMethodAssay") ? false : !!t.requiresSystemSuitability);
    setMethodAbbreviation(t.methodAbbreviation ?? "");
    setHplcMethodId(t.hplcMethodId ?? "");
    setIcpMethodId(t.icpMethodId ?? "");
    setReplicateCount(t.replicateCount != null ? String(t.replicateCount) : "1");
    setEvaluationBasis(t.evaluationBasis || "Mean");
    setConditionFields(t.conditionFields || "");
    setUsesTare(!!t.usesTare);
    setTitration(titrationFormFromDefinition(t));
    setDissolutionS1Offset(t.dissolutionS1Offset != null ? String(t.dissolutionS1Offset) : "5");
    setDissolutionS2MinOffset(t.dissolutionS2MinOffset != null ? String(t.dissolutionS2MinOffset) : "15");
    setDissolutionS3MinOffset(t.dissolutionS3MinOffset != null ? String(t.dissolutionS3MinOffset) : "25");
    setDissolutionS3MaxBelowS2Min(t.dissolutionS3MaxBelowS2Min != null ? String(t.dissolutionS3MaxBelowS2Min) : "2");
    setDisintegrationStage1Units(t.disintegrationStage1Units != null ? String(t.disintegrationStage1Units) : "6");
    setDisintegrationStage2Units(t.disintegrationStage2Units != null ? String(t.disintegrationStage2Units) : "12");
    setDisintegrationMaxStage1Failures(t.disintegrationMaxStage1Failures != null ? String(t.disintegrationMaxStage1Failures) : "2");
    setDisintegrationMinPassTotal(t.disintegrationMinPassTotal != null ? String(t.disintegrationMinPassTotal) : "16");
    setWvUnitCount(t.wvUnitCount != null ? String(t.wvUnitCount) : "20");
    setWvTabletBand1MaxMg(t.wvTabletBand1MaxMg != null ? String(t.wvTabletBand1MaxMg) : "130");
    setWvTabletBand1Percent(t.wvTabletBand1Percent != null ? String(t.wvTabletBand1Percent) : "10");
    setWvTabletBand2MaxMg(t.wvTabletBand2MaxMg != null ? String(t.wvTabletBand2MaxMg) : "324");
    setWvTabletBand2Percent(t.wvTabletBand2Percent != null ? String(t.wvTabletBand2Percent) : "7.5");
    setWvTabletBand3Percent(t.wvTabletBand3Percent != null ? String(t.wvTabletBand3Percent) : "5");
    setWvTabletMaxOutside(t.wvTabletMaxOutside != null ? String(t.wvTabletMaxOutside) : "2");
    setWvCapsuleInnerPercent(t.wvCapsuleInnerPercent != null ? String(t.wvCapsuleInnerPercent) : "10");
    setWvCapsuleOuterPercent(t.wvCapsuleOuterPercent != null ? String(t.wvCapsuleOuterPercent) : "25");
    setWvCapsuleS1MaxOutside(t.wvCapsuleS1MaxOutside != null ? String(t.wvCapsuleS1MaxOutside) : "2");
    setWvCapsuleS1MaxForRetest(t.wvCapsuleS1MaxForRetest != null ? String(t.wvCapsuleS1MaxForRetest) : "6");
    setWvCapsuleS2ExtraUnits(t.wvCapsuleS2ExtraUnits != null ? String(t.wvCapsuleS2ExtraUnits) : "40");
    setWvCapsuleS2MaxOutside(t.wvCapsuleS2MaxOutside != null ? String(t.wvCapsuleS2MaxOutside) : "6");
    setPhyschemArea(t.physchemArea ?? (area ? defaultAreaFor(area) : null));
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setEditingTest(null);
    setPhyschemArea(null);
    setChangeReason("");
    setChangeReasonError(null);
    setDialogError(null);
  };

  const save = async () => {
    setDialogError(null);
    setMessage(null);

    const trimmedCode = code.trim();
    const trimmedDisplayName = displayName.trim();

    if (!trimmedCode || !trimmedDisplayName) {
      setDialogError("Both Code and Display Name are required.");
      return;
    }
    if (sectionId === "") {
      setDialogError("Laboratory section is required.");
      return;
    }

    const chosenSectionId = Number(sectionId);
    const isDissolution = workflowType === "Dissolution";

    if (isDissolution) {
      const trimmedAbbr = methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        setDialogError("Method abbreviation is required when system suitability is enabled.");
        return;
      }
      if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        setDialogError("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        return;
      }
    }

    if (isDissolution) {
      const s1 = dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5;
      const s2 = dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15;
      const s3 = dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25;
      const maxBelow = dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2;

      if (isNaN(s1) || s1 < 0 || isNaN(s2) || s2 < 0 || isNaN(s3) || s3 < 0 || isNaN(maxBelow) || maxBelow < 0) {
        setDialogError("Dissolution stage offsets must be greater than or equal to zero.");
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    const isDisintegration = workflowType === "Disintegration";
    const isWeightVariation = workflowType === "WeightVariation";

    if (isDisintegration) {
      const s1 = disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6;
      const s2 = disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12;
      const maxFail = disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2;
      const minPass = disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16;

      if (isNaN(s1) || s1 < 1 || isNaN(s2) || s2 < 1) {
        setDialogError("Disintegration stage units must be greater than or equal to 1.");
        return;
      }
      if (isNaN(maxFail) || maxFail < 0 || maxFail >= s1) {
        setDialogError(`Disintegration maximum Stage 1 failures must be between 0 and ${s1 - 1}.`);
        return;
      }
      if (isNaN(minPass) || minPass < 1 || minPass > (s1 + s2)) {
        setDialogError(`Disintegration minimum pass total must be between 1 and ${s1 + s2}.`);
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    if (isWeightVariation) {
      const uCount = wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20;
      const b1Mg = wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130;
      const b1Pct = wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10;
      const b2Mg = wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324;
      const b2Pct = wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5;
      const b3Pct = wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5;
      const tabMaxOut = wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2;

      const capInner = wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10;
      const capOuter = wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25;
      const capS1Out = wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2;
      const capS1Retest = wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6;
      const capS2Extra = wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40;
      const capS2Out = wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6;

      if (isNaN(uCount) || uCount < 1 || isNaN(capS2Extra) || capS2Extra < 1) {
        setDialogError("Weight variation unit count and extra units must be greater than or equal to 1.");
        return;
      }
      if (isNaN(tabMaxOut) || tabMaxOut < 0 || isNaN(capS1Out) || capS1Out < 0 || isNaN(capS2Out) || capS2Out < 0) {
        setDialogError("Weight variation maximum outside counts must be greater than or equal to 0.");
        return;
      }
      if (isNaN(b1Mg) || b1Mg <= 0 || isNaN(b2Mg) || b2Mg <= 0) {
        setDialogError("Weight variation tablet band weight limits must be greater than zero.");
        return;
      }
      if (isNaN(b1Pct) || b1Pct <= 0 || isNaN(b2Pct) || b2Pct <= 0 || isNaN(b3Pct) || b3Pct <= 0 || isNaN(capInner) || capInner <= 0 || isNaN(capOuter) || capOuter <= 0) {
        setDialogError("Weight variation percentages must be greater than zero.");
        return;
      }
      if (b1Mg >= b2Mg) {
        setDialogError("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
        return;
      }
      if (capInner >= capOuter) {
        setDialogError("Weight variation capsule inner percentage must be less than outer percentage.");
        return;
      }
      if (capS1Out >= capS1Retest || capS1Retest > uCount) {
        setDialogError("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
        return;
      }
      if (capS2Out >= uCount + capS2Extra) {
        setDialogError(`Weight variation capsule Stage 2 max outside must be less than total units (${uCount + capS2Extra}).`);
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    const isMeasurement = workflowType === "Measurement";
    const isGravimetric = workflowType === "Gravimetric";
    const isQualitative = workflowType === "Qualitative";
    const isHplcMethodAssay = workflowType === "HplcMethodAssay";
    const isIcpMethodAssay = workflowType === "IcpMethodAssay";
    const isTitration = workflowType === "Titration";

    if (isTitration) {
      const hint = validateTitrationForm(titration);
      if (hint) {
        setDialogError(hint);
        return;
      }
    }

    const reasonCheck = editingId
      ? titrationChangeReasonCheck(editingTest, titration, workflowType, changeReason, isFp ? physchemArea : null)
      : { required: false, error: null, payload: null };
    setChangeReasonError(reasonCheck.error);
    if (reasonCheck.error) return;

    if (isHplcMethodAssay) {
      if (!hplcMethodId) {
        setDialogError("HPLC method is required for HPLC method assay tests.");
        return;
      }
    }

    if (isIcpMethodAssay) {
      if (!icpMethodId) {
        setDialogError("ICP method is required for ICP method assay tests.");
        return;
      }
    }

    if (isMeasurement) {
      const rep = Number(replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        setDialogError("Replicate count must be between 1 and 30 for Measurement tests.");
        return;
      }
      if (!evaluationBasis) {
        setDialogError("Evaluation basis is required for Measurement tests.");
        return;
      }
    }

    if (isGravimetric) {
      const rep = Number(replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        setDialogError("Replicate count must be between 1 and 30 for Gravimetric tests.");
        return;
      }
      if (equationType !== "GravimetricLoss" && equationType !== "GravimetricResidue") {
        setDialogError("Equation type must be GravimetricLoss or GravimetricResidue for Gravimetric tests.");
        return;
      }
      if (conditionFields.length > 500) {
        setDialogError("Condition fields cannot exceed 500 characters.");
        return;
      }
    }

    setSaving(true);
    try {
      const resolvedEquationType = isHplcMethodAssay
        ? "HplcMethodAssay"
        : isIcpMethodAssay
        ? "IcpMethodAssay"
        : isMeasurement
        ? "Measurement"
        : isGravimetric
        ? equationType
        : isQualitative
        ? "Qualitative"
        : isDissolution
        ? "Dissolution"
        : isDisintegration
        ? "Disintegration"
        : isWeightVariation
        ? "WeightVariation"
        : isTitration
        ? "Titration"
        : "None";

      if (editingId) {
        const payload: UpdateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: resolvedEquationType,
          requiresSystemSuitability: (isDissolution || isHplcMethodAssay) ? true : false,
          methodAbbreviation: isDissolution || isHplcMethodAssay || isIcpMethodAssay ? methodAbbreviation.trim().toUpperCase() : null,
          replicateCount: isTitration ? Number(titration.replicateCount) : (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
          evaluationBasis: isMeasurement ? evaluationBasis : null,
          conditionFields: (isGravimetric || isDissolution || isDisintegration || isWeightVariation) ? (conditionFields.trim() || null) : null,
          usesTare: isGravimetric ? usesTare : null,
          dissolutionS1Offset: isDissolution ? (dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5) : null,
          dissolutionS2MinOffset: isDissolution ? (dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15) : null,
          dissolutionS3MinOffset: isDissolution ? (dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25) : null,
          dissolutionS3MaxBelowS2Min: isDissolution ? (dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2) : null,
          disintegrationStage1Units: isDisintegration ? (disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6) : null,
          disintegrationStage2Units: isDisintegration ? (disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12) : null,
          disintegrationMaxStage1Failures: isDisintegration ? (disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2) : null,
          disintegrationMinPassTotal: isDisintegration ? (disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16) : null,
          wvUnitCount: isWeightVariation ? (wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20) : null,
          wvTabletBand1MaxMg: isWeightVariation ? (wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130) : null,
          wvTabletBand1Percent: isWeightVariation ? (wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10) : null,
          wvTabletBand2MaxMg: isWeightVariation ? (wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324) : null,
          wvTabletBand2Percent: isWeightVariation ? (wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5) : null,
          wvTabletBand3Percent: isWeightVariation ? (wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5) : null,
          wvTabletMaxOutside: isWeightVariation ? (wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2) : null,
          wvCapsuleInnerPercent: isWeightVariation ? (wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10) : null,
          wvCapsuleOuterPercent: isWeightVariation ? (wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25) : null,
          wvCapsuleS1MaxOutside: isWeightVariation ? (wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2) : null,
          wvCapsuleS1MaxForRetest: isWeightVariation ? (wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6) : null,
          wvCapsuleS2ExtraUnits: isWeightVariation ? (wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40) : null,
          wvCapsuleS2MaxOutside: isWeightVariation ? (wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6) : null,
          ...titrationPayloadFields(titration, isTitration),
          physchemArea: isFp ? physchemArea ?? defaultAreaFor(area ?? "fp") : null,
          changeReason: reasonCheck.payload,
          hplcMethodId: (isHplcMethodAssay || workflowType === "Dissolution") && hplcMethodId !== "" ? Number(hplcMethodId) : null,
          icpMethodId: isIcpMethodAssay && icpMethodId !== "" ? Number(icpMethodId) : null
        };
        await update(editingId, payload);
        setMessage({ text: `Test "${trimmedCode}" updated.`, ok: true });
      } else {
        const payload: CreateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: resolvedEquationType,
          requiresSystemSuitability: (isDissolution || isHplcMethodAssay) ? true : false,
          methodAbbreviation: isDissolution || isHplcMethodAssay || isIcpMethodAssay ? methodAbbreviation.trim().toUpperCase() : null,
          replicateCount: isTitration ? Number(titration.replicateCount) : (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
          evaluationBasis: isMeasurement ? evaluationBasis : null,
          conditionFields: (isGravimetric || isDissolution || isDisintegration || isWeightVariation) ? (conditionFields.trim() || null) : null,
          usesTare: isGravimetric ? usesTare : null,
          dissolutionS1Offset: isDissolution ? (dissolutionS1Offset.trim() !== "" ? Number(dissolutionS1Offset) : 5) : null,
          dissolutionS2MinOffset: isDissolution ? (dissolutionS2MinOffset.trim() !== "" ? Number(dissolutionS2MinOffset) : 15) : null,
          dissolutionS3MinOffset: isDissolution ? (dissolutionS3MinOffset.trim() !== "" ? Number(dissolutionS3MinOffset) : 25) : null,
          dissolutionS3MaxBelowS2Min: isDissolution ? (dissolutionS3MaxBelowS2Min.trim() !== "" ? Number(dissolutionS3MaxBelowS2Min) : 2) : null,
          disintegrationStage1Units: isDisintegration ? (disintegrationStage1Units.trim() !== "" ? Number(disintegrationStage1Units) : 6) : null,
          disintegrationStage2Units: isDisintegration ? (disintegrationStage2Units.trim() !== "" ? Number(disintegrationStage2Units) : 12) : null,
          disintegrationMaxStage1Failures: isDisintegration ? (disintegrationMaxStage1Failures.trim() !== "" ? Number(disintegrationMaxStage1Failures) : 2) : null,
          disintegrationMinPassTotal: isDisintegration ? (disintegrationMinPassTotal.trim() !== "" ? Number(disintegrationMinPassTotal) : 16) : null,
          wvUnitCount: isWeightVariation ? (wvUnitCount.trim() !== "" ? Number(wvUnitCount) : 20) : null,
          wvTabletBand1MaxMg: isWeightVariation ? (wvTabletBand1MaxMg.trim() !== "" ? Number(wvTabletBand1MaxMg) : 130) : null,
          wvTabletBand1Percent: isWeightVariation ? (wvTabletBand1Percent.trim() !== "" ? Number(wvTabletBand1Percent) : 10) : null,
          wvTabletBand2MaxMg: isWeightVariation ? (wvTabletBand2MaxMg.trim() !== "" ? Number(wvTabletBand2MaxMg) : 324) : null,
          wvTabletBand2Percent: isWeightVariation ? (wvTabletBand2Percent.trim() !== "" ? Number(wvTabletBand2Percent) : 7.5) : null,
          wvTabletBand3Percent: isWeightVariation ? (wvTabletBand3Percent.trim() !== "" ? Number(wvTabletBand3Percent) : 5) : null,
          wvTabletMaxOutside: isWeightVariation ? (wvTabletMaxOutside.trim() !== "" ? Number(wvTabletMaxOutside) : 2) : null,
          wvCapsuleInnerPercent: isWeightVariation ? (wvCapsuleInnerPercent.trim() !== "" ? Number(wvCapsuleInnerPercent) : 10) : null,
          wvCapsuleOuterPercent: isWeightVariation ? (wvCapsuleOuterPercent.trim() !== "" ? Number(wvCapsuleOuterPercent) : 25) : null,
          wvCapsuleS1MaxOutside: isWeightVariation ? (wvCapsuleS1MaxOutside.trim() !== "" ? Number(wvCapsuleS1MaxOutside) : 2) : null,
          wvCapsuleS1MaxForRetest: isWeightVariation ? (wvCapsuleS1MaxForRetest.trim() !== "" ? Number(wvCapsuleS1MaxForRetest) : 6) : null,
          wvCapsuleS2ExtraUnits: isWeightVariation ? (wvCapsuleS2ExtraUnits.trim() !== "" ? Number(wvCapsuleS2ExtraUnits) : 40) : null,
          wvCapsuleS2MaxOutside: isWeightVariation ? (wvCapsuleS2MaxOutside.trim() !== "" ? Number(wvCapsuleS2MaxOutside) : 6) : null,
          ...titrationPayloadFields(titration, isTitration),
          physchemArea: isFp ? physchemArea ?? defaultAreaFor(area ?? "fp") : null,
          hplcMethodId: (isHplcMethodAssay || workflowType === "Dissolution") && hplcMethodId !== "" ? Number(hplcMethodId) : null,
          icpMethodId: isIcpMethodAssay && icpMethodId !== "" ? Number(icpMethodId) : null
        };
        await addNew(payload);
        setMessage({ text: `Test "${trimmedCode}" added to the Test Master.`, ok: true });
      }
      closeDialog();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setDialogError(err?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this test.`);
    } finally {
      setSaving(false);
    }
  };

  const toggleFreeze = async (t: TestDefinitionOption) => {
    setMessage(null);
    try {
      await setActive(t.id, !t.isActive);
      setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setMessage({ text: err?.response?.data?.message ?? "Could not update this test's status.", ok: false });
    }
  };

  const columns: RegisterColumn<TestDefinitionOption>[] = [
    { key: "code", label: "Code", sortable: true, render: (t) => <span style={{ fontFamily: monospaceFontFamily }}>{t.code}</span> },
    {
      key: "displayName",
      label: "Display Name",
      sortable: true,
      render: (t) => (
                    <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                      <span>{t.displayName}</span>
                      {t.workflowType === "HplcMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="HPLC Assay"
                          sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "IcpMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="ICP Assay"
                          sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Dissolution" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Dissolution"
                          sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Disintegration" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Disintegration"
                          sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "WeightVariation" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Weight Variation"
                          sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                        />
                      )}
                      {t.requiresSystemSuitability && (
                        <Tooltip
                          title={[
                            t.sstMaxRsdPercent != null ? `Max RSD: ${t.sstMaxRsdPercent}%` : null,
                            t.sstMinResolution != null ? `Min Res: ${t.sstMinResolution}` : null,
                            t.sstMaxTailingFactor != null ? `Max Tailing: ${t.sstMaxTailingFactor}` : null,
                            t.sstMinTheoreticalPlates != null ? `Min Plates: ${t.sstMinTheoreticalPlates}` : null
                          ].filter(Boolean).join(" | ") || "System suitability required"}
                        >
                          <Chip
                            size="small"
                            color="info"
                            variant="outlined"
                            label={`SST: ${t.methodAbbreviation ?? "Required"}`}
                            sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                    </Stack>
      )
    },
    { key: "section", label: "Section", sortable: true, sortValue: (t) => t.section?.name ?? "", render: (t) => t.section?.name ?? "—" },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (t) => (t.isActive ? "Active" : "Frozen"),
      render: (t) => <StatusBadge status={t.isActive ? "Active" : "Frozen"} />
    }
  ];

  const q = search.trim().toLowerCase();
  const visibleOptions = q
    ? options.filter((t) => t.code.toLowerCase().includes(q) || t.displayName.toLowerCase().includes(q) || (t.section?.name ?? "").toLowerCase().includes(q))
    : options;
  const detailsTest = expandedId !== null ? options.find((t) => t.id === expandedId) ?? null : null;

  const isDissolutionMethod = workflowType === "Dissolution";

  return (
    <>
      <LabPage
        title={isFp ? (area === "rmpm" ? "RM & PM Test Master" : "FP Test Master") : "Microbiology Test Master"}
        subtitle={isFp
          ? "Physicochemical tests: HPLC methods, equation type and system suitability criteria."
          : "Microbiology tests available to assign to Items, Sampling Points, Rooms, and Machine Parts."}
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>}
        filters={
          <FilterBar
            search={search}
            onSearch={setSearch}
            placeholder="Search code, name, section"
            resultCount={visibleOptions.length}
            onRefresh={() => { void reload(); }}
          />
        }
      >
        {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
        {sectionsState === "failed" && (
          <LoadErrorAlert
            message="The laboratory sections could not be loaded, so the tests cannot be sorted into this laboratory and none are shown."
            onRetry={() => setSectionsReloadKey((k) => k + 1)}
          />
        )}
        <RegisterTable
          columns={columns}
          rows={visibleOptions}
          getRowId={(t) => t.id}
          loading={sectionsState === "loading"}
          onRowClick={(t) => setExpandedId(t.id)}
          rowActions={(t) => [
            { label: "Details & Workflow Steps", onClick: () => setExpandedId(t.id) },
            { label: "Edit", onClick: () => startEdit(t) },
            { label: t.isActive ? "Freeze" : "Unfreeze", onClick: () => { void toggleFreeze(t); }, danger: t.isActive }
          ]}
          empty={{
            title: "No tests found",
            description: q ? "No test matches your search." : "Add a test to get started.",
            action: q ? undefined : <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>
          }}
        />
      </LabPage>

      <FloatingDialog
        open={detailsTest !== null}
        title={detailsTest ? `Details & Workflow Steps: ${detailsTest.code}` : ""}
        onClose={() => setExpandedId(null)}
        maxWidth="lg"
      >
        {detailsTest && (
          <WorkflowStepsSection test={detailsTest} workflowTypes={workflowTypes} onWorkflowTypeChanged={reload} />
        )}
      </FloatingDialog>

      <FloatingDialog
        open={dialogOpen}
        title={editingId ? "Edit Test" : "Add Test"}
        onClose={closeDialog}
        maxWidth="md"
        actions={
          <>
            <Button onClick={closeDialog} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button variant="contained" onClick={save} disabled={saving}>
              {saving ? "Saving…" : editingId ? "Save Changes" : "Add Test"}
            </Button>
          </>
        }
      >
        <LoadFailuresAlert failed={dialogListFailures} retryHint="Reload the page to try again." sx={{ mb: 2 }} />
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}

        <Stack spacing={2} sx={{ pt: 1 }}>
          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <TextField
              size="small"
              label="Code"
              placeholder="e.g. HPLC_VITC"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              sx={{ flex: 1, minWidth: 200 }}
            />
            <TextField
              size="small"
              label="Display Name"
              placeholder="e.g. Vitamin C Assay"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              sx={{ flex: 1.5, minWidth: 240 }}
            />
          </Stack>

          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required>
              <InputLabel id="test-section-select-label">Section</InputLabel>
              <Select<number | "">
                labelId="test-section-select-label"
                label="Section"
                value={sectionId}
                onChange={(e) => setSectionId(e.target.value === "" ? "" : Number(e.target.value))}
                inputProps={{ "aria-label": "Section" }}
              >
                {mySections.length > 1 && <MenuItem value=""><em>Select Section</em></MenuItem>}
                {editingSectionId !== null && !mySections.some((s) => s.sectionId === editingSectionId) && (
                  <MenuItem value={editingSectionId}>
                    {editingTest?.section?.name ?? `Section #${editingSectionId}`} (Current)
                  </MenuItem>
                )}
                {mySections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName} ({s.departmentName})
                  </MenuItem>
                ))}
              </Select>
              {editingId && editingSectionId !== null && sectionId !== "" && sectionId !== editingSectionId && (
                <FormHelperText>Existing test orders keep their current section.</FormHelperText>
              )}
            </FormControl>

            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }}>
              <InputLabel id="dialog-workflow-type-label">Workflow Type</InputLabel>
              <Select
                labelId="dialog-workflow-type-label"
                label="Workflow Type"
                value={workflowType}
                onChange={(e) => {
                  const next = e.target.value;
                  setWorkflowType(next);
                  if (next === "HplcMethodAssay") {
                    setEquationType("HplcMethodAssay");
                    setRequiresSystemSuitability(true);
                  } else if (next === "IcpMethodAssay") {
                    setEquationType("IcpMethodAssay");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Measurement") {
                    setEquationType("Measurement");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Gravimetric") {
                    if (equationType !== "GravimetricLoss" && equationType !== "GravimetricResidue") {
                      setEquationType("GravimetricLoss");
                    }
                    setRequiresSystemSuitability(false);
                  } else if (next === "Qualitative") {
                    setEquationType("Qualitative");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Dissolution") {
                    setEquationType("Dissolution");
                    setRequiresSystemSuitability(true);
                  } else if (next === "Disintegration") {
                    setEquationType("Disintegration");
                    setRequiresSystemSuitability(false);
                  } else if (next === "WeightVariation") {
                    setEquationType("WeightVariation");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Titration") {
                    setEquationType("Titration");
                    setRequiresSystemSuitability(false);
                  } else {
                    setEquationType("None");
                    setRequiresSystemSuitability(false);
                  }
                }}
              >
                {workflowTypes.map((w) => (
                  <MenuItem key={w} value={w}>
                    {WORKFLOW_TYPE_LABELS[w] ?? w}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          {isFp && (
            <FormControl size="small" fullWidth>
              <InputLabel id="dialog-equation-type-label">Equation Type</InputLabel>
              <Select
                labelId="dialog-equation-type-label"
                label="Equation Type"
                value={equationType}
                onChange={(e) => {
                  const next = e.target.value;
                  setEquationType(next);
                  if (next === "HplcMethodAssay") {
                    setWorkflowType("HplcMethodAssay");
                    setRequiresSystemSuitability(true);
                  } else if (next === "IcpMethodAssay") {
                    setWorkflowType("IcpMethodAssay");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Disintegration") {
                    setWorkflowType("Disintegration");
                    setRequiresSystemSuitability(false);
                  } else if (next === "WeightVariation") {
                    setWorkflowType("WeightVariation");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Dissolution") {
                    setWorkflowType("Dissolution");
                    setRequiresSystemSuitability(true);
                  }
                }}
              >
                {EQUATION_TYPES.filter((eq) => {
                  if (workflowType === "HplcMethodAssay") {
                    return eq === "HplcMethodAssay";
                  }
                  if (workflowType === "IcpMethodAssay") {
                    return eq === "IcpMethodAssay";
                  }
                  if (workflowType === "Disintegration") {
                    return eq === "Disintegration";
                  }
                  if (workflowType === "WeightVariation") {
                    return eq === "WeightVariation";
                  }
                  if (workflowType === "Titration") {
                    return eq === "Titration";
                  }
                  if (workflowType === "Dissolution") {
                    return eq === "Dissolution";
                  }
                  if (workflowType === "Measurement") {
                    return eq === "Measurement";
                  }
                  if (workflowType === "Gravimetric") {
                    return eq === "GravimetricLoss" || eq === "GravimetricResidue";
                  }
                  if (workflowType === "Qualitative") {
                    return eq === "Qualitative";
                  }
                  return eq !== "Measurement" && eq !== "GravimetricLoss" && eq !== "GravimetricResidue" && eq !== "Qualitative" && eq !== "Dissolution" && eq !== "Disintegration" && eq !== "WeightVariation" && eq !== "Titration" && eq !== "HplcMethodAssay" && eq !== "IcpMethodAssay";
                }).map((eq) => (
                  <MenuItem key={eq} value={eq}>
                    {EQUATION_TYPE_LABELS[eq] ?? eq}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {(workflowType === "HplcMethodAssay" || workflowType === "Dissolution") && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                {isDissolutionMethod ? "HPLC Method (supplies the dissolution standard)" : "HPLC Method Assay Configuration"}
              </Typography>
              <Stack spacing={2}>
                <FormControl size="small" fullWidth required={!isDissolutionMethod}>
                  <InputLabel id="dialog-hplc-method-label">{isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}</InputLabel>
                  <Select<number | "">
                    labelId="dialog-hplc-method-label"
                    label={isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}
                    value={hplcMethodId}
                    onChange={(e) => {
                      const val = e.target.value === "" ? "" : Number(e.target.value);
                      setHplcMethodId(val);
                      const chosen = hplcMethods.find((m) => m.id === val);
                      if (chosen && workflowType === "HplcMethodAssay") {
                        setMethodAbbreviation(chosen.abbreviation);
                      }
                    }}
                    inputProps={{ "aria-label": "HPLC Method" }}
                  >
                    <MenuItem value=""><em>Select HPLC Method</em></MenuItem>
                    {hplcMethods
                      .filter((m) => m.isActive || m.id === hplcMethodId)
                      .map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.name} ({m.abbreviation}){!m.isActive ? " (Inactive)" : ""}
                        </MenuItem>
                      ))}
                  </Select>
                  <FormHelperText>Select the active master HPLC method that defines this test&apos;s chromatographic conditions and analytes.</FormHelperText>
                </FormControl>

                <TextField
                  size="small"
                  label="Method Abbreviation"
                  value={methodAbbreviation}
                  slotProps={{
                    input: {
                      readOnly: true,
                    },
                  }}
                  helperText={workflowType === "Dissolution" ? "Dissolution keeps its own abbreviation." : "Auto-populated from the selected HPLC method (read-only)."}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "IcpMethodAssay" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                ICP Method Assay Configuration
              </Typography>
              <Stack spacing={2}>
                <FormControl size="small" fullWidth required>
                  <InputLabel id="dialog-icp-method-label">ICP Method *</InputLabel>
                  <Select<number | "">
                    labelId="dialog-icp-method-label"
                    label="ICP Method *"
                    value={icpMethodId}
                    onChange={(e) => {
                      const val = e.target.value === "" ? "" : Number(e.target.value);
                      setIcpMethodId(val);
                      const chosen = icpMethods.find((m) => m.id === val);
                      if (chosen) {
                        setMethodAbbreviation(chosen.abbreviation);
                      }
                    }}
                    inputProps={{ "aria-label": "ICP Method" }}
                  >
                    <MenuItem value=""><em>Select ICP Method</em></MenuItem>
                    {icpMethods
                      .filter((m) => m.isActive || m.id === icpMethodId)
                      .map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.name} ({m.abbreviation}){!m.isActive ? " (Inactive)" : ""}
                        </MenuItem>
                      ))}
                  </Select>
                  <FormHelperText>Select the active master ICP method that defines this test&apos;s elements and calibration standards.</FormHelperText>
                </FormControl>

                <TextField
                  size="small"
                  label="Method Abbreviation"
                  value={methodAbbreviation}
                  slotProps={{
                    input: {
                      readOnly: true,
                    },
                  }}
                  helperText="Auto-populated from the selected ICP method (read-only)."
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Measurement" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Measurement Settings
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Replicate Count *"
                  value={replicateCount}
                  onChange={(e) => setReplicateCount(e.target.value)}
                  slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                  helperText="Replicates per parameter (1–30)"
                  required
                  sx={{ flex: 1 }}
                />
                <FormControl size="small" sx={{ flex: 1 }} required>
                  <InputLabel id="dialog-eval-basis-label">Evaluation Basis *</InputLabel>
                  <Select
                    labelId="dialog-eval-basis-label"
                    label="Evaluation Basis *"
                    value={evaluationBasis}
                    onChange={(e) => setEvaluationBasis(e.target.value as "Mean" | "EachValue" | "Min" | "Max")}
                  >
                    <MenuItem value="Mean">Mean</MenuItem>
                    <MenuItem value="EachValue">Each Value</MenuItem>
                    <MenuItem value="Min">Minimum</MenuItem>
                    <MenuItem value="Max">Maximum</MenuItem>
                  </Select>
                </FormControl>
              </Stack>
            </Box>
          )}

          {workflowType === "Gravimetric" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Gravimetric Settings
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Replicate Count *"
                    value={replicateCount}
                    onChange={(e) => setReplicateCount(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                    helperText="Replicates per parameter (1–30)"
                    required
                    sx={{ flex: 1 }}
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        checked={usesTare}
                        onChange={(e) => setUsesTare(e.target.checked)}
                      />
                    }
                    label="Uses tare (container weight)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="e.g. Temperature (°C),Time (h)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Temperature (°C),Time (h)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Titration" && (
            <TitrationConfigSection form={titration} onChange={setTitration} sectionId={sectionId} />
          )}

          {isFp && area && <TestAreaField pageArea={area} value={physchemArea} onChange={setPhyschemArea} />}

          {editingId && hasTitrationSettingsChanged(editingTest, titration, workflowType, isFp ? physchemArea : null) && (
            <TitrationChangeReasonField value={changeReason} onChange={setChangeReason} error={changeReasonError} />
          )}

          {workflowType === "Qualitative" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary" }}>
                Qualitative test: records compliance observation against specifications directly (no replicates or steps).
              </Typography>
            </Box>
          )}

          {workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Dissolution Acceptance Offsets (USP &lt;711&gt; / EP 2.9.3)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S1: each unit ≥ Q + "
                    value={dissolutionS1Offset}
                    onChange={(e) => setDissolutionS1Offset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 5 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S2: no unit < Q − "
                    value={dissolutionS2MinOffset}
                    onChange={(e) => setDissolutionS2MinOffset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 15 (%)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S3: no unit < Q − "
                    value={dissolutionS3MinOffset}
                    onChange={(e) => setDissolutionS3MinOffset(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 25 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S3: max units < Q − S2 value"
                    value={dissolutionS3MaxBelowS2Min}
                    onChange={(e) => setDissolutionS3MaxBelowS2Min(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (units)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Disintegration" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 1 Units"
                    value={disintegrationStage1Units}
                    onChange={(e) => setDisintegrationStage1Units(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 6"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 2 Units"
                    value={disintegrationStage2Units}
                    onChange={(e) => setDisintegrationStage2Units(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 12"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Max S1 Failures"
                    value={disintegrationMaxStage1Failures}
                    onChange={(e) => setDisintegrationMaxStage1Failures(e.target.value)}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (0..S1-1)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Min Pass Total"
                    value={disintegrationMinPassTotal}
                    onChange={(e) => setDisintegrationMinPassTotal(e.target.value)}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 16 (1..S1+S2)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Medium,Temperature (°C),Discs"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Medium,Temperature (°C),Discs"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "WeightVariation" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Weight Variation Acceptance (USP &lt;2091&gt;)
              </Typography>
              <Stack spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Unit Count (Stage 1)"
                  value={wvUnitCount}
                  onChange={(e) => setWvUnitCount(e.target.value)}
                  slotProps={{ htmlInput: { min: 1, step: 1 } }}
                  helperText="Default: 20"
                  sx={{ maxWidth: 220 }}
                />

                {/* Group: Tablets */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Tablets
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Max (mg)"
                        value={wvTabletBand1MaxMg}
                        onChange={(e) => setWvTabletBand1MaxMg(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 130"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Deviation (%)"
                        value={wvTabletBand1Percent}
                        onChange={(e) => setWvTabletBand1Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Max (mg)"
                        value={wvTabletBand2MaxMg}
                        onChange={(e) => setWvTabletBand2MaxMg(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 324"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Deviation (%)"
                        value={wvTabletBand2Percent}
                        onChange={(e) => setWvTabletBand2Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 7.5"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 3 Deviation (%)"
                        value={wvTabletBand3Percent}
                        onChange={(e) => setWvTabletBand3Percent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 5 (> Band 2)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Max Outside Count"
                        value={wvTabletMaxOutside}
                        onChange={(e) => setWvTabletMaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                {/* Group: Capsules */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Capsules
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Inner Limit (%)"
                        value={wvCapsuleInnerPercent}
                        onChange={(e) => setWvCapsuleInnerPercent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Outer Limit (%)"
                        value={wvCapsuleOuterPercent}
                        onChange={(e) => setWvCapsuleOuterPercent(e.target.value)}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 25"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max Outside"
                        value={wvCapsuleS1MaxOutside}
                        onChange={(e) => setWvCapsuleS1MaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2 (pass without retest)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max For Retest"
                        value={wvCapsuleS1MaxForRetest}
                        onChange={(e) => setWvCapsuleS1MaxForRetest(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (qualifies for S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Extra Units"
                        value={wvCapsuleS2ExtraUnits}
                        onChange={(e) => setWvCapsuleS2ExtraUnits(e.target.value)}
                        slotProps={{ htmlInput: { min: 1, step: 1 } }}
                        helperText="Default: 40"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Max Outside"
                        value={wvCapsuleS2MaxOutside}
                        onChange={(e) => setWvCapsuleS2MaxOutside(e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (across S1+S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Balance ID,Temperature (°C)"
                  value={conditionFields}
                  onChange={(e) => setConditionFields(e.target.value)}
                  helperText="comma-separated, e.g. Balance ID,Temperature (°C)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={<Switch checked={requiresSystemSuitability} disabled />}
                label="Requires system suitability (standard from the assigned HPLC run)"
              />

              <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
                <TextField
                  size="small"
                  label="Method Abbreviation"
                  placeholder="e.g. VIT-C"
                  value={methodAbbreviation}
                  onChange={(e) => setMethodAbbreviation(e.target.value.toUpperCase())}
                  required
                  fullWidth
                  slotProps={{
                    htmlInput: {
                      maxLength: 20
                    }
                  }}
                  helperText="1–20 uppercase alphanumeric characters or hyphens (auto-uppercased)"
                />
              </Box>
            </Box>
          )}

          {/* Titration uses its own replicate count, not replicates per stage. */}
          {workflowType !== "Titration" && (isFp || (fpSectionId !== null && (sectionId === fpSectionId || editingTest?.sectionId === fpSectionId))) && (
            editingId ? (
              <TestStageReplicatesSection testDefinitionId={editingId} />
            ) : (
              <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 0.5 }}>
                  Replicates per stage
                </Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Save this test first to configure replicates per stage.
                </Typography>
              </Box>
            )
          )}
        </Stack>
      </FloatingDialog>
    </>
  );
}
