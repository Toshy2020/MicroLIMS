import { Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import type { QualityTypeActionsProps } from "../typeActions";
import { CancelRecordAction, isOpenRecord, isRaiserOrOwner } from "../steps/stepKit";
import { ChangeControlAssessmentForm } from "./ChangeControlAssessmentForm";
import { ChangeControlCloseForm } from "./ChangeControlCloseForm";
import { ChangeControlDecisionForm, RejectChangeControl } from "./ChangeControlDecisionForm";

function Waiting({ title, text }: { title: string; text: string }) {
  return (
    <PanelFrame title={title}>
      <Typography variant="body2" color="text.secondary">
        {text}
      </Typography>
    </PanelFrame>
  );
}

// The steps of a change control, one form for the step it is in: proposal and impact assessment (Section Head),
// approval or rejection (QA), implementation (task owners, in the actions list) and closure (QA). Proposing a change
// control is done from the register.
export function ChangeControlActions({ record, permissions, currentUserId, onChanged }: QualityTypeActionsProps) {
  const { t } = useTranslation("quality");
  const status = record.changeControl?.status;
  if (!status) return null;

  if (!isOpenRecord(record)) {
    return <Waiting title={t("steps.common.title")} text={t("steps.common.noFurtherSteps")} />;
  }

  const rejectLock = isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null;

  return (
    <Stack spacing={2}>
      {status === "Proposed" && (
        <>
          <ChangeControlAssessmentForm record={record} permissions={permissions} currentUserId={currentUserId} onChanged={onChanged} />
          {permissions.approve && (
            <PanelFrame title={t("steps.changeControl.decision.rejectTitle")}>
              <RejectChangeControl record={record} lockedReason={rejectLock} onChanged={onChanged} />
            </PanelFrame>
          )}
        </>
      )}
      {status === "Assessed" &&
        (permissions.approve ? (
          <ChangeControlDecisionForm record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.changeControl.decision.title")} text={t("steps.changeControl.decision.waitingForQa")} />
        ))}
      {status === "ApprovedForImplementation" &&
        (permissions.approve ? (
          <ChangeControlCloseForm record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.changeControl.implementation.title")} text={t("steps.changeControl.implementation.text")} />
        ))}
      <CancelRecordAction record={record} canApprove={permissions.approve} currentUserId={currentUserId} onChanged={onChanged} />
    </Stack>
  );
}
