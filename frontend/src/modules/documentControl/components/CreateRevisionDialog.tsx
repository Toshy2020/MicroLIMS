import React, { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  FormLabel,
  RadioGroup,
  FormControlLabel,
  Radio,
  Alert,
  Box,
  Typography,
  CircularProgress,
  Divider,
  Checkbox
} from "@mui/material";
import { documentRevisionService } from "../services/documentRevisionService";
import type { RevisionType, DocumentRevisionDto } from "../types/documentControlTypes";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

interface CreateRevisionDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (newRev: DocumentRevisionDto) => void;
  masterId: number;
  companyDocumentCode: string;
  currentEffectiveRevisionNumber: string;
  isController: boolean;
}

export const CreateRevisionDialog: React.FC<CreateRevisionDialogProps> = ({
  open,
  onClose,
  onCreated,
  masterId,
  companyDocumentCode,
  currentEffectiveRevisionNumber,
  isController
}) => {
  const { t } = useTranslation("documentControl");
  const [revisionType, setRevisionType] = useState<RevisionType>("Major");
  const [proposedNumber, setProposedNumber] = useState<string>("");
  const [reasonForRevision, setReasonForRevision] = useState<string>("");
  const [changeSummary, setChangeSummary] = useState<string>("");
  const [changeReference, setChangeReference] = useState<string>("");

  const [allowOverride, setAllowOverride] = useState<boolean>(false);
  const [customRevisionNumber, setCustomRevisionNumber] = useState<string>("");

  const [loadingProposed, setLoadingProposed] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setError(null);
      setReasonForRevision("");
      setChangeSummary("");
      setChangeReference("");
      setAllowOverride(false);
      setCustomRevisionNumber("");
      loadProposedNumber(revisionType);
    }
  }, [open]);

  const loadProposedNumber = async (type: RevisionType) => {
    setLoadingProposed(true);
    setError(null);
    try {
      const res = await documentRevisionService.proposeNextRevision(masterId, type);
      setProposedNumber(res.proposedRevisionNumber);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("createRevision.proposeError")));
    } finally {
      setLoadingProposed(false);
    }
  };

  const handleTypeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value as RevisionType;
    setRevisionType(val);
    loadProposedNumber(val);
  };

  const isReasonValid = reasonForRevision.trim().length >= 10;
  const isSummaryValid = !changeSummary.trim() || changeSummary.trim().length >= 10;
  const isCustomNumberValid = !allowOverride || (customRevisionNumber.trim().length > 0 && /^[a-zA-Z0-9.-_]{1,20}$/.test(customRevisionNumber.trim()));

  const canSubmit = isReasonValid && isSummaryValid && isCustomNumberValid && !submitting && !loadingProposed;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);

    try {
      const result = await documentRevisionService.createRevisionFromEffective(masterId, {
        revisionType,
        reasonForRevision: reasonForRevision.trim(),
        changeSummary: changeSummary.trim() || undefined,
        changeReference: changeReference.trim() || undefined,
        customRevisionNumber: allowOverride && customRevisionNumber.trim() ? customRevisionNumber.trim() : undefined
      });
      onCreated(result);
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("createRevision.createError")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>
        {t("createRevision.title", { code: companyDocumentCode })}
      </DialogTitle>
      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5, mt: 1 }}>
          <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
            {t("createRevision.info", { rev: currentEffectiveRevisionNumber })}
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          {/* Revision Type */}
          <FormControl component="fieldset">
            <FormLabel component="legend" sx={{ fontWeight: 600, fontSize: "0.9rem" }}>
              {t("createRevision.classificationLabel")}
            </FormLabel>
            <RadioGroup row value={revisionType} onChange={handleTypeChange}>
              <FormControlLabel value="Major" control={<Radio />} label={t("createRevision.majorLabel")} />
              <FormControlLabel value="Minor" control={<Radio />} label={t("createRevision.minorLabel")} />
            </RadioGroup>
          </FormControl>

          {/* Proposed Number Highlight */}
          <Box sx={{ p: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.100", borderRadius: 1.5, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("createRevision.proposedNumberLabel")}
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700, color: "primary.main" }}>
                {loadingProposed ? <CircularProgress size={20} /> : t("common.rev", { number: proposedNumber || "—" })}
              </Typography>
            </Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {t("createRevision.currentLabel", { rev: currentEffectiveRevisionNumber })}
            </Typography>
          </Box>

          {/* Document Controller Override Option */}
          {isController && (
            <Box sx={{ p: 1.5, border: "1px dashed", borderColor: "divider", borderRadius: 1 }}>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={allowOverride}
                    onChange={(e) => setAllowOverride(e.target.checked)}
                    size="small"
                  />
                }
                label={<Typography variant="body2" sx={{ fontWeight: 500 }}>{t("createRevision.overrideLabel")}</Typography>}
              />
              {allowOverride && (
                <TextField
                  label={t("createRevision.customNumberLabel")}
                  size="small"
                  fullWidth
                  value={customRevisionNumber}
                  onChange={(e) => setCustomRevisionNumber(e.target.value)}
                  placeholder={t("createRevision.customNumberPlaceholder")}
                  helperText={t("createRevision.customNumberHelper")}
                  sx={{ mt: 1 }}
                />
              )}
            </Box>
          )}

          <Divider />

          {/* Mandatory Reason for Revision */}
          <TextField
            label={t("createRevision.reasonLabel")}
            multiline
            rows={3}
            required
            value={reasonForRevision}
            onChange={(e) => setReasonForRevision(e.target.value)}
            placeholder={t("createRevision.reasonPlaceholder")}
            helperText={t("createRevision.reasonHelper", { count: reasonForRevision.trim().length })}
            error={reasonForRevision.length > 0 && !isReasonValid}
            fullWidth
          />

          {/* Change Summary */}
          <TextField
            label={t("createRevision.summaryLabel")}
            multiline
            rows={3}
            value={changeSummary}
            onChange={(e) => setChangeSummary(e.target.value)}
            placeholder={t("createRevision.summaryPlaceholder")}
            helperText={changeSummary.length > 0 && !isSummaryValid ? t("createRevision.summaryTooShort") : t("createRevision.summaryOptional")}
            error={changeSummary.length > 0 && !isSummaryValid}
            fullWidth
          />

          {/* Change Reference */}
          <TextField
            label={t("createRevision.referenceLabel")}
            size="small"
            value={changeReference}
            onChange={(e) => setChangeReference(e.target.value)}
            placeholder={t("createRevision.referencePlaceholder")}
            helperText={t("createRevision.referenceHelper")}
            fullWidth
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!canSubmit}
        >
          {submitting ? <CircularProgress size={24} /> : t("createRevision.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
