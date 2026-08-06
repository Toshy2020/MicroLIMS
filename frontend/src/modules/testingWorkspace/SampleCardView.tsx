import { Grid, Paper, Box, Typography, Stack } from "@mui/material";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge, StatusBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { useAuth } from "../../contexts/AuthContext";

interface Props {
  samples: SampleCardType[];
  onTestClick: (test: TestOrderSummary, sample: SampleCardType) => void;
  onNeedsPreparationClick: (sample: SampleCardType) => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
}

const formatDate = (d: string) => new Date(d).toLocaleDateString();

// Same data/handlers as SampleTableRow, just laid out as cards - no new
// interaction model, only a different layout for the same click targets.
export function SampleCardView({ samples, onTestClick, onNeedsPreparationClick, onLifecycleBadgeClick }: Props) {
  const { role } = useAuth();

  return (
    <Grid container spacing={2}>
      {samples.map((sample) => {
        const needsPreparation = sample.preparationStatus === "NeedsPreparation";
        return (
          <Grid item xs={12} sm={6} md={4} key={sample.sampleId}>
            <Paper sx={{ p: 2, height: "100%", borderLeft: needsPreparation ? "3px solid #f59e0b" : "3px solid transparent" }}>
              <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 1 }}>
                <Box>
                  <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{sample.displayName}</Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{sample.referenceNumber}</Typography>
                </Box>
                <CategoryBadge category={sample.category} />
              </Stack>

              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{sample.causeOfTesting} · {formatDate(sample.receivedAt)}</Typography>

              <Box sx={{ mt: 1.5, mb: 1.5 }}>
                {needsPreparation ? (
                  <Box
                    onClick={() => onNeedsPreparationClick(sample)}
                    sx={{
                      cursor: "pointer", display: "inline-block", px: 1.25, py: 0.5, borderRadius: 5, fontSize: 12, fontWeight: 700,
                      border: "1px solid #f59e0b", bgcolor: "#fef3c7", color: "#92400e", "&:hover": { bgcolor: "#fde68a" }
                    }}
                  >
                    Needs Preparation
                  </Box>
                ) : (
                  <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                    {sample.assignedTests.map((test) => (
                      <Box
                        key={test.testOrderId}
                        onClick={() => onTestClick(test, sample)}
                        sx={{
                          cursor: "pointer", display: "flex", alignItems: "center", gap: 0.5,
                          px: 0.75, py: 0.25, borderRadius: 1.5, bgcolor: "#f3e8ff", "&:hover": { bgcolor: "#e9d5ff" }
                        }}
                      >
                        <Typography sx={{ fontSize: 11, fontWeight: 600 }}>{test.testCode}</Typography>
                        <StatusBadge status={test.status} />
                      </Box>
                    ))}
                  </Stack>
                )}
              </Box>

              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName ?? "Unassigned"}
                </Typography>
                <SampleLifecycleBadge status={sample.status} role={role} onClick={() => onLifecycleBadgeClick(sample.sampleId)} />
              </Stack>
            </Paper>
          </Grid>
        );
      })}
      {samples.length === 0 && (
        <Grid item xs={12}><Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No samples match this filter.</Typography></Grid>
      )}
    </Grid>
  );
}
