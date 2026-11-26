import { useState } from "react";
import { List, ListItem, ListItemText, TextField, Typography } from "@mui/material";
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

  // The server decides which affected specifications or tests were never changed (the closure refuses without a
  // justification for them); the form asks for the justification only then.
  const unchangedAffected = (record.changeControl?.affected ?? []).filter((entry) => entry.unchanged);

  const unfinished = record.actions.find((task) => task.kind === "ChangeTask" && task.doneAtUtc === null);
  const lock = isRaiserOrOwner(record, currentUserId)
    ? t("steps.common.raiserOrOwner")
    : unfinished
      ? t("steps.changeControl.close.unfinished", { description: unfinished.description })
      : !review.trim()
        ? t("steps.changeControl.close.needsReview")
        : unchangedAffected.length > 0 && !justification.trim()
          ? t("steps.changeControl.close.needsJustification")
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
        {unchangedAffected.length > 0 && (
          <>
            <Typography variant="body2">
              {t("steps.changeControl.close.unchangedTitle")}
            </Typography>
            <List dense disablePadding aria-label={t("steps.changeControl.close.unchangedList")}>
              {unchangedAffected.map((entry) => (
                <ListItem key={entry.linkId} disableGutters>
                  <ListItemText
                    primary={`${t(`steps.changeControl.affected.kinds.${entry.targetType}`)}: ${entry.number ?? ""}`}
                    secondary={entry.isRestricted ? t("steps.changeControl.close.restricted") : (entry.label ?? undefined)}
                  />
                </ListItem>
              ))}
            </List>
            <TextField
              required
              multiline
              minRows={2}
              label={t("steps.changeControl.close.justification")}
              helperText={t("steps.changeControl.close.justificationHelp")}
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
            />
          </>
        )}
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="QualityClosed"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.closeChangeControl(
            record.id,
            { postChangeReview: review.trim(), unchangedJustification: unchangedAffected.length > 0 ? justification.trim() || null : null, password },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
