import { Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import type { QualityTypeActionsProps } from "../typeActions";
import { CancelRecordAction, isOpenRecord } from "../steps/stepKit";
import { AssessDeviationForm } from "./AssessDeviationForm";
import { DeviationCloseStep } from "./DeviationCloseStep";
import { DeviationDecisionForm } from "./DeviationDecisionForm";
import { DeviationInvestigationForm } from "./DeviationInvestigationForm";

// The steps of a deviation, one form for the step the record is in: assess (QA), investigation and laboratory
// conclusion (owner, Section Head), approval (QA), closure (QA). The server decides what is allowed; each form
// only offers what the record's state and the person's permissions make possible.
export function DeviationActions({ record, permissions, currentUserId, onChanged }: QualityTypeActionsProps) {
  const { t } = useTranslation("quality");
  const status = record.deviation?.status;
  if (!status) return null;

  if (!isOpenRecord(record)) {
    return (
      <PanelFrame title={t("steps.common.title")}>
        <Typography variant="body2" color="text.secondary">
          {t("steps.common.noFurtherSteps")}
        </Typography>
      </PanelFrame>
    );
  }

  return (
    <Stack spacing={2}>
      {status === "Raised" &&
        (permissions.approve ? (
          <AssessDeviationForm record={record} onChanged={onChanged} />
        ) : (
          <PanelFrame title={t("steps.deviation.assess.title")}>
            <Typography variant="body2" color="text.secondary">
              {t("steps.deviation.assess.waitingForQa")}
            </Typography>
          </PanelFrame>
        ))}
      {status === "Investigation" && (
        <DeviationInvestigationForm record={record} permissions={permissions} currentUserId={currentUserId} onChanged={onChanged} />
      )}
      {status === "LabConcluded" && (
        <DeviationDecisionForm record={record} canApprove={permissions.approve} currentUserId={currentUserId} onChanged={onChanged} />
      )}
      {status === "Approved" && (
        <DeviationCloseStep record={record} permissions={permissions} currentUserId={currentUserId} onChanged={onChanged} />
      )}
      <CancelRecordAction record={record} canApprove={permissions.approve} onChanged={onChanged} />
    </Stack>
  );
}
