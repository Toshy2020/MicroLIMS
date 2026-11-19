import { useEffect, useState } from "react";
import { Button, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { i18n, useTranslation } from "../../../i18n";
import { SolutionPreparationService } from "../services/SolutionPreparationService";

interface Props {
  open: boolean;
  preparationId: number;
  code: string | null;
  solutionName: string;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// Release gate for a prepared solution whose recipe needs release before use. The comment
// is asked for when rejecting, but the server decides: its message (comment missing, same
// person who prepared it, wrong password) is shown in the signature dialog.
export function ReleasePreparationDialog({ open, preparationId, code, solutionName, onClose, onSuccess }: Props) {
  const { t } = useTranslation("solutionPreparation");
  const tEn = i18n.getFixedT("en", "solutionPreparation");
  const [decision, setDecision] = useState<"approve" | "reject">("approve");
  const [comment, setComment] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setDecision("approve");
      setComment("");
      setSignatureOpen(false);
    }
  }, [open, preparationId]);

  const approved = decision === "approve";
  const trimmedComment = comment.trim();
  const label = `${solutionName}${code ? ` (${code})` : ""}`;
  const meaningKey = approved
    ? (trimmedComment ? "releaseDialog.meaningApproveWithComment" : "releaseDialog.meaningApprove")
    : (trimmedComment ? "releaseDialog.meaningRejectWithComment" : "releaseDialog.meaningReject");
  const meaningParams = { label, comment: trimmedComment };
  const meaningStatement = tEn(meaningKey, meaningParams);
  const meaningStatementTranslation = t(meaningKey, meaningParams);

  const handleConfirmSignature = async (password: string) => {
    await SolutionPreparationService.release(preparationId, {
      approved,
      comment: trimmedComment || null,
      password
    });
    setSignatureOpen(false);
    onSuccess(approved ? t("releaseDialog.toastApproved", { label }) : t("releaseDialog.toastRejected", { label }));
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={t("releaseDialog.title", { solutionName })}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>{t("releaseDialog.cancel")}</Button>
            <Button variant="contained" onClick={() => setSignatureOpen(true)}>
              {t("releaseDialog.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {code
              ? t("releaseDialog.descriptionWithCode", { code })
              : t("releaseDialog.description")}
          </Typography>
          <FormControl>
            <FormLabel id="solution-release-decision-label">{t("releaseDialog.decision")}</FormLabel>
            <RadioGroup
              row
              aria-labelledby="solution-release-decision-label"
              value={decision}
              onChange={(e) => setDecision(e.target.value as "approve" | "reject")}
            >
              <FormControlLabel value="approve" control={<Radio />} label={t("releaseDialog.approve")} />
              <FormControlLabel value="reject" control={<Radio />} label={t("releaseDialog.reject")} />
            </RadioGroup>
          </FormControl>
          <TextField
            label={t("releaseDialog.comment")}
            multiline
            rows={3}
            size="small"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={approved ? t("releaseDialog.placeholderOptional") : t("releaseDialog.placeholderReject")}
            helperText={approved ? undefined : t("releaseDialog.helperReject")}
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
