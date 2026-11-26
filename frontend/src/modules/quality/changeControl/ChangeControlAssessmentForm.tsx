import { useState } from "react";
import { Alert, Box, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityChecklistAnswerValue, type QualityRecordDetailDto } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";
import { useUserOptions } from "../steps/lookups";
import { ActionRowsEditor, isCompleteRow, type ActionRow } from "../steps/ActionRowsEditor";
import { GatedButton, SignStepDialog, StepError, StepForm, TEXT_MAX, useStepRunner } from "../steps/stepKit";
import { AffectedPicker, type AffectedItem } from "./AffectedPicker";
import { useChecklist } from "./useChecklist";

interface ChangeControlAssessmentFormProps {
  record: QualityRecordDetailDto;
  permissions: QualityTypeActionsProps["permissions"];
  onChanged: () => Promise<void>;
}

const ANSWERS: QualityChecklistAnswerValue[] = ["Yes", "No", "NotApplicable"];

// While the change control is proposed: affected records can still be added, and the Section Head records the impact
// assessment (summary, checklist answers, implementation tasks) and signs it. Signing freezes the affected list.
export function ChangeControlAssessmentForm({ record, permissions, onChanged }: ChangeControlAssessmentFormProps) {
  const { t } = useTranslation("quality");
  const users = useUserOptions();
  const checklist = useChecklist(record.id, record.sectionId);
  const savedTasks = record.actions.filter((action) => action.kind === "ChangeTask");
  const [summary, setSummary] = useState(record.changeControl?.impactSummary ?? "");
  const [rows, setRows] = useState<ActionRow[]>(
    savedTasks.map((task) => ({
      kind: "ChangeTask",
      description: task.description,
      responsibleUserId: task.responsibleUserId,
      dueDate: task.dueDate
    }))
  );
  const [dirty, setDirty] = useState(false);
  const [signing, setSigning] = useState(false);
  const saver = useStepRunner(onChanged);
  const adder = useStepRunner(onChanged);

  const canAssess = permissions.investigate;
  const saved = Boolean(record.changeControl?.impactSummary?.trim());
  const unanswered = checklist.items.filter((item) => !checklist.answers[item.key]?.answer);
  const complete = Boolean(summary.trim()) && rows.every(isCompleteRow);

  const edit = <T,>(setter: (value: T) => void) => (value: T) => {
    setter(value);
    setDirty(true);
  };

  const save = async () => {
    const ok = await saver.run(
      () =>
        qualityService.saveChangeControlAssessment(
          record.id,
          {
            impactSummary: summary.trim(),
            tasks: rows.map((row) => ({
              description: row.description.trim(),
              responsibleUserId: Number(row.responsibleUserId),
              dueDate: row.dueDate
            })),
            answers: checklist.items
              .filter((item) => checklist.answers[item.key]?.answer)
              .map((item) => ({
                itemKey: item.key,
                answer: checklist.answers[item.key].answer as QualityChecklistAnswerValue,
                comment: checklist.answers[item.key].comment.trim() || null
              }))
          },
          record.version
        ),
      t("steps.changeControl.assessment.saveFailed")
    );
    if (ok) setDirty(false);
  };

  const signLock = !saved
    ? t("steps.changeControl.assessment.saveFirst")
    : dirty
      ? t("steps.changeControl.assessment.unsaved")
      : unanswered.length > 0
        ? t("steps.changeControl.assessment.unanswered", { left: unanswered.length })
        : null;

  const addAffected = (item: AffectedItem) =>
    void adder.run(
      () => qualityService.addChangeControlAffected(record.id, { targetType: item.targetType, targetId: item.targetId, newRecordCode: item.newRecordCode }, record.version),
      t("steps.changeControl.affected.addFailed")
    );

  return (
    <Stack spacing={2}>
      {permissions.raise && (
        <PanelFrame title={t("steps.changeControl.affected.title")}>
          <Stack spacing={1.5}>
            <Typography variant="body2" color="text.secondary">
              {t("steps.changeControl.affected.intro")}
            </Typography>
            <AffectedPicker onAdd={addAffected} disabled={adder.busy} />
            <StepError message={adder.error} />
          </Stack>
        </PanelFrame>
      )}

      <PanelFrame title={t("steps.changeControl.assessment.title")}>
        <StepForm
          error={saver.error}
          actions={
            canAssess ? (
              <>
                <GatedButton
                  label={t("steps.changeControl.assessment.save")}
                  lockedReason={complete ? null : t("steps.changeControl.assessment.needsFields")}
                  busy={saver.busy}
                  onClick={() => void save()}
                />
                <GatedButton
                  label={t("steps.changeControl.assessment.sign")}
                  variant="outlined"
                  lockedReason={signLock}
                  onClick={() => setSigning(true)}
                />
              </>
            ) : null
          }
        >
          {!canAssess && (
            <Typography variant="body2" color="text.secondary">
              {t("steps.changeControl.assessment.withSectionHead")}
            </Typography>
          )}
          <TextField
            required
            multiline
            minRows={3}
            label={t("steps.changeControl.assessment.summary")}
            value={summary}
            disabled={!canAssess}
            onChange={(e) => edit(setSummary)(e.target.value)}
            slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
          />

          <Box component="section" aria-label={t("steps.changeControl.assessment.checklist")}>
            <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
              {t("steps.changeControl.assessment.checklist")}
            </Typography>
            {checklist.status === "loading" && (
              <Typography variant="body2" color="text.secondary" role="status">
                {t("steps.changeControl.assessment.checklistLoading")}
              </Typography>
            )}
            {checklist.status === "failed" && (
              <Alert severity="error" role="alert">
                {t("steps.changeControl.assessment.checklistFailed")}
              </Alert>
            )}
            {checklist.status === "ready" && checklist.items.length === 0 && (
              <Typography variant="body2" color="text.secondary">
                {t("steps.changeControl.assessment.noChecklist")}
              </Typography>
            )}
            <Stack spacing={2}>
              {checklist.items.map((item) => (
                <FormControl key={item.key} component="fieldset" disabled={!canAssess}>
                  <FormLabel component="legend">{item.text}</FormLabel>
                  <RadioGroup
                    row
                    value={checklist.answers[item.key]?.answer ?? ""}
                    onChange={(e) => {
                      checklist.setAnswer(item.key, { answer: ANSWERS.find((a) => a === e.target.value) ?? "" });
                      setDirty(true);
                    }}
                  >
                    {ANSWERS.map((answer) => (
                      <FormControlLabel key={answer} value={answer} control={<Radio />} label={t(`steps.changeControl.assessment.answers.${answer}`)} />
                    ))}
                  </RadioGroup>
                  <TextField
                    size="small"
                    label={t("steps.changeControl.assessment.itemComment")}
                    slotProps={{ htmlInput: { "aria-label": t("steps.changeControl.assessment.itemCommentFor", { item: item.text }), maxLength: TEXT_MAX } }}
                    value={checklist.answers[item.key]?.comment ?? ""}
                    onChange={(e) => {
                      checklist.setAnswer(item.key, { comment: e.target.value });
                      setDirty(true);
                    }}
                  />
                </FormControl>
              ))}
            </Stack>
          </Box>

          <Box component="section" aria-label={t("steps.changeControl.assessment.tasks")}>
            <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
              {t("steps.changeControl.assessment.tasks")}
            </Typography>
            <ActionRowsEditor
              rows={rows}
              onChange={edit(setRows)}
              lockedCount={savedTasks.length}
              noun="task"
              users={users}
              disabled={!canAssess}
            />
          </Box>
        </StepForm>
        <SignStepDialog
          open={signing}
          meaning="ChangeImpactAssessed"
          onCancel={() => setSigning(false)}
          onSign={async (password) => {
            await qualityService.signChangeControlAssessment(record.id, { password }, record.version);
            setSigning(false);
            await onChanged();
          }}
        />
      </PanelFrame>
    </Stack>
  );
}

