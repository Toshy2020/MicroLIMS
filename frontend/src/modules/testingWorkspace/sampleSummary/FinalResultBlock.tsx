import { Box, Typography, Stack } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";
import { pathogenObservationLabel } from "../utils/pathogenObservationLabel";
import { formatDate } from "./summaryFormatting";
import { LocationResultsTable, SummaryField } from "./SummaryCards";
import { AnalysisResultBlock } from "./AnalysisBlocks";

// Final Result Section component (Separated from Incubation)
export function FinalResultBlock({ order }: { order: TestOrderSummaryDetail }) {
  const { t } = useTranslation("testingWorkspace");
  if (order.analysis) return <AnalysisResultBlock analysis={order.analysis} />;

  const hasLocations = order.locations.length > 0;
  const hasCountReadings = order.countTestReadings.length > 0;
  const hasPathogens = order.pathogenObservations.length > 0;
  const hasBiochemical = order.biochemicalResults.length > 0;
  const hasResults = order.results.length > 0;

  if (!hasLocations && !hasCountReadings && !hasPathogens && !hasBiochemical && !hasResults) {
    return (
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 0.5 }}>
          {t("sampleSummary.finalResult.title")}
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {t("sampleSummary.finalResult.none")}
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 1.5 }}>
        {t("sampleSummary.finalResult.title")}
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
              <SummaryField label={t("sampleSummary.finalResult.plateReadings")} value={r.plateReadings ? <Ltr>{r.plateReadings}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.dilutionFactor")} value={r.dilutionFactor != null ? <Ltr>{r.dilutionFactor}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.average")} value={r.average != null ? <Ltr>{r.average}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.calculated")} value={r.calculatedResult != null ? <Ltr>{r.calculatedResult}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.reportedResult")} value={r.reportedResult ? <Ltr>{r.reportedResult}</Ltr> : null} highlight />
              <SummaryField
                label={t("sampleSummary.finalResult.limits")}
                value={<Ltr>{`${r.alertLimit ?? "—"} / ${r.actionLimit ?? "—"} / ${r.specLimit ?? "—"}`}</Ltr>}
              />
              <Box>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  {t("sampleSummary.finalResult.status")}
                </Typography>
                <Box sx={{ mt: 0.5 }}>
                  <StatusBadge status={r.status} />
                </Box>
              </Box>
              <SummaryField label={t("sampleSummary.finalResult.enteredBy")} value={r.enteredByName ? <Ltr>{r.enteredByName}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.enteredAt")} value={<Ltr>{formatDate(r.enteredAt)}</Ltr>} />
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
              <SummaryField label={t("sampleSummary.finalResult.step")} value={p.stepName} />
              <SummaryField
                label={t("sampleSummary.finalResult.observation")}
                value={t(`sampleSummary.observation.${p.observation}`, { defaultValue: pathogenObservationLabel(p.observation, t) })}
                highlight
              />
              <SummaryField label={t("sampleSummary.finalResult.observedBy")} value={p.observedByName ? <Ltr>{p.observedByName}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.observedAt")} value={<Ltr>{formatDate(p.observedAt)}</Ltr>} />
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
              <SummaryField label={t("sampleSummary.finalResult.step")} value={b.stepName} />
              <SummaryField
                label={t("sampleSummary.finalResult.interpretation")}
                value={
                  b.organismDetected === true
                    ? t("sampleSummary.finalResult.detected")
                    : b.organismDetected === false
                    ? t("sampleSummary.finalResult.notDetected")
                    : t("sampleSummary.finalResult.undetermined")
                }
                highlight
              />
              <SummaryField label={t("sampleSummary.finalResult.result")} value={b.biochemicalResultText} />
              <SummaryField label={t("sampleSummary.finalResult.submittedBy")} value={b.submittedByName ? <Ltr>{b.submittedByName}</Ltr> : null} />
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
              <SummaryField
                label={t("sampleSummary.finalResult.result")}
                value={(r.interpretedValue ?? r.rawValue) ? <Ltr>{r.interpretedValue ?? r.rawValue}</Ltr> : null}
                highlight
              />
              <SummaryField label={t("sampleSummary.finalResult.enteredBy")} value={r.enteredByName ? <Ltr>{r.enteredByName}</Ltr> : null} />
              <SummaryField label={t("sampleSummary.finalResult.enteredAt")} value={<Ltr>{formatDate(r.enteredAt)}</Ltr>} />
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
