import { useEffect, useState } from "react";
import { MenuItem, TextField } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import {
  qualityService,
  type ComplaintOutcome,
  type QualityRecordDetailDto,
  type QualityRecordListItemDto
} from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner } from "../steps/stepKit";

interface ComplaintConcludeFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

const OUTCOMES: ComplaintOutcome[] = ["Justified", "NotJustified", "Inconclusive"];

// QA concludes the complaint after the signed laboratory report: justified (requires a linked follow-up deviation or
// CAPA), not justified or inconclusive, with a summary. Signed.
export function ComplaintConcludeForm({ record, currentUserId, onChanged }: ComplaintConcludeFormProps) {
  const { t } = useTranslation("quality");
  const [outcome, setOutcome] = useState<ComplaintOutcome | "">("");
  const [summary, setSummary] = useState("");
  const [followUpRecordId, setFollowUpRecordId] = useState<number | "">("");
  const [followUpOptions, setFollowUpOptions] = useState<QualityRecordListItemDto[]>([]);
  const [signing, setSigning] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      qualityService.listRecords({ type: "Deviation", pageSize: 100 }, controller.signal),
      qualityService.listRecords({ type: "Capa", pageSize: 100 }, controller.signal)
    ])
      .then(([devs, capas]) => {
        if (controller.signal.aborted) return;
        const seen = new Set<number>();
        const combined = [...devs.items, ...capas.items].filter((item) => {
          if (item.isCancelled || item.id === record.id || seen.has(item.id)) return false;
          seen.add(item.id);
          return true;
        });
        setFollowUpOptions(combined);
      })
      .catch(() => {
        if (!controller.signal.aborted) setFollowUpOptions([]);
      });
    return () => controller.abort();
  }, [record.id]);

  const needsFollowUp = outcome === "Justified" && followUpRecordId === "";
  const lock = isRaiserOrOwner(record, currentUserId)
    ? t("steps.common.raiserOrOwner")
    : outcome === "" || !summary.trim()
      ? t("steps.complaint.conclude.needsFields")
      : needsFollowUp
        ? t("steps.complaint.conclude.needsFollowUp")
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
          select
          required={outcome === "Justified"}
          label={outcome === "Justified" ? t("steps.complaint.conclude.followUp") : t("steps.complaint.conclude.followUpOptional")}
          value={followUpRecordId}
          onChange={(e) => setFollowUpRecordId(e.target.value === "" ? "" : Number(e.target.value))}
        >
          <MenuItem value="">{t("steps.complaint.conclude.followUpNone")}</MenuItem>
          {followUpOptions.map((item) => (
            <MenuItem key={item.id} value={item.id}>
              {`${item.number} · ${item.title}`}
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
          await qualityService.concludeComplaint(
            record.id,
            {
              outcome,
              summary: summary.trim(),
              password,
              ...(followUpRecordId === "" ? {} : { followUpRecordId })
            },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
