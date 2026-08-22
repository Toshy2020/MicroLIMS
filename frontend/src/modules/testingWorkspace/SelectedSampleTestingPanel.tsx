import React from "react";
import {
  Box,
  Paper,
  Typography,
  Stack,
  Button,
  Divider,
  Tooltip,
  useTheme
} from "@mui/material";
import { Theme } from "@mui/material/styles";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import FiberManualRecordIcon from "@mui/icons-material/FiberManualRecord";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge, StatusBadge, statusColor } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { EditableCell } from "./EditableCell";
import { WorkspaceService } from "./services/WorkspaceService";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { ItemDocumentsCard } from "./ItemDocumentsCard";

interface Props {
  sample: SampleCardType;
  onTestClick: (test: TestOrderSummary, sample: SampleCardType) => void;
  onClose: () => void;
  onNeedsPreparationClick: (sample: SampleCardType) => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
  onCorrected: () => void;
  onViewAuditHistory: (sampleId: number) => void;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];
const formatDate = (d: string | null) => (d ? new Date(d).toLocaleDateString() : "—");

export function resolveEffectiveTestStatus(
  test: TestOrderSummary,
  theme: Theme
): { label: string; icon: React.ReactNode; color: string } {
  const successColor = theme.custom.status.notDetected.text;
  const infoColor = theme.custom.status.info.text;
  const pendingColor = theme.custom.status.pending.text;
  const inconclusiveColor = theme.custom.status.inconclusive.text;

  if (test.workflowStateDisplay) {
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return { label: "Completed & Approved", icon: <CheckCircleIcon sx={{ fontSize: 14, color: successColor }} />, color: successColor };
    }
    if (test.workflowState === "TSB_INCUBATING") {
      return { label: "TSB Incubating", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "DOWNSTREAM_INCUBATING") {
      return { label: "Selective Plating In Progress", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return { label: "Ready for Downstream Testing", icon: <CheckCircleIcon sx={{ fontSize: 14, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "AWAITING_RESULTS") {
      return { label: "Awaiting Final Result", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "RESULTS_RECORDED") {
      return { label: "Result Recorded — Pending Review", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "INCUBATING" || test.workflowState === "RUNNING") {
      return { label: test.workflowStateDisplay, icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    return { label: test.workflowStateDisplay, icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: pendingColor }} />, color: pendingColor };
  }

  if (test.status === "Approved") {
    return { label: "Completed & Approved", icon: <CheckCircleIcon sx={{ fontSize: 14, color: successColor }} />, color: successColor };
  }
  if (test.status === "UnderReview") {
    return { label: "Under Review", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: inconclusiveColor }} />, color: inconclusiveColor };
  }
  return { label: test.status || "Pending", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: pendingColor }} />, color: pendingColor };
}

export function SelectedSampleTestingPanel({
  sample,
  onTestClick,
  onClose,
  onNeedsPreparationClick,
  onLifecycleBadgeClick,
  onCorrected,
  onViewAuditHistory
}: Props) {
  const { role } = useAuth();
  const theme = useTheme();
  const [openPathogenWorkflow, setOpenPathogenWorkflow] = React.useState(false);
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";
  const isProductLike = PRODUCT_LIKE.includes(sample.category);
  const isWater = sample.category === "Water";

  const correct = async (field: "batchNumber" | "controlNumber", value: string) => {
    await WorkspaceService.correctSample(
      sample.sampleId,
      field === "batchNumber" ? value : undefined,
      field === "controlNumber" ? value : undefined
    );
    onCorrected();
  };

  const handleOpenReport = () => {
    window.open(`/samples/${sample.sampleId}/report`, "_blank");
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        border: "1.5px solid",
        borderColor: "divider",
        borderRadius: 2.5,
        bgcolor: "background.paper",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 2,
        overflowY: "auto"
      }}
    >
      {/* Top Header: Title, Reference, Badges, and Close Button */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1.5 }}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main, lineHeight: 1.2 }}>
              {sample.displayName}
            </Typography>
            <CategoryBadge category={sample.category} />
            <SampleLifecycleBadge
              status={sample.status}
              role={role}
              onClick={() => onLifecycleBadgeClick(sample.sampleId)}
            />
          </Box>

          <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
            Reference: {sample.referenceNumber} · Sample #{sample.sampleId}
          </Typography>
        </Box>

        <Tooltip title="Close Selected Sample (Return to Full Register)">
          <Button
            size="small"
            variant="outlined"
            onClick={onClose}
            startIcon={<CloseIcon sx={{ fontSize: 16 }} />}
            sx={{
              borderColor: "divider",
              color: "text.secondary",
              fontWeight: 600,
              fontSize: 12,
              whiteSpace: "nowrap",
              "&:hover": { borderColor: "text.secondary", bgcolor: "background.default" }
            }}
          >
            Deselect
          </Button>
        </Tooltip>
      </Box>

      {/* Action Toolbar */}
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ pt: 0.5 }}>
        <Button
          size="small"
          variant="outlined"
          startIcon={<DescriptionOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={() => onLifecycleBadgeClick(sample.sampleId)}
          sx={{
            borderColor: theme.custom.status.purple.border,
            color: theme.palette.primary.main,
            fontSize: 12,
            fontWeight: 600,
            bgcolor: theme.custom.status.purple.bg,
            "&:hover": { bgcolor: theme.custom.status.purple.border, borderColor: theme.palette.primary.main }
          }}
        >
          Sample Summary
        </Button>

        <Button
          size="small"
          variant="outlined"
          startIcon={<PictureAsPdfOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={handleOpenReport}
          sx={{
            borderColor: theme.custom.status.info.border,
            color: theme.custom.status.info.text,
            fontSize: 12,
            fontWeight: 600,
            bgcolor: theme.custom.status.info.bg,
            "&:hover": { bgcolor: theme.custom.status.info.border, borderColor: theme.custom.status.info.text }
          }}
        >
          View Full Report
        </Button>

        <Button
          size="small"
          variant="outlined"
          startIcon={<HistoryOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={() => onViewAuditHistory(sample.sampleId)}
          sx={{
            borderColor: "divider",
            color: "text.secondary",
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "background.paper",
            "&:hover": { bgcolor: "background.default" }
          }}
        >
          Audit History
        </Button>

        {needsPreparation && (
          <Button
            size="small"
            variant="contained"
            startIcon={<ScienceOutlinedIcon sx={{ fontSize: 16 }} />}
            onClick={() => onNeedsPreparationClick(sample)}
            sx={{
              bgcolor: theme.custom.status.action.text,
              color: "#ffffff",
              fontSize: 12,
              fontWeight: 700,
              "&:hover": { bgcolor: theme.custom.status.action.text, opacity: 0.85 }
            }}
          >
            Prepare Sample
          </Button>
        )}
      </Stack>

      <Divider />

      {/* Metadata Detail Card */}
      <Box
        sx={{
          p: 1.5,
          borderRadius: 2,
          bgcolor: "background.default",
          border: "1px solid",
          borderColor: "divider",
          display: "grid",
          gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" },
          gap: 1.5
        }}
      >
        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Assigned To</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
            {sample.assignedAnalystName || sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName || "Unassigned"}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Preparation Status</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: needsPreparation ? theme.custom.status.action.text : theme.custom.status.notDetected.text }}>
            {sample.preparationStatus || "—"}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Cause of Testing</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
            {sample.causeOfTesting || "—"}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Batch Number</Typography>
          {isProductLike ? (
            <EditableCell
              value={sample.batchNumber ?? ""}
              editable={!sample.incubationStarted}
              onSave={(v) => correct("batchNumber", v)}
            />
          ) : (
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
              {sample.batchNumber || "—"}
            </Typography>
          )}
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Control Number</Typography>
          <EditableCell
            value={sample.controlNumber}
            editable={!sample.incubationStarted}
            onSave={(v) => correct("controlNumber", v)}
          />
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Received At</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
            {new Date(sample.receivedAt).toLocaleString("en-GB", {
              day: "2-digit",
              month: "short",
              year: "numeric",
              hour: "2-digit",
              minute: "2-digit"
            })}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sampled By</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
            {sample.sampledBy || "—"}
          </Typography>
        </Box>

        {sample.sampleQuantity && (
          <Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sample Quantity</Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
              {sample.sampleQuantity}
            </Typography>
          </Box>
        )}

        {sample.category === "FinishedProduct" && sample.productionStage && (
          <Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Production Stage</Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
              {sample.productionStage}
            </Typography>
          </Box>
        )}

        {isProductLike && (
          <>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Mfg Date</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                {formatDate(sample.mfgDate)}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Exp Date</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                {formatDate(sample.expDate)}
              </Typography>
            </Box>
          </>
        )}

        {isWater && (
          <>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sampling Point</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                {sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode} — ${sample.waterSamplingPointLocation}` : "—"}
              </Typography>
            </Box>
            {sample.storageCondition && (
              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Storage Condition</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {sample.storageCondition === "Refrigerator"
                    ? `Refrigerator (${sample.storageTimeHours ?? "?"}h)`
                    : sample.storageCondition}
                </Typography>
              </Box>
            )}
          </>
        )}
      </Box>

      {/* Item Controlled Documents Card */}
      {isProductLike && sample.itemId && (
        <ItemDocumentsCard itemId={sample.itemId} itemName={sample.displayName} />
      )}

      {/* Assigned Tests Section */}
      <Box sx={{ mt: 1 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
              Assigned Tests ({sample.assignedTests.length})
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Test Master configured laboratory workflows
            </Typography>
          </Box>

          <Button
            variant="contained"
            size="small"
            startIcon={<ScienceOutlinedIcon />}
            onClick={() => setOpenPathogenWorkflow(true)}
            sx={{
              bgcolor: brandColors.sectionTitle,
              color: "#ffffff",
              fontWeight: 700,
              fontSize: 12,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            Open Pathogen Workflow
          </Button>
        </Box>

        {needsPreparation && (
          <Paper
            elevation={0}
            sx={{
              p: 2,
              mb: 2,
              borderRadius: 2,
              border: "1px solid",
              borderColor: theme.custom.status.inconclusive.border,
              bgcolor: theme.custom.status.inconclusive.bg,
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 2
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
              <WarningAmberOutlinedIcon sx={{ color: theme.custom.status.inconclusive.text, fontSize: 24 }} />
              <Box>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.inconclusive.text }}>
                  Sample Needs Preparation
                </Typography>
                <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text }}>
                  Test locations and configuration must be completed before starting laboratory tests.
                </Typography>
              </Box>
            </Box>

            <Button
              variant="contained"
              size="small"
              onClick={() => onNeedsPreparationClick(sample)}
              sx={{
                bgcolor: theme.custom.status.action.text,
                color: "#ffffff",
                fontWeight: 700,
                fontSize: 12,
                whiteSpace: "nowrap",
                "&:hover": { bgcolor: theme.custom.status.action.text, opacity: 0.85 }
              }}
            >
              Start Preparation
            </Button>
          </Paper>
        )}

        {sample.assignedTests.length === 0 ? (
          <Typography sx={{ color: "text.secondary", fontSize: 13, py: 3, textAlign: "center" }}>
            No tests assigned to this sample.
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {sample.assignedTests.map((test) => {
              const unit = sample.category === "EnvironmentalMonitoring" ? "rooms" : "parts";
              const locationLabel = test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";
              const stepInfo = resolveEffectiveTestStatus(test, theme);

              return (
                <Paper
                  key={test.testOrderId}
                  elevation={0}
                  onClick={() => onTestClick(test, sample)}
                  sx={{
                    p: 2,
                    borderRadius: 2,
                    border: "1.5px solid",
                    borderColor: "divider",
                    bgcolor: "background.paper",
                    cursor: "pointer",
                    transition: "all 0.15s ease-in-out",
                    "&:hover": {
                      borderColor: theme.palette.primary.main,
                      boxShadow: "0 2px 10px rgba(123, 45, 142, 0.08)",
                      transform: "translateY(-1px)",
                      bgcolor: theme.custom.status.purple.bg
                    }
                  }}
                >
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1 }}>
                    <Box>
                      <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
                        {test.testCode}{locationLabel}
                      </Typography>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        Test Order #{test.testOrderId} · Assigned: {test.assignedAnalystName || "Unassigned"}
                      </Typography>
                    </Box>

                    <StatusBadge status={test.workflowStatus ?? test.status} />
                  </Box>

                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      pt: 1,
                      borderTop: "1px solid",
                      borderColor: "divider"
                    }}
                  >
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                      {stepInfo.icon}
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: stepInfo.color }}>
                        {stepInfo.label}
                      </Typography>
                    </Box>

                    <Button
                      size="small"
                      variant="text"
                      endIcon={<ArrowForwardIcon sx={{ fontSize: 14 }} />}
                      sx={{
                        color: theme.palette.primary.main,
                        fontSize: 12,
                        fontWeight: 700,
                        p: 0,
                        minWidth: "auto",
                        "&:hover": { bgcolor: "transparent", textDecoration: "underline" }
                      }}
                    >
                      Open Workflow
                    </Button>
                  </Box>
                </Paper>
              );
            })}
          </Stack>
        )}
      </Box>

      {/* Pathogen Testing Session Dialog */}
      <PathogenSessionDialog
        open={openPathogenWorkflow}
        sampleId={sample.sampleId}
        onClose={() => setOpenPathogenWorkflow(false)}
        onSessionUpdated={() => onCorrected()}
        onSessionCompleted={() => {
          setOpenPathogenWorkflow(false);
          onCorrected();
        }}
      />
    </Paper>
  );
}
