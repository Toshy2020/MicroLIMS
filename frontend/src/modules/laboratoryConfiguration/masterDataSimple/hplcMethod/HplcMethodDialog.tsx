import {
  Box,
  Button,
  Stack,
  Alert,
  Tabs,
  Tab,
  CircularProgress
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SolutionMaster } from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import { ReasonDialog } from "../solutionMaster/ReasonDialog";
import { CHANNELS } from "./hplcMethodForm";
import { HplcGeneralSection } from "./HplcGeneralSection";
import { HplcColumnSection } from "./HplcColumnSection";
import { HplcElutionDetectionTab } from "./HplcElutionDetectionTab";
import { HplcSolutionsSection } from "./HplcSolutionsSection";
import { HplcAnalytesSection } from "./HplcAnalytesSection";
import { useHplcMethodDialogState } from "./useHplcMethodDialogState";

export interface HplcMethodDialogProps {
  open: boolean;
  editingId: number | null;
  solutions: SolutionMaster[];
  materialMasters: MaterialMasterEntry[];
  sections: LaboratorySection[];
  mySections: LaboratorySection[];
  onClose: () => void;
  onSuccess: () => void;
}

export function HplcMethodDialog({
  open,
  editingId,
  solutions,
  materialMasters,
  sections,
  mySections,
  onClose,
  onSuccess
}: HplcMethodDialogProps) {
  const {
    tabIndex,
    setTabIndex,
    loadingMethod,
    loadedEntity,
    form,
    updateField,
    dialogError,
    setDialogError,
    saving,
    reasonDialogOpen,
    setReasonDialogOpen,
    editReason,
    setEditReason,
    availableDiluents,
    availableMobilePhases,
    availableReferenceStandards,
    handleInitiateSave,
    handleConfirmEditWithReason
  } = useHplcMethodDialogState({
    open,
    editingId,
    solutions,
    materialMasters,
    mySections,
    onClose,
    onSuccess
  });

  return (
    <>
      <FloatingDialog
        open={open}
        title={editingId ? `Edit HPLC Method: ${form.name || loadedEntity?.name || ""}` : "Add HPLC Method Master"}
        onClose={onClose}
        maxWidth="md"
        actions={
          <>
            <Button onClick={onClose} disabled={saving} sx={{ textTransform: "none" }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleInitiateSave}
              disabled={saving || loadingMethod}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingId ? "Save Changes" : "Create Method"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {dialogError && (
            <Alert severity="error" onClose={() => setDialogError(null)}>
              {dialogError}
            </Alert>
          )}

          {loadingMethod ? (
            <Box sx={{ py: 6, display: "flex", justifyContent: "center" }}>
              <CircularProgress size={32} />
            </Box>
          ) : (
            <>
              <HplcGeneralSection
                name={form.name}
                abbreviation={form.abbreviation}
                effectiveDate={form.effectiveDate}
                sectionId={form.sectionId}
                editingId={editingId}
                mySections={mySections}
                sections={sections}
                onNameChange={(val) => updateField("name", val)}
                onAbbreviationChange={(val) => updateField("abbreviation", val)}
                onEffectiveDateChange={(val) => updateField("effectiveDate", val)}
                onSectionIdChange={(val) => updateField("sectionId", val)}
              />

              <Tabs
                value={tabIndex}
                onChange={(_, next) => setTabIndex(next)}
                sx={{ borderBottom: 1, borderColor: "divider" }}
              >
                <Tab label="Column" sx={{ textTransform: "none", fontWeight: 600 }} />
                <Tab label="Elution & Detection" sx={{ textTransform: "none", fontWeight: 600 }} />
                <Tab label="Solutions" sx={{ textTransform: "none", fontWeight: 600 }} />
                <Tab label={`Analytes (${form.analytes.length})`} sx={{ textTransform: "none", fontWeight: 600 }} />
              </Tabs>

              {tabIndex === 0 && (
                <HplcColumnSection
                  columnDesignation={form.columnDesignation}
                  columnLengthMm={form.columnLengthMm}
                  columnInternalDiameterMm={form.columnInternalDiameterMm}
                  particleSizeUm={form.particleSizeUm}
                  columnBrand={form.columnBrand}
                  columnPartNumber={form.columnPartNumber}
                  columnTemperatureC={form.columnTemperatureC}
                  onColumnDesignationChange={(val) => updateField("columnDesignation", val)}
                  onColumnLengthMmChange={(val) => updateField("columnLengthMm", val)}
                  onColumnInternalDiameterMmChange={(val) => updateField("columnInternalDiameterMm", val)}
                  onParticleSizeUmChange={(val) => updateField("particleSizeUm", val)}
                  onColumnBrandChange={(val) => updateField("columnBrand", val)}
                  onColumnPartNumberChange={(val) => updateField("columnPartNumber", val)}
                  onColumnTemperatureCChange={(val) => updateField("columnTemperatureC", val)}
                />
              )}

              {tabIndex === 1 && (
                <HplcElutionDetectionTab
                  elutionMode={form.elutionMode}
                  flowRateMlPerMin={form.flowRateMlPerMin}
                  equilibrationMin={form.equilibrationMin}
                  gradientSteps={form.gradientSteps}
                  mobilePhases={form.mobilePhases}
                  detectorType={form.detectorType}
                  injectionVolumeUl={form.injectionVolumeUl}
                  runTimeMin={form.runTimeMin}
                  onElutionModeChange={(val) => updateField("elutionMode", val)}
                  onFlowRateMlPerMinChange={(val) => updateField("flowRateMlPerMin", val)}
                  onEquilibrationMinChange={(val) => updateField("equilibrationMin", val)}
                  onGradientStepsChange={(steps) => updateField("gradientSteps", steps)}
                  onDetectorTypeChange={(val) => updateField("detectorType", val)}
                  onInjectionVolumeUlChange={(val) => updateField("injectionVolumeUl", val)}
                  onRunTimeMinChange={(val) => updateField("runTimeMin", val)}
                />
              )}

              {tabIndex === 2 && (
                <HplcSolutionsSection
                  diluentSolutionId={form.diluentSolutionId}
                  mobilePhases={form.mobilePhases}
                  elutionMode={form.elutionMode}
                  availableDiluents={availableDiluents}
                  availableMobilePhases={availableMobilePhases}
                  onDiluentSolutionIdChange={(val) => updateField("diluentSolutionId", val)}
                  onMobilePhaseChange={(idx, field, val) => {
                    const next = [...form.mobilePhases];
                    next[idx] = { ...next[idx], [field]: val };
                    updateField("mobilePhases", next);
                  }}
                  onAddChannel={() => {
                    const used = new Set(form.mobilePhases.map((p) => p.channel));
                    const nextCh = CHANNELS.find((c) => !used.has(c));
                    if (nextCh) {
                      updateField("mobilePhases", [
                        ...form.mobilePhases,
                        { channel: nextCh, solutionMasterId: "", ratioPercent: "" }
                      ]);
                    }
                  }}
                  onRemoveChannel={(idx) => {
                    updateField("mobilePhases", form.mobilePhases.filter((_, i) => i !== idx));
                  }}
                />
              )}

              {tabIndex === 3 && (
                <HplcAnalytesSection
                  analytes={form.analytes}
                  availableStandards={availableReferenceStandards}
                  onAnalyteChange={(idx, field, val) => {
                    const next = [...form.analytes];
                    next[idx] = { ...next[idx], [field]: val };
                    updateField("analytes", next);
                  }}
                  onAddAnalyte={() => {
                    updateField("analytes", [
                      ...form.analytes,
                      {
                        name: "",
                        wavelengthNm: "254",
                        standardEntryId: "",
                        theoreticalWeightStdMg: "50",
                        theoreticalWeightTestMg: "50",
                        standardInjections: "5",
                        sstMaxRsdPercent: "2.0",
                        sstMinResolution: "",
                        sstMaxTailingFactor: "2.0",
                        sstMinTheoreticalPlates: "2000",
                        sstMinRetentionFactor: "",
                        sstMinSignalToNoise: "",
                        sstMinPeakToValley: ""
                      }
                    ]);
                  }}
                  onRemoveAnalyte={(idx) => {
                    updateField("analytes", form.analytes.filter((_, i) => i !== idx));
                  }}
                  onMoveAnalyte={(idx, direction) => {
                    const targetIdx = direction === "up" ? idx - 1 : idx + 1;
                    if (targetIdx < 0 || targetIdx >= form.analytes.length) return;
                    const copy = [...form.analytes];
                    const temp = copy[idx];
                    copy[idx] = copy[targetIdx];
                    copy[targetIdx] = temp;
                    updateField("analytes", copy);
                  }}
                />
              )}
            </>
          )}
        </Stack>
      </FloatingDialog>

      <ReasonDialog
        open={reasonDialogOpen}
        title="Confirm HPLC Method Changes"
        onClose={() => { if (!saving) setReasonDialogOpen(false); }}
        onConfirm={handleConfirmEditWithReason}
        confirmText="Save Method"
        loading={saving}
        reason={editReason}
        onReasonChange={setEditReason}
        label="Reason for Change *"
        placeholder="Explain why this HPLC method is being updated (required by data integrity standards)..."
        disabled={!editReason.trim() || saving}
      />
    </>
  );
}
