import { Grid, Paper, Box, Typography, Stack, Tooltip, useTheme } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { brandColors } from "../../theme";
import { isInteractiveElement } from "../../utils/isInteractiveElement";
import { dueLabel } from "../receiving/components/SampleRegisterTable";
import { assignedAnalystsLabel } from "../receiving/utils/assignedAnalysts";

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
  const theme = useTheme();

  return (
    <Grid container spacing={2}>
      {samples.map((sample) => {
        const needsPreparation = sample.preparationStatus === "NeedsPreparation";
        const isSelected = selectedSampleId === sample.sampleId;
        const due = dueLabel(sample.dueAt, sample.isOverdue);
        const dueColor = sample.isOverdue ? theme.custom.status.detected.text : theme.palette.text.secondary;

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
                // Overdue wins even over selection, as in the register table.
                borderLeft: sample.isOverdue
                  ? `4px solid ${theme.custom.status.detected.text}`
                  : isSelected
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
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {sample.referenceNumber}
                  </Typography>
                </Box>
                <CategoryBadge category={sample.category} />
              </Stack>

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {sample.causeOfTesting} · {formatDate(sample.receivedAt)}
              </Typography>

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: due ? 1 : 1.5 }}>
                {sample.batchNumber ? `Batch: ${sample.batchNumber}` : `Control: ${sample.controlNumber}`} · Tests: {sample.assignedTests.length}
              </Typography>

              {due && sample.dueAt && (
                <Tooltip title={`Due ${new Date(sample.dueAt).toLocaleString()}`}>
                  <Typography
                    sx={{
                      display: "inline-flex", alignItems: "center", gap: 0.75, mb: 1.5,
                      fontSize: 12, fontWeight: sample.isOverdue ? 700 : 500, color: dueColor,
                      "&::before": { content: '""', width: 6, height: 6, borderRadius: "50%", bgcolor: dueColor }
                    }}
                  >
                    {due}
                  </Typography>
                </Tooltip>
              )}

              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "center"
                }}>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {assignedAnalystsLabel(sample.assignedTests) ?? "Unassigned"}
                </Typography>
                <SampleLifecycleBadge
                  status={sample.status}
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
