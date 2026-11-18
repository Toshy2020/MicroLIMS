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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("hplcWorkspace");
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
      setError(e.response?.data?.message ?? e.message ?? t("samples.eligibleQual.loadError"));
    } finally {
      setLoading(false);
    }
  }, [runId, t]);

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
      toast.success(t("samples.eligibleQual.assignedToast", { count: selectedIds.length }));
      onAssigned();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("samples.eligibleQual.assignError"));
    } finally {
      setAssigning(false);
    }
  };

  const allSelected = filteredQualifications.length > 0 && selectedIds.length === filteredQualifications.length;
  const someSelected = selectedIds.length > 0 && selectedIds.length < filteredQualifications.length;

  return (
    <Dialog open={open} onClose={() => !assigning && onClose()} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        {t("samples.eligibleQual.title")}
      </DialogTitle>
      <DialogContent dividers sx={{ p: 2.5 }}>
        <Box sx={{ mb: 2 }}>
          <TextField
            size="small"
            fullWidth
            placeholder={t("samples.eligibleQual.searchPlaceholder")}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            disabled={loading || assigning}
            slotProps={{
              htmlInput: { "aria-label": t("samples.eligibleQual.searchAria") },
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
                      slotProps={{ input: { "aria-label": t("samples.eligibleQual.selectAllAria") } }}
                    />
                  </TableCell>
                  <TableCell>{t("samples.eligibleQual.columns.code")}</TableCell>
                  <TableCell>{t("samples.eligibleQual.columns.material")}</TableCell>
                  <TableCell>{t("samples.eligibleQual.columns.batch")}</TableCell>
                  <TableCell>{t("samples.eligibleQual.columns.analyte")}</TableCell>
                  <TableCell>{t("samples.eligibleQual.columns.kind")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredQualifications.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} sx={{ textAlign: "center", py: 4, color: "text.secondary" }}>
                      {t("samples.eligibleQual.empty")}
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
                            slotProps={{ input: { "aria-label": t("samples.eligibleQual.selectRowAria", { code: q.code, materialName: q.materialName }) } }}
                          />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}><Ltr>{q.code}</Ltr></TableCell>
                        <TableCell>{q.materialName}</TableCell>
                        <TableCell>{q.batchNumber ? <Ltr>{q.batchNumber}</Ltr> : "—"}</TableCell>
                        <TableCell sx={{ fontWeight: 600 }}><Ltr>{q.analyteName}</Ltr></TableCell>
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
            {t("samples.eligibleQual.selectedCount", { selected: selectedIds.length, total: filteredQualifications.length })}
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 2.5, py: 2 }}>
        <Button onClick={onClose} disabled={assigning}>
          {t("samples.eligibleQual.cancel")}
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleAssign}
          disabled={selectedIds.length === 0 || assigning}
          startIcon={assigning ? <CircularProgress size={16} color="inherit" /> : <AssignmentTurnedInIcon />}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {assigning ? t("samples.eligibleQual.assigning") : t("samples.eligibleQual.assignSelected", { count: selectedIds.length })}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
