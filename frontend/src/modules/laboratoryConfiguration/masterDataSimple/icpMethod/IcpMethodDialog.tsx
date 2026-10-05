import { useEffect, useRef } from "react";
import {
  Box,
  Button,
  Stack,
  Alert,
  Tabs,
  Tab,
  CircularProgress,
  Badge,
  Typography
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import type { MaterialMasterEntry } from "../services/MaterialMasterService";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import { ReasonDialog } from "../solutionMaster/ReasonDialog";
import { useIcpMethodDialogState } from "./useIcpMethodDialogState";
import { IcpGeneralSection } from "./IcpGeneralSection";
import { IcpCalibrationSection } from "./IcpCalibrationSection";
import { IcpOptionalChecksSection } from "./IcpOptionalChecksSection";
import { IcpSamplePrepSection } from "./IcpSamplePrepSection";
import { IcpElementsTable } from "./IcpElementsTable";

export interface IcpMethodDialogProps {
  open: boolean;
  editingId: number | null;
  materialMasters: MaterialMasterEntry[];
  sections: LaboratorySection[];
  mySections: LaboratorySection[];
  onClose: () => void;
  onSuccess: () => void;
}

export function IcpMethodDialog({
  open,
  editingId,
  materialMasters,
  sections,
  mySections,
  onClose,
  onSuccess
}: IcpMethodDialogProps) {
  const {
    tabIndex,
    setTabIndex,
    loadingMethod,
    loadedEntity,
    form,
    updateField,
    addElementRow,
    removeElementRow,
    moveElementRow,
    updateElementRow,
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
    availableReferenceStandards,
    handleInitiateSave,
    handleConfirmEditWithReason
  } = useIcpMethodDialogState({
    open,
    editingId,
    materialMasters,
    mySections,
    onClose,
    onSuccess
  });

  const contentRef = useRef<HTMLDivElement>(null);

  // Scroll to first error after failed submit
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
      <Badge color="error" badgeContent={count} sx={{ "& .MuiBadge-badge": { right: -12, top: 2 } }}>
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
            ? `Edit ICP Method: ${form.name || loadedEntity?.name || ""}`
            : "Add ICP Method Master"
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
              {saving ? "Saving..." : editingId ? "Save Changes" : "Create ICP Method"}
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
              <Tabs
                value={tabIndex}
                onChange={(_, next) => setTabIndex(next)}
                variant="scrollable"
                scrollButtons="auto"
                sx={{ borderBottom: 1, borderColor: "divider" }}
              >
                <Tab label={tabLabel("General", 0)} />
                <Tab label={tabLabel("Calibration", 1)} />
                <Tab label={tabLabel("Optional Checks", 2)} />
                <Tab label={tabLabel("Sample Prep", 3)} />
                <Tab label={tabLabel("Elements", 4)} />
              </Tabs>

              <Box sx={{ pt: 1 }}>
                {tabIndex === 0 && (
                  <IcpGeneralSection
                    name={form.name}
                    abbreviation={form.abbreviation}
                    effectiveDate={form.effectiveDate}
                    sectionId={form.sectionId}
                    mode={form.mode}
                    errors={errors}
                    editingId={editingId}
                    mySections={mySections}
                    sections={sections}
                    onNameChange={(val) => updateField("name", val)}
                    onAbbreviationChange={(val) => updateField("abbreviation", val)}
                    onEffectiveDateChange={(val) => updateField("effectiveDate", val)}
                    onSectionIdChange={(val) => updateField("sectionId", val)}
                    onModeChange={(val) => updateField("mode", val)}
                  />
                )}

                {tabIndex === 1 && (
                  <IcpCalibrationSection
                    standardLevelsMgPerL={form.standardLevelsMgPerL}
                    calibrationStandardEntryId={form.calibrationStandardEntryId}
                    minCorrelation={form.minCorrelation}
                    maxCalibrationAgeHours={form.maxCalibrationAgeHours}
                    availableStandards={availableReferenceStandards}
                    errors={errors}
                    onStandardLevelsChange={(val) => updateField("standardLevelsMgPerL", val)}
                    onCalibrationStandardChange={(val) => updateField("calibrationStandardEntryId", val)}
                    onMinCorrelationChange={(val) => updateField("minCorrelation", val)}
                    onMaxCalibrationAgeHoursChange={(val) => updateField("maxCalibrationAgeHours", val)}
                  />
                )}

                {tabIndex === 2 && (
                  <IcpOptionalChecksSection
                    requireBlank={form.requireBlank}
                    blankMaxMgPerL={form.blankMaxMgPerL}
                    requireIcv={form.requireIcv}
                    icvStandardEntryId={form.icvStandardEntryId}
                    icvNominalMgPerL={form.icvNominalMgPerL}
                    icvRecoveryLowPercent={form.icvRecoveryLowPercent}
                    icvRecoveryHighPercent={form.icvRecoveryHighPercent}
                    requireCcv={form.requireCcv}
                    ccvNominalMgPerL={form.ccvNominalMgPerL}
                    ccvRecoveryLowPercent={form.ccvRecoveryLowPercent}
                    ccvRecoveryHighPercent={form.ccvRecoveryHighPercent}
                    calibrationStandardEntryId={form.calibrationStandardEntryId}
                    availableStandards={availableReferenceStandards}
                    errors={errors}
                    onRequireBlankChange={(val) => updateField("requireBlank", val)}
                    onBlankMaxChange={(val) => updateField("blankMaxMgPerL", val)}
                    onRequireIcvChange={(val) => updateField("requireIcv", val)}
                    onIcvStandardChange={(val) => updateField("icvStandardEntryId", val)}
                    onIcvNominalChange={(val) => updateField("icvNominalMgPerL", val)}
                    onIcvRecoveryLowChange={(val) => updateField("icvRecoveryLowPercent", val)}
                    onIcvRecoveryHighChange={(val) => updateField("icvRecoveryHighPercent", val)}
                    onRequireCcvChange={(val) => updateField("requireCcv", val)}
                    onCcvNominalChange={(val) => updateField("ccvNominalMgPerL", val)}
                    onCcvRecoveryLowChange={(val) => updateField("ccvRecoveryLowPercent", val)}
                    onCcvRecoveryHighChange={(val) => updateField("ccvRecoveryHighPercent", val)}
                  />
                )}

                {tabIndex === 3 && (
                  <IcpSamplePrepSection
                    sampleVolumeMl={form.sampleVolumeMl}
                    dilutionFactor={form.dilutionFactor}
                    errors={errors}
                    onSampleVolumeChange={(val) => updateField("sampleVolumeMl", val)}
                    onDilutionFactorChange={(val) => updateField("dilutionFactor", val)}
                  />
                )}

                {tabIndex === 4 && (
                  <IcpElementsTable
                    elements={form.elements}
                    errors={errors}
                    onAddElement={addElementRow}
                    onRemoveElement={removeElementRow}
                    onMoveElement={moveElementRow}
                    onUpdateElement={updateElementRow}
                  />
                )}
              </Box>
            </>
          )}
        </Stack>
      </FloatingDialog>

      {/* Edit Confirmation Reason Dialog */}
      <ReasonDialog
        open={reasonDialogOpen}
        title="Reason for Changing Method"
        onClose={() => setReasonDialogOpen(false)}
        onConfirm={handleConfirmEditWithReason}
        confirmText="Save Method Changes"
        confirmColor="primary"
        loading={saving}
        loadingText="Updating..."
        reason={editReason}
        onReasonChange={setEditReason}
        label="Reason for Modification *"
        placeholder="State the regulatory or operational reason for modifying this ICP method..."
        disabled={!editReason.trim() || saving}
      >
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Modifications to analytical methods are audit logged. Please provide an explicit justification for this revision.
        </Typography>
      </ReasonDialog>
    </>
  );
}
