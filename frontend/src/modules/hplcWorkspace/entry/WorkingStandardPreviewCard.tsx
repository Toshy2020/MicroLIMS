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
  Stack,
  Paper,
  useTheme
} from "@mui/material";
import { Trans } from "react-i18next";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import type { WorkingStandardPreviewDto } from "../types";

export interface WorkingStandardPreviewCardProps {
  preview?: WorkingStandardPreviewDto | null;
  submitted?: boolean;
}

export function WorkingStandardPreviewCard({
  preview,
  submitted
}: WorkingStandardPreviewCardProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();

  const metrics = preview ? [
    { label: t("entry.wsPreview.meanAssay"), value: `${preview.meanAssayPercent.toFixed(2)}%` },
    { label: t("entry.wsPreview.rsd"), value: preview.rsdPercent != null ? `${preview.rsdPercent.toFixed(2)}%` : "—" },
    { label: t("entry.wsPreview.potency"), value: `${preview.potencyPercent.toFixed(3)}%` },
    {
      label: t("entry.wsPreview.acceptanceCriteria"),
      value: preview.passed ? t("entry.wsPreview.passed") : t("entry.wsPreview.failed"),
      color: preview.passed ? "success.main" : "error.main",
      isStatusText: true
    }
  ] : [];

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
        {preview && (
          <StatusBadge
            status={preview.passed ? "Passed" : "Failed"}
            label={preview.passed ? t("entry.wsPreview.evalPassed") : t("entry.wsPreview.evalFailed")}
          />
        )}
        <StatusBadge
          status={submitted ? "Completed" : "PendingReview"}
          label={submitted ? t("entry.wsPreview.officialBadge") : t("entry.wsPreview.provisionalBadge")}
        />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("entry.wsPreview.subtitle")}
        </Typography>
      </Box>

      {!preview ? (
        <Box sx={{ p: 2 }}>
          <Alert severity="info">
            <Trans
              i18nKey="entry.wsPreview.noPreview"
              ns="hplcWorkspace"
              components={{ strong: <strong /> }}
            />
          </Alert>
        </Box>
      ) : (
        <Stack spacing={2}>
          {/* Key metrics grid */}
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" },
              gap: 1.5
            }}
          >
            {metrics.map((m) => (
              <Paper
                key={m.label}
                elevation={0}
                sx={{
                  p: 1.5,
                  border: `1px solid ${theme.palette.divider}`,
                  borderRadius: 1,
                  bgcolor: theme.palette.background.paper
                }}
              >
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {m.label}
                </Typography>
                <Typography
                  variant="h6"
                  sx={{
                    fontWeight: 700,
                    fontVariantNumeric: "tabular-nums",
                    ...(m.color ? { color: m.color } : {})
                  }}
                >
                  {m.isStatusText ? m.value : <Ltr>{m.value}</Ltr>}
                </Typography>
              </Paper>
            ))}
          </Box>

          {!preview.passed && preview.failureReasons && (
            <Alert severity="error">
              <strong>{t("entry.wsPreview.evalFailure")}</strong> {preview.failureReasons}
            </Alert>
          )}

          {/* Replicate Assay Details Table */}
          {preview.replicateAssayPercents && preview.replicateAssayPercents.length > 0 && (
            <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>{t("entry.wsPreview.replicate")}</TableCell>
                    <TableCell align="right">{t("entry.wsPreview.assayPercent")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {preview.replicateAssayPercents.map((val, idx) => (
                    <TableRow key={`rep-assay-${idx}`} hover>
                      <TableCell sx={{ fontWeight: 600 }}>{t("entry.wsPreview.replicateNumber", { num: idx + 1 })}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                        <Ltr>{`${val.toFixed(2)}%`}</Ltr>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {!submitted && (
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("entry.wsPreview.provisionalNote")}
            </Typography>
          )}
        </Stack>
      )}
    </Box>
  );
}
