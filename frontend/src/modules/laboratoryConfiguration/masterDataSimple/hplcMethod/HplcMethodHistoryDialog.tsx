import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  CircularProgress,
  Alert,
  IconButton,
  Collapse,
  Button,
  Stack,
  Chip,
  TableContainer
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import HistoryIcon from "@mui/icons-material/History";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import {
  HplcMethodService,
  HplcMethodListItem,
  HplcMethodHistoryEntry
} from "../services/HplcMethodService";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
import { useTranslation } from "../../../../i18n";

export interface HplcMethodHistoryDialogProps {
  open: boolean;
  method: HplcMethodListItem | null;
  onClose: () => void;
}

export function HplcMethodHistoryDialog({
  open,
  method,
  onClose
}: HplcMethodHistoryDialogProps) {
  const { t } = useTranslation("masterData");
  const [history, setHistory] = useState<HplcMethodHistoryEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expandedRow, setExpandedRow] = useState<number | null>(null);

  useEffect(() => {
    if (open && method) {
      setLoading(true);
      setError(null);
      setExpandedRow(null);
      HplcMethodService.getHistory(method.id)
        .then((res) => {
          setHistory(res);
        })
        .catch((err: unknown) => {
          const errObj = err as { response?: { data?: { message?: string } }; message?: string };
          setError(errObj.response?.data?.message ?? errObj.message ?? t("hplcMethod.history.loadError"));
        })
        .finally(() => {
          setLoading(false);
        });
    }
  }, [open, method, t]);

  const toggleRow = (idx: number) => {
    setExpandedRow((prev) => (prev === idx ? null : idx));
  };

  const formatJson = (raw?: string | null) => {
    if (!raw) return "—";
    try {
      const parsed = JSON.parse(raw);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return raw;
    }
  };

  return (
    <FloatingDialog
      open={open}
      title={method ? t("hplcMethod.history.titleWithMethod", { name: method.name, abbr: method.abbreviation }) : t("hplcMethod.history.title")}
      onClose={onClose}
      maxWidth="md"
      actions={
        <Button onClick={onClose}>
          {t("hplcMethod.history.close")}
        </Button>
      }
    >
      <Box sx={{ pt: 1 }}>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        {loading ? (
          <Box sx={{ py: 6, display: "flex", flexDirection: "column", alignItems: "center" }}>
            <CircularProgress size={32} />
            <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
              {t("hplcMethod.history.loading")}
            </Typography>
          </Box>
        ) : history.length === 0 ? (
          <Box sx={{ py: 6, textAlign: "center" }}>
            <HistoryIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
            <Typography variant="body1" sx={{ color: "text.secondary" }}>
              {t("hplcMethod.history.empty")}
            </Typography>
          </Box>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ width: 40 }} />
                  <TableCell sx={{ fontWeight: 600 }}>{t("hplcMethod.history.columns.timestamp")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("hplcMethod.history.columns.user")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("hplcMethod.history.columns.action")}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{t("hplcMethod.history.columns.reason")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.map((entry, idx) => {
                  const isExpanded = expandedRow === idx;
                  const hasDetails = Boolean(entry.beforeJson || entry.afterJson);

                  return (
                    <TableRow key={idx} sx={{ "& > *": { borderBottom: "unset" } }}>
                      <TableCell>
                        {hasDetails ? (
                          <IconButton aria-label={isExpanded ? t("hplcMethod.history.collapseAria") : t("hplcMethod.history.expandAria")} aria-expanded={Boolean(isExpanded)} size="small" onClick={() => toggleRow(idx)}>
                            {isExpanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                          </IconButton>
                        ) : null}
                      </TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap", fontSize: 13 }}>
                        {formatDateTimeWithTimeZone(entry.at)}
                      </TableCell>
                      <TableCell sx={{ fontWeight: 500, fontSize: 13 }}>{entry.userName}</TableCell>
                      <TableCell>
                        <Chip
                          label={entry.action}
                          size="small"
                          variant="outlined"
                          color={entry.action.includes("Create") ? "success" : entry.action.includes("Deactivate") ? "error" : "primary"}
                          sx={{ fontSize: 12, fontWeight: 600 }}
                        />
                      </TableCell>
                      <TableCell sx={{ fontSize: 13, color: "text.secondary" }}>
                        {entry.reason || "—"}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {expandedRow !== null && history[expandedRow] && (
          <Collapse in={expandedRow !== null}>
            <Box sx={{ mt: 2, p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
                {t("hplcMethod.history.snapshotTitle")}
              </Typography>
              <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                {history[expandedRow].beforeJson && (
                  <Box sx={{ flex: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 700, color: "error.main", display: "block", mb: 0.5 }}>
                      {t("hplcMethod.history.beforeChange")}
                    </Typography>
                    <Box
                      component="pre"
                      sx={{
                        p: 1.5,
                        bgcolor: "background.paper",
                        border: "1px solid",
                        borderColor: "divider",
                        borderRadius: 1,
                        fontSize: 12,
                        maxHeight: 280,
                        overflow: "auto"
                      }}
                    >
                      {formatJson(history[expandedRow].beforeJson)}
                    </Box>
                  </Box>
                )}
                {history[expandedRow].afterJson && (
                  <Box sx={{ flex: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 700, color: "success.main", display: "block", mb: 0.5 }}>
                      {t("hplcMethod.history.afterChange")}
                    </Typography>
                    <Box
                      component="pre"
                      sx={{
                        p: 1.5,
                        bgcolor: "background.paper",
                        border: "1px solid",
                        borderColor: "divider",
                        borderRadius: 1,
                        fontSize: 12,
                        maxHeight: 280,
                        overflow: "auto"
                      }}
                    >
                      {formatJson(history[expandedRow].afterJson)}
                    </Box>
                  </Box>
                )}
              </Stack>
            </Box>
          </Collapse>
        )}
      </Box>
    </FloatingDialog>
  );
}
