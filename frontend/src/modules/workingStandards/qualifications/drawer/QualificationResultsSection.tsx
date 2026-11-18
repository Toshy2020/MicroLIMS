import React from "react";
import {
  Card,
  CardContent,
  Typography,
  Box,
  Button,
  Stack,
  Alert,
  Chip
} from "@mui/material";
import LaunchIcon from "@mui/icons-material/Launch";
import DescriptionIcon from "@mui/icons-material/Description";
import { Trans } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Ltr } from "../../../../components/Ltr";
import { useTranslation } from "../../../../i18n";
import { HplcWorkspaceService } from "../../../hplcWorkspace/services/HplcWorkspaceService";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationResultsSectionProps {
  qualification: WorkingStandardQualificationDto;
}

export const QualificationResultsSection: React.FC<QualificationResultsSectionProps> = ({
  qualification
}) => {
  const { t } = useTranslation("workingStandards");
  const navigate = useNavigate();
  const { run, replicateAssayPercents, meanAssayPercent, rsdPercent, potencyPercent, passed, failureReasons, runEvidenceIds } = qualification;

  const handleNavigateToRun = () => {
    if (!run) return;
    navigate(`/hplc-workspace/${run.equipmentId}/run/${run.hplcRunId}/qualification/${run.runSampleId}`);
  };

  const handleOpenEvidence = async (id: number) => {
    try {
      await HplcWorkspaceService.openEvidenceInNewTab(id);
    } catch {
      toast.error(t("results.openEvidenceError", { id }));
    }
  };

  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          {t("results.heading")}
        </Typography>

        {/* HPLC Run Link */}
        <Box sx={{ mb: 2 }}>
          {run ? (
            <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
              <Button
                variant="outlined"
                size="small"
                endIcon={<LaunchIcon fontSize="small" />}
                onClick={handleNavigateToRun}
                sx={{ fontFamily: "monospace", fontWeight: 600 }}
              >
                <Trans
                  i18nKey="results.runButton"
                  ns="workingStandards"
                  values={{ runCode: run.runCode, sampleId: run.runSampleId }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Button>
              <Chip size="small" label={run.status} sx={{ fontSize: 12 }} />
            </Stack>
          ) : (
            <Alert severity="info" sx={{ py: 0.5 }}>
              {t("results.awaitingRun")}
            </Alert>
          )}
        </Box>

        {/* Pass/Fail Banner */}
        {qualification.status !== "Draft" && (
          <Box sx={{ mb: 2 }}>
            {passed ? (
              <Alert severity="success" sx={{ py: 0.5 }}>
                {t("results.passed")}
              </Alert>
            ) : (
              <Alert severity="error" sx={{ py: 0.5 }}>
                {t("results.failed", { reasons: failureReasons || t("results.criteriaNotMet") })}
              </Alert>
            )}
          </Box>
        )}

        {/* Results Values (API only - no client math) */}
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2, mb: 2 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">{t("results.meanAssay")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700 }}>
              {meanAssayPercent != null ? <Ltr>{t("units.percent", { value: meanAssayPercent.toFixed(2) })}</Ltr> : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">{t("results.rsd")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700 }}>
              {rsdPercent != null ? <Ltr>{t("units.percent", { value: rsdPercent.toFixed(2) })}</Ltr> : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">{t("results.potency")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700, color: "primary.main" }}>
              {potencyPercent != null ? <Ltr>{t("units.percent", { value: potencyPercent.toFixed(2) })}</Ltr> : "—"}
            </Typography>
          </Box>
        </Box>

        {/* Replicate Assay Percents */}
        {replicateAssayPercents && replicateAssayPercents.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
              <Trans
                i18nKey="results.replicateAssays"
                ns="workingStandards"
                values={{ count: replicateAssayPercents.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              {replicateAssayPercents.map((val, idx) => (
                <Chip
                  key={idx}
                  size="small"
                  variant="outlined"
                  label={<Ltr>{t("results.replicateChip", { index: idx + 1, value: val.toFixed(2) })}</Ltr>}
                  sx={{ fontSize: 12 }}
                />
              ))}
            </Box>
          </Box>
        )}

        {/* Run Evidence Links */}
        <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: "block", mb: 0.75 }}>
            {t("results.evidenceHeading")}
          </Typography>
          {runEvidenceIds && runEvidenceIds.length > 0 ? (
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              {runEvidenceIds.map((evId) => (
                <Button
                  key={evId}
                  size="small"
                  variant="outlined"
                  startIcon={<DescriptionIcon fontSize="small" />}
                  endIcon={<LaunchIcon sx={{ fontSize: 13 }} />}
                  onClick={() => handleOpenEvidence(evId)}
                  sx={{ fontSize: 12, py: 0.25 }}
                >
                  <Trans
                    i18nKey="results.evidenceButton"
                    ns="workingStandards"
                    values={{ id: evId }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </Button>
              ))}
            </Box>
          ) : (
            <Typography variant="caption" color="text.secondary" sx={{ fontStyle: "italic" }}>
              {t("results.noEvidence")}
            </Typography>
          )}
        </Box>
      </CardContent>
    </Card>
  );
};
