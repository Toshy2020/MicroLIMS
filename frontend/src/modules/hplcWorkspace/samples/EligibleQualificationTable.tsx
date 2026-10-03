import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Checkbox,
  CircularProgress,
  Alert,
  Box,
  Typography,
  InputAdornment,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AssignmentTurnedInIcon from "@mui/icons-material/AssignmentTurnedIn";
import { toast } from "sonner";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { EligibleQualificationDto } from "../types";

export interface EligibleQualificationTableProps {
  open: boolean;
  runId: number;
  onClose: () => void;
  onAssigned: () => void;
}

export function EligibleQualificationTable({
  open,
  runId,
  onClose,
  onAssigned
}: EligibleQualificationTableProps) {
  const theme = useTheme();
  const [searchTerm, setSearchTerm] = useState("");
  const [loading, setLoading] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [qualifications, setQualifications] = useState<EligibleQualificationDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  const fetchEligible = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await HplcWorkspaceService.getEligibleQualifications(runId);
      setQualifications(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to load eligible qualifications.");
    } finally {
      setLoading(false);
    }
  }, [runId]);

  useEffect(() => {
    if (open) {
      setSelectedIds([]);
      setSearchTerm("");
      fetchEligible();
    }
  }, [open, fetchEligible]);

  const filteredQualifications = useMemo(() => {
    if (!searchTerm.trim()) return qualifications;
    const term = searchTerm.toLowerCase();
    return qualifications.filter(
      (q) =>
        q.code.toLowerCase().includes(term) ||
        q.materialName.toLowerCase().includes(term) ||
        q.batchNumber.toLowerCase().includes(term) ||
        q.analyteName.toLowerCase().includes(term) ||
        q.kind.toLowerCase().includes(term)
    );
  }, [qualifications, searchTerm]);

  const handleToggleSelectAll = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.checked) {
      setSelectedIds(filteredQualifications.map((q) => q.qualificationId));
    } else {
      setSelectedIds([]);
    }
  };

  const handleToggleRow = (qualificationId: number) => {
    setSelectedIds((prev) =>
      prev.includes(qualificationId) ? prev.filter((id) => id !== qualificationId) : [...prev, qualificationId]
    );
  };

  const handleAssign = async () => {
    if (selectedIds.length === 0) return;
    setAssigning(true);
    setError(null);
    try {
      await HplcWorkspaceService.assignQualifications(runId, selectedIds);
      toast.success(`Assigned ${selectedIds.length} working standard qualification(s) to this run.`);
      onAssigned();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not assign qualifications.");
    } finally {
      setAssigning(false);
    }
  };

  const allSelected = filteredQualifications.length > 0 && selectedIds.length === filteredQualifications.length;
  const someSelected = selectedIds.length > 0 && selectedIds.length < filteredQualifications.length;

  return (
    <Dialog open={open} onClose={() => !assigning && onClose()} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        Assign Working Standard Qualifications to Run
      </DialogTitle>
      <DialogContent dividers sx={{ p: 2.5 }}>
        <Box sx={{ mb: 2 }}>
          <TextField
            size="small"
            fullWidth
            placeholder="Search by code, material, batch, or analyte..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            disabled={loading || assigning}
            slotProps={{
              htmlInput: { "aria-label": "Search eligible qualifications" },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                )
              }
            }}
          />
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <TableContainer sx={{ maxHeight: 400, border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
            <Table size="small" stickyHeader>
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell padding="checkbox">
                    <Checkbox
                      size="small"
                      checked={allSelected}
                      indeterminate={someSelected}
                      onChange={handleToggleSelectAll}
                      disabled={filteredQualifications.length === 0 || assigning}
                      slotProps={{ input: { "aria-label": "Select all eligible qualifications" } }}
                    />
                  </TableCell>
                  <TableCell>Code</TableCell>
                  <TableCell>Material</TableCell>
                  <TableCell>Batch</TableCell>
                  <TableCell>Analyte</TableCell>
                  <TableCell>Kind</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredQualifications.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} sx={{ textAlign: "center", py: 4, color: "text.secondary" }}>
                      No eligible working standard qualifications found matching this run.
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredQualifications.map((q) => {
                    const isSelected = selectedIds.includes(q.qualificationId);
                    return (
                      <TableRow
                        key={q.qualificationId}
                        hover
                        selected={isSelected}
                        onClick={() => !assigning && handleToggleRow(q.qualificationId)}
                        sx={{ cursor: assigning ? "default" : "pointer" }}
                      >
                        <TableCell padding="checkbox">
                          <Checkbox
                            size="small"
                            checked={isSelected}
                            disabled={assigning}
                            slotProps={{ input: { "aria-label": `Select ${q.code} - ${q.materialName}` } }}
                          />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}>{q.code}</TableCell>
                        <TableCell>{q.materialName}</TableCell>
                        <TableCell>{q.batchNumber || "—"}</TableCell>
                        <TableCell sx={{ fontWeight: 600 }}>{q.analyteName}</TableCell>
                        <TableCell>{q.kind}</TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        <Box sx={{ mt: 1.5, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {selectedIds.length} of {filteredQualifications.length} qualification(s) selected
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 2.5, py: 2 }}>
        <Button onClick={onClose} disabled={assigning}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleAssign}
          disabled={selectedIds.length === 0 || assigning}
          startIcon={assigning ? <CircularProgress size={16} color="inherit" /> : <AssignmentTurnedInIcon />}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {assigning ? "Assigning..." : `Assign Selected (${selectedIds.length})`}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
