import { useEffect, useState } from "react";
import { Button, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../../i18n";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";

interface MaterialReleaseDialogProps {
  open: boolean;
  material: MaterialItem | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// Release gate for a lot in a category that needs approval before use. The server refuses the
// person who received the lot and shows its message in the signature dialog.
// The recorded meaning of the signature is always the English text; the screen language only adds a translation.
const tEn = i18n.getFixedT("en", "inventory");

function buildLabel(tf: TFunction, material: MaterialItem): string {
  return tf("materials.release.lotLabel", { name: material.materialName, lot: material.batchNumber });
}

function buildMeaning(tf: TFunction, label: string, approved: boolean, comment: string): string {
  if (!approved) return tf("materials.release.meaningReject", { label, comment });
  return comment ? tf("materials.release.meaningApproveComment", { label, comment }) : tf("materials.release.meaningApprove", { label });
}

export function MaterialReleaseDialog({ open, material, onClose, onSuccess }: MaterialReleaseDialogProps) {
  const { t } = useTranslation("inventory");
  const [decision, setDecision] = useState<"approve" | "reject">("approve");
  const [comment, setComment] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setDecision("approve");
      setComment("");
      setSignatureOpen(false);
    }
  }, [open, material]);

  if (!material) return null;

  const approved = decision === "approve";
  const trimmedComment = comment.trim();
  const label = buildLabel(t, material);
  const meaningStatement = buildMeaning(tEn, buildLabel(tEn, material), approved, trimmedComment);
  const meaningStatementTranslation = buildMeaning(t, label, approved, trimmedComment);

  const handleConfirmSignature = async (password: string) => {
    await MaterialService.release(material.id, { password, approved, comment: trimmedComment || null });
    setSignatureOpen(false);
    onSuccess(approved ? t("materials.release.released", { label }) : t("materials.release.rejectedMessage", { label }));
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={t("materials.release.title", { name: material.materialName })}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>{t("common.cancel")}</Button>
            <Button
              variant="contained"
              disabled={!approved && !trimmedComment}
              onClick={() => setSignatureOpen(true)}
            >
              {t("materials.release.continue")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {material.categoryName
              ? t("materials.release.introCategory", { lot: material.batchNumber, category: material.categoryName })
              : t("materials.release.intro", { lot: material.batchNumber })}
          </Typography>
          <FormControl>
            <FormLabel id="material-release-decision-label">{t("materials.release.decision")}</FormLabel>
            <RadioGroup
              row
              aria-labelledby="material-release-decision-label"
              value={decision}
              onChange={(e) => setDecision(e.target.value as "approve" | "reject")}
            >
              <FormControlLabel value="approve" control={<Radio />} label={t("materials.release.approve")} />
              <FormControlLabel value="reject" control={<Radio />} label={t("materials.release.reject")} />
            </RadioGroup>
          </FormControl>
          <TextField
            label={t("materials.release.comment")}
            required={!approved}
            multiline
            rows={3}
            size="small"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={approved ? t("materials.release.optional") : t("materials.release.rejectPlaceholder")}
            helperText={approved ? undefined : t("materials.release.requiredToReject")}
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
