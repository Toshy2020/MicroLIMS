import { Trans } from "react-i18next";
import { Box, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography, useTheme } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import { HplcReportsSection } from "./HplcReportsSection";

interface RelatedSubstancePeak {
  label: string;
  relativeRetention: number | null;
  area: number;
  correctionFactor: number;
  percent: number;
  disregarded: boolean;
}

export interface RelatedSubstanceCalculationData {
  quantity: "RelatedSubstance";
  analyte: string;
  runCode: string;
  sstCode: string;
  referenceSolutionPercent: number;
  disregardLimitPercent: number | null;
  replicates: { replicateNo: number; referenceArea: number; peaks: RelatedSubstancePeak[] }[];
}

export function isRelatedSubstanceCalculation(json: string | null): boolean {
  if (!json) return false;
  try {
    return JSON.parse(json)?.quantity === "RelatedSubstance";
  } catch {
    return false;
  }
}

// The peaks behind a related-substances result; the reported rows are in the table above this panel.
export function RelatedSubstancesReview({ calc, testOrderId }: { calc: RelatedSubstanceCalculationData; testOrderId: number }) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  return (
    <Box sx={{ mt: 1.5, p: 2, borderRadius: 2, border: `1px solid ${theme.palette.divider}`, backgroundColor: theme.palette.background.paper }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
            <Trans
              i18nKey="review.relatedSubstances.title"
              ns="hplcWorkspace"
              values={{ analyte: calc.analyte }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <StatusBadge status="Prepared" label={t("review.relatedSubstances.referenceBadge", { percent: calc.referenceSolutionPercent })} />
          <StatusBadge
            status="Prepared"
            label={
              calc.disregardLimitPercent == null
                ? t("review.relatedSubstances.noDisregard")
                : t("review.relatedSubstances.disregardBadge", { percent: calc.disregardLimitPercent })
            }
          />
        </Stack>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="review.relatedSubstances.runSstMeta"
            ns="hplcWorkspace"
            values={{ runCode: calc.runCode, sstCode: calc.sstCode }}
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Typography>
      </Box>

      {calc.replicates.map((r) => (
        <Box key={r.replicateNo} sx={{ mb: 1.5 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, mb: 0.5 }}>
            <Trans
              i18nKey="review.relatedSubstances.replicateMeta"
              ns="hplcWorkspace"
              values={{ repNo: r.replicateNo, area: r.referenceArea }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell sx={{ fontSize: 12 }}>{t("review.relatedSubstances.columns.peak")}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.relatedSubstances.columns.rrt")}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.relatedSubstances.columns.area")}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.relatedSubstances.columns.correctionFactor")}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.relatedSubstances.columns.percent")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {r.peaks.map((p, i) => (
                  <TableRow key={i} hover>
                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}><Ltr>{p.label}</Ltr></TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}><Ltr>{p.relativeRetention ?? "—"}</Ltr></TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}><Ltr>{p.area}</Ltr></TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}><Ltr>{p.correctionFactor}</Ltr></TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}>
                      <Ltr>{p.percent}</Ltr>
                      {p.disregarded ? t("review.relatedSubstances.disregardedSuffix") : ""}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      ))}

      <HplcReportsSection testOrderId={testOrderId} />
    </Box>
  );
}
