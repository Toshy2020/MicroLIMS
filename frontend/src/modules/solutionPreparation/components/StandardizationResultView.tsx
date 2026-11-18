import {
  Box,
  Typography,
  Alert,
  Stack,
  Paper,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer
} from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";
import type { TitrantStandardizationResponse, RecipeSnapshot } from "../types";

interface Props {
  result: TitrantStandardizationResponse;
  recipe: RecipeSnapshot | null;
  blankRequired: boolean;
}

export function StandardizationResultView({ result, recipe, blankRequired }: Props) {
  const { t } = useTranslation("solutionPreparation");
  return (
    <Stack spacing={2.5}>
      <Alert severity={result.passed ? "success" : "error"}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          {result.passed ? t("standardizationResult.passedTitle") : t("standardizationResult.failedTitle")}
        </Typography>
        <Typography variant="body2">
          {result.passed ? (
            <Trans
              i18nKey="standardizationResult.passedBody"
              ns="solutionPreparation"
              values={{ factor: result.meanFactor.toFixed(4) }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          ) : (
            t("standardizationResult.failedBody")
          )}
        </Typography>
        {!result.passed && result.failureReasons && (
          <Typography variant="body2" sx={{ mt: 1, whiteSpace: "pre-line", fontWeight: 600 }}>
            {result.failureReasons}
          </Typography>
        )}
      </Alert>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" }, gap: 2 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("standardizationResult.meanFactor")}
            </Typography>
            <Typography variant="h6" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              <Ltr>{result.meanFactor.toFixed(4)}</Ltr>
            </Typography>
            {recipe?.factorMin != null && recipe?.factorMax != null && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                <Trans
                  i18nKey="standardizationResult.limits"
                  ns="solutionPreparation"
                  values={{ min: recipe.factorMin.toFixed(4), max: recipe.factorMax.toFixed(4) }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            )}
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("standardizationResult.rsd")}
            </Typography>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              {result.rsdPercent != null
                ? <Ltr>{t("units.percent", { value: result.rsdPercent.toFixed(2) })}</Ltr>
                : t("standardizationResult.na")}
            </Typography>
            {recipe?.maxRsdPercent != null && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                <Trans
                  i18nKey="standardizationResult.maxRsd"
                  ns="solutionPreparation"
                  values={{ max: recipe.maxRsdPercent.toFixed(2) }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            )}
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("standardizationResult.standardizedAt")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{formatDateTimeWithTimeZone(result.standardizedAt)}</Ltr>
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("standardizationResult.validUntil")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {result.validUntil
                ? <Ltr>{formatLabCalendarDate(result.validUntil)}</Ltr>
                : (result.passed ? t("standardizationResult.beforeEachUse") : "—")}
            </Typography>
          </Box>
        </Box>
      </Paper>

      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
        {t("standardizationResult.replicatesEvaluated")}
      </Typography>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>{t("standardizationResult.columns.rep")}</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>{t("standardizationResult.columns.titrantVol")}</TableCell>
              {blankRequired && <TableCell sx={{ fontWeight: 700 }}>{t("standardizationResult.columns.blank")}</TableCell>}
              <TableCell sx={{ fontWeight: 700 }}>{t("standardizationResult.columns.calculatedFactor")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {result.replicates.map((r) => (
              <TableRow key={r.id || r.replicateNo}>
                <TableCell sx={{ fontWeight: 600 }}><Ltr>{r.replicateNo}</Ltr></TableCell>
                <TableCell><Ltr>{r.titrantVolumeMl}</Ltr></TableCell>
                {blankRequired && <TableCell><Ltr>{r.blankMl != null ? r.blankMl : 0}</Ltr></TableCell>}
                <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                  <Ltr>{r.factor.toFixed(4)}</Ltr>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Stack>
  );
}
