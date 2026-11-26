import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner, useStepRunner } from "../steps/stepKit";

interface CapaPlanDecisionProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA approves the saved plan (signed) or returns it to the owner with a comment.
export function CapaPlanDecision({ record, currentUserId, onChanged }: CapaPlanDecisionProps) {
  const { t } = useTranslation("quality");
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const returner = useStepRunner(onChanged);

  const capa = record.capa;
  const planSaved = record.actions.length > 0 && Boolean(capa?.effectivenessCriteria) && Boolean(capa?.effectivenessMethod) && Boolean(capa?.effectivenessPlannedDate);
  const ownLock = isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null;

  return (
    <PanelFrame title={t("steps.capa.decision.title")}>
      <StepForm
        error={returner.error}
        actions={
          <>
            <GatedButton
              label={t("steps.capa.decision.approve")}
              lockedReason={ownLock ?? (planSaved ? null : t("steps.capa.decision.planNotSaved"))}
              onClick={() => setSigning(true)}
            />
            <GatedButton
              label={t("steps.capa.decision.return")}
              variant="outlined"
              lockedReason={ownLock ?? (comment.trim() ? null : t("steps.capa.decision.returnNeedsComment"))}
              busy={returner.busy}
              onClick={() =>
                void returner.run(
                  () => qualityService.returnCapaPlan(record.id, { comment: comment.trim() }, record.version),
                  t("steps.capa.decision.returnFailed")
                )
              }
            />
          </>
        }
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.capa.decision.intro")}
        </Typography>
        <TextField
          multiline
          minRows={2}
          label={t("steps.capa.decision.comment")}
          helperText={t("steps.capa.decision.commentHelp")}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="CapaPlanApproved"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.approveCapaPlan(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
