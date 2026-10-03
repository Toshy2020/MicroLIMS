import { useState } from "react";
import {
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  Typography,
  Chip,
  IconButton,
  Collapse,
  Box,
  CircularProgress,
  Alert,
  useTheme
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import { tableHeadSx } from "../../../theme";
import { formatLabDate, formatLabDateTime } from "../../../utils/formatDate";
import type {
  TitrantStandardizationResponse,
  TitrantStandardizationReplicateResponse
} from "../types";

interface Props {
  standardizations: TitrantStandardizationResponse[];
  loading?: boolean;
}

function ReplicatesSubTable({
  replicates,
  isPrimary
}: {
  replicates: TitrantStandardizationReplicateResponse[];
  isPrimary: boolean;
}) {
  const theme = useTheme();

  return (
    <TableContainer>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, width: 60 }}>Rep #</TableCell>
            {isPrimary ? (
              <>
                <TableCell sx={{ fontWeight: 700 }}>Standard Lot</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Weight (mg)</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Purity (%)</TableCell>
              </>
            ) : (
              <>
                <TableCell sx={{ fontWeight: 700 }}>Reference Solution</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Ref Volume (mL)</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Ref Factor</TableCell>
              </>
            )}
            <TableCell sx={{ fontWeight: 700 }}>Titrant Vol (mL)</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Blank (mL)</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Calculated Factor</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {replicates.map((r) => (
            <TableRow key={r.id || r.replicateNo}>
              <TableCell sx={{ fontWeight: 600 }}>{r.replicateNo}</TableCell>
              {isPrimary ? (
                <>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    {r.standardLotBatchNumber || "—"}
                  </TableCell>
                  <TableCell>{r.standardWeightMg != null ? r.standardWeightMg : "—"}</TableCell>
                  <TableCell>{r.standardPurityPercent != null ? `${r.standardPurityPercent}%` : "100%"}</TableCell>
                </>
              ) : (
                <>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    {r.referencePreparationCode || `Prep #${r.referencePreparationId}`}
                  </TableCell>
                  <TableCell>{r.referenceVolumeMl != null ? r.referenceVolumeMl : "—"}</TableCell>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    {r.referenceFactor != null ? r.referenceFactor.toFixed(4) : "—"}
                  </TableCell>
                </>
              )}
              <TableCell>{r.titrantVolumeMl}</TableCell>
              <TableCell>{r.blankMl != null ? r.blankMl : "—"}</TableCell>
              <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                {r.factor != null ? r.factor.toFixed(4) : "—"}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

function HistoryRow({ record }: { record: TitrantStandardizationResponse }) {
  const [open, setOpen] = useState(false);
  const isPrimary = record.mode === "PrimaryStandard";

  const validUntilText = (() => {
    if (record.validUntil) return formatLabDate(record.validUntil);
    if (record.passed) return "Before each use";
    return "—";
  })();

  return (
    <>
      <TableRow hover sx={{ "& > *": { borderBottom: "unset" } }}>
        <TableCell sx={{ width: 48, p: 0.5 }}>
          <IconButton size="small" onClick={() => setOpen(!open)} aria-label="expand row">
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell sx={{ fontWeight: 600 }}>{formatLabDateTime(record.standardizedAt)}</TableCell>
        <TableCell>{record.standardizedByUserName || "—"}</TableCell>
        <TableCell>
          <Typography variant="body2" sx={{ fontSize: 13 }}>
            {isPrimary ? "Primary Standard" : "Against Reference VS"}
          </Typography>
        </TableCell>
        <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
          {record.meanFactor.toFixed(4)}
        </TableCell>
        <TableCell>
          {record.rsdPercent != null ? `${record.rsdPercent.toFixed(2)}%` : "—"}
        </TableCell>
        <TableCell>
          <Chip
            label={record.passed ? "Passed" : "Failed"}
            color={record.passed ? "success" : "error"}
            size="small"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        </TableCell>
        <TableCell>{validUntilText}</TableCell>
        <TableCell>{record.temperatureC != null ? record.temperatureC : "—"}</TableCell>
      </TableRow>

      <TableRow>
        <TableCell sx={{ py: 0, px: 2 }} colSpan={9}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ my: 2, pl: 4 }}>
              {!record.passed && record.failureReasons && (
                <Alert severity="error" sx={{ mb: 1.5 }}>
                  <Typography variant="caption" sx={{ fontWeight: 700, display: "block" }}>
                    Failure Reasons:
                  </Typography>
                  <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                    {record.failureReasons}
                  </Typography>
                </Alert>
              )}
              <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", mb: 1, display: "block" }}>
                Replicate Titrations ({record.replicates.length})
              </Typography>
              <Paper variant="outlined">
                <ReplicatesSubTable replicates={record.replicates} isPrimary={isPrimary} />
              </Paper>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

export function StandardizationHistoryTable({ standardizations, loading }: Props) {
  const theme = useTheme();

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (standardizations.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: "text.secondary", py: 2, textAlign: "center" }}>
        No standardization history recorded yet.
      </Typography>
    );
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ width: 48 }} />
            <TableCell sx={{ fontWeight: 700 }}>Date & Time</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Analyst</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Mode</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Mean Factor</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>RSD (%)</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Valid Until</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Temp (°C)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {standardizations.map((item) => (
            <HistoryRow key={item.id} record={item} />
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
