import { SopMethodDetails } from "../components/SopMethodDetails";
import { useEffect, useRef } from "react";
import { Box, Typography, Stack, Button, Accordion, AccordionSummary, AccordionDetails, Chip, useTheme } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import AssignmentReturnOutlinedIcon from "@mui/icons-material/AssignmentReturnOutlined";
import { Trans } from "react-i18next";
import { GatedAction } from "../../../components/GatedAction";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { useActionAvailability } from "../../../hooks/useActionAvailability";
import { StatusBadge } from "../../../components/StatusBadge";
import { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";
import { humanize } from "../SampleReportPage";
import { IncubationStageBlock } from "./SummaryCards";
import { FinalResultBlock } from "./FinalResultBlock";
import { formatExactTime } from "./summaryFormatting";

// Return to Analyst for one test order, gated by the server's answer for that order.
function ReturnToAnalystAction({
  order,
  onReturn
}: {
  order: TestOrderSummaryDetail;
  onReturn?: (order: TestOrderSummaryDetail) => void;
}) {
  const { t } = useTranslation("testingWorkspace");
  const availability = useActionAvailability("test-order", order.testOrderId);
  const { refresh } = availability;
  // The order moved on (returned, reviewed): its reasons moved with it.
  const status = useRef(order.status);
  useEffect(() => {
    if (status.current === order.status) return;
    status.current = order.status;
    refresh();
  }, [order.status, refresh]);

  return (
    <GatedAction action="review.return" availability={availability}>
      <Button
        variant="outlined"
        color="warning"
        size="small"
        startIcon={<AssignmentReturnOutlinedIcon />}
        onClick={() => onReturn?.(order)}
        sx={{
          fontWeight: 600,
          fontSize: 12,
          borderColor: "warning.main",
          color: "warning.dark",
          "&:hover": {
            borderColor: "warning.dark",
            bgcolor: "rgba(237, 108, 2, 0.08)"
          }
        }}
      >
        {t("sampleSummary.testResults.returnToAnalyst")}
      </Button>
    </GatedAction>
  );
}

// 2. Test Results Section
export function TestResultsSection({
  testOrders,
  overallStatus,
  showSop,
  labTimeZoneId,
  sopCaptureStartDateUtc,
  onReturnTest
}: {
  testOrders: TestOrderSummaryDetail[];
  overallStatus: string;
  showSop?: boolean;
  labTimeZoneId?: string;
  sopCaptureStartDateUtc?: string | null;
  onReturnTest?: (order: TestOrderSummaryDetail) => void;
}) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 16, color: theme.palette.primary.main }}>
          {t("sampleSummary.testResults.title")}
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
            {t("sampleSummary.testResults.overallStatus")}
          </Typography>
          <StatusBadge status={overallStatus} />
        </Box>
      </Box>

      <Stack spacing={2}>
        {testOrders.map((order, i) => {
          const isCountTest =
            (order.countTestReadings.length > 0 ||
              (order.results.length > 0 && order.biochemicalResults.length === 0 && order.locations.length === 0)) &&
            order.pathogenObservations.length === 0;
          // Only a count test's result can be returned; whether it can be now, and by this user, is the server's answer.
          const canReturnOrder = !order.isSuperseded && isCountTest;

          return (
            <Accordion
              key={order.testOrderId}
              defaultExpanded={!order.isSuperseded && (i === 0 || order.status === "ResultEntered")}
              sx={{
                border: "1px solid",
                borderColor: "divider",
                borderRadius: "8px !important",
                overflow: "hidden",
                "&:before": { display: "none" }
              }}
            >
              <AccordionSummary
                expandIcon={<ExpandMoreIcon />}
                sx={{ bgcolor: "background.default", px: 2.5, py: 0.5, borderBottom: "1px solid", borderBottomColor: "divider" }}
              >
                <Stack useFlexGap
                  direction="row"
                  spacing={1.5}
                  sx={{
                    alignItems: "center",
                    flexWrap: "wrap"
                  }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
                    {order.testDisplayName} (<Ltr>{order.testCode}</Ltr>)
                  </Typography>
                  {order.sectionName && (
                    <Chip
                      label={order.sectionName}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12, height: 20 }}
                    />
                  )}
                  <StatusBadge status={order.workflowStateDisplay || order.status} />
                  {order.isSuperseded && <StatusBadge status="Superseded" />}
                  {/* Closed-testing cancellation (design.md §5.3): the step
                      (and incubation stage, if any) this test had reached
                      when another lab's rejection closed this one's work. */}
                  {order.status === "Cancelled" && order.cancelledAtStep && (
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {order.cancelledAtStage != null ? (
                        <Trans
                          i18nKey="sampleSummary.testResults.cancelledAtStage"
                          ns="testingWorkspace"
                          values={{ step: humanize(order.cancelledAtStep), stage: order.cancelledAtStage }}
                          components={{ ltr: <Ltr>{null}</Ltr> }}
                        />
                      ) : (
                        t("sampleSummary.testResults.cancelledAt", { step: humanize(order.cancelledAtStep) })
                      )}
                    </Typography>
                  )}
                </Stack>
              </AccordionSummary>
              <AccordionDetails sx={{ p: 2.5 }}>
                <Stack spacing={2}>
                  {(showSop || order.sopCapture) && (
                    <SopMethodDetails sopCapture={order.sopCapture} newerSopRevision={order.newerSopRevision} sopCaptureStartDateUtc={sopCaptureStartDateUtc} timeZone={labTimeZoneId} />
                  )}
                  {order.competencyOverrides?.map((override, index) => (
                    <Box key={index} sx={{ borderInlineStart: "2px solid", borderColor: "divider", paddingInlineStart: 2 }}>
                      <Typography variant="subtitle2">{t("sampleSummary.testResults.competencyOverride")}</Typography>
                      <Typography variant="body2">
                        {t("sampleSummary.testResults.overrideAnalyst", { analyst: override.analystName })}
                      </Typography>
                      <Typography variant="body2">
                        {t("sampleSummary.testResults.overrideSignedBy", { signer: override.grantedByFullName })}
                        {" "}<Ltr>{formatExactTime(override.grantedAtUtc)}</Ltr>
                      </Typography>
                      <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{override.reason}</Typography>
                      {override.endedAtUtc && (
                        <Typography variant="body2" color="text.secondary">
                          {t("sampleSummary.testResults.overrideEnded")} <Ltr>{formatExactTime(override.endedAtUtc)}</Ltr>
                        </Typography>
                      )}
                    </Box>
                  ))}
                  {/* Incubation Stages */}
                  {order.incubations.map((inc, idx) => (
                    <IncubationStageBlock
                      key={idx}
                      inc={inc}
                      totalStages={order.incubations.length}
                      stageIndex={idx}
                    />
                  ))}

                  {/* Final Result */}
                  <FinalResultBlock order={order} />

                  {/* Return to Analyst Action */}
                  {canReturnOrder && (
                    <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 0.5 }}>
                      <ReturnToAnalystAction order={order} onReturn={onReturnTest} />
                    </Box>
                  )}
                </Stack>
              </AccordionDetails>
            </Accordion>
          );
        })}
      </Stack>
    </Box>
  );
}
