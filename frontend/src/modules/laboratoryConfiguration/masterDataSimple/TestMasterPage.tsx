import { Fragment, useEffect, useState } from "react";
import {
  Paper,
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
  Collapse,
  Box,
  Typography,
  Checkbox,
  FormControlLabel,
  Chip,
  Tooltip,
  FormControl,
  InputLabel,
  FormHelperText,
  Switch
} from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteIcon from "@mui/icons-material/Delete";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import AddIcon from "@mui/icons-material/Add";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import {
  masterDataOptions,
  incubationConditionLabel,
  MediaIncubationConditionOption,
  CreateTestDefinitionPayload,
  UpdateTestDefinitionPayload,
  TestAnalyteDto
} from "../../../services/masterDataOptions";
import { tableHeadSx } from "../../../theme";

// Microbiology and the Finished Product (chemistry) lab each have their own
// Test Master page: same component, filtered to the lab's section and
// workflow types. FP tests have no workflow steps or media.
export type TestMasterLab = "micro" | "fp";
const FP_SECTION_CODE = "FP";
const WORKFLOW_TYPES_BY_LAB: Record<TestMasterLab, string[]> = {
  micro: ["CountTest", "Observation"],
  fp: ["HplcAssay", "ElementalAssay"]
};
const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  CountTest: "Count Test",
  Observation: "Observation",
  HplcAssay: "HPLC Assay",
  ElementalAssay: "Elemental Assay (ICP-OES)"
};

