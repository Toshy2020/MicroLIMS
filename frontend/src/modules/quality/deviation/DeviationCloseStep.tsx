import { useState } from "react";
import { Button, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { APP_ROUTES } from "../../../routes/routes";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";
import { CreateCapaDialog } from "../capa/CreateCapaDialog";
import { GatedButton, SignStepDialog, StepForm, isRaiserOrOwner } from "../steps/stepKit";

interface DeviationCloseStepProps {
  record: QualityRecordDetailDto;
  permissions: QualityTypeActionsProps["permissions"];
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA closes an approved deviation. When the approval said a CAPA is required, the CAPA is raised from here and
// its plan must be approved before the server lets the deviation close; the server's refusal is shown in the
// signature dialog.
export function DeviationCloseStep({ record, permissions, currentUserId, onChanged }: DeviationCloseStepProps) {
  const { t } = useTranslation("quality");
  const navigate = useNavigate();
  const [signing, setSigning] = useState(false);
  const [creatingCapa, setCreatingCapa] = useState(false);
  const capaRequired = record.deviation?.capaRequired === true;
  const canRaiseCapa = permissions.approve || permissions.investigate;

  return (
    <PanelFrame title={t("steps.deviation.close.title")}>
      {!permissions.approve && !canRaiseCapa ? (
        <Typography variant="body2" color="text.secondary">
          {t("steps.deviation.close.waitingForQa")}
        </Typography>
      ) : (
        <StepForm
          error={null}
          actions={
            <>
              {capaRequired && canRaiseCapa && (
                <Button variant="outlined" onClick={() => setCreatingCapa(true)}>
                  {t("steps.deviation.close.createCapa")}
                </Button>
              )}
              {permissions.approve && (
                <GatedButton
                  label={t("steps.deviation.close.close")}
                  lockedReason={isRaiserOrOwner(record, currentUserId) ? t("steps.common.raiserOrOwner") : null}
                  onClick={() => setSigning(true)}
                />
              )}
            </>
          }
        >
          <Typography variant="body2">
            {capaRequired ? t("steps.deviation.close.capaRequired") : t("steps.deviation.close.noCapa")}
          </Typography>
        </StepForm>
      )}
      <SignStepDialog
        open={signing}
        meaning="QualityClosed"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.closeDeviation(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
      <CreateCapaDialog
        open={creatingCapa}
        source={{ id: record.id, number: record.number, sectionId: record.sectionId }}
        onClose={() => setCreatingCapa(false)}
        onCreated={(created) => {
          setCreatingCapa(false);
          navigate(APP_ROUTES.QUALITY_DETAIL(created.id));
        }}
      />
    </PanelFrame>
  );
}
