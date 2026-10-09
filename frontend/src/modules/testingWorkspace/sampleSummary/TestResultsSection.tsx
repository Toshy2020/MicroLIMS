import { Box, Typography, Stack, Button, Accordion, AccordionSummary, AccordionDetails, Chip, useTheme } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import AssignmentReturnOutlinedIcon from "@mui/icons-material/AssignmentReturnOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";
import { humanize } from "../SampleReportPage";
import { IncubationStageBlock } from "./SummaryCards";
import { FinalResultBlock } from "./FinalResultBlock";

// 2. Test Results Section
export function TestResultsSection({
  testOrders,
  overallStatus,
  canReturn,
  onReturnTest
}: {
  testOrders: TestOrderSummaryDetail[];
  overallStatus: string;
  canReturn?: boolean;
  onReturnTest?: (order: TestOrderSummaryDetail) => void;
}) {
  const theme = useTheme();
  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 16, color: theme.palette.primary.main }}>
          Test Results
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
            Overall Status:
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
          const canReturnOrder = Boolean(
            canReturn && !order.isSuperseded && order.status === "ResultEntered" && isCountTest
          );

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
                    {order.testCode}: {order.testDisplayName}
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
                      Cancelled at {humanize(order.cancelledAtStep)}
                      {order.cancelledAtStage != null ? `, stage ${order.cancelledAtStage}` : ""}
                    </Typography>
                  )}
                </Stack>
              </AccordionSummary>
              <AccordionDetails sx={{ p: 2.5 }}>
                <Stack spacing={2}>
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
                      <Button
                        variant="outlined"
                        color="warning"
                        size="small"
                        startIcon={<AssignmentReturnOutlinedIcon />}
                        onClick={() => onReturnTest?.(order)}
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
                        Return to Analyst
                      </Button>
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
