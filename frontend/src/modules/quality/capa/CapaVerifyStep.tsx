import { useState } from "react";
import { Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, isRaiserOrOwner } from "../steps/stepKit";

interface CapaVerifyStepProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA signs that every action is done and its evidence is acceptable. The actions are completed one by one by
// their responsible people in the actions list of the record page.
export function CapaVerifyStep({ record, currentUserId, onChanged }: CapaVerifyStepProps) {
  const { t } = useTranslation("quality");
  const [signing, setSigning] = useState(false);
  const unfinished = record.actions.find((action) => action.doneAtUtc === null);
  const lock = isRaiserOrOwner(record, currentUserId)
    ? t("steps.common.raiserOrOwner")
    : unfinished
      ? t("steps.capa.verify.unfinished", { description: unfinished.description })
      : null;

  return (
    <PanelFrame title={t("steps.capa.verify.title")}>
      <StepForm
        error={null}
        actions={<GatedButton label={t("steps.capa.verify.verify")} lockedReason={lock} onClick={() => setSigning(true)} />}
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.capa.verify.intro")}
        </Typography>
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="CapaCompletionVerified"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.verifyCapaCompletion(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
