import React from "react";
import { Trans } from "react-i18next";
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Stack,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { HplcStatusBadge } from "./HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ResultStatus } from "../types";
import { formatNumber } from "../../../utils/formatCount";

export interface ResidualSolventReplicateData {
  replicateNo: number;
  sampleWeightMg: number;
  response: number;
  ppm: number;
  ppmDisplay: string;
}

export interface ResidualSolventCalculationData {
  analyte: string;
  hplcMethodAnalyteId: number;
  quantity: "ResidualSolventPpm";
  basis: string;
  standardConcentrationUgPerMl: number;
  sampleSolutionVolumeMl: number;
  standardMeanResponse: number;
  replicates: ResidualSolventReplicateData[];
  reportedValue: number;
  notDetected: boolean;
  runCode: string;
  sstCode: string;
  hplcMethodId: number;
  hplcRunId?: number;
  hplcRunSampleId?: number;
}

export interface ResidualSolventCalculationViewProps {
  calc: ResidualSolventCalculationData;
  reportedDisplay?: string | null;
  specLimit?: string | null;
  status?: ResultStatus | string;
  reportsSection?: React.ReactNode;
}

export function ResidualSolventCalculationView({
  calc,
  reportedDisplay,
  specLimit,
  status,
  reportsSection
}: ResidualSolventCalculationViewProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();

  const formattedReportedResult = calc.notDetected
    ? t("residualSolvent.notDetected")
    : reportedDisplay
      ? (reportedDisplay.toLowerCase().includes("ppm") ? reportedDisplay : `${reportedDisplay} ppm`)
      : `${calc.reportedValue} ppm`;

  return (
    <Box
      sx={{
        mt: 1.5,
        p: 2,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      {/* Title & Run Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
            <Trans
              i18nKey="residualSolvent.title"
              ns="hplcWorkspace"
              values={{ analyte: calc.analyte }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <StatusBadge status="Prepared" label={t("residualSolvent.basis", { basis: calc.basis || t("residualSolvent.basisMean") })} />
          <StatusBadge status="Draft" label={t("residualSolvent.badge")} />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="residualSolvent.runSstMeta"
            ns="hplcWorkspace"
            values={{ runCode: calc.runCode, sstCode: calc.sstCode }}
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Typography>
      </Box>

      {/* Snapshot Reference & Standard Calibration */}
      <Paper
        elevation={0}
        sx={{
          p: 1.5,
          mb: 1.5,
          borderRadius: 1,
          backgroundColor: theme.palette.action.hover,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
          {t("residualSolvent.methodSnapshotTitle")}
        </Typography>
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("residualSolvent.standardConc")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{`${calc.standardConcentrationUgPerMl} µg/mL`}</Ltr>
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("residualSolvent.sampleSolutionVolume")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{`${calc.sampleSolutionVolumeMl} mL`}</Ltr>
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("residualSolvent.standardMeanResponse")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              <Ltr>{formatNumber(calc.standardMeanResponse, { maximumFractionDigits: 2, useGrouping: false })}</Ltr>
            </Typography>
          </Box>
        </Stack>
        <Box sx={{ pt: 0.5, borderTop: `1px dashed ${theme.palette.divider}` }}>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", fontStyle: "italic" }}>
            <Trans
              i18nKey="residualSolvent.formula"
              ns="hplcWorkspace"
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
        </Box>
      </Paper>

      {/* Replicate Injections Table */}
      {calc.replicates && calc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 12 }}>{t("residualSolvent.columns.replicate")}</TableCell>
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("residualSolvent.columns.sampleWeight")}</TableCell>
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("residualSolvent.columns.area")}</TableCell>
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("residualSolvent.columns.calculated")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}><Ltr>#{r.replicateNo}</Ltr></TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    <Ltr>{r.sampleWeightMg}</Ltr>
                  </TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    <Ltr>{r.response}</Ltr>
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    <Ltr>{r.ppmDisplay || `${r.ppm} ppm`}</Ltr>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Optional reports slot */}
      {reportsSection}

      {/* Final Outcome */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            {t("residualSolvent.reportedResult")} <strong>{calc.notDetected ? formattedReportedResult : <Ltr>{formattedReportedResult}</Ltr>}</strong>
          </Typography>
          {specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="residualSolvent.spec"
                ns="hplcWorkspace"
                values={{ specLimit }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          )}
          {status && <HplcStatusBadge status={status} />}
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <LinkIcon fontSize="small" sx={{ fontSize: 14, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <Trans
              i18nKey="residualSolvent.evidenceLink"
              ns="hplcWorkspace"
              values={{ runCode: calc.runCode }}
              components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
            />
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
