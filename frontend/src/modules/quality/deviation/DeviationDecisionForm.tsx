import { useState } from "react";
import { Checkbox, FormControlLabel, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner, useStepRunner } from "../steps/stepKit";

interface DeviationDecisionFormProps {
  record: QualityRecordDetailDto;
  canApprove: boolean;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA approves the laboratory conclusion (and decides whether a CAPA is required) or returns it to the owner.
export function DeviationDecisionForm({ record, canApprove, currentUserId, onChanged }: DeviationDecisionFormProps) {
  const { t } = useTranslation("quality");
  const [capaRequired, setCapaRequired] = useState(false);
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const returner = useStepRunner(onChanged);

  const lock = isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null;

  return (
    <PanelFrame title={t("steps.deviation.decision.title")}>
      {!canApprove ? (
        <Typography variant="body2" color="text.secondary">
          {t("steps.deviation.decision.waitingForQa")}
        </Typography>
      ) : (
        <StepForm
          error={returner.error}
          actions={
            <>
              <GatedButton label={t("steps.deviation.decision.approve")} lockedReason={lock} onClick={() => setSigning(true)} />
              <GatedButton
                label={t("steps.deviation.decision.return")}
                variant="outlined"
                lockedReason={lock ?? (comment.trim() ? null : t("steps.deviation.decision.returnNeedsComment"))}
                busy={returner.busy}
                onClick={() =>
                  void returner.run(
                    () => qualityService.returnDeviation(record.id, { comment: comment.trim() }, record.version),
                    t("steps.deviation.decision.returnFailed")
                  )
                }
              />
            </>
          }
        >
          <FormControlLabel
            control={<Checkbox checked={capaRequired} onChange={(e) => setCapaRequired(e.target.checked)} />}
            label={t("steps.deviation.decision.capaRequired")}
          />
          <TextField
            multiline
            minRows={2}
            label={t("steps.deviation.decision.comment")}
            helperText={t("steps.deviation.decision.commentHelp")}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
          />
        </StepForm>
      )}
      <SignStepDialog
        open={signing}
        meaning="QualityApproved"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.approveDeviation(
            record.id,
            { capaRequired, comment: comment.trim() || null, password },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
