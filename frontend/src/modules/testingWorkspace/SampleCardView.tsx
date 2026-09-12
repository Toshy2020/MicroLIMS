import { Grid, Paper, Box, Typography, Stack, useTheme } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { useAuth } from "../../contexts/AuthContext";
import { brandColors } from "../../theme";
import { isInteractiveElement } from "../../utils/isInteractiveElement";

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
  const theme = useTheme();

  return (
    <Grid container spacing={2}>
      {samples.map((sample) => {
        const needsPreparation = sample.preparationStatus === "NeedsPreparation";
        const isSelected = selectedSampleId === sample.sampleId;

        return (
          <Grid
            key={sample.sampleId}
            size={{
              xs: 12,
              sm: 6,
              md: 4
            }}>
            <Paper
              tabIndex={0}
              role="button"
              onClick={(e) => {
                if (isInteractiveElement(e.target, e.currentTarget)) {
                  return;
                }
                onSelectSample(sample);
              }}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  if (isInteractiveElement(e.target, e.currentTarget)) {
                    return;
                  }
                  e.preventDefault();
                  onSelectSample(sample);
                }
              }}
              sx={{
                p: 2,
                height: "100%",
                cursor: "pointer",
                borderLeft: isSelected
                  ? `4px solid ${brandColors.sectionTitle}`
                  : needsPreparation
                  ? `4px solid ${theme.custom.status.inconclusive.text}`
                  : "4px solid transparent",
                bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
                transition: "all 0.15s ease",
                "&:hover": {
                  boxShadow: theme.palette.mode === "dark" ? "0 4px 12px rgba(0,0,0,0.4)" : "0 4px 12px rgba(0,0,0,0.08)",
                  transform: "translateY(-1px)"
                }
              }}
            >
              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                  mb: 1
                }}>
                <Box>
                  <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 14, color: isSelected ? brandColors.pageTitle : "text.primary" }}>
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

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1.5 }}>
                {sample.batchNumber ? `Batch: ${sample.batchNumber}` : `Control: ${sample.controlNumber}`} · Tests: {sample.assignedTests.length}
              </Typography>

              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "center"
                }}>
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
        <Grid size={12}>
          <Typography sx={{ color: "text.secondary", fontSize: 13, p: 2 }}>
            No samples match this filter.
          </Typography>
        </Grid>
      )}
    </Grid>
  );
}
