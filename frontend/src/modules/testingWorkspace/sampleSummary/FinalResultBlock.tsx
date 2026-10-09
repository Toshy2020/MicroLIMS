import { Box, Typography, Stack, Alert } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";
import { pathogenObservationLabel } from "../utils/pathogenObservationLabel";
import { formatDate } from "./summaryFormatting";
import { LocationResultsTable, SummaryField } from "./SummaryCards";
import { AnalysisResultBlock } from "./AnalysisBlocks";

// Final Result Section component (Separated from Incubation)
export function FinalResultBlock({ order }: { order: TestOrderSummaryDetail }) {
  if (order.analysis) return <AnalysisResultBlock analysis={order.analysis} />;

  const hasLocations = order.locations.length > 0;
  const hasCountReadings = order.countTestReadings.length > 0;
  const hasPathogens = order.pathogenObservations.length > 0;
  const hasBiochemical = order.biochemicalResults.length > 0;
  const hasResults = order.results.length > 0;

  if (!hasLocations && !hasCountReadings && !hasPathogens && !hasBiochemical && !hasResults) {
    return (
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 0.5 }}>Final Result</Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No result recorded yet.</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 1.5 }}>
        Final Result
      </Typography>

      {hasLocations && <LocationResultsTable locations={order.locations} />}

      {!hasLocations && hasCountReadings && (
        <Stack spacing={1.5}>
          {order.countTestReadings.map((r, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(5, 1fr)"
                },
                gap: 1.5
              }}
            >
              <SummaryField label="Plate Readings" value={r.plateReadings} />
              <SummaryField label="Dilution Factor" value={r.dilutionFactor} />
              <SummaryField label="Average" value={r.average} />
              <SummaryField label="Calculated" value={r.calculatedResult} />
              <SummaryField label="Reported Result" value={r.reportedResult} highlight />
              <SummaryField label="Alert / Action / Spec" value={`${r.alertLimit ?? "—"} / ${r.actionLimit ?? "—"} / ${r.specLimit ?? "—"}`} />
              <Box>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>Status</Typography>
                <Box sx={{ mt: 0.5 }}>
                  <StatusBadge status={r.status} />
                </Box>
              </Box>
              <SummaryField label="Entered By" value={r.enteredByName} />
              <SummaryField label="Entered At" value={formatDate(r.enteredAt)} />
            </Box>
          ))}
        </Stack>
      )}

      {hasPathogens && (
        <Stack spacing={1} sx={{ mt: hasCountReadings ? 1.5 : 0 }}>
          {order.pathogenObservations.map((p, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(4, 1fr)"
                },
                gap: 1.5,
                p: 1.25,
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1
              }}
            >
              <SummaryField label="Step" value={p.stepName} />
              <SummaryField label="Observation" value={pathogenObservationLabel(p.observation)} highlight />
              <SummaryField label="Observed By" value={p.observedByName} />
              <SummaryField label="Observed At" value={formatDate(p.observedAt)} />
            </Box>
          ))}
        </Stack>
      )}

      {hasBiochemical && (
        <Stack spacing={1} sx={{ mt: (hasCountReadings || hasPathogens) ? 1.5 : 0 }}>
          {order.biochemicalResults.map((b, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(4, 1fr)"
                },
                gap: 1.5,
                p: 1.25,
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1
              }}
            >
              <SummaryField label="Step" value={b.stepName} />
              <SummaryField
                label="Interpretation"
                value={b.organismDetected === true ? "Detected" : b.organismDetected === false ? "Not Detected" : "Undetermined"}
                highlight
              />
              <SummaryField label="Result" value={b.biochemicalResultText} />
              <SummaryField label="Submitted By" value={b.submittedByName} />
            </Box>
          ))}
        </Stack>
      )}

      {hasResults && (
        <Stack spacing={1} sx={{ mt: (hasCountReadings || hasPathogens || hasBiochemical) ? 1.5 : 0 }}>
          {order.results.map((r, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(3, 1fr)"
                },
                gap: 1.5,
                p: 1.25,
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1
              }}
            >
              <SummaryField label="Result" value={r.interpretedValue ?? r.rawValue} highlight />
              <SummaryField label="Entered By" value={r.enteredByName} />
              <SummaryField label="Entered At" value={formatDate(r.enteredAt)} />
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
