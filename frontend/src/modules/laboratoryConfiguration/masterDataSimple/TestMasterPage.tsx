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
  TestAnalyteDto,
  TestDefinitionStageReplicateDto,
  ProductionStageRole
} from "../../../services/masterDataOptions";
import { tableHeadSx } from "../../../theme";

// Microbiology and the Finished Product (chemistry) lab each have their own
// Test Master page: same component, filtered to the lab's section and
// workflow types. FP tests have no workflow steps or media.
export type TestMasterLab = "micro" | "fp";
const FP_SECTION_CODE = "FP";
const WORKFLOW_TYPES_BY_LAB: Record<TestMasterLab, string[]> = {
  micro: ["CountTest", "Observation"],
  fp: ["StandardComparison", "ElementalAssay", "Measurement", "Gravimetric", "Qualitative", "Dissolution", "Disintegration", "WeightVariation"]
};
const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  CountTest: "Count Test",
  Observation: "Observation",
  StandardComparison: "Standard-Comparison Assay",
  ElementalAssay: "Elemental Assay (ICP-OES / AAS)",
  Measurement: "Measurement",
  Gravimetric: "Gravimetric",
  Qualitative: "Qualitative",
  Dissolution: "Dissolution",
  Disintegration: "Disintegration",
  WeightVariation: "Weight Variation"
};

