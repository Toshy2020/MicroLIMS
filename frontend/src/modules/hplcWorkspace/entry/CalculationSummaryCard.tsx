import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  useTheme
} from "@mui/material";
import { Trans } from "react-i18next";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import type { HplcPreviewResultDto, HplcOfficialResultDto } from "../types";


export interface OfficialResultsCardProps {
  official: HplcOfficialResultDto[];
  basis?: string;
}

export function OfficialResultsCard({
  official,
  basis
}: OfficialResultsCardProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1 }}>
        {basis && <StatusBadge status="Prepared" label={t("entry.calcSummary.basis", { basis })} />}
        <StatusBadge status="Completed" label={t("entry.calcSummary.officialBadge")} />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("entry.calcSummary.officialSubtitle")}
        </Typography>
      </Box>

      {official.length === 0 ? (
        <Box sx={{ p: 3 }}>
          <Alert severity="info">
            {t("entry.calcSummary.noOfficial")}
          </Alert>
        </Box>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{t("entry.calcSummary.columns.parameter")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.quantity")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.replicateLevel")}</TableCell>
                <TableCell align="right">{t("entry.calcSummary.columns.result")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.specLimit")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.status")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {official.map((o, idx) => {
                const isReported = o.replicateNo === null || o.replicateNo === undefined;
                const quantityLabel =
                  o.quantity === "AssayPercent"
                    ? t("entry.quantities.assayPercent")
                    : o.quantity === "AmountPerUnit"
                    ? t("entry.quantities.amountPerUnit")
                    : o.quantity === "ResidualSolventPpm" || o.quantity === "Residual solvent (ppm)"
                    ? t("entry.quantities.residualSolventPpm")
                    : o.quantity === "RelatedSubstance"
                    ? t("entry.quantities.relatedSubstance")
                    : o.quantity;

                return (
                  <TableRow
                    key={`${o.parameterName}-${o.quantity}-${o.replicateNo ?? "rep"}-${idx}`}
                    hover
                    sx={{
                      backgroundColor: isReported ? theme.palette.action.hover : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontWeight: isReported ? 700 : 500 }}>
                      <Ltr>{o.parameterName}</Ltr>
                    </TableCell>
                    <TableCell>{quantityLabel}</TableCell>
                    <TableCell>
                      {isReported ? (
                        <StatusBadge status="Assigned" label={t("entry.calcSummary.reportedResult")} />
                      ) : (
                        t("entry.calcSummary.replicateNumber", { num: o.replicateNo })
                      )}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                      <Ltr>{o.display}</Ltr>
                    </TableCell>
                    <TableCell>{o.specLimit ? <Ltr>{o.specLimit}</Ltr> : "—"}</TableCell>
                    <TableCell>
                      <HplcStatusBadge status={o.status} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

export interface CalculationSummaryCardProps {
  preview?: HplcPreviewResultDto[];
  basis?: string;
  official?: HplcOfficialResultDto[];
  submitted?: boolean;
  // From the entry's result mode: an empty preview cannot tell the modes apart by itself.
  relatedSubstances?: boolean;
}

export function CalculationSummaryCard({
  preview = [],
  basis = "",
  official,
  submitted,
  relatedSubstances = false
}: CalculationSummaryCardProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();

  if (submitted) {
    return <OfficialResultsCard official={official || []} basis={basis} />;
  }

  const isResidualSolventsPreview = preview.some(
    (p) => p.quantity === "Residual solvent (ppm)" || p.quantity === "ResidualSolventPpm"
  );

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1 }}>
        <StatusBadge status="Prepared" label={t("entry.calcSummary.basis", { basis })} />
        <StatusBadge status="Pending Review" label={t("entry.calcSummary.previewBadge")} />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("entry.calcSummary.previewSubtitle")}
        </Typography>
      </Box>

      {isResidualSolventsPreview && (
        <Alert severity="info" sx={{ mb: 1.5, py: 0.5, fontSize: 12 }}>
          <Trans
            i18nKey="entry.calcSummary.residualFormula"
            ns="hplcWorkspace"
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Alert>
      )}

      {preview.length === 0 ? (
        <Box sx={{ p: 3 }}>
          <Alert severity="info">
            {relatedSubstances
              ? t("entry.calcSummary.noPreviewRs")
              : t("entry.calcSummary.noPreviewOther", {
                  inputKind: isResidualSolventsPreview
                    ? t("entry.calcSummary.inputAreas")
                    : t("entry.calcSummary.inputResponses")
                })}
          </Alert>
        </Box>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{t("entry.calcSummary.columns.parameter")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.quantity")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.replicateLevel")}</TableCell>
                <TableCell align="right">{t("entry.calcSummary.columns.value")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.unit")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.specLimit")}</TableCell>
                <TableCell>{t("entry.calcSummary.columns.status")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preview.map((p, idx) => {
                const isReported = p.replicateNo === null || p.replicateNo === undefined;
                const quantityLabel =
                  p.quantity === "AssayPercent"
                    ? t("entry.quantities.assayPercent")
                    : p.quantity === "AmountPerUnit"
                    ? t("entry.quantities.amountPerUnit")
                    : p.quantity === "ResidualSolventPpm" || p.quantity === "Residual solvent (ppm)"
                    ? t("entry.quantities.residualSolventPpm")
                    : p.quantity === "RelatedSubstance"
                    ? t("entry.quantities.relatedSubstance")
                    : p.quantity;

                return (
                  <TableRow
                    key={`${p.hplcMethodAnalyteId}-${p.quantity}-${p.replicateNo ?? "rep"}-${idx}`}
                    hover
                    sx={{
                      backgroundColor: isReported ? theme.palette.action.hover : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontWeight: isReported ? 700 : 500 }}>
                      <Ltr>{p.parameterName}</Ltr>
                    </TableCell>
                    <TableCell>{quantityLabel}</TableCell>
                    <TableCell>
                      {isReported ? (
                        <StatusBadge status="Assigned" label={t("entry.calcSummary.reportedResult")} />
                      ) : (
                        t("entry.calcSummary.replicateNumber", { num: p.replicateNo })
                      )}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                      <Ltr>{p.display}</Ltr>
                    </TableCell>
                    <TableCell>{p.unit ? <Ltr>{p.unit}</Ltr> : "—"}</TableCell>
                    <TableCell>{p.specLimit ? <Ltr>{p.specLimit}</Ltr> : "—"}</TableCell>
                    <TableCell>
                      <HplcStatusBadge status={p.status} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {preview.length > 0 && (
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
          {t("entry.calcSummary.provisionalNote")}
        </Typography>
      )}
    </Box>
  );
}
