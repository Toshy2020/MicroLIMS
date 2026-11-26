import { useState } from "react";
import { MenuItem, TextField } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type ComplaintOutcome, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner } from "../steps/stepKit";

interface ComplaintConcludeFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

const OUTCOMES: ComplaintOutcome[] = ["Justified", "NotJustified", "Inconclusive"];

// QA concludes the complaint after the signed laboratory report: justified, not justified or inconclusive, with a
// summary. Signed.
export function ComplaintConcludeForm({ record, currentUserId, onChanged }: ComplaintConcludeFormProps) {
  const { t } = useTranslation("quality");
  const [outcome, setOutcome] = useState<ComplaintOutcome | "">("");
  const [summary, setSummary] = useState("");
  const [signing, setSigning] = useState(false);

  const lock = isRaiserOrOwner(record, currentUserId)
    ? t("steps.common.raiserOrOwner")
    : outcome === "" || !summary.trim()
      ? t("steps.complaint.conclude.needsFields")
      : null;

  return (
    <PanelFrame title={t("steps.complaint.conclude.title")}>
      <StepForm error={null} actions={<GatedButton label={t("steps.complaint.conclude.sign")} lockedReason={lock} onClick={() => setSigning(true)} />}>
        <TextField
          select
          required
          label={t("steps.complaint.conclude.outcome")}
          value={outcome}
          onChange={(e) => setOutcome(OUTCOMES.find((o) => o === e.target.value) ?? "")}
        >
          <MenuItem value="">{t("steps.common.choose")}</MenuItem>
          {OUTCOMES.map((value) => (
            <MenuItem key={value} value={value}>
              {t(`steps.complaint.conclude.outcomes.${value}`)}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          required
          multiline
          minRows={3}
          label={t("steps.complaint.conclude.summary")}
          value={summary}
          onChange={(e) => setSummary(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="ComplaintConcluded"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          if (outcome === "") return;
          await qualityService.concludeComplaint(record.id, { outcome, summary: summary.trim(), password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
