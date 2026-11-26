import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { useClassificationOptions, useUserOptions } from "../steps/lookups";
import { DestructiveSignAction, GatedButton, LookupSelect, SignStepDialog, StepForm } from "../steps/stepKit";

interface AssessDeviationFormProps {
  record: QualityRecordDetailDto;
  onChanged: () => Promise<void>;
}

// QA accepts the deviation (classification, owner, optional due date; signed) or rejects it as "not a deviation"
// (reason; signed).
export function AssessDeviationForm({ record, onChanged }: AssessDeviationFormProps) {
  const { t } = useTranslation("quality");
  const classifications = useClassificationOptions();
  const users = useUserOptions();
  const [classificationId, setClassificationId] = useState<number | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | "">("");
  const [dueDate, setDueDate] = useState("");
  const [signing, setSigning] = useState(false);

  const incomplete = classificationId === "" || ownerUserId === "";

  return (
    <PanelFrame title={t("steps.deviation.assess.title")}>
      <StepForm
        error={null}
        actions={
          <>
            <GatedButton
              label={t("steps.deviation.assess.accept")}
              lockedReason={incomplete ? t("steps.deviation.assess.needsFields") : null}
              onClick={() => setSigning(true)}
            />
            <DestructiveSignAction
              label={t("steps.deviation.assess.reject")}
              confirmTitle={t("steps.deviation.assess.rejectConfirmTitle")}
              confirmMessage={t("steps.deviation.assess.rejectConfirmMessage", { number: record.number })}
              reasonLabel={t("steps.deviation.assess.rejectReason")}
              meaning="DeviationRejected"
              onSign={async (password, reason) => {
                await qualityService.rejectDeviation(record.id, { reason, password }, record.version);
                await onChanged();
              }}
            />
          </>
        }
      >
        <Typography variant="body2" color="text.secondary">
          {t("steps.deviation.assess.intro")}
        </Typography>
        <LookupSelect
          required
          label={t("steps.deviation.assess.classification")}
          value={classificationId}
          onChange={setClassificationId}
          lookup={classifications}
        />
        <LookupSelect required label={t("steps.deviation.assess.owner")} value={ownerUserId} onChange={setOwnerUserId} lookup={users} />
        <TextField
          type="date"
          label={t("steps.deviation.assess.dueDate")}
          helperText={t("steps.deviation.assess.dueDateHelp")}
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="DeviationAssessed"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          if (classificationId === "" || ownerUserId === "") return;
          await qualityService.assessDeviation(
            record.id,
            { accept: true, classificationId, ownerUserId, dueDate: dueDate || null, password },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
