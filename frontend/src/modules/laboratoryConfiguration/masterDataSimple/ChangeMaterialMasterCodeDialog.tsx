import { useState, useEffect } from "react";
import { Button, TextField, Stack } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { i18n, useTranslation } from "../../../i18n";
import { MaterialMasterService, MaterialMasterEntry, MATERIAL_MASTER_CODE_PREFIX } from "./services/MaterialMasterService";

interface ChangeMaterialMasterCodeDialogProps {
  open: boolean;
  entry: MaterialMasterEntry | null;
  // Other materials: the code prefix of the entry's material category (without the dash).
  categoryPrefix?: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function ChangeMaterialMasterCodeDialog({ open, entry, categoryPrefix, onClose, onSuccess }: ChangeMaterialMasterCodeDialogProps) {
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
  }, [open, entry]);

  if (!entry) return null;

  const prefix = entry.category === "Other" ? `${categoryPrefix ?? ""}-` : MATERIAL_MASTER_CODE_PREFIX[entry.category];
  const trimmedNewCode = newCode.trim().toUpperCase();
  const trimmedReason = reason.trim();

  const meaningParams = {
    name: entry.name,
    oldCode: entry.code,
    newCode: trimmedNewCode,
    reason: trimmedReason
  };
  const meaningStatement = i18n.getFixedT("en", "masterData")("changeMaterialCode.meaningStatement", meaningParams);
  const meaningStatementTranslation = t("changeMaterialCode.meaningStatement", meaningParams);

  const handleConfirmSignature = async (password: string) => {
    await MaterialMasterService.changeCode(entry.id, trimmedNewCode, trimmedReason, password);
    setSignatureOpen(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={t("changeMaterialCode.title", { name: entry.name })}
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
              {t("changeMaterialCode.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label={t("changeMaterialCode.currentCodeLabel")} value={entry.code} slotProps={{ input: { readOnly: true } }} size="small" />
          <TextField
            label={t("changeMaterialCode.newCodeLabel")}
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value.toUpperCase())}
            placeholder={t("changeMaterialCode.newCodePlaceholder", { prefix })}
            helperText={t("changeMaterialCode.newCodeHelper", { prefix })}
            size="small"
            autoFocus
          />
          <TextField
            label={t("changeMaterialCode.reasonLabel")}
            required
            multiline
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("changeMaterialCode.reasonPlaceholder")}
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
