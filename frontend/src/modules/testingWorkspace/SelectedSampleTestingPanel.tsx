import React from "react";
import {
  Box,
  Paper,
  Typography,
  Stack,
  Button,
  Divider,
  Tooltip
} from "@mui/material";
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

export function resolveEffectiveTestStatus(test: TestOrderSummary): { label: string; icon: React.ReactNode; color: string } {
  if (test.workflowStateDisplay) {
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return { label: "Completed & Approved", icon: <CheckCircleIcon sx={{ fontSize: 14, color: "#16a34a" }} />, color: "#16a34a" };
    }
    if (test.workflowState === "TSB_INCUBATING") {
      return { label: "TSB Incubating", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#2563eb" }} />, color: "#2563eb" };
    }
    if (test.workflowState === "DOWNSTREAM_INCUBATING") {
      return { label: "Selective Plating In Progress", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#2563eb" }} />, color: "#2563eb" };
    }
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return { label: "Ready for Downstream Testing", icon: <CheckCircleIcon sx={{ fontSize: 14, color: "#2563eb" }} />, color: "#2563eb" };
    }
    if (test.workflowState === "AWAITING_RESULTS") {
      return { label: "Awaiting Final Result", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#2563eb" }} />, color: "#2563eb" };
    }
    if (test.workflowState === "RESULTS_RECORDED") {
      return { label: "Result Recorded — Pending Review", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#2563eb" }} />, color: "#2563eb" };
    }
    if (test.workflowState === "INCUBATING" || test.workflowState === "RUNNING") {
      return { label: test.workflowStateDisplay, icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#2563eb" }} />, color: "#2563eb" };
    }
    return { label: test.workflowStateDisplay, icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#6b7280" }} />, color: "#6b7280" };
  }

  if (test.status === "Approved") {
    return { label: "Completed & Approved", icon: <CheckCircleIcon sx={{ fontSize: 14, color: "#16a34a" }} />, color: "#16a34a" };
  }
  if (test.status === "UnderReview") {
    return { label: "Under Review", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#d97706" }} />, color: "#d97706" };
  }
  return { label: test.status || "Pending", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: "#6b7280" }} />, color: "#6b7280" };
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
        border: "1.5px solid #e5e7eb",
        borderRadius: 2.5,
        bgcolor: "#ffffff",
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
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: brandColors.pageTitle, lineHeight: 1.2 }}>
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
              borderColor: "#d1d5db",
              color: "#4b5563",
              fontWeight: 600,
              fontSize: 12,
              whiteSpace: "nowrap",
              "&:hover": { borderColor: "#9ca3af", bgcolor: "#f3f4f6" }
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
            borderColor: "#d8b4fe",
            color: brandColors.sectionTitle,
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "#faf5ff",
            "&:hover": { bgcolor: "#f3e8ff", borderColor: brandColors.sectionTitle }
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
            borderColor: "#bfdbfe",
            color: "#1d4ed8",
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "#eff6ff",
            "&:hover": { bgcolor: "#dbeafe", borderColor: "#2563eb" }
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
            borderColor: "#e5e7eb",
            color: "#4b5563",
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "#ffffff",
            "&:hover": { bgcolor: "#f9fafb" }
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
              bgcolor: "#d97706",
              color: "#ffffff",
              fontSize: 12,
              fontWeight: 700,
              "&:hover": { bgcolor: "#b45309" }
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
          bgcolor: "#faf9fc",
          border: "1px solid #f0edf4",
          display: "grid",
          gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" },
          gap: 1.5
        }}
      >
        <Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Cause of Testing</Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
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
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
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
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
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
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
            {sample.sampledBy || "—"}
          </Typography>
        </Box>

        {sample.sampleQuantity && (
          <Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sample Quantity</Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
              {sample.sampleQuantity}
            </Typography>
          </Box>
        )}

        {sample.category === "FinishedProduct" && sample.productionStage && (
          <Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Production Stage</Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
              {sample.productionStage}
            </Typography>
          </Box>
        )}

        {isProductLike && (
          <>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Mfg Date</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                {formatDate(sample.mfgDate)}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Exp Date</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                {formatDate(sample.expDate)}
              </Typography>
            </Box>
          </>
        )}

        {isWater && (
          <>
            <Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sampling Point</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                {sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode} — ${sample.waterSamplingPointLocation}` : "—"}
              </Typography>
            </Box>
            {sample.storageCondition && (
              <Box>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Storage Condition</Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                  {sample.storageCondition === "Refrigerator"
                    ? `Refrigerator (${sample.storageTimeHours ?? "?"}h)`
                    : sample.storageCondition}
                </Typography>
              </Box>
            )}
          </>
        )}
      </Box>

      {/* Assigned Tests Section */}
      <Box sx={{ mt: 1 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.pageTitle }}>
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
              border: "1px solid #fde68a",
              bgcolor: "#fffbeb",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 2
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
              <WarningAmberOutlinedIcon sx={{ color: "#d97706", fontSize: 24 }} />
              <Box>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#92400e" }}>
                  Sample Needs Preparation
                </Typography>
                <Typography sx={{ fontSize: 12, color: "#b45309" }}>
                  Test locations and configuration must be completed before starting laboratory tests.
                </Typography>
              </Box>
            </Box>

            <Button
              variant="contained"
              size="small"
              onClick={() => onNeedsPreparationClick(sample)}
              sx={{
                bgcolor: "#d97706",
                fontWeight: 700,
                fontSize: 12,
                whiteSpace: "nowrap",
                "&:hover": { bgcolor: "#b45309" }
              }}
            >
              Start Preparation
            </Button>
          </Paper>
        )}

        {sample.assignedTests.length === 0 ? (
          <Typography sx={{ color: "#9ca3af", fontSize: 13, py: 3, textAlign: "center" }}>
            No tests assigned to this sample.
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {sample.assignedTests.map((test) => {
              const unit = sample.category === "EnvironmentalMonitoring" ? "rooms" : "parts";
              const locationLabel = test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";
              const stepInfo = resolveEffectiveTestStatus(test);

              return (
                <Paper
                  key={test.testOrderId}
                  elevation={0}
                  onClick={() => onTestClick(test, sample)}
                  sx={{
                    p: 2,
                    borderRadius: 2,
                    border: "1.5px solid #e5e7eb",
                    bgcolor: "#ffffff",
                    cursor: "pointer",
                    transition: "all 0.15s ease-in-out",
                    "&:hover": {
                      borderColor: brandColors.sectionTitle,
                      boxShadow: "0 2px 10px rgba(123, 45, 142, 0.08)",
                      transform: "translateY(-1px)",
                      bgcolor: "#faf7fb"
                    }
                  }}
                >
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1 }}>
                    <Box>
                      <Typography sx={{ fontSize: 14, fontWeight: 700, color: "#111827" }}>
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
                      borderTop: "1px solid #f3f4f6"
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
                        color: brandColors.sectionTitle,
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
