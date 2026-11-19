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
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";
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
  const { t } = useTranslation("solutionPreparation");
  const theme = useTheme();

  return (
    <TableContainer>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, width: 60 }}>{t("standardizationHistory.subColumns.rep")}</TableCell>
            {isPrimary ? (
              <>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.standardLot")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.weightMg")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.purityPercent")}</TableCell>
              </>
            ) : (
              <>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.referenceSolution")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.refVolumeMl")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.refFactor")}</TableCell>
              </>
            )}
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.titrantVolMl")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.blankMl")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.subColumns.calculatedFactor")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {replicates.map((r) => (
            <TableRow key={r.id || r.replicateNo}>
              <TableCell sx={{ fontWeight: 600 }}><Ltr>{r.replicateNo}</Ltr></TableCell>
              {isPrimary ? (
                <>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    {r.standardLotBatchNumber ? <Ltr>{r.standardLotBatchNumber}</Ltr> : "—"}
                  </TableCell>
                  <TableCell>{r.standardWeightMg != null ? <Ltr>{r.standardWeightMg}</Ltr> : "—"}</TableCell>
                  <TableCell><Ltr>{t("units.percent", { value: r.standardPurityPercent != null ? r.standardPurityPercent : 100 })}</Ltr></TableCell>
                </>
              ) : (
                <>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    <Ltr>{r.referencePreparationCode || t("standardizationHistory.prepFallback", { id: r.referencePreparationId })}</Ltr>
                  </TableCell>
                  <TableCell>{r.referenceVolumeMl != null ? <Ltr>{r.referenceVolumeMl}</Ltr> : "—"}</TableCell>
                  <TableCell sx={{ fontFamily: "monospace" }}>
                    {r.referenceFactor != null ? <Ltr>{r.referenceFactor.toFixed(4)}</Ltr> : "—"}
                  </TableCell>
                </>
              )}
              <TableCell><Ltr>{r.titrantVolumeMl}</Ltr></TableCell>
              <TableCell>{r.blankMl != null ? <Ltr>{r.blankMl}</Ltr> : "—"}</TableCell>
              <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                {r.factor != null ? <Ltr>{r.factor.toFixed(4)}</Ltr> : "—"}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

function HistoryRow({ record }: { record: TitrantStandardizationResponse }) {
  const { t } = useTranslation("solutionPreparation");
  const [open, setOpen] = useState(false);
  const isPrimary = record.mode === "PrimaryStandard";

  const validUntilText = (() => {
    if (record.validUntil) return <Ltr>{formatLabCalendarDate(record.validUntil)}</Ltr>;
    if (record.passed) return t("standardizationHistory.beforeEachUse");
    return "—";
  })();

  return (
    <>
      <TableRow hover sx={{ "& > *": { borderBottom: "unset" } }}>
        <TableCell sx={{ width: 48, p: 0.5 }}>
          <IconButton size="small" onClick={() => setOpen(!open)} aria-label={t("standardizationHistory.expandRow")}>
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell sx={{ fontWeight: 600 }}><Ltr>{formatDateTimeWithTimeZone(record.standardizedAt)}</Ltr></TableCell>
        <TableCell>{record.standardizedByUserName || "—"}</TableCell>
        <TableCell>
          <Typography variant="body2" sx={{ fontSize: 13 }}>
            {isPrimary ? t("standardizationHistory.modePrimary") : t("standardizationHistory.modeReference")}
          </Typography>
        </TableCell>
        <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
          <Ltr>{record.meanFactor.toFixed(4)}</Ltr>
        </TableCell>
        <TableCell>
          {record.rsdPercent != null ? <Ltr>{t("units.percent", { value: record.rsdPercent.toFixed(2) })}</Ltr> : "—"}
        </TableCell>
        <TableCell>
          <Chip
            label={record.passed ? t("standardizationHistory.passed") : t("standardizationHistory.failed")}
            color={record.passed ? "success" : "error"}
            size="small"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        </TableCell>
        <TableCell>{validUntilText}</TableCell>
        <TableCell>{record.temperatureC != null ? <Ltr>{record.temperatureC}</Ltr> : "—"}</TableCell>
      </TableRow>

      <TableRow>
        <TableCell sx={{ py: 0, px: 2 }} colSpan={9}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box sx={{ my: 2, pl: 4 }}>
              {!record.passed && record.failureReasons && (
                <Alert severity="error" sx={{ mb: 1.5 }}>
                  <Typography variant="caption" sx={{ fontWeight: 700, display: "block" }}>
                    {t("standardizationHistory.failureReasons")}
                  </Typography>
                  <Typography variant="body2" sx={{ whiteSpace: "pre-line" }}>
                    {record.failureReasons}
                  </Typography>
                </Alert>
              )}
              <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", mb: 1, display: "block" }}>
                <Trans
                  i18nKey="standardizationHistory.replicateTitrations"
                  ns="solutionPreparation"
                  values={{ count: record.replicates.length }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
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
  const { t } = useTranslation("solutionPreparation");
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
        {t("standardizationHistory.empty")}
      </Typography>
    );
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ width: 48 }} />
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.dateTime")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.analyst")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.mode")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.meanFactor")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.rsd")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.status")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.validUntil")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("standardizationHistory.columns.temp")}</TableCell>
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
