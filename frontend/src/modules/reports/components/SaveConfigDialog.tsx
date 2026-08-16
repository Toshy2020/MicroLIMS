import { useState } from "react";
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, FormControl, InputLabel, Select, MenuItem, Stack, Typography } from "@mui/material";
import { ReportBuilderCriteria, ReportBuilderOptions, ReportPurpose, SampleCategory } from "../types/reportingTypes";
import { SavedReportsService } from "../services/SavedReportsService";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";

interface SaveConfigDialogProps {
  open: boolean;
  onClose: () => void;
  criteria: ReportBuilderCriteria;
  options: ReportBuilderOptions;
  onSaved: () => void;
}

export function SaveConfigDialog({ open, onClose, criteria, options, onSaved }: SaveConfigDialogProps) {
  const { fullName, userId } = useAuth();
  const [name, setName] = useState("");
  const [purpose, setPurpose] = useState<ReportPurpose>(criteria.reportPurpose);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async () => {
    if (!name.trim()) {
      setError("Please provide a configuration name.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await SavedReportsService.saveConfiguration({
        name: name.trim(),
        reportType: criteria.reportType,
        purpose,
        categories: criteria.category ? [criteria.category as SampleCategory] : ["FinishedProduct"],
        criteria,
        options,
        modifiedBy: fullName || "Current User",
        modifiedByUserId: userId || 101,
        status: "Active"
      });
      setName("");
      onSaved();
      onClose();
    } catch {
      setError("Failed to save report configuration.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ borderBottom: 1, borderColor: "divider", color: brandColors.sectionTitle, fontWeight: 700 }}>
        Save Report Configuration
      </DialogTitle>
      <DialogContent sx={{ pt: 2.5 }}>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            Save the currently selected criteria and formatting options as a reusable report template.
          </Typography>

          <TextField
            label="Configuration Name"
            fullWidth
            size="small"
            required
            placeholder="e.g. Monthly Finished Product Release Summary"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              setError(null);
            }}
            error={!!error}
            helperText={error}
          />

          <FormControl fullWidth size="small">
            <InputLabel>Report Purpose</InputLabel>
            <Select
              label="Report Purpose"
              value={purpose}
              onChange={(e) => setPurpose(e.target.value as ReportPurpose)}
            >
              <MenuItem value="Ad-Hoc Analysis">Ad-Hoc Analysis</MenuItem>
              <MenuItem value="Operational Report">Operational Report</MenuItem>
              <MenuItem value="Controlled Report">Controlled Report</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2, borderTop: 1, borderColor: "divider" }}>
        <Button onClick={onClose} disabled={saving}>Cancel</Button>
        <Button variant="contained" onClick={handleSave} disabled={saving}>
          {saving ? "Saving…" : "Save Configuration"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
