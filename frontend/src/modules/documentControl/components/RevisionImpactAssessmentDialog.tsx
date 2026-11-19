import React, { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControlLabel,
  Switch,
  Box,
  Typography,
  Alert,
  CircularProgress,
  Divider,
  Paper
} from "@mui/material";
import { documentRevisionService } from "../services/documentRevisionService";
import type {
  RevisionImpactAssessmentDto,
  SaveImpactAssessmentRequest
} from "../types/documentControlTypes";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

interface RevisionImpactAssessmentDialogProps {
  open: boolean;
  onClose: () => void;
  revisionId: number;
  revisionNumber: string;
  isEditable: boolean;
  onSaved?: () => void;
}

interface CategoryState {
  key: string;
  label: string;
  hasImpact: boolean;
  details: string;
}

export const RevisionImpactAssessmentDialog: React.FC<RevisionImpactAssessmentDialogProps> = ({
  open,
  onClose,
  revisionId,
  revisionNumber,
  isEditable,
  onSaved
}) => {
  const { t } = useTranslation("documentControl");
  const [loading, setLoading] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [existingAssessment, setExistingAssessment] = useState<RevisionImpactAssessmentDto | null>(null);

  const [categories, setCategories] = useState<CategoryState[]>([
    { key: "procedure", label: "impactAssessment.categories.procedure", hasImpact: false, details: "" },
    { key: "training", label: "impactAssessment.categories.training", hasImpact: false, details: "" },
    { key: "forms", label: "impactAssessment.categories.forms", hasImpact: false, details: "" },
    { key: "specifications", label: "impactAssessment.categories.specifications", hasImpact: false, details: "" },
    { key: "equipment", label: "impactAssessment.categories.equipment", hasImpact: false, details: "" },
    { key: "materials", label: "impactAssessment.categories.materials", hasImpact: false, details: "" },
    { key: "validation", label: "impactAssessment.categories.validation", hasImpact: false, details: "" },
    { key: "regulatory", label: "impactAssessment.categories.regulatory", hasImpact: false, details: "" },
    { key: "relatedDocs", label: "impactAssessment.categories.relatedDocs", hasImpact: false, details: "" }
  ]);

  useEffect(() => {
    if (open) {
      setError(null);
      setSuccess(null);
      loadAssessment();
    }
  }, [open, revisionId]);

  const loadAssessment = async () => {
    setLoading(true);
    try {
      const data = await documentRevisionService.getImpactAssessment(revisionId);
      setExistingAssessment(data);
      if (data) {
        setCategories([
          { key: "procedure", label: "impactAssessment.categories.procedure", hasImpact: data.procedureOrMethodImpact, details: data.procedureOrMethodDetails || "" },
          { key: "training", label: "impactAssessment.categories.training", hasImpact: data.trainingImpact, details: data.trainingDetails || "" },
          { key: "forms", label: "impactAssessment.categories.forms", hasImpact: data.formsOrTemplatesImpact, details: data.formsOrTemplatesDetails || "" },
          { key: "specifications", label: "impactAssessment.categories.specifications", hasImpact: data.specificationsImpact, details: data.specificationsDetails || "" },
          { key: "equipment", label: "impactAssessment.categories.equipment", hasImpact: data.equipmentImpact, details: data.equipmentDetails || "" },
          { key: "materials", label: "impactAssessment.categories.materials", hasImpact: data.materialsOrMediaImpact, details: data.materialsOrMediaDetails || "" },
          { key: "validation", label: "impactAssessment.categories.validation", hasImpact: data.validationImpact, details: data.validationDetails || "" },
          { key: "regulatory", label: "impactAssessment.categories.regulatory", hasImpact: data.regulatoryCommitmentImpact, details: data.regulatoryCommitmentDetails || "" },
          { key: "relatedDocs", label: "impactAssessment.categories.relatedDocs", hasImpact: data.relatedDocumentsImpact, details: data.relatedDocumentsDetails || "" }
        ]);
      } else {
        // Reset
        setCategories([
          { key: "procedure", label: "impactAssessment.categories.procedure", hasImpact: false, details: "" },
          { key: "training", label: "impactAssessment.categories.training", hasImpact: false, details: "" },
          { key: "forms", label: "impactAssessment.categories.forms", hasImpact: false, details: "" },
          { key: "specifications", label: "impactAssessment.categories.specifications", hasImpact: false, details: "" },
          { key: "equipment", label: "impactAssessment.categories.equipment", hasImpact: false, details: "" },
          { key: "materials", label: "impactAssessment.categories.materials", hasImpact: false, details: "" },
          { key: "validation", label: "impactAssessment.categories.validation", hasImpact: false, details: "" },
          { key: "regulatory", label: "impactAssessment.categories.regulatory", hasImpact: false, details: "" },
          { key: "relatedDocs", label: "impactAssessment.categories.relatedDocs", hasImpact: false, details: "" }
        ]);
      }
    } catch (_err: unknown) {
      setError(t("impactAssessment.loadError"));
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = (index: number, checked: boolean) => {
    const updated = [...categories];
    updated[index].hasImpact = checked;
    if (!checked) {
      updated[index].details = "";
    }
    setCategories(updated);
  };

  const handleDetailsChange = (index: number, text: string) => {
    const updated = [...categories];
    updated[index].details = text;
    setCategories(updated);
  };

  const allDetailsValid = categories.every(
    (c) => !c.hasImpact || c.details.trim().length >= 5
  );

  const handleSave = async () => {
    if (!allDetailsValid) {
      setError(t("impactAssessment.detailsRequiredError"));
      return;
    }

    setSaving(true);
    setError(null);
    setSuccess(null);

    const req: SaveImpactAssessmentRequest = {
      procedureOrMethodImpact: categories[0].hasImpact,
      procedureOrMethodDetails: categories[0].hasImpact ? categories[0].details.trim() : null,
      trainingImpact: categories[1].hasImpact,
      trainingDetails: categories[1].hasImpact ? categories[1].details.trim() : null,
      formsOrTemplatesImpact: categories[2].hasImpact,
      formsOrTemplatesDetails: categories[2].hasImpact ? categories[2].details.trim() : null,
      specificationsImpact: categories[3].hasImpact,
      specificationsDetails: categories[3].hasImpact ? categories[3].details.trim() : null,
      equipmentImpact: categories[4].hasImpact,
      equipmentDetails: categories[4].hasImpact ? categories[4].details.trim() : null,
      materialsOrMediaImpact: categories[5].hasImpact,
      materialsOrMediaDetails: categories[5].hasImpact ? categories[5].details.trim() : null,
      validationImpact: categories[6].hasImpact,
      validationDetails: categories[6].hasImpact ? categories[6].details.trim() : null,
      regulatoryCommitmentImpact: categories[7].hasImpact,
      regulatoryCommitmentDetails: categories[7].hasImpact ? categories[7].details.trim() : null,
      relatedDocumentsImpact: categories[8].hasImpact,
      relatedDocumentsDetails: categories[8].hasImpact ? categories[8].details.trim() : null
    };

    try {
      const res = await documentRevisionService.saveImpactAssessment(revisionId, req);
      setExistingAssessment(res);
      setSuccess(t("impactAssessment.savedSuccess"));
      if (onSaved) onSaved();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("impactAssessment.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>
        {t("impactAssessment.title", { rev: revisionNumber })}
      </DialogTitle>
      <DialogContent dividers>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
              {t("impactAssessment.info")}
            </Alert>

            {error && <Alert severity="error">{error}</Alert>}
            {success && <Alert severity="success">{success}</Alert>}

            {existingAssessment && (
              <Box sx={{
                p: 1.5,
                bgcolor: (t) => t.palette.mode === "dark" ? "rgba(46, 125, 50, 0.15)" : "success.50",
                border: "1px solid",
                borderColor: (t) => t.custom.status.notDetected.border,
                borderRadius: 1
              }}>
                <Typography variant="caption" sx={{ color: (t) => t.palette.mode === "dark" ? "success.light" : "success.dark", fontWeight: 600 }}>
                  {t("impactAssessment.completeBanner", {
                    name: existingAssessment.completedByFullName,
                    username: existingAssessment.completedByUsername,
                    date: formatDateTimeWithTimeZone(existingAssessment.completedAt)
                  })}
                </Typography>
              </Box>
            )}

            <Divider sx={{ my: 1 }} />

            {categories.map((cat, idx) => (
              <Paper key={cat.key} variant="outlined" sx={{ p: 2, bgcolor: cat.hasImpact ? "action.hover" : "background.paper" }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                    {t(cat.label)}
                  </Typography>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={cat.hasImpact}
                        onChange={(e) => handleToggle(idx, e.target.checked)}
                        disabled={!isEditable || saving}
                        color="warning"
                      />
                    }
                    label={cat.hasImpact ? t("impactAssessment.impactIdentified") : t("impactAssessment.noImpact")}
                    labelPlacement="start"
                  />
                </Box>

                {cat.hasImpact && (
                  <Box sx={{ mt: 1.5 }}>
                    <TextField
                      label={t("impactAssessment.detailsLabel")}
                      multiline
                      rows={2}
                      fullWidth
                      size="small"
                      value={cat.details}
                      onChange={(e) => handleDetailsChange(idx, e.target.value)}
                      disabled={!isEditable || saving}
                      placeholder={t("impactAssessment.detailsPlaceholder", { category: t(cat.label) })}
                      error={cat.details.trim().length < 5}
                      helperText={cat.details.trim().length < 5 ? t("impactAssessment.minCharsError") : ""}
                    />
                  </Box>
                )}
              </Paper>
            ))}
          </Box>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={saving}>
          {t("impactAssessment.close")}
        </Button>
        {isEditable && (
          <Button
            onClick={handleSave}
            variant="contained"
            disabled={saving || !allDetailsValid}
          >
            {saving ? <CircularProgress size={24} /> : t("impactAssessment.submit")}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};
