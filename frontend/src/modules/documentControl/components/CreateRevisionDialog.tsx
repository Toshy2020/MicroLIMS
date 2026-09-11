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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to calculate next revision number.");
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
  const isCustomNumberValid = !allowOverride || (customRevisionNumber.trim().length > 0 && /^[a-zA-Z0-9\.\-_]{1,20}$/.test(customRevisionNumber.trim()));

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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to create document revision.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>
        Create New Revision — {companyDocumentCode}
      </DialogTitle>
      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5, mt: 1 }}>
          <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
            The current effective revision (Rev {currentEffectiveRevisionNumber}) will remain active and fully accessible to laboratory personnel while this new draft is prepared and reviewed.
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          {/* Revision Type */}
          <FormControl component="fieldset">
            <FormLabel component="legend" sx={{ fontWeight: 600, fontSize: "0.9rem" }}>
              Revision Classification
            </FormLabel>
            <RadioGroup row value={revisionType} onChange={handleTypeChange}>
              <FormControlLabel value="Major" control={<Radio />} label="Major Revision (e.g. 02, 03)" />
              <FormControlLabel value="Minor" control={<Radio />} label="Minor Revision (e.g. 01.1, 01.2)" />
            </RadioGroup>
          </FormControl>

          {/* Proposed Number Highlight */}
          <Box sx={{ p: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.100", borderRadius: 1.5, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Proposed Revision Number:
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700, color: "primary.main" }}>
                {loadingProposed ? <CircularProgress size={20} /> : `Rev ${proposedNumber || "—"}`}
              </Typography>
            </Box>
            <Typography variant="caption" color="text.secondary">
              Current: Rev {currentEffectiveRevisionNumber}
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
                label={<Typography variant="body2" sx={{ fontWeight: 500 }}>Override Revision Number (Document Controller Only)</Typography>}
              />
              {allowOverride && (
                <TextField
                  label="Custom Revision Number"
                  size="small"
                  fullWidth
                  value={customRevisionNumber}
                  onChange={(e) => setCustomRevisionNumber(e.target.value)}
                  placeholder="e.g. 02.A or 02-REV"
                  helperText="Must be unique alphanumeric format (1-20 characters). Override is strictly audited."
                  sx={{ mt: 1 }}
                />
              )}
            </Box>
          )}

          <Divider />

          {/* Mandatory Reason for Revision */}
          <TextField
            label="Reason for Revision *"
            multiline
            rows={3}
            required
            value={reasonForRevision}
            onChange={(e) => setReasonForRevision(e.target.value)}
            placeholder="Document the regulatory, compendial, or operational justification for this new revision..."
            helperText={`${reasonForRevision.trim().length} / 10 characters minimum`}
            error={reasonForRevision.length > 0 && !isReasonValid}
            fullWidth
          />

          {/* Change Summary */}
          <TextField
            label="Summary of Changes"
            multiline
            rows={3}
            value={changeSummary}
            onChange={(e) => setChangeSummary(e.target.value)}
            placeholder="Briefly describe what sections or instructions have been modified..."
            helperText={changeSummary.length > 0 && !isSummaryValid ? "Must be at least 10 characters if provided." : "Optional overview of modifications"}
            error={changeSummary.length > 0 && !isSummaryValid}
            fullWidth
          />

          {/* Change Reference */}
          <TextField
            label="Change Reference / Ticket #"
            size="small"
            value={changeReference}
            onChange={(e) => setChangeReference(e.target.value)}
            placeholder="e.g. CC-2026-0042, CAPA-2026-19"
            helperText="Optional reference to Change Control or Quality Event ticket"
            fullWidth
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!canSubmit}
        >
          {submitting ? <CircularProgress size={24} /> : "Create Draft Revision"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
