import { useState, useEffect } from "react";
import { Button, TextField, Stack } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { i18n, useTranslation } from "../../../i18n";
import { ReferenceOrganismService, ReferenceOrganism, REFERENCE_ORGANISM_CODE_PREFIX } from "./services/ReferenceOrganismService";

interface ChangeReferenceOrganismCodeDialogProps {
  open: boolean;
  strain: ReferenceOrganism | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ChangeReferenceOrganismCodeDialog({ open, strain, onClose, onSuccess }: ChangeReferenceOrganismCodeDialogProps) {
  const { t } = useTranslation("masterData");
  const [newCode, setNewCode] = useState("");
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setNewCode("");
      setReason("");
      setSignatureOpen(false);
    }
  }, [open, strain]);

  if (!strain) return null;

  const trimmedNewCode = newCode.trim().toUpperCase();
  const trimmedReason = reason.trim();

  const meaningParams = {
    name: strain.name,
    oldCode: strain.code,
    newCode: trimmedNewCode,
    reason: trimmedReason
  };
  const meaningStatement = i18n.getFixedT("en", "masterData")("changeOrganismCode.meaningStatement", meaningParams);
  const meaningStatementTranslation = t("changeOrganismCode.meaningStatement", meaningParams);

  const handleConfirmSignature = async (password: string) => {
    await ReferenceOrganismService.changeCode(strain.id, trimmedNewCode, trimmedReason, password);
    setSignatureOpen(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={t("changeOrganismCode.title", { name: strain.name })}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>{t("common.cancel")}</Button>
            <Button
              variant="contained"
              disabled={!trimmedNewCode || !trimmedReason}
              onClick={() => setSignatureOpen(true)}
            >
              {t("changeOrganismCode.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label={t("changeOrganismCode.currentCodeLabel")} value={strain.code} slotProps={{ input: { readOnly: true } }} size="small" />
          <TextField
            label={t("changeOrganismCode.newCodeLabel")}
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value.toUpperCase())}
            placeholder={t("changeOrganismCode.newCodePlaceholder", { prefix: REFERENCE_ORGANISM_CODE_PREFIX })}
            helperText={t("changeOrganismCode.newCodeHelper", { prefix: REFERENCE_ORGANISM_CODE_PREFIX })}
            size="small"
            autoFocus
          />
          <TextField
            label={t("changeOrganismCode.reasonLabel")}
            required
            multiline
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("changeOrganismCode.reasonPlaceholder")}
            size="small"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
