import { useEffect, useRef } from "react";
import {
  Box,
  Button,
  Stack,
  Alert,
  Tabs,
  Tab,
  CircularProgress,
  Badge
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
import { GcConditionsSection } from "./GcConditionsSection";
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
    handleTechniqueChange,
    handleResultModeChange,
    dialogError,
    setDialogError,
    errors,
    formErrorMessages,
    tabErrorCounts,
    scrollTick,
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

  const contentRef = useRef<HTMLDivElement>(null);
  const isGc = form.technique === "Gc";

  // After a failed submit, bring the first field with an error into view.
  useEffect(() => {
    if (scrollTick === 0) return;
    const timer = window.setTimeout(() => {
      contentRef.current
        ?.querySelector(".Mui-error")
        ?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 50);
    return () => window.clearTimeout(timer);
  }, [scrollTick]);

  const tabLabel = (label: string, tab: number) => {
    const count = tabErrorCounts[tab] ?? 0;
    return count > 0 ? (
      <Badge color="error" badgeContent={count} sx={{ "& .MuiBadge-badge": { right: -14, top: 2 } }}>
        {label}
      </Badge>
    ) : (
      label
    );
  };

  return (
    <>
      <FloatingDialog
        open={open}
        title={
          editingId
            ? `Edit ${isGc ? "GC" : "HPLC"} Method: ${form.name || loadedEntity?.name || ""}`
            : `Add ${isGc ? "GC" : "HPLC"} Method Master`
        }
        onClose={onClose}
        maxWidth="md"
        actions={
          <>
            <Button onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleInitiateSave}
              disabled={saving || loadingMethod}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingId ? "Save Changes" : `Create ${isGc ? "GC" : "HPLC"} Method`}
            </Button>
          </>
        }
      >
        <Stack ref={contentRef} spacing={2} sx={{ pt: 1 }}>
          {formErrorMessages.length > 0 && (
            <Alert severity="error">
              {formErrorMessages.map((message) => (
                <div key={message}>{message}</div>
              ))}
            </Alert>
          )}
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
                technique={form.technique}
                resultMode={form.resultMode}
                onTechniqueChange={handleTechniqueChange}
                onResultModeChange={handleResultModeChange}
                name={form.name}
                abbreviation={form.abbreviation}
                effectiveDate={form.effectiveDate}
                sectionId={form.sectionId}
                errors={errors}
                editingId={editingId}
                mySections={mySections}
                sections={sections}
                onNameChange={(val) => updateField("name", val)}
                onAbbreviationChange={(val) => updateField("abbreviation", val)}
                onEffectiveDateChange={(val) => updateField("effectiveDate", val)}
                onSectionIdChange={(val) => updateField("sectionId", val)}
              />

              {isGc ? (
                // GC Tabs: Column (0), Conditions & Oven (1), Analytes (2)
                <Tabs
                  value={tabIndex > 2 ? 0 : tabIndex}
                  onChange={(_, next) => setTabIndex(next)}
                  sx={{ borderBottom: 1, borderColor: "divider" }}
                >
                  <Tab label={tabLabel("Column", 0)} sx={{ textTransform: "none", fontWeight: 600 }} />
                  <Tab label={tabLabel("GC Conditions & Oven", 1)} sx={{ textTransform: "none", fontWeight: 600 }} />
                  <Tab label={tabLabel(`Analytes (${form.analytes.length})`, 2)} sx={{ textTransform: "none", fontWeight: 600 }} />
                </Tabs>
              ) : (
                // HPLC Tabs: Column (0), Elution & Detection (1), Solutions (2), Analytes (3)
                <Tabs
                  value={tabIndex}
                  onChange={(_, next) => setTabIndex(next)}
                  sx={{ borderBottom: 1, borderColor: "divider" }}
                >
                  <Tab label={tabLabel("Column", 0)} sx={{ textTransform: "none", fontWeight: 600 }} />
                  <Tab label={tabLabel("Elution & Detection", 1)} sx={{ textTransform: "none", fontWeight: 600 }} />
                  <Tab label={tabLabel("Solutions", 2)} sx={{ textTransform: "none", fontWeight: 600 }} />
                  <Tab label={tabLabel(`Analytes (${form.analytes.length})`, 3)} sx={{ textTransform: "none", fontWeight: 600 }} />
                </Tabs>
              )}

              {/* Tab 0: Column (Shared component, adjusts fields by technique) */}
              {tabIndex === 0 && (
                <HplcColumnSection
                  technique={form.technique}
                  columnDesignation={form.columnDesignation}
                  columnLength={form.columnLength}
                  columnInternalDiameterMm={form.columnInternalDiameterMm}
                  particleSizeUm={form.particleSizeUm}
                  filmThicknessUm={form.filmThicknessUm}
                  columnBrand={form.columnBrand}
                  columnPartNumber={form.columnPartNumber}
                  columnTemperatureC={form.columnTemperatureC}
                  errors={errors}
                  onColumnDesignationChange={(val) => updateField("columnDesignation", val)}
                  onColumnLengthChange={(val) => {
                    updateField("columnLength", val);
                  }}
                  onColumnInternalDiameterMmChange={(val) => updateField("columnInternalDiameterMm", val)}
                  onParticleSizeUmChange={(val) => updateField("particleSizeUm", val)}
                  onFilmThicknessUmChange={(val) => updateField("filmThicknessUm", val)}
                  onColumnBrandChange={(val) => updateField("columnBrand", val)}
                  onColumnPartNumberChange={(val) => updateField("columnPartNumber", val)}
                  onColumnTemperatureCChange={(val) => updateField("columnTemperatureC", val)}
                />
              )}

              {/* Tab 1: GC Conditions & Oven (GC) or Elution & Detection (HPLC) */}
              {tabIndex === 1 && (
                isGc ? (
                  <GcConditionsSection
                    carrierGas={form.carrierGas}
                    splitRatio={form.splitRatio}
                    flowRateMlPerMin={form.flowRateMlPerMin}
                    inletTemperatureC={form.inletTemperatureC}
                    detectorTemperatureC={form.detectorTemperatureC}
                    detectorType={form.detectorType}
                    injectionVolumeUl={form.injectionVolumeUl}
                    runTimeMin={form.runTimeMin}
                    diluentSolutionId={form.diluentSolutionId}
                    availableDiluents={availableDiluents}
                    headspaceEnabled={form.headspaceEnabled}
                    headspaceEquilibrationTemperatureC={form.headspaceEquilibrationTemperatureC}
                    headspaceEquilibrationMin={form.headspaceEquilibrationMin}
                    headspaceTransferLineTemperatureC={form.headspaceTransferLineTemperatureC}
                    ovenSteps={form.ovenSteps}
                    errors={errors}
                    onCarrierGasChange={(val) => updateField("carrierGas", val)}
                    onSplitRatioChange={(val) => updateField("splitRatio", val)}
                    onFlowRateMlPerMinChange={(val) => updateField("flowRateMlPerMin", val)}
                    onInletTemperatureCChange={(val) => updateField("inletTemperatureC", val)}
                    onDetectorTemperatureCChange={(val) => updateField("detectorTemperatureC", val)}
                    onDetectorTypeChange={(val) => updateField("detectorType", val)}
                    onInjectionVolumeUlChange={(val) => updateField("injectionVolumeUl", val)}
                    onRunTimeMinChange={(val) => updateField("runTimeMin", val)}
                    onDiluentSolutionIdChange={(val) => updateField("diluentSolutionId", val)}
                    onHeadspaceEnabledChange={(val) => updateField("headspaceEnabled", val)}
                    onHeadspaceEquilibrationTemperatureCChange={(val) =>
                      updateField("headspaceEquilibrationTemperatureC", val)
                    }
                    onHeadspaceEquilibrationMinChange={(val) =>
                      updateField("headspaceEquilibrationMin", val)
                    }
                    onHeadspaceTransferLineTemperatureCChange={(val) =>
                      updateField("headspaceTransferLineTemperatureC", val)
                    }
                    onOvenStepsChange={(steps) => updateField("ovenSteps", steps)}
                  />
                ) : (
                  <HplcElutionDetectionTab
                    technique="Hplc"
                    elutionMode={form.elutionMode}
                    flowRateMlPerMin={form.flowRateMlPerMin}
                    equilibrationMin={form.equilibrationMin}
                    gradientSteps={form.gradientSteps}
                    mobilePhases={form.mobilePhases}
                    detectorType={form.detectorType}
                    injectionVolumeUl={form.injectionVolumeUl}
                    runTimeMin={form.runTimeMin}
                    errors={errors}
                    onElutionModeChange={(val) => updateField("elutionMode", val)}
                    onFlowRateMlPerMinChange={(val) => updateField("flowRateMlPerMin", val)}
                    onEquilibrationMinChange={(val) => updateField("equilibrationMin", val)}
                    onGradientStepsChange={(steps) => updateField("gradientSteps", steps)}
                    onDetectorTypeChange={(val) => updateField("detectorType", val)}
                    onInjectionVolumeUlChange={(val) => updateField("injectionVolumeUl", val)}
                    onRunTimeMinChange={(val) => updateField("runTimeMin", val)}
                  />
                )
              )}

              {/* Tab 2: Solutions (HPLC) or Analytes (GC) */}
              {tabIndex === 2 && (
                isGc ? (
                  <HplcAnalytesSection
                    technique="Gc"
                    resultMode={form.resultMode}
                    sampleSolutionVolumeMl={form.sampleSolutionVolumeMl}
                    onSampleSolutionVolumeMlChange={(val) => updateField("sampleSolutionVolumeMl", val)}
                    analytes={form.analytes}
                    availableStandards={availableReferenceStandards}
                    errors={errors}
                    onAnalyteChange={(idx, field, val) => {
                      const next = [...form.analytes];
                      next[idx] = { ...next[idx], [field]: val };
                      updateField("analytes", next);
                    }}
                    onAddAnalyte={() => {
                      const isResidual = form.resultMode === "ResidualSolvents";
                      updateField("analytes", [
                        ...form.analytes,
                        {
                          name: "",
                          wavelengthNm: "",
                          standardEntryId: "",
                          theoreticalWeightStdMg: isResidual ? "0" : "50",
                          theoreticalWeightTestMg: isResidual ? "0" : "50",
                          standardDilution: "",
                          standardInjections: "5",
                          sstMaxRsdPercent: "2.0",
                          sstMinResolution: "",
                          sstMaxTailingFactor: "2.0",
                          sstMinTheoreticalPlates: "2000",
                          sstMinRetentionFactor: "",
                          sstMinSignalToNoise: "",
                          sstMinPeakToValley: "",
                          standardConcentrationUgPerMl: isResidual ? "100" : ""
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
                ) : (
                  <HplcSolutionsSection
                    diluentSolutionId={form.diluentSolutionId}
                    mobilePhases={form.mobilePhases}
                    elutionMode={form.elutionMode}
                    availableDiluents={availableDiluents}
                    availableMobilePhases={availableMobilePhases}
                    errors={errors}
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
                )
              )}

              {/* Tab 3: Analytes (HPLC only) */}
              {!isGc && tabIndex === 3 && (
                <HplcAnalytesSection
                  technique="Hplc"
                  resultMode="Assay"
                  analytes={form.analytes}
                  availableStandards={availableReferenceStandards}
                  errors={errors}
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
                        standardDilution: "",
                        standardInjections: "5",
                        sstMaxRsdPercent: "2.0",
                        sstMinResolution: "",
                        sstMaxTailingFactor: "2.0",
                        sstMinTheoreticalPlates: "2000",
                        sstMinRetentionFactor: "",
                        sstMinSignalToNoise: "",
                        sstMinPeakToValley: "",
                        standardConcentrationUgPerMl: ""
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
        title={`Confirm ${isGc ? "GC" : "HPLC"} Method Changes`}
        onClose={() => {
          if (!saving) setReasonDialogOpen(false);
        }}
        onConfirm={handleConfirmEditWithReason}
        confirmText="Save Method"
        loading={saving}
        reason={editReason}
        onReasonChange={setEditReason}
        label="Reason for Change *"
        placeholder={`Explain why this ${isGc ? "GC" : "HPLC"} method is being updated (required by data integrity standards)...`}
        disabled={!editReason.trim() || saving}
      />
    </>
  );
}
