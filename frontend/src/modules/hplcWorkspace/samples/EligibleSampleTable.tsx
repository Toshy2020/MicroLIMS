import { useState, useEffect, useCallback } from "react";
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
import type { EligibleTestDto } from "../types";

export interface EligibleSampleTableProps {
  open: boolean;
  runId: number;
  onClose: () => void;
  onAssigned: () => void;
}

export function EligibleSampleTable({
  open,
  runId,
  onClose,
  onAssigned
}: EligibleSampleTableProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const [searchTerm, setSearchTerm] = useState("");
  const [loading, setLoading] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [eligibleTests, setEligibleTests] = useState<EligibleTestDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  const fetchEligible = useCallback(async (searchQuery: string) => {
    setLoading(true);
    setError(null);
    try {
      const tests = await HplcWorkspaceService.getEligibleTests(runId, searchQuery || undefined);
      setEligibleTests(tests);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("samples.eligible.loadError"));
    } finally {
      setLoading(false);
    }
  }, [runId, t]);

  useEffect(() => {
    if (open) {
      setSelectedIds([]);
      setSearchTerm("");
      fetchEligible("");
    }
  }, [open, fetchEligible]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    fetchEligible(searchTerm);
  };

  const handleToggleSelectAll = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.checked) {
      setSelectedIds(eligibleTests.map((t) => t.testOrderId));
    } else {
      setSelectedIds([]);
    }
  };

  const handleToggleRow = (testOrderId: number) => {
    setSelectedIds((prev) =>
      prev.includes(testOrderId) ? prev.filter((id) => id !== testOrderId) : [...prev, testOrderId]
    );
  };

  const handleAssign = async () => {
    if (selectedIds.length === 0) return;
    setAssigning(true);
    setError(null);
    try {
      await HplcWorkspaceService.assignSamples(runId, selectedIds);
      toast.success(t("samples.eligible.assignedToast", { count: selectedIds.length }));
      onAssigned();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("samples.eligible.assignError"));
    } finally {
      setAssigning(false);
    }
  };

  const allSelected = eligibleTests.length > 0 && selectedIds.length === eligibleTests.length;
  const someSelected = selectedIds.length > 0 && selectedIds.length < eligibleTests.length;

  return (
    <Dialog open={open} onClose={() => !assigning && onClose()} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        {t("samples.eligible.title")}
      </DialogTitle>
      <DialogContent dividers sx={{ p: 2.5 }}>
        <Box component="form" onSubmit={handleSearchSubmit} sx={{ mb: 2 }}>
          <TextField
            size="small"
            fullWidth
            placeholder={t("samples.eligible.searchPlaceholder")}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            disabled={loading || assigning}
            slotProps={{
              htmlInput: { "aria-label": t("samples.eligible.searchAria") },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                ),
                endAdornment: (
                  <InputAdornment position="end">
                    <Button
                      type="submit"
                      size="small"
                      disabled={loading || assigning}
                    >
                      {t("samples.eligible.searchButton")}
                    </Button>
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
          <TableContainer
            sx={{
              maxHeight: 400,
              border: `1px solid ${theme.palette.divider}`,
              borderRadius: 1
            }}
          >
            <Table size="small" stickyHeader>
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell padding="checkbox">
                    <Checkbox
                      size="small"
                      checked={allSelected}
                      slotProps={{ input: { "aria-label": t("samples.eligible.selectAllAria") } }}
                      indeterminate={someSelected}
                      onChange={handleToggleSelectAll}
                      disabled={eligibleTests.length === 0 || assigning}
                    />
                  </TableCell>
                  <TableCell>{t("samples.eligible.columns.sampleNumber")}</TableCell>
                  <TableCell>{t("samples.eligible.columns.batch")}</TableCell>
                  <TableCell>{t("samples.eligible.columns.product")}</TableCell>
                  <TableCell>{t("samples.eligible.columns.testCode")}</TableCell>
                  <TableCell>{t("samples.eligible.columns.stage")}</TableCell>
                  <TableCell>{t("samples.eligible.columns.receivedAt")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {eligibleTests.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} sx={{ textAlign: "center", py: 4, color: "text.secondary" }}>
                      {t("samples.eligible.empty")}
                    </TableCell>
                  </TableRow>
                ) : (
                  eligibleTests.map((testItem) => {
                    const isSelected = selectedIds.includes(testItem.testOrderId);
                    return (
                      <TableRow
                        key={testItem.testOrderId}
                        hover
                        selected={isSelected}
                        onClick={() => !assigning && handleToggleRow(testItem.testOrderId)}
                        sx={{ cursor: assigning ? "default" : "pointer" }}
                      >
                        <TableCell padding="checkbox">
                          <Checkbox
                            size="small"
                            checked={isSelected}
                            disabled={assigning}
                            slotProps={{ input: { "aria-label": t("samples.eligible.selectRowAria", { sampleNumber: testItem.sampleNumber, testCode: testItem.testCode }) } }}
                          />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}>
                          <div><Ltr>{testItem.sampleNumber}</Ltr></div>
                          {testItem.subjectName && (
                            <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontWeight: 400 }}>
                              {testItem.subjectName}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>{testItem.batchNumber ? <Ltr>{testItem.batchNumber}</Ltr> : "—"}</TableCell>
                        <TableCell>{testItem.productName ?? "—"}</TableCell>
                        <TableCell sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}><Ltr>{testItem.testCode}</Ltr></TableCell>
                        <TableCell>{testItem.stageName ?? "—"}</TableCell>
                        <TableCell>
                          {testItem.receivedAt ? <Ltr>{new Date(testItem.receivedAt).toLocaleDateString()}</Ltr> : "—"}
                        </TableCell>
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
            {t("samples.eligible.selectedCount", { selected: selectedIds.length, total: eligibleTests.length })}
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 2.5, py: 2 }}>
        <Button
          onClick={onClose}
          disabled={assigning}
        >
          {t("samples.eligible.cancel")}
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleAssign}
          disabled={selectedIds.length === 0 || assigning}
          startIcon={assigning ? <CircularProgress size={16} color="inherit" /> : <AssignmentTurnedInIcon />}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {assigning ? t("samples.eligible.assigning") : t("samples.eligible.assignSelected", { count: selectedIds.length })}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
