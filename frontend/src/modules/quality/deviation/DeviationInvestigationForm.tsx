import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";
import { useCauseCategoryOptions } from "../steps/lookups";
import { GatedButton, LookupSelect, SignStepDialog, StepForm, TEXT_MAX, useStepRunner } from "../steps/stepKit";

interface DeviationInvestigationFormProps {
  record: QualityRecordDetailDto;
  permissions: QualityTypeActionsProps["permissions"];
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// The owner (or a Section Head) records the investigation; a Section Head other than the owner signs the
// laboratory conclusion once it is complete.
export function DeviationInvestigationForm({ record, permissions, currentUserId, onChanged }: DeviationInvestigationFormProps) {
  const { t } = useTranslation("quality");
  const detail = record.deviation;
  const categories = useCauseCategoryOptions();
  const [impact, setImpact] = useState(detail?.impactAssessment ?? "");
  const [categoryId, setCategoryId] = useState<number | "">(detail?.rootCauseCategoryId ?? "");
  const [causeText, setCauseText] = useState(detail?.rootCauseDescription ?? "");
  const [proposed, setProposed] = useState(detail?.proposedActions ?? "");
  const [signing, setSigning] = useState(false);
  const saver = useStepRunner(onChanged);

  const isOwner = currentUserId !== null && record.ownerUserId === currentUserId;
  // QA never investigates; the owner and holders of Quality.Investigate do.
  const canSave = !permissions.approve && (isOwner || permissions.investigate);
  const saved =
    Boolean(detail?.impactAssessment?.trim()) &&
    detail?.rootCauseCategoryId != null &&
    Boolean(detail?.rootCauseDescription?.trim()) &&
    Boolean(detail?.proposedActions?.trim());
  const complete = Boolean(impact.trim()) && categoryId !== "" && Boolean(causeText.trim()) && Boolean(proposed.trim());

  const conclusionLock = isOwner
    ? t("steps.deviation.investigation.ownerCannotConclude")
    : !saved
      ? t("steps.deviation.investigation.saveFirst")
      : null;

  const save = () => {
    if (categoryId === "") return Promise.resolve(false);
    return saver.run(
      () =>
        qualityService.saveDeviationInvestigation(
          record.id,
          {
            impactAssessment: impact.trim(),
            rootCauseCategoryId: categoryId,
            rootCauseDescription: causeText.trim(),
            proposedActions: proposed.trim()
          },
          record.version
        ),
      t("steps.deviation.investigation.saveFailed")
    );
  };

  return (
    <PanelFrame title={t("steps.deviation.investigation.title")}>
      <StepForm
        error={saver.error}
        actions={
          <>
            {canSave && (
              <GatedButton
                label={t("steps.deviation.investigation.save")}
                lockedReason={complete ? null : t("steps.deviation.investigation.needsFields")}
                busy={saver.busy}
                onClick={() => void save()}
              />
            )}
            {permissions.labConclude && (
              <GatedButton
                label={t("steps.deviation.investigation.sign")}
                variant="outlined"
                lockedReason={conclusionLock}
                onClick={() => setSigning(true)}
              />
            )}
          </>
        }
      >
        {!canSave && !permissions.labConclude && (
          <Typography variant="body2" color="text.secondary">
            {t("steps.deviation.investigation.withOwner")}
          </Typography>
        )}
        <TextField
          required
          multiline
          minRows={3}
          label={t("steps.deviation.investigation.impact")}
          helperText={t("steps.deviation.investigation.impactHelp")}
          value={impact}
          onChange={(e) => setImpact(e.target.value)}
          disabled={!canSave}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        <LookupSelect
          required
          label={t("steps.deviation.investigation.category")}
          value={categoryId}
          onChange={setCategoryId}
          lookup={categories}
          disabled={!canSave}
        />
        <TextField
          required
          multiline
          minRows={2}
          label={t("steps.deviation.investigation.cause")}
          value={causeText}
          onChange={(e) => setCauseText(e.target.value)}
          disabled={!canSave}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        <TextField
          required
          multiline
          minRows={2}
          label={t("steps.deviation.investigation.proposed")}
          value={proposed}
          onChange={(e) => setProposed(e.target.value)}
          disabled={!canSave}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="QualityLabConclusion"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.signDeviationLabConclusion(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
