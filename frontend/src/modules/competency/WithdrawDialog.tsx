import { useState } from "react";
import { toast } from "sonner";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useTranslation } from "../../i18n";
import {
  competencyService,
  type AnalystQualificationDto
} from "../../services/competencyService";

export interface WithdrawDialogProps {
  open: boolean;
  qualificationId: number;
  analystName: string;
  testLabel: string;
  onClose: () => void;
  onWithdrawn: (qualification: AnalystQualificationDto) => void;
}

export function WithdrawDialog({
  open,
  qualificationId,
  analystName,
  testLabel,
  onClose,
  onWithdrawn
}: WithdrawDialogProps) {
  const { t } = useTranslation("competency");
  const [reason, setReason] = useState("");

  const handleCancel = () => {
    setReason("");
    onClose();
  };

  const handleConfirm = async (password: string) => {
    const updated = await competencyService.withdraw(qualificationId, {
      reason: reason.trim(),
      password
    });
    toast.success(t("withdraw.success"));
    setReason("");
    onWithdrawn(updated);
    onClose();
  };

  return (
    <SignatureDialog
      open={open}
      showComment
      commentRequired
      commentLabel={t("withdraw.reasonLabel")}
      comment={reason}
      onCommentChange={setReason}
      meaningStatement={t("withdraw.meaningStatement", {
        lng: "en",
        analyst: analystName,
        test: testLabel
      })}
      meaningStatementTranslation={t("withdraw.meaningStatementAr", {
        analyst: analystName,
        test: testLabel
      })}
      onCancel={handleCancel}
      onConfirm={handleConfirm}
    />
  );
}
