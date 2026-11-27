import { Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import type { QualityTypeActionsProps } from "../typeActions";
import { CancelRecordAction, isOpenRecord } from "../steps/stepKit";
import { CapaEffectivenessForm } from "./CapaEffectivenessForm";
import { CapaPlanDecision } from "./CapaPlanDecision";
import { CapaPlanForm } from "./CapaPlanForm";
import { CapaVerifyStep } from "./CapaVerifyStep";

function Waiting({ title, text }: { title: string; text: string }) {
  return (
    <PanelFrame title={title}>
      <Typography variant="body2" color="text.secondary">
        {text}
      </Typography>
    </PanelFrame>
  );
}

// The steps of a CAPA, one form for the step it is in: plan (owner) and its approval (QA), implementation (each
// responsible person, in the actions list), completion verification (QA) and the effectiveness check (QA). Creating
// a CAPA from a deviation is a step of the deviation.
export function CapaActions({ record, permissions, currentUserId, onChanged }: QualityTypeActionsProps) {
  const { t } = useTranslation("quality");
  const status = record.capa?.status;
  if (!status) return null;

  if (!isOpenRecord(record)) {
    return <Waiting title={t("steps.common.title")} text={t("steps.common.noFurtherSteps")} />;
  }

  return (
    <Stack spacing={2}>
      {status === "Plan" &&
        (permissions.approve ? (
          <CapaPlanDecision record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <CapaPlanForm record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ))}
      {status === "PlanApproved" &&
        (permissions.approve ? (
          <CapaVerifyStep record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.capa.implementation.title")} text={t("steps.capa.implementation.text")} />
        ))}
      {status === "CompletionVerified" &&
        (permissions.approve ? (
          <CapaEffectivenessForm record={record} currentUserId={currentUserId} onChanged={onChanged} />
        ) : (
          <Waiting title={t("steps.capa.effectiveness.title")} text={t("steps.capa.effectiveness.waitingForQa")} />
        ))}
      <CancelRecordAction record={record} canApprove={permissions.approve} currentUserId={currentUserId} onChanged={onChanged} />
    </Stack>
  );
}
