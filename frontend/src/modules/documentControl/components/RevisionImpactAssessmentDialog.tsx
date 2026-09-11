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
  const [loading, setLoading] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [existingAssessment, setExistingAssessment] = useState<RevisionImpactAssessmentDto | null>(null);

  const [categories, setCategories] = useState<CategoryState[]>([
    { key: "procedure", label: "1. Procedure or Method Impact", hasImpact: false, details: "" },
    { key: "training", label: "2. Training Impact", hasImpact: false, details: "" },
    { key: "forms", label: "3. Forms or Templates Impact", hasImpact: false, details: "" },
    { key: "specifications", label: "4. Specifications Impact", hasImpact: false, details: "" },
    { key: "equipment", label: "5. Equipment Impact", hasImpact: false, details: "" },
    { key: "materials", label: "6. Materials or Media Impact", hasImpact: false, details: "" },
    { key: "validation", label: "7. Validation Impact", hasImpact: false, details: "" },
    { key: "regulatory", label: "8. Regulatory Commitment Impact", hasImpact: false, details: "" },
    { key: "relatedDocs", label: "9. Related Documents Impact", hasImpact: false, details: "" }
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
          { key: "procedure", label: "1. Procedure or Method Impact", hasImpact: data.procedureOrMethodImpact, details: data.procedureOrMethodDetails || "" },
          { key: "training", label: "2. Training Impact", hasImpact: data.trainingImpact, details: data.trainingDetails || "" },
          { key: "forms", label: "3. Forms or Templates Impact", hasImpact: data.formsOrTemplatesImpact, details: data.formsOrTemplatesDetails || "" },
          { key: "specifications", label: "4. Specifications Impact", hasImpact: data.specificationsImpact, details: data.specificationsDetails || "" },
          { key: "equipment", label: "5. Equipment Impact", hasImpact: data.equipmentImpact, details: data.equipmentDetails || "" },
          { key: "materials", label: "6. Materials or Media Impact", hasImpact: data.materialsOrMediaImpact, details: data.materialsOrMediaDetails || "" },
          { key: "validation", label: "7. Validation Impact", hasImpact: data.validationImpact, details: data.validationDetails || "" },
          { key: "regulatory", label: "8. Regulatory Commitment Impact", hasImpact: data.regulatoryCommitmentImpact, details: data.regulatoryCommitmentDetails || "" },
          { key: "relatedDocs", label: "9. Related Documents Impact", hasImpact: data.relatedDocumentsImpact, details: data.relatedDocumentsDetails || "" }
        ]);
      } else {
        // Reset
        setCategories([
          { key: "procedure", label: "1. Procedure or Method Impact", hasImpact: false, details: "" },
          { key: "training", label: "2. Training Impact", hasImpact: false, details: "" },
          { key: "forms", label: "3. Forms or Templates Impact", hasImpact: false, details: "" },
          { key: "specifications", label: "4. Specifications Impact", hasImpact: false, details: "" },
          { key: "equipment", label: "5. Equipment Impact", hasImpact: false, details: "" },
          { key: "materials", label: "6. Materials or Media Impact", hasImpact: false, details: "" },
          { key: "validation", label: "7. Validation Impact", hasImpact: false, details: "" },
          { key: "regulatory", label: "8. Regulatory Commitment Impact", hasImpact: false, details: "" },
          { key: "relatedDocs", label: "9. Related Documents Impact", hasImpact: false, details: "" }
        ]);
      }
    } catch (err: any) {
      setError("Failed to load impact assessment.");
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
      setError("For every category marked as having an impact, details must contain at least 5 characters.");
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
      setSuccess("Revision impact assessment successfully saved and baselined.");
      if (onSaved) onSaved();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to save impact assessment.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>
        Revision Impact Assessment — Rev {revisionNumber}
      </DialogTitle>
      <DialogContent dividers>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
              Every new document revision must undergo a structured impact assessment across all 9 GMP categories. Technical review submission is strictly blocked until this assessment is completed.
            </Alert>

            {error && <Alert severity="error">{error}</Alert>}
            {success && <Alert severity="success">{success}</Alert>}

            {existingAssessment && (
              <Box sx={{
                p: 1.5,
                bgcolor: (t) => t.palette.mode === "dark" ? "rgba(46, 125, 50, 0.15)" : "success.50",
                border: "1px solid",
                borderColor: (t) => t.palette.mode === "dark" ? "success.dark" : "#c8e6c9",
                borderRadius: 1
              }}>
                <Typography variant="caption" sx={{ color: (t) => t.palette.mode === "dark" ? "success.light" : "success.dark", fontWeight: 600 }}>
                  Assessment Status: COMPLETE &bull; Last Completed by: {existingAssessment.completedByFullName} ({existingAssessment.completedByUsername}) on {new Date(existingAssessment.completedAt).toLocaleString()}
                </Typography>
              </Box>
            )}

            <Divider sx={{ my: 1 }} />

            {categories.map((cat, idx) => (
              <Paper key={cat.key} variant="outlined" sx={{ p: 2, bgcolor: cat.hasImpact ? "action.hover" : "background.paper" }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                    {cat.label}
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
                    label={cat.hasImpact ? "Impact Identified" : "No Impact"}
                    labelPlacement="start"
                  />
                </Box>

                {cat.hasImpact && (
                  <Box sx={{ mt: 1.5 }}>
                    <TextField
                      label="Impact Details & Mitigation *"
                      multiline
                      rows={2}
                      fullWidth
                      size="small"
                      value={cat.details}
                      onChange={(e) => handleDetailsChange(idx, e.target.value)}
                      disabled={!isEditable || saving}
                      placeholder={`Document specific changes, required actions, or mitigations for ${cat.label}...`}
                      error={cat.details.trim().length < 5}
                      helperText={cat.details.trim().length < 5 ? "Minimum 5 characters required" : ""}
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
          Close
        </Button>
        {isEditable && (
          <Button
            onClick={handleSave}
            variant="contained"
            disabled={saving || !allDetailsValid}
          >
            {saving ? <CircularProgress size={24} /> : "Save & Complete Assessment"}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};
