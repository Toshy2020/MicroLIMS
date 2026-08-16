import { Grid, Paper, Box, Typography, Stack } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { useAuth } from "../../contexts/AuthContext";
import { brandColors } from "../../theme";

interface Props {
  samples: SampleCardType[];
  selectedSampleId?: number | null;
  onSelectSample: (sample: SampleCardType) => void;
  onNeedsPreparationClick: (sample: SampleCardType) => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
}

const formatDate = (d: string) => new Date(d).toLocaleDateString();

export function SampleCardView({
  samples,
  selectedSampleId,
  onSelectSample,
  onNeedsPreparationClick,
  onLifecycleBadgeClick
}: Props) {
  const { role } = useAuth();

  return (
    <Grid container spacing={2}>
      {samples.map((sample) => {
        const needsPreparation = sample.preparationStatus === "NeedsPreparation";
        const isSelected = selectedSampleId === sample.sampleId;

        return (
          <Grid item xs={12} sm={6} md={4} key={sample.sampleId}>
            <Paper
              onClick={() => onSelectSample(sample)}
              sx={{
                p: 2,
                height: "100%",
                cursor: "pointer",
                borderLeft: isSelected
                  ? `4px solid ${brandColors.sectionTitle}`
                  : needsPreparation
                  ? "4px solid #f59e0b"
                  : "4px solid transparent",
                bgcolor: isSelected ? "#faf5ff" : "#ffffff",
                transition: "all 0.15s ease",
                "&:hover": {
                  boxShadow: "0 4px 12px rgba(0,0,0,0.08)",
                  transform: "translateY(-1px)"
                }
              }}
            >
              <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 1 }}>
                <Box>
                  <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 14, color: isSelected ? brandColors.pageTitle : "#111827" }}>
                    {sample.displayName}
                  </Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                    {sample.referenceNumber}
                  </Typography>
                </Box>
                <CategoryBadge category={sample.category} />
              </Stack>

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {sample.causeOfTesting} · {formatDate(sample.receivedAt)}
              </Typography>

              <Typography sx={{ fontSize: 12, color: "#374151", mb: 1.5 }}>
                {sample.batchNumber ? `Batch: ${sample.batchNumber}` : `Control: ${sample.controlNumber}`} · Tests: {sample.assignedTests.length}
              </Typography>

              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName ?? "Unassigned"}
                </Typography>
                <SampleLifecycleBadge
                  status={sample.status}
                  role={role}
                  onClick={() => onLifecycleBadgeClick(sample.sampleId)}
                />
              </Stack>
            </Paper>
          </Grid>
        );
      })}
      {samples.length === 0 && (
        <Grid item xs={12}>
          <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>
            No samples match this filter.
          </Typography>
        </Grid>
      )}
    </Grid>
  );
}