const EQUATION_TYPES = ["None", "HplcAssay", "SystemSuitability", "CalibrationCurve"];
const EQUATION_TYPE_LABELS: Record<string, string> = {
  None: "None",
  HplcAssay: "HPLC Assay",
  SystemSuitability: "System Suitability",
  CalibrationCurve: "Calibration Curve"
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
function stage1Ranges(stepMedia: any[], min: string, max: string): string {
  if (!stepMedia?.length) return "—";
  const distinct = Array.from(new Set(stepMedia.map((m) => `${m[min]}-${m[max]}`)));
  return distinct.join("; ");
}

function stepNeedsConfiguration(s: any): boolean {
  if (STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (s.stepType !== "BiochemicalTest" && (s.stepMedia?.length ?? 0) === 0) return true;
  if (s.stepType === "BiochemicalTest" && !s.phenotypicTestType && (s.phenotypicTestTypes?.length ?? 0) === 0) return true;
  return false;
}

function TestAnalytesSection({ test }: { test: TestDefinitionOption }) {
  const [analytes, setAnalytes] = useState<TestAnalyteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingAnalyte, setEditingAnalyte] = useState<TestAnalyteDto | null>(null);
  const [element, setElement] = useState("");
  const [wavelengthNm, setWavelengthNm] = useState("");
  const [view, setView] = useState<"Axial" | "Radial">("Axial");
  const [loqMgPerL, setLoqMgPerL] = useState("");
  const [displayOrder, setDisplayOrder] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadAnalytes = () => {
    setLoading(true);
    setError(null);
    masterDataOptions
      .getTestAnalytes(test.id)
      .then(setAnalytes)
      .catch((e: unknown) => {
        const errObj = e as { response?: { data?: { message?: string } }; message?: string };
        setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load test analytes.");
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadAnalytes();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [test.id]);

  const openAdd = () => {
    setEditingAnalyte(null);
    setElement("");
    setWavelengthNm("");
    setView("Axial");
    setLoqMgPerL("");
    setDisplayOrder(String(analytes.length + 1));
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (a: TestAnalyteDto) => {
    setEditingAnalyte(a);
    setElement(a.element);
    setWavelengthNm(String(a.wavelengthNm));
    setView(a.view);
    setLoqMgPerL(String(a.loqMgPerL));
    setDisplayOrder(String(a.displayOrder));
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSaveAnalyte = async () => {
    const trimmedEl = element.trim();
    if (!trimmedEl) {
      setDialogError("Element symbol is required.");
      return;
    }
    if (trimmedEl.length > 20) {
      setDialogError("Element symbol cannot exceed 20 characters.");
      return;
    }
    const wave = Number(wavelengthNm);
    if (!wave || wave <= 0) {
      setDialogError("Wavelength must be greater than 0.");
      return;
    }
    const loq = Number(loqMgPerL);
    if (!loq || loq <= 0) {
      setDialogError("LOQ must be greater than 0.");
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      if (editingAnalyte) {
        await masterDataOptions.updateTestAnalyte(test.id, editingAnalyte.id, {
          element: trimmedEl,
          wavelengthNm: wave,
          view,
          loqMgPerL: loq,
          displayOrder: displayOrder ? Number(displayOrder) : editingAnalyte.displayOrder
        });
      } else {
        await masterDataOptions.createTestAnalyte(test.id, {
          element: trimmedEl,
          wavelengthNm: wave,
          view,
          loqMgPerL: loq,
          displayOrder: displayOrder ? Number(displayOrder) : 0
        });
      }
      setDialogOpen(false);
      loadAnalytes();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not save analyte.");
    } finally {
      setSaving(false);
    }
  };

  const handleToggleActive = async (a: TestAnalyteDto) => {
    setError(null);
    try {
      if (a.isActive) {
        await masterDataOptions.deleteTestAnalyte(test.id, a.id);
      } else {
        await masterDataOptions.updateTestAnalyte(test.id, a.id, { isActive: true });
      }
      loadAnalytes();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? "Could not update analyte status.");
    }
  };

  return (
    <Box sx={{ mt: 2 }}>
      <Stack sx={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          Test Analytes ({analytes.filter((a) => a.isActive).length} active)
        </Typography>
        <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={openAdd}>
          Add Analyte
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
        <TableHead>
          <TableRow sx={tableHeadSx}>
            <TableCell>Order</TableCell>
            <TableCell>Element</TableCell>
            <TableCell>Wavelength (nm)</TableCell>
            <TableCell>Plasma View</TableCell>
            <TableCell>LOQ (mg/L)</TableCell>
            <TableCell>Status</TableCell>
            <TableCell align="right">Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {analytes.map((a) => (
            <TableRow key={a.id} sx={{ opacity: a.isActive ? 1 : 0.6 }}>
              <TableCell>{a.displayOrder}</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{a.element}</TableCell>
              <TableCell>{a.wavelengthNm}</TableCell>
              <TableCell><Chip size="small" label={a.view} variant="outlined" /></TableCell>
              <TableCell>{a.loqMgPerL}</TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={a.isActive ? "Active" : "Deactivated"}
                  color={a.isActive ? "success" : "default"}
                />
              </TableCell>
              <TableCell align="right">
                <IconButton size="small" onClick={() => openEdit(a)} title="Edit Analyte">
                  <EditIcon fontSize="small" />
                </IconButton>
                <Tooltip title={a.isActive ? "Deactivate Analyte" : "Re-activate Analyte"}>
                  <IconButton size="small" color={a.isActive ? "error" : "primary"} onClick={() => handleToggleActive(a)}>
                    {a.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
                  </IconButton>
                </Tooltip>
              </TableCell>
            </TableRow>
          ))}
          {analytes.length === 0 && !loading && (
            <TableRow>
              <TableCell colSpan={7} align="center" sx={{ py: 2, color: "text.secondary" }}>
                No analytes configured yet. Click "Add Analyte" to configure wavelengths and LOQs for this test method.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      <FloatingDialog
        open={dialogOpen}
        title={editingAnalyte ? `Edit Analyte: ${editingAnalyte.element}` : "Add Test Analyte"}
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} disabled={saving}>Cancel</Button>
            <Button variant="contained" onClick={handleSaveAnalyte} disabled={saving}>
              {saving ? "Saving…" : editingAnalyte ? "Save Changes" : "Add Analyte"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {dialogError && <Alert severity="error">{dialogError}</Alert>}
          <TextField
            size="small"
            label="Element Symbol"
            placeholder="e.g. Zn, Pb, Ca"
            value={element}
            onChange={(e) => setElement(e.target.value.trim())}
            required
            fullWidth
            slotProps={{ htmlInput: { maxLength: 20 } }}
          />
          <TextField
            size="small"
            type="number"
            label="Wavelength (nm)"
            placeholder="e.g. 213.856"
            value={wavelengthNm}
            onChange={(e) => setWavelengthNm(e.target.value)}
            required
            fullWidth
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
          />
          <FormControl size="small" fullWidth required>
            <InputLabel id="plasma-view-label">Plasma View</InputLabel>
            <Select
              labelId="plasma-view-label"
              label="Plasma View"
              value={view}
              onChange={(e) => setView(e.target.value as "Axial" | "Radial")}
            >
              <MenuItem value="Axial">Axial</MenuItem>
              <MenuItem value="Radial">Radial</MenuItem>
            </Select>
          </FormControl>
          <TextField
            size="small"
            type="number"
            label="Limit of Quantification - LOQ (mg/L)"
            placeholder="e.g. 0.05"
            value={loqMgPerL}
            onChange={(e) => setLoqMgPerL(e.target.value)}
            required
            fullWidth
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
          />
          <TextField
            size="small"
            type="number"
            label="Display Order"
            value={displayOrder}
            onChange={(e) => setDisplayOrder(e.target.value)}
            fullWidth
          />
        </Stack>
      </FloatingDialog>
    </Box>
  );
}

// Shown when a Test Master row is expanded, alongside Approved Media -
// the configurable workflow template TestWorkflowEngine reads instead
// of a hardcoded per-test-code chain (see backend TestWorkflowStep.cs).
// A step can only be deleted if no TestOrder has used it yet (server-
// enforced); reordering swaps StepOrder with the adjacent step.
function WorkflowStepsSection({ test, workflowTypes, onWorkflowTypeChanged }: { test: TestDefinitionOption; workflowTypes: string[]; onWorkflowTypeChanged: () => void }) {
  const [steps, setSteps] = useState<any[]>([]);
  const [organisms, setOrganisms] = useState<any[]>([]);
  const [materials, setMaterials] = useState<any[]>([]);
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
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not update the workflow type.");
    }
  };

  const startEditStep = (s: any) => {
    setEditingStepId(s.id);
    const stage2 = (s.incubationStages ?? []).find((x: any) => x.stageNumber === 2);
    setForm({
      stepName: s.stepName, isFinalStep: s.isFinalStep, stepType: s.stepType,
      targetOrganismId: s.targetOrganismId ?? null,
      stepMedia: (s.stepMedia ?? []).map((m: any) => ({
        materialId: m.materialId,
        mediaIncubationConditionId: m.mediaIncubationConditionId ?? "",
        isRequired: m.isRequired,
        displayOrder: m.displayOrder
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
        await masterDataOptions.updateTestWorkflowStep(editingStepId, payload);
      } else {
        await masterDataOptions.createTestWorkflowStep(test.id, payload);
      }
      cancelEditStep();
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? `Could not ${editingStepId ? "update" : "add"} this step.`);
    }
  };

  const move = async (stepId: number, direction: "up" | "down") => {
    setError(null);
    try {
      await masterDataOptions.moveTestWorkflowStep(stepId, direction);
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not reorder this step.");
    }
  };

  const remove = async (stepId: number) => {
    setError(null);
    try {
      await masterDataOptions.deleteTestWorkflowStep(stepId);
      await loadSteps();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not delete this step.");
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
          {test.workflowType === "HplcAssay" || test.workflowType === "ElementalAssay" ? "Workflow Type" : "Workflow Steps"}
        </Typography>
        <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)}>
          {workflowTypes.map((w) => <MenuItem key={w} value={w}>{WORKFLOW_TYPE_LABELS[w] ?? w}</MenuItem>)}
        </Select>
      </Stack>
      {test.workflowType === "HplcAssay" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>HPLC Configuration</Typography>
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "None"] ?? test.equationType ?? "None"}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.requiresSystemSuitability ? `Required (${test.methodAbbreviation ?? "No abbr"})` : "Not required"}
              </Typography>
            </Box>
            {test.requiresSystemSuitability && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>SST Criteria</Typography>
                <Typography variant="body2">
                  {[
                    test.sstMaxRsdPercent != null ? `Max RSD: ${test.sstMaxRsdPercent}%` : null,
                    test.sstMinResolution != null ? `Min Res: ${test.sstMinResolution}` : null,
                    test.sstMaxTailingFactor != null ? `Max Tailing: ${test.sstMaxTailingFactor}` : null,
                    test.sstMinTheoreticalPlates != null ? `Min Plates: ${test.sstMinTheoreticalPlates}` : null
                  ].filter(Boolean).join(", ") || "None specified"}
                </Typography>
              </Box>
            )}
          </Stack>
        </Box>
      )}
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {test.workflowType === "ElementalAssay" || test.equationType === "CalibrationCurve" ? (
        <>
          <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "secondary.main" }}>ICP-OES Calibration Curve Configuration</Typography>
            <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "None"] ?? test.equationType ?? "None"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Method Abbr</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.methodAbbreviation ?? "—"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Min Correlation</Typography>
                <Typography variant="body2">
                  {test.calMinCorrelation != null ? `>= ${test.calMinCorrelation} (${test.calCorrelationType === "RSquared" ? "r²" : "r"})` : "—"}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Min Standards</Typography>
                <Typography variant="body2">{test.calMinStandards ?? "—"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Check Recovery (ICV/CCV)</Typography>
                <Typography variant="body2">
                  {test.calCheckRecoveryLowPercent != null && test.calCheckRecoveryHighPercent != null
                    ? `${test.calCheckRecoveryLowPercent}% – ${test.calCheckRecoveryHighPercent}%`
                    : "—"}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max Blank</Typography>
                <Typography variant="body2">{test.calBlankMax != null ? `${test.calBlankMax} mg/L` : "Analyte LOQ"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Internal Standard</Typography>
                <Typography variant="body2">
                  {test.calRequireInternalStandard
                    ? `Required (${test.calIsRecoveryLowPercent ?? "—"}% – ${test.calIsRecoveryHighPercent ?? "—"}%)`
                    : "Not required"}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Required Checks</Typography>
                <Stack direction="row" spacing={0.5} sx={{ mt: 0.25, flexWrap: "wrap", alignItems: "center" }}>
                  {test.calRequireBlank && <Chip size="small" label="Blank" sx={{ height: 18, fontSize: "0.65rem" }} />}
                  {test.calRequireIcv && <Chip size="small" label="ICV" sx={{ height: 18, fontSize: "0.65rem" }} />}
                  {test.calRequireCcv && <Chip size="small" label="CCV" sx={{ height: 18, fontSize: "0.65rem" }} />}
                  {test.calRequireInternalStandard && <Chip size="small" label="IS" sx={{ height: 18, fontSize: "0.65rem" }} />}
                  {!test.calRequireBlank && !test.calRequireIcv && !test.calRequireCcv && !test.calRequireInternalStandard && (
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>None</Typography>
                  )}
                </Stack>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max Run Age</Typography>
                <Typography variant="body2">{test.calMaxRunAgeHours ?? 24} hours</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Reported Basis</Typography>
                <Typography variant="body2">ppm in the sample (SamplePpm)</Typography>
              </Box>
            </Stack>
          </Box>
          <TestAnalytesSection test={test} />
        </>
      ) : test.workflowType === "HplcAssay" ? (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          HPLC assay tests have no workflow steps or media: the result is entered from the chromatography data against a passed System Suitability run.
        </Typography>
      ) : (
      <>
      {steps.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell>#</TableCell><TableCell>Step</TableCell><TableCell>Incubation</TableCell>
              <TableCell>Temp °C</TableCell><TableCell>Step Type</TableCell><TableCell>Media</TableCell><TableCell>Organism</TableCell>
              <TableCell>Status</TableCell><TableCell>Final</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {steps.map((s, i) => {
              const stage2 = (s.incubationStages ?? []).find((x: any) => x.stageNumber === 2);
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
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
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
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    {s.stepType === "BiochemicalTest"
                      ? (s.phenotypicTestTypes?.length > 0
                          ? s.phenotypicTestTypes.map((t: string) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")
                          : s.phenotypicTestType ? PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType : <em>—</em>)
                      : (s.stepMedia?.length > 0 ? s.stepMedia.map((m: any) => m.materialName).join(", ") : <em>—</em>)}
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
      ) : (
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            mb: 1.5
          }}>No workflow steps configured yet.</Typography>
      )}

      <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1 }}>{editingStepId ? "Edit Step" : "Add Step"}</Typography>
      <Stack
        direction="row"
        spacing={1.5}
        sx={{
          flexWrap: "wrap",
          alignItems: "center"
        }}>
        <TextField size="small" label="Step Name" placeholder="e.g. TSB" value={form.stepName ?? ""} onChange={(e) => setForm({ ...form, stepName: e.target.value })} sx={{ minWidth: 140 }} />
        {isBiochemical && (
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap"
            }}>
            <Select size="small" displayEmpty value={pendingPhenotypicTest} onChange={(e) => setPendingPhenotypicTest(e.target.value as string)} sx={{ minWidth: 180 }}>
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
        <Select size="small" value={form.stepType} onChange={(e) => changeStepType(e.target.value)} sx={{ minWidth: 180 }}>
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
          <Stack
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
          <Select<number | ""> size="small" displayEmpty value={form.targetOrganismId ?? ""} onChange={(e) => setForm({ ...form, targetOrganismId: e.target.value === "" ? null : Number(e.target.value) })} sx={{ minWidth: 220 }}>
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
                <Stack
                  key={idx}
                  direction="row"
                  spacing={1.5}
                  sx={{
                    alignItems: "center",
                    flexWrap: "wrap"
                  }}>
                  <Select<number | ""> size="small" displayEmpty value={row.materialId} onChange={(e) => updateMediaRow(idx, { materialId: e.target.value === "" ? "" : Number(e.target.value) })} sx={{ minWidth: 200 }}>
                    <MenuItem value=""><em>Material</em></MenuItem>
                    {materials.map((m) => <MenuItem key={m.id} value={m.id}>{m.materialName}</MenuItem>)}
                  </Select>
                  <Select<number | "">
                    size="small"
                    displayEmpty
                    value={row.mediaIncubationConditionId}
                    disabled={productConditions.length === 0}
                    onChange={(e) => updateMediaRow(idx, { mediaIncubationConditionId: e.target.value === "" ? "" : Number(e.target.value) })}
                    sx={{ minWidth: 240 }}>
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
export function TestMasterPage({ lab = "micro" }: { lab?: TestMasterLab }) {
  const { options: allOptions, addNew, update, setActive, reload } = useTestDefinitions();
  const isFp = lab === "fp";
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[lab];
  const defaultWorkflowType: string = isFp ? "HplcAssay" : "Observation";
  const [fpSectionId, setFpSectionId] = useState<number | null>(null);
  useEffect(() => {
    getSections()
      .then((secs) => setFpSectionId(secs.find((s) => s.sectionCode === FP_SECTION_CODE)?.sectionId ?? null))
      .catch(() => setFpSectionId(null));
  }, []);
  const inLab = (sid?: number | null) => (isFp ? sid === fpSectionId : sid !== fpSectionId);
  const options = allOptions.filter((t) => fpSectionId !== null && inLab(t.sectionId));
  const [code, setCode] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
  const [workflowType, setWorkflowType] = useState<string>(defaultWorkflowType);
  const [equationType, setEquationType] = useState<string>(isFp ? "HplcAssay" : "None");
  const [requiresSystemSuitability, setRequiresSystemSuitability] = useState<boolean>(false);
  const [methodAbbreviation, setMethodAbbreviation] = useState<string>("");
  const [sstMaxRsdPercent, setSstMaxRsdPercent] = useState<string>("");
  const [sstMinResolution, setSstMinResolution] = useState<string>("");
  const [sstMaxTailingFactor, setSstMaxTailingFactor] = useState<string>("");
  const [sstMinTheoreticalPlates, setSstMinTheoreticalPlates] = useState<string>("");

  const [calMinCorrelation, setCalMinCorrelation] = useState<string>("0.999");
  const [calCorrelationType, setCalCorrelationType] = useState<"R" | "RSquared">("R");
  const [calMinStandards, setCalMinStandards] = useState<string>("5");
  const [calCheckRecoveryLowPercent, setCalCheckRecoveryLowPercent] = useState<string>("90");
  const [calCheckRecoveryHighPercent, setCalCheckRecoveryHighPercent] = useState<string>("110");
  const [calBlankMax, setCalBlankMax] = useState<string>("");
  const [calIsRecoveryLowPercent, setCalIsRecoveryLowPercent] = useState<string>("80");
  const [calIsRecoveryHighPercent, setCalIsRecoveryHighPercent] = useState<string>("120");
  const [calRequireBlank, setCalRequireBlank] = useState<boolean>(true);
  const [calRequireIcv, setCalRequireIcv] = useState<boolean>(true);
  const [calRequireCcv, setCalRequireCcv] = useState<boolean>(true);
  const [calRequireInternalStandard, setCalRequireInternalStandard] = useState<boolean>(false);
  const [calMaxRunAgeHours, setCalMaxRunAgeHours] = useState<string>("24");

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editingSectionId, setEditingSectionId] = useState<number | null>(null);
  const [editingTest, setEditingTest] = useState<TestDefinitionOption | null>(null);
  const [allMySections, setMySections] = useState<LaboratorySection[]>([]);
  const mySections = allMySections.filter((s) => fpSectionId !== null && inLab(s.sectionId));
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);

  useEffect(() => {
    getMySections()
      .then((secs) => setMySections(secs))
      .catch(() => {});
  }, []);

  const openCreateDialog = () => {
    setEditingId(null);
    setEditingTest(null);
    setCode("");
    setDisplayName("");
    setSectionId(mySections.length === 1 ? mySections[0].sectionId : "");
    setEditingSectionId(null);
    setWorkflowType(defaultWorkflowType);
    setEquationType(isFp ? (defaultWorkflowType === "ElementalAssay" ? "CalibrationCurve" : "HplcAssay") : "None");
    setRequiresSystemSuitability(false);
    setMethodAbbreviation("");
    setSstMaxRsdPercent("");
    setSstMinResolution("");
    setSstMaxTailingFactor("");
    setSstMinTheoreticalPlates("");
    setCalMinCorrelation("0.999");
    setCalCorrelationType("R");
    setCalMinStandards("5");
    setCalCheckRecoveryLowPercent("90");
    setCalCheckRecoveryHighPercent("110");
    setCalBlankMax("");
    setCalIsRecoveryLowPercent("80");
    setCalIsRecoveryHighPercent("120");
    setCalRequireBlank(true);
    setCalRequireIcv(true);
    setCalRequireCcv(true);
    setCalRequireInternalStandard(false);
    setCalMaxRunAgeHours("24");
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
    setEquationType(t.equationType || (t.workflowType === "ElementalAssay" ? "CalibrationCurve" : t.workflowType === "HplcAssay" ? "HplcAssay" : "None"));
    setRequiresSystemSuitability(!!t.requiresSystemSuitability);
    setMethodAbbreviation(t.methodAbbreviation ?? "");
    setSstMaxRsdPercent(t.sstMaxRsdPercent != null ? String(t.sstMaxRsdPercent) : "");
    setSstMinResolution(t.sstMinResolution != null ? String(t.sstMinResolution) : "");
    setSstMaxTailingFactor(t.sstMaxTailingFactor != null ? String(t.sstMaxTailingFactor) : "");
    setSstMinTheoreticalPlates(t.sstMinTheoreticalPlates != null ? String(t.sstMinTheoreticalPlates) : "");
    setCalMinCorrelation(t.calMinCorrelation != null ? String(t.calMinCorrelation) : "0.999");
    setCalCorrelationType((t.calCorrelationType as "R" | "RSquared") || "R");
    setCalMinStandards(t.calMinStandards != null ? String(t.calMinStandards) : "5");
    setCalCheckRecoveryLowPercent(t.calCheckRecoveryLowPercent != null ? String(t.calCheckRecoveryLowPercent) : "90");
    setCalCheckRecoveryHighPercent(t.calCheckRecoveryHighPercent != null ? String(t.calCheckRecoveryHighPercent) : "110");
    setCalBlankMax(t.calBlankMax != null ? String(t.calBlankMax) : "");
    setCalIsRecoveryLowPercent(t.calIsRecoveryLowPercent != null ? String(t.calIsRecoveryLowPercent) : "80");
    setCalIsRecoveryHighPercent(t.calIsRecoveryHighPercent != null ? String(t.calIsRecoveryHighPercent) : "120");
    setCalRequireBlank(t.calRequireBlank !== false);
    setCalRequireIcv(t.calRequireIcv !== false);
    setCalRequireCcv(t.calRequireCcv !== false);
    setCalRequireInternalStandard(!!t.calRequireInternalStandard);
    setCalMaxRunAgeHours(t.calMaxRunAgeHours != null ? String(t.calMaxRunAgeHours) : "24");
    setDialogError(null);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setEditingTest(null);
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
    const isHplc = workflowType === "HplcAssay";
    const isCalCurve = isFp && equationType === "CalibrationCurve";

    if (isHplc && requiresSystemSuitability) {
      const trimmedAbbr = methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        setDialogError("Method abbreviation is required when system suitability is enabled.");
        return;
      }
      if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        setDialogError("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        return;
      }
      const hasRsd = sstMaxRsdPercent.trim() !== "";
      const hasRes = sstMinResolution.trim() !== "";
      const hasTailing = sstMaxTailingFactor.trim() !== "";
      const hasPlates = sstMinTheoreticalPlates.trim() !== "";

      if (!hasRsd && !hasRes && !hasTailing && !hasPlates) {
        setDialogError("At least one system suitability criterion is required when system suitability is enabled.");
        return;
      }
    }

    if (isCalCurve) {
      if (workflowType !== "ElementalAssay") {
        setDialogError("Workflow type must be Elemental Assay when equation type is Calibration Curve.");
        return;
      }
      const trimmedAbbr = methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        setDialogError("Method abbreviation is required when equation type is Calibration Curve.");
        return;
      }
      if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        setDialogError("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        return;
      }
      const minCorr = Number(calMinCorrelation);
      if (!minCorr || minCorr <= 0 || minCorr > 1) {
        setDialogError("Minimum correlation must be in (0, 1] when equation type is Calibration Curve.");
        return;
      }
      const minStds = Number(calMinStandards);
      if (!minStds || minStds < 1) {
        setDialogError("Minimum standards must be at least 1 when equation type is Calibration Curve.");
        return;
      }
      if (calCheckRecoveryLowPercent.trim() === "" || calCheckRecoveryHighPercent.trim() === "") {
        setDialogError("Both check recovery window bounds (low and high) are required when equation type is Calibration Curve.");
        return;
      }
      if (Number(calCheckRecoveryLowPercent) > Number(calCheckRecoveryHighPercent)) {
        setDialogError("Check recovery low percent must be less than or equal to high percent.");
        return;
      }
      if (calRequireInternalStandard) {
        if (calIsRecoveryLowPercent.trim() === "" || calIsRecoveryHighPercent.trim() === "") {
          setDialogError("Both internal standard recovery bounds (low and high) are required when internal standards are required.");
          return;
        }
        if (Number(calIsRecoveryLowPercent) > Number(calIsRecoveryHighPercent)) {
          setDialogError("Internal standard recovery low percent must be less than or equal to high percent.");
          return;
        }
      }
      const maxAge = calMaxRunAgeHours.trim() === "" ? 24 : Number(calMaxRunAgeHours);
      if (maxAge < 1) {
        setDialogError("Maximum run age must be at least 1 hour when equation type is Calibration Curve.");
        return;
      }
    }

    setSaving(true);
    try {
      if (editingId) {
        const payload: UpdateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: isCalCurve ? "CalibrationCurve" : isHplc ? equationType : "None",
          requiresSystemSuitability: isHplc ? requiresSystemSuitability : false,
          methodAbbreviation: (isHplc && requiresSystemSuitability) || isCalCurve ? methodAbbreviation.trim().toUpperCase() : null,
          sstMaxRsdPercent: isHplc && requiresSystemSuitability && sstMaxRsdPercent.trim() !== "" ? Number(sstMaxRsdPercent) : null,
          sstMinResolution: isHplc && requiresSystemSuitability && sstMinResolution.trim() !== "" ? Number(sstMinResolution) : null,
          sstMaxTailingFactor: isHplc && requiresSystemSuitability && sstMaxTailingFactor.trim() !== "" ? Number(sstMaxTailingFactor) : null,
          sstMinTheoreticalPlates: isHplc && requiresSystemSuitability && sstMinTheoreticalPlates.trim() !== "" ? Number(sstMinTheoreticalPlates) : null,
          calibrationEntryMode: isCalCurve ? "InstrumentReported" : null,
          calMinCorrelation: isCalCurve && calMinCorrelation.trim() !== "" ? Number(calMinCorrelation) : null,
          calCorrelationType: isCalCurve ? calCorrelationType : null,
          calMinStandards: isCalCurve && calMinStandards.trim() !== "" ? Number(calMinStandards) : null,
          calCheckRecoveryLowPercent: isCalCurve && calCheckRecoveryLowPercent.trim() !== "" ? Number(calCheckRecoveryLowPercent) : null,
          calCheckRecoveryHighPercent: isCalCurve && calCheckRecoveryHighPercent.trim() !== "" ? Number(calCheckRecoveryHighPercent) : null,
          calBlankMax: isCalCurve && calBlankMax.trim() !== "" ? Number(calBlankMax) : null,
          calIsRecoveryLowPercent: isCalCurve && calIsRecoveryLowPercent.trim() !== "" ? Number(calIsRecoveryLowPercent) : null,
          calIsRecoveryHighPercent: isCalCurve && calIsRecoveryHighPercent.trim() !== "" ? Number(calIsRecoveryHighPercent) : null,
          calRequireBlank: isCalCurve ? calRequireBlank : null,
          calRequireIcv: isCalCurve ? calRequireIcv : null,
          calRequireCcv: isCalCurve ? calRequireCcv : null,
          calRequireInternalStandard: isCalCurve ? calRequireInternalStandard : null,
          reportedConcentrationBasis: isCalCurve ? "SamplePpm" : null,
          calMaxRunAgeHours: isCalCurve ? (calMaxRunAgeHours.trim() !== "" ? Number(calMaxRunAgeHours) : 24) : null
        };
        await update(editingId, payload);
        setMessage({ text: `Test "${trimmedCode}" updated.`, ok: true });
      } else {
        const payload: CreateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: isCalCurve ? "CalibrationCurve" : isHplc ? equationType : "None",
          requiresSystemSuitability: isHplc ? requiresSystemSuitability : false,
          methodAbbreviation: (isHplc && requiresSystemSuitability) || isCalCurve ? methodAbbreviation.trim().toUpperCase() : null,
          sstMaxRsdPercent: isHplc && requiresSystemSuitability && sstMaxRsdPercent.trim() !== "" ? Number(sstMaxRsdPercent) : null,
          sstMinResolution: isHplc && requiresSystemSuitability && sstMinResolution.trim() !== "" ? Number(sstMinResolution) : null,
          sstMaxTailingFactor: isHplc && requiresSystemSuitability && sstMaxTailingFactor.trim() !== "" ? Number(sstMaxTailingFactor) : null,
          sstMinTheoreticalPlates: isHplc && requiresSystemSuitability && sstMinTheoreticalPlates.trim() !== "" ? Number(sstMinTheoreticalPlates) : null,
          calibrationEntryMode: isCalCurve ? "InstrumentReported" : null,
          calMinCorrelation: isCalCurve && calMinCorrelation.trim() !== "" ? Number(calMinCorrelation) : null,
          calCorrelationType: isCalCurve ? calCorrelationType : null,
          calMinStandards: isCalCurve && calMinStandards.trim() !== "" ? Number(calMinStandards) : null,
          calCheckRecoveryLowPercent: isCalCurve && calCheckRecoveryLowPercent.trim() !== "" ? Number(calCheckRecoveryLowPercent) : null,
          calCheckRecoveryHighPercent: isCalCurve && calCheckRecoveryHighPercent.trim() !== "" ? Number(calCheckRecoveryHighPercent) : null,
          calBlankMax: isCalCurve && calBlankMax.trim() !== "" ? Number(calBlankMax) : null,
          calIsRecoveryLowPercent: isCalCurve && calIsRecoveryLowPercent.trim() !== "" ? Number(calIsRecoveryLowPercent) : null,
          calIsRecoveryHighPercent: isCalCurve && calIsRecoveryHighPercent.trim() !== "" ? Number(calIsRecoveryHighPercent) : null,
          calRequireBlank: isCalCurve ? calRequireBlank : null,
          calRequireIcv: isCalCurve ? calRequireIcv : null,
          calRequireCcv: isCalCurve ? calRequireCcv : null,
          calRequireInternalStandard: isCalCurve ? calRequireInternalStandard : null,
          reportedConcentrationBasis: isCalCurve ? "SamplePpm" : null,
          calMaxRunAgeHours: isCalCurve ? (calMaxRunAgeHours.trim() !== "" ? Number(calMaxRunAgeHours) : 24) : null
        };
        await addNew(payload);
        setMessage({ text: `Test "${trimmedCode}" added to the Test Master.`, ok: true });
      }
      closeDialog();
    } catch (e: any) {
      setDialogError(e?.response?.data?.message ?? `Could not ${editingId ? "update" : "add"} this test.`);
    } finally {
      setSaving(false);
    }
  };

  const toggleFreeze = async (t: TestDefinitionOption) => {
    setMessage(null);
    try {
      await setActive(t.id, !t.isActive);
      setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update this test's status.", ok: false });
    }
  };

  return (
    <>
      <PageHeader
        title={isFp ? "Finished Product Test Master" : "Microbiology Test Master"}
        subtitle={isFp
          ? "Finished Product (chemistry) tests: HPLC methods, equation type and system suitability criteria."
          : "Microbiology tests available to assign to Items, Sampling Points, Rooms, and Machine Parts."}
      >
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>
          Add Test
        </Button>
      </PageHeader>
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Stack direction="row" spacing={1.5} sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <SectionTitle>All Tests</SectionTitle>
        <Button variant="outlined" size="small" startIcon={<AddIcon />} onClick={openCreateDialog}>
          Add Test
        </Button>
      </Stack>

      <Paper sx={{ p: 2.5 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell />
              <TableCell>Code</TableCell>
              <TableCell>Display Name</TableCell>
              <TableCell>Section</TableCell>
              <TableCell>Status</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {options.map((t) => (
              <Fragment key={t.id}>
                <TableRow sx={{ opacity: t.isActive ? 1 : 0.6 }}>
                  <TableCell sx={{ width: 40 }}>
                    <IconButton size="small" onClick={() => setExpandedId(expandedId === t.id ? null : t.id)} title="Details & Workflow Steps">
                      {expandedId === t.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{t.code}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                      <span>{t.displayName}</span>
                      {t.workflowType === "HplcAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="HPLC"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "ElementalAssay" && (
                        <Chip
                          size="small"
                          color="secondary"
                          variant="outlined"
                          label="ICP-OES"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
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
                            sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                      {t.equationType === "CalibrationCurve" && (
                        <Tooltip
                          title={`Calibration Curve (${t.methodAbbreviation ?? "Required"}): min corr ${t.calMinCorrelation ?? "—"}`}
                        >
                          <Chip
                            size="small"
                            color="info"
                            variant="outlined"
                            label={`CAL: ${t.methodAbbreviation ?? "Required"}`}
                            sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>{t.section?.name ?? "—"}</TableCell>
                  <TableCell><StatusBadge status={t.isActive ? "Active" : "Frozen"} /></TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startEdit(t)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" onClick={() => toggleFreeze(t)} title={t.isActive ? "Freeze" : "Unfreeze"}>
                      {t.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={6}>
                    <Collapse in={expandedId === t.id} unmountOnExit>
                      <WorkflowStepsSection test={t} workflowTypes={workflowTypes} onWorkflowTypeChanged={reload} />
                    </Collapse>
                  </TableCell>
                </TableRow>
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {/* Test Master Create / Edit Dialog */}
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
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}

        <Stack spacing={2} sx={{ pt: 1 }}>
          <Stack direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
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

          <Stack direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required>
              <InputLabel id="test-section-select-label">Section</InputLabel>
              <Select<number | "">
                labelId="test-section-select-label"
                label="Section"
                value={sectionId}
                onChange={(e) => setSectionId(e.target.value === "" ? "" : Number(e.target.value))}
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
                  if (next === "HplcAssay") {
                    if (equationType === "None") setEquationType("HplcAssay");
                  } else if (next === "ElementalAssay") {
                    setEquationType("CalibrationCurve");
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
                  if (next === "CalibrationCurve") {
                    setRequiresSystemSuitability(false);
                  }
                }}
              >
                {EQUATION_TYPES.filter((eq) => {
                  if (workflowType === "HplcAssay") {
                    return eq !== "CalibrationCurve";
                  }
                  if (workflowType === "ElementalAssay") {
                    return eq === "CalibrationCurve" || eq === "None";
                  }
                  return eq !== "HplcAssay" && eq !== "HplcUniformityOfDosageUnits" && eq !== "HplcDissolutionMultiPoint";
                }).map((eq) => (
                  <MenuItem key={eq} value={eq}>
                    {EQUATION_TYPE_LABELS[eq] ?? eq}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {equationType === "CalibrationCurve" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={true}
                    disabled
                  />
                }
                label="Requires calibration run (locked)"
              />

              <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
                <TextField
                  size="small"
                  label="Method Abbreviation"
                  placeholder="e.g. MIN-ICP"
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
                  sx={{ mb: 2 }}
                />

                <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Calibration Curve Acceptance Criteria
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
                  Define the correlation threshold, calibration standard counts, and QC check limits.
                </Typography>

                <Stack spacing={2}>
                  <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                    <TextField
                      size="small"
                      type="number"
                      label="Min Correlation Coefficient"
                      placeholder="e.g. 0.9995"
                      value={calMinCorrelation}
                      onChange={(e) => setCalMinCorrelation(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, max: 1, step: "any" } }}
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                    <FormControl size="small" sx={{ flex: "1 1 140px", minWidth: 120 }}>
                      <InputLabel id="cal-corr-type-label">Correlation Type</InputLabel>
                      <Select
                        labelId="cal-corr-type-label"
                        label="Correlation Type"
                        value={calCorrelationType}
                        onChange={(e) => setCalCorrelationType(e.target.value as "R" | "RSquared")}
                      >
                        <MenuItem value="R">r (Correlation coefficient)</MenuItem>
                        <MenuItem value="RSquared">r² (Coefficient of determination)</MenuItem>
                      </Select>
                    </FormControl>
                    <TextField
                      size="small"
                      type="number"
                      label="Min Standards"
                      placeholder="e.g. 5"
                      value={calMinStandards}
                      onChange={(e) => setCalMinStandards(e.target.value)}
                      slotProps={{ htmlInput: { min: 1, step: 1 } }}
                      sx={{ flex: "1 1 140px", minWidth: 120 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="Max Run Age (Hours)"
                      placeholder="24"
                      value={calMaxRunAgeHours}
                      onChange={(e) => setCalMaxRunAgeHours(e.target.value)}
                      slotProps={{ htmlInput: { min: 1, step: 1 } }}
                      sx={{ flex: "1 1 140px", minWidth: 120 }}
                    />
                  </Stack>

                  <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                    <TextField
                      size="small"
                      type="number"
                      label="ICV / CCV Min Recovery (%)"
                      placeholder="e.g. 90.0"
                      value={calCheckRecoveryLowPercent}
                      onChange={(e) => setCalCheckRecoveryLowPercent(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 200px", minWidth: 160 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="ICV / CCV Max Recovery (%)"
                      placeholder="e.g. 110.0"
                      value={calCheckRecoveryHighPercent}
                      onChange={(e) => setCalCheckRecoveryHighPercent(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 200px", minWidth: 160 }}
                    />
                  </Stack>

                  <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                    <TextField
                      size="small"
                      type="number"
                      label="Blank Max Conc. (mg/L)"
                      placeholder="LOQ default"
                      value={calBlankMax}
                      onChange={(e) => setCalBlankMax(e.target.value)}
                      helperText="Leave empty to use each analyte's LOQ"
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 200px", minWidth: 160 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="IS Min Recovery (%)"
                      placeholder="e.g. 80.0"
                      value={calIsRecoveryLowPercent}
                      onChange={(e) => setCalIsRecoveryLowPercent(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 140px", minWidth: 120 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="IS Max Recovery (%)"
                      placeholder="e.g. 120.0"
                      value={calIsRecoveryHighPercent}
                      onChange={(e) => setCalIsRecoveryHighPercent(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 140px", minWidth: 120 }}
                    />
                  </Stack>

                  <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                    <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1, color: "text.secondary" }}>
                      REQUIRED CALIBRATION CHECKS
                    </Typography>
                    <Stack direction="row" spacing={2} sx={{ flexWrap: "wrap" }}>
                      <FormControlLabel
                        control={
                          <Checkbox
                            checked={calRequireBlank}
                            onChange={(e) => setCalRequireBlank(e.target.checked)}
                          />
                        }
                        label="Requires Blank"
                      />
                      <FormControlLabel
                        control={
                          <Checkbox
                            checked={calRequireIcv}
                            onChange={(e) => setCalRequireIcv(e.target.checked)}
                          />
                        }
                        label="Requires ICV"
                      />
                      <FormControlLabel
                        control={
                          <Checkbox
                            checked={calRequireCcv}
                            onChange={(e) => setCalRequireCcv(e.target.checked)}
                          />
                        }
                        label="Requires CCV"
                      />
                      <FormControlLabel
                        control={
                          <Checkbox
                            checked={calRequireInternalStandard}
                            onChange={(e) => setCalRequireInternalStandard(e.target.checked)}
                          />
                        }
                        label="Requires Internal Standard"
                      />
                    </Stack>
                    {!calRequireIcv && !calRequireCcv && (
                      <Alert severity="warning" sx={{ mt: 1 }}>
                        A valid calibration run requires at least an ICV or a CCV check.
                      </Alert>
                    )}
                  </Box>

                  <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                      Reported Concentration Basis
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600, color: "text.primary", mt: 0.5 }}>
                      ppm in the sample (Syngistix applies weight, volume and dilution)
                    </Typography>
                  </Box>
                </Stack>
              </Box>
            </Box>
          )}

          {workflowType === "HplcAssay" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={requiresSystemSuitability}
                    onChange={(e) => setRequiresSystemSuitability(e.target.checked)}
                  />
                }
                label="Requires system suitability"
              />

              {requiresSystemSuitability && (
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
                    sx={{ mb: 2 }}
                  />

                  <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                    System Suitability Acceptance Criteria
                  </Typography>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
                    At least one criterion is required when system suitability is enabled.
                  </Typography>

                  <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                    <TextField
                      size="small"
                      type="number"
                      label="Max RSD (%)"
                      placeholder="e.g. 2.0"
                      value={sstMaxRsdPercent}
                      onChange={(e) => setSstMaxRsdPercent(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="Min Resolution"
                      placeholder="e.g. 1.5"
                      value={sstMinResolution}
                      onChange={(e) => setSstMinResolution(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="Max Tailing Factor"
                      placeholder="e.g. 2.0"
                      value={sstMaxTailingFactor}
                      onChange={(e) => setSstMaxTailingFactor(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label="Min Theoretical Plates"
                      placeholder="e.g. 2000"
                      value={sstMinTheoreticalPlates}
                      onChange={(e) => setSstMinTheoreticalPlates(e.target.value)}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ flex: "1 1 180px", minWidth: 140 }}
                    />
                  </Stack>
                </Box>
              )}
            </Box>
          )}
        </Stack>
      </FloatingDialog>
    </>
  );
}
