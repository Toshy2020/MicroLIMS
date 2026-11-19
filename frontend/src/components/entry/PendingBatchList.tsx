import type { ReactNode } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import type { SxProps, Theme } from "@mui/material";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "../../i18n";
import { tableHeadSx } from "../../theme";

export interface PendingBatchColumn<T> {
  key: string;
  label: string;
  render: (row: T) => ReactNode;
  sx?: SxProps<Theme>;
}

export interface PendingBatchListProps<T extends { errors?: Record<string, string> }> {
  title: string;
  subtitle: string;
  ariaLabel: string;
  columns: PendingBatchColumn<T>[];
  rows: T[];
  getRowKey: (row: T, index: number) => string;
  getRowLabel: (row: T, index: number) => string;
  saving: boolean;
  error: string | null;
  onSave: () => void;
  onDiscard: () => void;
  onRemoveRow: (index: number) => void;
}

/**
 * Shared pending-rows review table for configuration paste workflows.
 * Nothing is saved until Save; server row errors are shown against each row.
 */
export function PendingBatchList<T extends { errors?: Record<string, string> }>({
  title,
  subtitle,
  ariaLabel,
  columns,
  rows,
  getRowKey,
  getRowLabel,
  saving,
  error,
  onSave,
  onDiscard,
  onRemoveRow
}: PendingBatchListProps<T>) {
  const { t } = useTranslation();
  const theme = useTheme();

  if (rows.length === 0) return null;

  return (
    <Paper
      variant="outlined"
      role="region"
      aria-label={ariaLabel}
      sx={{ mb: 2.5, borderRadius: 2, overflow: "hidden", borderColor: error ? "error.main" : "primary.main" }}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        sx={{
          px: 2.5,
          py: 2,
          borderBottom: "1px solid",
          borderColor: "divider",
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 1.5
        }}
      >
        <Box>
          <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>
            {title}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {subtitle}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={onDiscard} disabled={saving} sx={{ textTransform: "none" }}>
            {t("components.pendingBatchList.discard")}
          </Button>
          <Button
            variant="contained"
            onClick={onSave}
            disabled={saving}
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            {saving ? t("components.pendingBatchList.saving") : t("components.pendingBatchList.save")}
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mx: 2.5, mt: 2 }}>
          {error}
        </Alert>
      )}

      <TableContainer sx={{ maxHeight: "40vh" }}>
        <Table size="small" stickyHeader aria-label={ariaLabel}>
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell>{t("components.pendingBatchList.rowColumn")}</TableCell>
              {columns.map((c) => (
                <TableCell key={c.key}>{c.label}</TableCell>
              ))}
              <TableCell>{t("components.pendingBatchList.problemColumn")}</TableCell>
              <TableCell align="right">{t("components.pendingBatchList.actionsColumn")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r, i) => {
              const rowProblems = r.errors ? Object.values(r.errors) : [];
              const hasProblem = rowProblems.length > 0;
              return (
                <TableRow
                  key={getRowKey(r, i)}
                  sx={hasProblem ? { bgcolor: theme.custom.status.detected.bg } : undefined}
                >
                  <TableCell component="th" scope="row" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {i + 1}
                  </TableCell>
                  {columns.map((c) => (
                    <TableCell key={c.key} sx={c.sx}>
                      {c.render(r)}
                    </TableCell>
                  ))}
                  <TableCell sx={hasProblem ? { color: theme.custom.status.detected.text, fontWeight: 600 } : undefined}>
                    {hasProblem ? rowProblems.join(" ") : t("components.pendingBatchList.noProblem")}
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title={t("components.pendingBatchList.removeRow")}>
                      <span>
                        <IconButton
                          size="small"
                          aria-label={t("components.pendingBatchList.removeRowAria", { row: i + 1, label: getRowLabel(r, i) })}
                          onClick={() => onRemoveRow(i)}
                          disabled={saving}
                        >
                          <DeleteOutlinedIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
