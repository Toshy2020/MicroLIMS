import { useState } from "react";
import { Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { DestructiveSignAction, GatedButton, SignStepDialog, StepForm, isRaiserOrOwner } from "../steps/stepKit";

interface ChangeControlDecisionFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA approves the signed impact assessment for implementation, or rejects the change with a reason. Both are signed.
export function ChangeControlDecisionForm({ record, currentUserId, onChanged }: ChangeControlDecisionFormProps) {
  const { t } = useTranslation("quality");
  const [signing, setSigning] = useState(false);
  const lock = isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null;

  return (
    <PanelFrame title={t("steps.changeControl.decision.title")}>
      <StepForm
        error={null}
        actions={
          <>
            <GatedButton label={t("steps.changeControl.decision.approve")} lockedReason={lock} onClick={() => setSigning(true)} />
            <RejectChangeControl record={record} lockedReason={lock} onChanged={onChanged} />
          </>
        }
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.changeControl.decision.intro")}
        </Typography>
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="ChangeApproved"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.approveChangeControl(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}

interface RejectChangeControlProps {
  record: QualityRecordDetailDto;
  lockedReason: string | null;
  onChanged: () => Promise<void>;
}

// Shared by the proposed and the assessed state: QA can reject a change control before it is approved.
export function RejectChangeControl({ record, lockedReason, onChanged }: RejectChangeControlProps) {
  const { t } = useTranslation("quality");
  return (
    <DestructiveSignAction
      label={t("steps.changeControl.decision.reject")}
      confirmTitle={t("steps.changeControl.decision.rejectConfirmTitle")}
      confirmMessage={t("steps.changeControl.decision.rejectConfirmMessage", { number: record.number })}
      reasonLabel={t("steps.changeControl.decision.rejectReason")}
      meaning="ChangeRejected"
      lockedReason={lockedReason}
      onSign={async (password, reason) => {
        await qualityService.rejectChangeControl(record.id, { reason, password }, record.version);
        await onChanged();
      }}
    />
  );
}
