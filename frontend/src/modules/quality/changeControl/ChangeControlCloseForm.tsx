import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner } from "../steps/stepKit";

interface ChangeControlCloseFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA closes the change control once every task is done: a post-change review comment, and a justification when an
// affected specification or test was not changed. The server names what is missing when it refuses.
export function ChangeControlCloseForm({ record, currentUserId, onChanged }: ChangeControlCloseFormProps) {
  const { t } = useTranslation("quality");
  const [review, setReview] = useState("");
  const [justification, setJustification] = useState("");
  const [signing, setSigning] = useState(false);

  const unfinished = record.actions.find((task) => task.kind === "ChangeTask" && task.doneAtUtc === null);
  const lock = isRaiserOrOwner(record, currentUserId)
    ? t("steps.common.raiserOrOwner")
    : unfinished
      ? t("steps.changeControl.close.unfinished", { description: unfinished.description })
      : !review.trim()
        ? t("steps.changeControl.close.needsReview")
        : null;

  return (
    <PanelFrame title={t("steps.changeControl.close.title")}>
      <StepForm
        error={null}
        actions={<GatedButton label={t("steps.changeControl.close.close")} lockedReason={lock} onClick={() => setSigning(true)} />}
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.changeControl.close.intro")}
        </Typography>
        <TextField
          required
          multiline
          minRows={3}
          label={t("steps.changeControl.close.review")}
          value={review}
          onChange={(e) => setReview(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        <TextField
          multiline
          minRows={2}
          label={t("steps.changeControl.close.justification")}
          helperText={t("steps.changeControl.close.justificationHelp")}
          value={justification}
          onChange={(e) => setJustification(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="QualityClosed"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.closeChangeControl(
            record.id,
            { postChangeReview: review.trim(), unchangedJustification: justification.trim() || null, password },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
