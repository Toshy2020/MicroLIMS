import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { useClassificationOptions, useUserOptions } from "../steps/lookups";
import { GatedButton, LookupSelect, StepForm, TEXT_MAX, useStepRunner } from "../steps/stepKit";

interface RequestInvestigationFormProps {
  record: QualityRecordDetailDto;
  onChanged: () => Promise<void>;
}

// QA asks the laboratory to investigate: classification, owner, optional due date, and what to test. Not signed.
export function RequestInvestigationForm({ record, onChanged }: RequestInvestigationFormProps) {
  const { t } = useTranslation("quality");
  const classifications = useClassificationOptions();
  const users = useUserOptions();
  const [classificationId, setClassificationId] = useState<number | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | "">("");
  const [dueDate, setDueDate] = useState("");
  const [whatToTest, setWhatToTest] = useState("");
  const runner = useStepRunner(onChanged);

  const complete = classificationId !== "" && ownerUserId !== "" && Boolean(whatToTest.trim());

  const submit = () => {
    if (classificationId === "" || ownerUserId === "") return;
    void runner.run(
      () =>
        qualityService.requestComplaintInvestigation(
          record.id,
          { classificationId, ownerUserId, dueDate: dueDate || null, whatToTest: whatToTest.trim() },
          record.version
        ),
      t("steps.complaint.request.failed")
    );
  };

  return (
    <PanelFrame title={t("steps.complaint.request.title")}>
      <StepForm
        error={runner.error}
        actions={
          <GatedButton
            label={t("steps.complaint.request.submit")}
            lockedReason={complete ? null : t("steps.complaint.request.needsFields")}
            busy={runner.busy}
            onClick={submit}
          />
        }
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.complaint.request.intro")}
        </Typography>
        <LookupSelect
          required
          label={t("steps.complaint.request.classification")}
          value={classificationId}
          onChange={setClassificationId}
          lookup={classifications}
        />
        <LookupSelect required label={t("steps.complaint.request.owner")} value={ownerUserId} onChange={setOwnerUserId} lookup={users} />
        <TextField
          type="date"
          label={t("steps.complaint.request.dueDate")}
          helperText={t("steps.complaint.request.dueDateHelp")}
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          required
          multiline
          minRows={2}
          label={t("steps.complaint.request.whatToTest")}
          helperText={t("steps.complaint.request.whatToTestHelp")}
          value={whatToTest}
          onChange={(e) => setWhatToTest(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </StepForm>
    </PanelFrame>
  );
}
