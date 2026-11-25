import { Box, Chip, Paper, Skeleton, Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type {
  OosChecklistFactsDto,
  OosInvestigationResultDto
} from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

interface FactsPanelProps {
  facts: OosChecklistFactsDto | null;
  results: OosInvestigationResultDto[];
  loading: boolean;
}

export function FactsPanel({ facts, results, loading }: FactsPanelProps) {
  const { t } = useTranslation("oosTracking");

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2, bgcolor: "background.default", borderRadius: 1.5 }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
        {t("facts.title")}
      </Typography>

      {loading ? (
        <Stack spacing={1}>
          <Skeleton variant="rounded" height={48} />
          <Skeleton variant="rounded" height={48} />
        </Stack>
      ) : !facts || facts.orders.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("facts.empty")}
        </Typography>
      ) : (
        <Stack spacing={2}>
          {facts.orders.map((order) => {
            const result = results.find((r) => r.testOrderId === order.testOrderId);
            return (
              <Box
                key={order.testOrderId}
                sx={{
                  p: 1.5,
                  bgcolor: "background.paper",
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 1
                }}
              >
                <Box
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    flexWrap: "wrap",
                    gap: 1,
                    mb: 1
                  }}
                >
                  <Typography
                    variant="body2"
                    sx={{ fontWeight: 700, fontFamily: monospaceFontFamily }}
                  >
                    {result ? `${result.testCode} — ${result.testDisplayName}` : `#${order.testOrderId}`}
                  </Typography>
                  <Chip
                    size="small"
                    variant="outlined"
                    label={t("facts.auditCount", { count: order.resultAuditCount })}
                    sx={{ fontVariantNumeric: "tabular-nums" }}
                  />
                </Box>

                <Stack spacing={1}>
                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                      {t("facts.equipment")}
                    </Typography>
                    {order.equipment.length === 0 ? (
                      <Typography variant="body2" color="text.secondary">
                        {t("facts.noEquipment")}
                      </Typography>
                    ) : (
                      order.equipment.map((eq, idx) => (
                        <Typography key={`${eq.equipmentId}-${idx}`} variant="body2">
                          #{eq.equipmentId}
                          {eq.runCalibrationStatus ? ` · ${eq.runCalibrationStatus}` : ""}
                          {eq.usedAt ? ` · ${formatDateTimeWithTimeZone(eq.usedAt)}` : ""}
                        </Typography>
                      ))
                    )}
                  </Box>

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                      {t("facts.media")}
                    </Typography>
                    {order.media.length === 0 ? (
                      <Typography variant="body2" color="text.secondary">
                        {t("facts.noMedia")}
                      </Typography>
                    ) : (
                      order.media.map((m) => (
                        <Typography
                          key={m.mediaId}
                          variant="body2"
                          sx={{ fontFamily: monospaceFontFamily }}
                        >
                          {m.lotNumber} ·{" "}
                          {m.isReleasedForUse ? t("facts.released") : t("facts.notReleased")} ·{" "}
                          {t("facts.expires", { date: m.expiryDate.slice(0, 10) })}
                        </Typography>
                      ))
                    )}
                  </Box>

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                      {t("facts.solutions")}
                    </Typography>
                    {order.solutions.length === 0 ? (
                      <Typography variant="body2" color="text.secondary">
                        {t("facts.noSolutions")}
                      </Typography>
                    ) : (
                      order.solutions.map((s) => (
                        <Typography
                          key={s.solutionPreparationId}
                          variant="body2"
                          sx={{ fontFamily: monospaceFontFamily }}
                        >
                          {s.code ?? `#${s.solutionPreparationId}`}
                          {s.approvalStatus ? ` · ${s.approvalStatus}` : ""}
                          {s.expiresAt ? ` · ${t("facts.expires", { date: s.expiresAt.slice(0, 10) })}` : ""}
                        </Typography>
                      ))
                    )}
                  </Box>

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                      {t("facts.standards")}
                    </Typography>
                    {order.standards.length === 0 ? (
                      <Typography variant="body2" color="text.secondary">
                        {t("facts.noStandards")}
                      </Typography>
                    ) : (
                      order.standards.map((st, idx) => (
                        <Typography key={`${st.materialId}-${idx}`} variant="body2">
                          #{st.materialId}
                          {st.purityPercent != null ? ` · ${st.purityPercent}%` : ""}
                        </Typography>
                      ))
                    )}
                  </Box>

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                      {t("facts.incubations")}
                    </Typography>
                    {order.incubations.length === 0 ? (
                      <Typography variant="body2" color="text.secondary">
                        {t("facts.noIncubations")}
                      </Typography>
                    ) : (
                      order.incubations.map((inc, idx) => (
                        <Typography
                          key={`${inc.stepNumber}-${inc.stageNumber}-${idx}`}
                          variant="body2"
                          sx={{ fontVariantNumeric: "tabular-nums" }}
                        >
                          {t("facts.incubationStep", {
                            step: inc.stepNumber,
                            stage: inc.stageNumber,
                            startedAt: formatDateTimeWithTimeZone(inc.startedAt)
                          })}
                          {inc.completedAt ? ` -> ${formatDateTimeWithTimeZone(inc.completedAt)}` : ""}
                          {inc.currentMinimumHours != null && inc.currentMaximumHours != null
                            ? ` (${t("facts.window", { min: inc.currentMinimumHours, max: inc.currentMaximumHours })})`
                            : ""}
                        </Typography>
                      ))
                    )}
                  </Box>
                </Stack>
              </Box>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
}
