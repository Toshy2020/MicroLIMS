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
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { HplcWorkspaceService } from "../../../hplcWorkspace/services/HplcWorkspaceService";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationResultsSectionProps {
  qualification: WorkingStandardQualificationDto;
}

export const QualificationResultsSection: React.FC<QualificationResultsSectionProps> = ({
  qualification
}) => {
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
      toast.error(`Failed to open evidence #${id}`);
    }
  };

  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          HPLC RUN & ASSAY RESULTS
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
                Run {run.runCode} (Sample #{run.runSampleId})
              </Button>
              <Chip size="small" label={run.status} sx={{ fontSize: 11 }} />
            </Stack>
          ) : (
            <Alert severity="info" sx={{ py: 0.5 }}>
              Awaiting HPLC workspace run assignment and execution.
            </Alert>
          )}
        </Box>

        {/* Pass/Fail Banner */}
        {qualification.status !== "Draft" && (
          <Box sx={{ mb: 2 }}>
            {passed ? (
              <Alert severity="success" sx={{ py: 0.5 }}>
                Passed analytical acceptance criteria.
              </Alert>
            ) : (
              <Alert severity="error" sx={{ py: 0.5 }}>
                Failed acceptance criteria: {failureReasons || "Criteria not met."}
              </Alert>
            )}
          </Box>
        )}

        {/* Results Values (API only - no client math) */}
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2, mb: 2 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">Mean Assay %</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700 }}>
              {meanAssayPercent != null ? `${meanAssayPercent.toFixed(2)} %` : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">RSD %</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700 }}>
              {rsdPercent != null ? `${rsdPercent.toFixed(2)} %` : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">Potency %</Typography>
            <Typography variant="body1" sx={{ fontWeight: 700, color: "primary.main" }}>
              {potencyPercent != null ? `${potencyPercent.toFixed(2)} %` : "—"}
            </Typography>
          </Box>
        </Box>

        {/* Replicate Assay Percents */}
        {replicateAssayPercents && replicateAssayPercents.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
              Replicate Assays ({replicateAssayPercents.length})
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              {replicateAssayPercents.map((val, idx) => (
                <Chip
                  key={idx}
                  size="small"
                  variant="outlined"
                  label={`R${idx + 1}: ${val.toFixed(2)} %`}
                  sx={{ fontSize: 11 }}
                />
              ))}
            </Box>
          </Box>
        )}

        {/* Run Evidence Links */}
        <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: "block", mb: 0.75 }}>
            HPLC RUN EVIDENCE
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
                  sx={{ fontSize: 11, py: 0.25 }}
                >
                  Evidence #{evId}
                </Button>
              ))}
            </Box>
          ) : (
            <Typography variant="caption" color="text.secondary" sx={{ fontStyle: "italic" }}>
              No HPLC run evidence files available.
            </Typography>
          )}
        </Box>
      </CardContent>
    </Card>
  );
};