const EQUATION_TYPES = [
  "None",
  "StandardComparison",
  "SystemSuitability",
  "CalibrationCurve",
  "Measurement",
  "GravimetricLoss",
  "GravimetricResidue",
  "Qualitative",
  "Dissolution",
  "Disintegration",
  "WeightVariation"
];
const EQUATION_TYPE_LABELS: Record<string, string> = {
  None: "None",
  StandardComparison: "Standard-Comparison Assay",
  SystemSuitability: "System Suitability",
  CalibrationCurve: "Calibration Curve",
  Measurement: "Measurement (pH, density…)",
  GravimetricLoss: "Loss on drying / Gravimetric loss",
  GravimetricResidue: "Ash / Gravimetric residue",
  Qualitative: "Qualitative (appearance, ID)",
  Dissolution: "Dissolution (HPLC finish, staged S1-S3)",
  Disintegration: "Disintegration (time per unit, staged)",
  WeightVariation: "Weight Variation (USP <2091>, staged)"
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
  // StandardComparison reuses this same TestAnalyte CRUD (Element, WavelengthNm,
  // DisplayOrder) but drops the ICP-only Plasma View/LOQ and adds its own
  // per-analyte system suitability criteria instead - see backend
  // MasterDataController.CreateTestAnalyte/UpdateTestAnalyte.
  const isHplcMulti = test.workflowType === "StandardComparison" || test.equationType === "StandardComparison";
  // Titration standard-comparison runs use only the RSD criterion (SC-4/SC-5a) -
  // resolution, tailing factor and theoretical plates are HPLC-only concepts.
  const isTitration = isHplcMulti && test.responseMode === "TitrationVolume";
  // AAS calibration-curve tests have no plasma view (that's an ICP-OES/torch
  // concept) - hide the field/column and always send null for these tests.
  const isAas = !isHplcMulti && test.calInstrumentType === "Aas";
  const hidePlasmaView = isHplcMulti || isAas;
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
  const [sstMaxRsdPercent, setSstMaxRsdPercent] = useState("");
  const [sstMinResolution, setSstMinResolution] = useState("");
  const [sstMaxTailingFactor, setSstMaxTailingFactor] = useState("");
  const [sstMinTheoreticalPlates, setSstMinTheoreticalPlates] = useState("");
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
    setSstMaxRsdPercent("");
    setSstMinResolution("");
    setSstMaxTailingFactor("");
    setSstMinTheoreticalPlates("");
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (a: TestAnalyteDto) => {
    setEditingAnalyte(a);
    setElement(a.element);
    setWavelengthNm(String(a.wavelengthNm));
    setView(a.view ?? "Axial");
    setLoqMgPerL(a.loqMgPerL != null ? String(a.loqMgPerL) : "");
    setDisplayOrder(String(a.displayOrder));
    setSstMaxRsdPercent(a.sstMaxRsdPercent != null ? String(a.sstMaxRsdPercent) : "");
    setSstMinResolution(a.sstMinResolution != null ? String(a.sstMinResolution) : "");
    setSstMaxTailingFactor(a.sstMaxTailingFactor != null ? String(a.sstMaxTailingFactor) : "");
    setSstMinTheoreticalPlates(a.sstMinTheoreticalPlates != null ? String(a.sstMinTheoreticalPlates) : "");
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSaveAnalyte = async () => {
    const trimmedEl = element.trim();
    if (!trimmedEl) {
      setDialogError(isHplcMulti ? "Vitamin / analyte name is required." : "Element symbol is required.");
      return;
    }
    if (trimmedEl.length > 20) {
      setDialogError(isHplcMulti ? "Vitamin / analyte name cannot exceed 20 characters." : "Element symbol cannot exceed 20 characters.");
      return;
    }
    const wave = Number(wavelengthNm);
    if (!wave || wave <= 0) {
      setDialogError("Wavelength must be greater than 0.");
      return;
    }
    if (!isHplcMulti) {
      const loq = Number(loqMgPerL);
      if (!loq || loq <= 0) {
        setDialogError("LOQ must be greater than 0.");
        return;
      }
    }
    const sstFields: [string, string][] = isTitration
      ? [["Max RSD", sstMaxRsdPercent]]
      : [
          ["Max RSD", sstMaxRsdPercent],
          ["Min Resolution", sstMinResolution],
          ["Max Tailing Factor", sstMaxTailingFactor],
          ["Min Theoretical Plates", sstMinTheoreticalPlates]
        ];
    if (isHplcMulti) {
      for (const [label, v] of sstFields) {
        if (v.trim() !== "" && Number(v) <= 0) {
          setDialogError(`${label} must be greater than 0 when provided.`);
          return;
        }
      }
    }

    setSaving(true);
    setDialogError(null);
    try {
      const sstPayload = isHplcMulti
        ? {
            sstMaxRsdPercent: sstMaxRsdPercent.trim() !== "" ? Number(sstMaxRsdPercent) : null,
            sstMinResolution: isTitration ? null : sstMinResolution.trim() !== "" ? Number(sstMinResolution) : null,
            sstMaxTailingFactor: isTitration ? null : sstMaxTailingFactor.trim() !== "" ? Number(sstMaxTailingFactor) : null,
            sstMinTheoreticalPlates: isTitration ? null : sstMinTheoreticalPlates.trim() !== "" ? Number(sstMinTheoreticalPlates) : null
          }
        : {};
      if (editingAnalyte) {
        await masterDataOptions.updateTestAnalyte(test.id, editingAnalyte.id, {
          element: trimmedEl,
          wavelengthNm: wave,
          view: hidePlasmaView ? null : view,
          loqMgPerL: isHplcMulti ? null : Number(loqMgPerL),
          displayOrder: displayOrder ? Number(displayOrder) : editingAnalyte.displayOrder,
          ...sstPayload
        });
      } else {
        await masterDataOptions.createTestAnalyte(test.id, {
          element: trimmedEl,
          wavelengthNm: wave,
          view: hidePlasmaView ? null : view,
          loqMgPerL: isHplcMulti ? null : Number(loqMgPerL),
          displayOrder: displayOrder ? Number(displayOrder) : 0,
          ...sstPayload
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
          {isHplcMulti ? "Vitamins / Analytes" : "Test Analytes"} ({analytes.filter((a) => a.isActive).length} active)
        </Typography>
        <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={openAdd}>
          {isHplcMulti ? "Add Vitamin / Analyte" : "Add Analyte"}
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
        <TableHead>
          <TableRow sx={tableHeadSx}>
            <TableCell>Order</TableCell>
            <TableCell>{isHplcMulti ? "Vitamin / Analyte" : "Element"}</TableCell>
            <TableCell>{isHplcMulti ? "Detection Wavelength (nm)" : "Wavelength (nm)"}</TableCell>
            {!hidePlasmaView && <TableCell>Plasma View</TableCell>}
            {!isHplcMulti && <TableCell>LOQ (mg/L)</TableCell>}
            {isHplcMulti && <TableCell>SST Criteria</TableCell>}
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
              {!hidePlasmaView && <TableCell><Chip size="small" label={a.view} variant="outlined" /></TableCell>}
              {!isHplcMulti && <TableCell>{a.loqMgPerL}</TableCell>}
              {isHplcMulti && (
                <TableCell sx={{ fontSize: 12 }}>
                  {[
                    a.sstMaxRsdPercent != null ? `Max RSD ${a.sstMaxRsdPercent}%` : null,
                    !isTitration && a.sstMinResolution != null ? `Min Res ${a.sstMinResolution}` : null,
                    !isTitration && a.sstMaxTailingFactor != null ? `Max Tailing ${a.sstMaxTailingFactor}` : null,
                    !isTitration && a.sstMinTheoreticalPlates != null ? `Min Plates ${a.sstMinTheoreticalPlates}` : null
                  ].filter(Boolean).join(", ") || "None specified"}
                </TableCell>
              )}
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
              <TableCell colSpan={hidePlasmaView ? 6 : 7} align="center" sx={{ py: 2, color: "text.secondary" }}>
                {isHplcMulti
                  ? "No vitamins/analytes configured yet. Click \"Add Vitamin / Analyte\" to configure detection wavelengths and suitability criteria for this test."
                  : "No analytes configured yet. Click \"Add Analyte\" to configure wavelengths and LOQs for this test method."}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      <FloatingDialog
        open={dialogOpen}
        title={editingAnalyte ? `Edit Analyte: ${editingAnalyte.element}` : (isHplcMulti ? "Add Vitamin / Analyte" : "Add Test Analyte")}
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
            label={isHplcMulti ? "Vitamin / analyte" : "Element Symbol"}
            placeholder={isHplcMulti ? "e.g. Vitamin B1 (thiamine)" : "e.g. Zn, Pb, Ca"}
            value={element}
            onChange={(e) => setElement(e.target.value)}
            required
            fullWidth
            slotProps={{ htmlInput: { maxLength: 20 } }}
          />
          <TextField
            size="small"
            type="number"
            label={isHplcMulti ? "Detection Wavelength (nm)" : "Wavelength (nm)"}
            placeholder="e.g. 213.856"
            value={wavelengthNm}
            onChange={(e) => setWavelengthNm(e.target.value)}
            required
            fullWidth
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
          />
          {!hidePlasmaView && (
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
          )}
          {!isHplcMulti && (
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
          )}
          <TextField
            size="small"
            type="number"
            label="Display Order"
            value={displayOrder}
            onChange={(e) => setDisplayOrder(e.target.value)}
            fullWidth
          />
          {isHplcMulti && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                System Suitability Criteria (this vitamin's peak)
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: -1 }}>
                Optional - blank means not checked.
              </Typography>
              <TextField
                size="small"
                type="number"
                label="Max RSD (%)"
                placeholder="e.g. 2.0"
                value={sstMaxRsdPercent}
                onChange={(e) => setSstMaxRsdPercent(e.target.value)}
                fullWidth
                slotProps={{ htmlInput: { min: 0, step: "any" } }}
              />
              {isTitration ? (
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  This test uses titration - resolution, tailing factor and theoretical plates do not apply.
                </Typography>
              ) : (
                <>
                  <TextField
                    size="small"
                    type="number"
                    label="Min Resolution"
                    placeholder="e.g. 1.5"
                    value={sstMinResolution}
                    onChange={(e) => setSstMinResolution(e.target.value)}
                    fullWidth
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Max Tailing Factor"
                    placeholder="e.g. 2.0"
                    value={sstMaxTailingFactor}
                    onChange={(e) => setSstMaxTailingFactor(e.target.value)}
                    fullWidth
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Min Theoretical Plates"
                    placeholder="e.g. 2000"
                    value={sstMinTheoreticalPlates}
                    onChange={(e) => setSstMinTheoreticalPlates(e.target.value)}
                    fullWidth
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                  />
                </>
              )}
            </>
          )}
        </Stack>
      </FloatingDialog>
    </Box>
  );
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
  const [standardReplicates, setStandardReplicates] = useState("");
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
    setStandardReplicates("");
    setSampleReplicates("");
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (r: TestDefinitionStageReplicateDto) => {
    setEditingReplicate(r);
    setRole(r.role);
    setStandardReplicates(String(r.standardReplicates));
    setSampleReplicates(String(r.sampleReplicates));
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSave = async () => {
    if (!editingReplicate && !role) {
      setDialogError("Production stage role is required.");
      return;
    }

    const stdStr = standardReplicates.trim();
    if (!stdStr) {
      setDialogError("Standard replicates is required.");
      return;
    }
    const std = Number(stdStr);
    if (!Number.isInteger(std) || std < 1) {
      setDialogError("Standard replicates must be an integer greater than or equal to 1.");
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
          standardReplicates: std,
          sampleReplicates: smp
        });
      } else {
        await masterDataOptions.createTestDefinitionStageReplicate(testDefinitionId, {
          role: role as ProductionStageRole,
          standardReplicates: std,
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

      <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
        <TableHead>
          <TableRow sx={tableHeadSx}>
            <TableCell>Stage Role</TableCell>
            <TableCell>Standard Replicates</TableCell>
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
              <TableCell>{r.standardReplicates}</TableCell>
              <TableCell>{r.sampleReplicates}</TableCell>
              <TableCell align="right">
                <IconButton size="small" onClick={() => openEdit(r)} title="Edit Replicates">
                  <EditIcon fontSize="small" />
                </IconButton>
                <IconButton
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
              <TableCell colSpan={4} align="center" sx={{ py: 2, color: "text.secondary" }}>
                No stage replicates configured yet. Click "Add Stage Replicate" to configure replicate counts.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
        Production stages without a configured row are not configured for this test (not zero). Both counts must be integers ≥ 1.
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
            label="Standard Replicates *"
            placeholder="e.g. 6"
            value={standardReplicates}
            onChange={(e) => setStandardReplicates(e.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText="Integer ≥ 1"
            required
            fullWidth
          />
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
          {["StandardComparison", "ElementalAssay", "Measurement", "Gravimetric", "Qualitative"].includes(test.workflowType) ? "Workflow Type" : "Workflow Steps"}
        </Typography>
        <Select size="small" value={test.workflowType} onChange={(e) => changeWorkflowType(e.target.value)}>
          {workflowTypes.map((w) => <MenuItem key={w} value={w}>{WORKFLOW_TYPE_LABELS[w] ?? w}</MenuItem>)}
        </Select>
      </Stack>
      {test.workflowType === "StandardComparison" && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>Standard-Comparison Assay Configuration</Typography>
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{EQUATION_TYPE_LABELS[test.equationType ?? "StandardComparison"] ?? test.equationType}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Response</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {test.responseMode === "TitrationVolume" ? "Titration volume (titrator, no column)" : "Peak area (HPLC)"}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>System Suitability</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                Required ({test.methodAbbreviation ?? "No abbr"}) - one row per analyte
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max Preparation RSD</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>{test.hplcMaxPreparationRsdPercent != null ? `${test.hplcMaxPreparationRsdPercent}%` : "Not checked"}</Typography>
            </Box>
          </Stack>
        </Box>
      )}
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      {test.workflowType === "ElementalAssay" || test.equationType === "CalibrationCurve" ? (
        <>
          <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "secondary.main" }}>{test.calInstrumentType === "Aas" ? "AAS" : "ICP-OES"} Calibration Curve Configuration</Typography>
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
              {!test.calStandardLevelsMgPerL && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Min Standards</Typography>
                  <Typography variant="body2">{test.calMinStandards ?? "—"}</Typography>
                </Box>
              )}
              {(test.calRequireIcv || test.calRequireCcv) && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    Check Recovery ({test.calRequireIcv && test.calRequireCcv ? "ICV/CCV" : test.calRequireIcv ? "ICV" : "CCV"})
                  </Typography>
                  <Typography variant="body2">
                    {test.calCheckRecoveryLowPercent != null && test.calCheckRecoveryHighPercent != null
                      ? `${test.calCheckRecoveryLowPercent}% – ${test.calCheckRecoveryHighPercent}%`
                      : "—"}
                  </Typography>
                </Box>
              )}
              {test.calRequireBlank && (
                <Box>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Max Blank</Typography>
                  <Typography variant="body2">{test.calBlankMax != null ? `${test.calBlankMax} mg/L` : "Analyte LOQ"}</Typography>
                </Box>
              )}
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
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Instrument</Typography>
                <Typography variant="body2">{test.calInstrumentType === "Aas" ? "AAS" : "ICP-OES"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Standard Levels (mg/L)</Typography>
                <Typography variant="body2">{test.calStandardLevelsMgPerL || "—"}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Reported Basis</Typography>
                <Typography variant="body2">ppm in the sample (SamplePpm)</Typography>
              </Box>
            </Stack>
          </Box>
          <TestAnalytesSection test={test} />
        </>
      ) : test.workflowType === "StandardComparison" ? (
        <>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
            Standard-Comparison Assay tests have no workflow steps: one signed result entry covers every analyte, against a
            System Suitability run with a row per analyte.
          </Typography>
          <TestAnalytesSection test={test} />
        </>
      ) : test.workflowType === "Measurement" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Measurement Configuration
          </Typography>
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
      ) : test.workflowType === "Qualitative" ? (
        <Box sx={{ p: 2, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1, color: "primary.main" }}>
            Qualitative Configuration
          </Typography>
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
          <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap", alignItems: "center", mb: 1 }}>
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
  const defaultWorkflowType: string = isFp ? "StandardComparison" : "Observation";
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
  const [equationType, setEquationType] = useState<string>(isFp ? "StandardComparison" : "None");
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
  const [calInstrumentType, setCalInstrumentType] = useState<"IcpOes" | "Aas">("IcpOes");
  const [calStandardLevelsMgPerL, setCalStandardLevelsMgPerL] = useState<string>("");

  const [replicateCount, setReplicateCount] = useState<string>("1");
  const [evaluationBasis, setEvaluationBasis] = useState<"Mean" | "EachValue" | "Min" | "Max">("Mean");
  const [conditionFields, setConditionFields] = useState<string>("");
  const [usesTare, setUsesTare] = useState<boolean>(false);

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

  const [hplcMaxPreparationRsdPercent, setHplcMaxPreparationRsdPercent] = useState<string>("");
  const [responseMode, setResponseMode] = useState<"PeakArea" | "TitrationVolume">("PeakArea");

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
    setEquationType(isFp ? (defaultWorkflowType === "ElementalAssay" ? "CalibrationCurve" : "StandardComparison") : "None");
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
    setCalInstrumentType("IcpOes");
    setCalStandardLevelsMgPerL("");
    setReplicateCount("1");
    setEvaluationBasis("Mean");
    setConditionFields("");
    setUsesTare(false);
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
    setHplcMaxPreparationRsdPercent("");
    setResponseMode("PeakArea");
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
    setEquationType(t.equationType || (t.workflowType === "ElementalAssay" ? "CalibrationCurve" : t.workflowType === "StandardComparison" ? "StandardComparison" : t.workflowType === "Measurement" ? "Measurement" : t.workflowType === "Gravimetric" ? "GravimetricLoss" : t.workflowType === "Qualitative" ? "Qualitative" : t.workflowType === "Dissolution" ? "Dissolution" : t.workflowType === "Disintegration" ? "Disintegration" : t.workflowType === "WeightVariation" ? "WeightVariation" : "None"));
    setRequiresSystemSuitability((t.workflowType === "Dissolution" || t.workflowType === "StandardComparison") ? true : (t.workflowType === "Disintegration" || t.workflowType === "WeightVariation") ? false : !!t.requiresSystemSuitability);
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
    setCalInstrumentType(t.calInstrumentType === "Aas" ? "Aas" : "IcpOes");
    setCalStandardLevelsMgPerL(t.calStandardLevelsMgPerL ?? "");
    setReplicateCount(t.replicateCount != null ? String(t.replicateCount) : "1");
    setEvaluationBasis(t.evaluationBasis || "Mean");
    setConditionFields(t.conditionFields || "");
    setUsesTare(!!t.usesTare);
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
    setHplcMaxPreparationRsdPercent(t.hplcMaxPreparationRsdPercent != null ? String(t.hplcMaxPreparationRsdPercent) : "");
    setResponseMode(t.responseMode === "TitrationVolume" ? "TitrationVolume" : "PeakArea");
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
    const isHplcMulti = workflowType === "StandardComparison";
    const isDissolution = workflowType === "Dissolution";
    const isCalCurve = isFp && equationType === "CalibrationCurve";

    if (isDissolution || isHplcMulti) {
      const trimmedAbbr = methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        setDialogError("Method abbreviation is required when system suitability is enabled.");
        return;
      }
      if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        setDialogError("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        return;
      }
      // StandardComparison's acceptance criteria live per analyte (Test
      // Analytes below), not on the test itself - no "at least one" gate here.
      if (!isHplcMulti) {
        const hasRsd = sstMaxRsdPercent.trim() !== "";
        const hasRes = sstMinResolution.trim() !== "";
        const hasTailing = sstMaxTailingFactor.trim() !== "";
        const hasPlates = sstMinTheoreticalPlates.trim() !== "";

        if (!hasRsd && !hasRes && !hasTailing && !hasPlates) {
          setDialogError("At least one system suitability criterion is required when system suitability is enabled.");
          return;
        }
      }
    }

    if (isHplcMulti) {
      if (hplcMaxPreparationRsdPercent.trim() !== "" && Number(hplcMaxPreparationRsdPercent) <= 0) {
        setDialogError("HPLC max preparation RSD must be greater than 0 when provided.");
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
      if (calStandardLevelsMgPerL.trim() !== "") {
        const levelParts = calStandardLevelsMgPerL.split(",").map((p) => p.trim()).filter((p) => p !== "");
        const levelNums = levelParts.map(Number);
        if (levelParts.length < 2 || levelNums.some((n) => isNaN(n) || n <= 0)) {
          setDialogError("Standard levels must be at least two comma-separated positive numbers (mg/L).");
          return;
        }
        if (new Set(levelNums).size !== levelNums.length) {
          setDialogError("Standard levels must be distinct.");
          return;
        }
      }
    }

    const isMeasurement = workflowType === "Measurement";
    const isGravimetric = workflowType === "Gravimetric";
    const isQualitative = workflowType === "Qualitative";

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
      const resolvedEquationType = isCalCurve
        ? "CalibrationCurve"
        : isHplcMulti
        ? "StandardComparison"
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
        : "None";

      if (editingId) {
        const payload: UpdateTestDefinitionPayload = {
          code: trimmedCode,
          displayName: trimmedDisplayName,
          sectionId: chosenSectionId,
          workflowType,
          equationType: resolvedEquationType,
          requiresSystemSuitability: (isDissolution || isHplcMulti) ? true : false,
          methodAbbreviation: isDissolution || isCalCurve || isHplcMulti ? methodAbbreviation.trim().toUpperCase() : null,
          sstMaxRsdPercent: isDissolution && sstMaxRsdPercent.trim() !== "" ? Number(sstMaxRsdPercent) : null,
          sstMinResolution: isDissolution && sstMinResolution.trim() !== "" ? Number(sstMinResolution) : null,
          sstMaxTailingFactor: isDissolution && sstMaxTailingFactor.trim() !== "" ? Number(sstMaxTailingFactor) : null,
          sstMinTheoreticalPlates: isDissolution && sstMinTheoreticalPlates.trim() !== "" ? Number(sstMinTheoreticalPlates) : null,
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
          calMaxRunAgeHours: isCalCurve ? (calMaxRunAgeHours.trim() !== "" ? Number(calMaxRunAgeHours) : 24) : null,
          calInstrumentType: isCalCurve ? calInstrumentType : null,
          calStandardLevelsMgPerL: isCalCurve
            ? (calStandardLevelsMgPerL.trim() !== ""
                ? calStandardLevelsMgPerL.trim()
                : (editingTest?.calStandardLevelsMgPerL ? "" : null))
            : null,
          replicateCount: (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
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
          hplcMaxPreparationRsdPercent: isHplcMulti && hplcMaxPreparationRsdPercent.trim() !== "" ? Number(hplcMaxPreparationRsdPercent) : null,
          responseMode: isHplcMulti ? responseMode : "PeakArea"
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
          requiresSystemSuitability: (isDissolution || isHplcMulti) ? true : false,
          methodAbbreviation: isDissolution || isCalCurve || isHplcMulti ? methodAbbreviation.trim().toUpperCase() : null,
          sstMaxRsdPercent: isDissolution && sstMaxRsdPercent.trim() !== "" ? Number(sstMaxRsdPercent) : null,
          sstMinResolution: isDissolution && sstMinResolution.trim() !== "" ? Number(sstMinResolution) : null,
          sstMaxTailingFactor: isDissolution && sstMaxTailingFactor.trim() !== "" ? Number(sstMaxTailingFactor) : null,
          sstMinTheoreticalPlates: isDissolution && sstMinTheoreticalPlates.trim() !== "" ? Number(sstMinTheoreticalPlates) : null,
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
          calMaxRunAgeHours: isCalCurve ? (calMaxRunAgeHours.trim() !== "" ? Number(calMaxRunAgeHours) : 24) : null,
          calInstrumentType: isCalCurve ? calInstrumentType : null,
          calStandardLevelsMgPerL: isCalCurve && calStandardLevelsMgPerL.trim() !== "" ? calStandardLevelsMgPerL.trim() : null,
          replicateCount: (isMeasurement || isGravimetric) ? Number(replicateCount) : null,
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
          hplcMaxPreparationRsdPercent: isHplcMulti && hplcMaxPreparationRsdPercent.trim() !== "" ? Number(hplcMaxPreparationRsdPercent) : null,
          responseMode: isHplcMulti ? responseMode : "PeakArea"
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
                      {t.workflowType === "StandardComparison" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="Standard-Comparison"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "ElementalAssay" && (
                        <Chip
                          size="small"
                          color="secondary"
                          variant="outlined"
                          label={t.calInstrumentType === "Aas" ? "AAS" : "ICP-OES"}
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Dissolution" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Dissolution"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Disintegration" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Disintegration"
                          sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "WeightVariation" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Weight Variation"
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
                  if (next === "StandardComparison") {
                    setEquationType("StandardComparison");
                    setRequiresSystemSuitability(true);
                  } else if (next === "ElementalAssay") {
                    setEquationType("CalibrationCurve");
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
                  if (next === "Disintegration") {
                    setWorkflowType("Disintegration");
                    setRequiresSystemSuitability(false);
                  } else if (next === "WeightVariation") {
                    setWorkflowType("WeightVariation");
                    setRequiresSystemSuitability(false);
                  } else if (next === "Dissolution") {
                    setWorkflowType("Dissolution");
                    setRequiresSystemSuitability(true);
                  } else if (next === "StandardComparison") {
                    setWorkflowType("StandardComparison");
                    setRequiresSystemSuitability(true);
                  } else if (next === "CalibrationCurve") {
                    setRequiresSystemSuitability(false);
                  }
                }}
              >
                {EQUATION_TYPES.filter((eq) => {
                  if (workflowType === "Disintegration") {
                    return eq === "Disintegration";
                  }
                  if (workflowType === "WeightVariation") {
                    return eq === "WeightVariation";
                  }
                  if (workflowType === "Dissolution") {
                    return eq === "Dissolution";
                  }
                  if (workflowType === "StandardComparison") {
                    return eq === "StandardComparison";
                  }
                  if (workflowType === "ElementalAssay") {
                    return eq === "CalibrationCurve" || eq === "None";
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
                  return eq !== "StandardComparison" && eq !== "Measurement" && eq !== "GravimetricLoss" && eq !== "GravimetricResidue" && eq !== "Qualitative" && eq !== "Dissolution" && eq !== "Disintegration" && eq !== "WeightVariation";
                }).map((eq) => (
                  <MenuItem key={eq} value={eq}>
                    {EQUATION_TYPE_LABELS[eq] ?? eq}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
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

          {workflowType === "Qualitative" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary" }}>
                Qualitative test: records compliance observation against specifications directly (no replicates or steps).
              </Typography>
            </Box>
          )}

          {workflowType === "StandardComparison" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Standard-Comparison Assay
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <FormControl size="small" sx={{ flex: 1 }}>
                  <InputLabel id="response-mode-label">Response</InputLabel>
                  <Select
                    labelId="response-mode-label"
                    label="Response"
                    value={responseMode}
                    onChange={(e) => setResponseMode(e.target.value as "PeakArea" | "TitrationVolume")}
                  >
                    <MenuItem value="PeakArea">Peak area (HPLC)</MenuItem>
                    <MenuItem value="TitrationVolume">Titration volume</MenuItem>
                  </Select>
                </FormControl>
                <TextField
                  size="small"
                  type="number"
                  label="Max Preparation RSD (%)"
                  value={hplcMaxPreparationRsdPercent}
                  onChange={(e) => setHplcMaxPreparationRsdPercent(e.target.value)}
                  slotProps={{ htmlInput: { min: 0, step: "any" } }}
                  helperText="Optional - blank = not checked"
                  sx={{ flex: 1 }}
                />
              </Stack>
              {responseMode === "TitrationVolume" && (
                <Typography variant="caption" sx={{ color: "warning.main", display: "block", mt: 1 }}>
                  Titration runs use a titrator (no chromatography column) and the RSD criterion only - resolution,
                  tailing factor and theoretical plates do not apply.
                </Typography>
              )}
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1.5 }}>
                Analytes, their detection wavelengths and their own system suitability criteria are configured
                after saving, in this test's expanded Test Analytes section. The response cannot be changed once
                suitability runs exist for this test.
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
                    <FormControl size="small" sx={{ flex: "1 1 160px", minWidth: 140 }}>
                      <InputLabel id="cal-instrument-type-label">Instrument</InputLabel>
                      <Select
                        labelId="cal-instrument-type-label"
                        label="Instrument"
                        value={calInstrumentType}
                        onChange={(e) => setCalInstrumentType(e.target.value as "IcpOes" | "Aas")}
                      >
                        <MenuItem value="IcpOes">ICP-OES</MenuItem>
                        <MenuItem value="Aas">AAS</MenuItem>
                      </Select>
                    </FormControl>
                    <TextField
                      size="small"
                      label="Standard Levels (mg/L)"
                      placeholder="e.g. 1, 5"
                      value={calStandardLevelsMgPerL}
                      onChange={(e) => setCalStandardLevelsMgPerL(e.target.value)}
                      helperText="Comma-separated, e.g. 1, 5. Leave empty to only check the minimum number of standards."
                      sx={{ flex: "1 1 280px", minWidth: 240 }}
                    />
                  </Stack>

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
                      disabled={calStandardLevelsMgPerL.trim() !== ""}
                      helperText={calStandardLevelsMgPerL.trim() !== "" ? "Determined by Standard Levels above" : undefined}
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
                      ppm in the sample ({calInstrumentType === "Aas" ? "the instrument software" : "Syngistix"} applies weight, volume and dilution)
                    </Typography>
                  </Box>
                </Stack>
              </Box>
            </Box>
          )}

          {(workflowType === "Dissolution" || workflowType === "StandardComparison") && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={workflowType === "StandardComparison" ? true : requiresSystemSuitability}
                    disabled={workflowType === "Dissolution" || workflowType === "StandardComparison"}
                    onChange={(e) => setRequiresSystemSuitability(e.target.checked)}
                  />
                }
                label={
                  workflowType === "Dissolution"
                    ? "Requires system suitability (standard from linked SST run)"
                    : workflowType === "StandardComparison"
                    ? "Requires system suitability (one row per analyte, from linked SST run)"
                    : "Requires system suitability"
                }
              />

              {(workflowType === "StandardComparison" || requiresSystemSuitability) && (
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
                    sx={{ mb: workflowType === "StandardComparison" ? 0 : 2 }}
                  />

                  {workflowType === "StandardComparison" ? (
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1.5 }}>
                      Acceptance criteria (RSD, resolution, tailing factor, theoretical plates) are set per analyte,
                      in this test's Test Analytes section, not here.
                    </Typography>
                  ) : (
                    <>
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
                    </>
                  )}
                </Box>
              )}
            </Box>
          )}

          {(isFp || (fpSectionId !== null && (sectionId === fpSectionId || editingTest?.sectionId === fpSectionId))) && (
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
