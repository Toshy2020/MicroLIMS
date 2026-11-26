import { useState } from "react";
import { Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";
import { CancelRecordAction, GatedButton, SignStepDialog, StepForm, isOpenRecord, isRaiserOrOwner } from "../steps/stepKit";
import { ComplaintConcludeForm } from "./ComplaintConcludeForm";
import { ComplaintInvestigationForm } from "./ComplaintInvestigationForm";
import { RequestInvestigationForm } from "./RequestInvestigationForm";

function Waiting({ title, text }: { title: string; text: string }) {
  return (
    <PanelFrame title={title}>
      <Typography variant="body2" color="text.secondary">
        {text}
      </Typography>
    </PanelFrame>
  );
}

// The steps of a complaint, one form for the step it is in: request the laboratory investigation (QA), investigation
// and laboratory report (owner, Section Head), conclusion (QA) and closure (QA). Receiving a complaint is done from
// the register.
export function ComplaintActions({ record, permissions, currentUserId, onChanged }: QualityTypeActionsProps) {
  const { t } = useTranslation("quality");
  const [closing, setClosing] = useState(false);
  const status = record.complaint?.status;
  if (!status) return null;

  if (!isOpenRecord(record)) {
    return <Waiting title={t("steps.common.title")} text={t("steps.common.noFurtherSteps")} />;
  }

  return (
    <Stack spacing={2}>
      {status === "Received" &&
        (permissions.approve ? (
          <RequestInvestigationForm record={record} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.complaint.request.title")} text={t("steps.complaint.request.waitingForQa")} />
        ))}
      {status === "Investigation" && (
        <ComplaintInvestigationForm record={record} permissions={permissions} currentUserId={currentUserId} onChanged={onChanged} />
      )}
      {status === "LabReported" &&
        (permissions.approve ? (
          <ComplaintConcludeForm record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.complaint.conclude.title")} text={t("steps.complaint.conclude.waitingForQa")} />
        ))}
      {status === "Concluded" &&
        (permissions.approve ? (
          <PanelFrame title={t("steps.complaint.close.title")}>
            <StepForm
              error={null}
              actions={
                <GatedButton
                  label={t("steps.complaint.close.close")}
                  lockedReason={isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null}
                  onClick={() => setClosing(true)}
                />
              }
            >
              <Typography variant="body2" color="text.secondary">
                {t("steps.complaint.close.intro")}
              </Typography>
            </StepForm>
            <SignStepDialog
              open={closing}
              meaning="QualityClosed"
              onCancel={() => setClosing(false)}
              onSign={async (password) => {
                await qualityService.closeComplaint(record.id, { password }, record.version);
                setClosing(false);
                await onChanged();
              }}
            />
          </PanelFrame>
        ) : (
          <Waiting title={t("steps.complaint.close.title")} text={t("steps.complaint.close.waitingForQa")} />
        ))}
      <CancelRecordAction record={record} canApprove={permissions.approve} onChanged={onChanged} />
    </Stack>
  );
}
