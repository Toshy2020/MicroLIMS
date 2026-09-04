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
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { Link } from "react-router-dom";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge, StatusBadge, statusColor } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { EditableCell } from "./EditableCell";
import { WorkspaceService } from "./services/WorkspaceService";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { ItemDocumentsCard } from "./ItemDocumentsCard";
import { AssignedTestCard } from "./components/AssignedTestCard";

interface Props {
  sample: SampleCardType;
  onTestClick: (test: TestOrderSummary, sample: SampleCardType) => void;
  onClose: () => void;
  onNeedsPreparationClick: (sample: SampleCardType) => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
  onCorrected: () => void;
  onViewAuditHistory: (sampleId: number) => void;
  onVoid?: (sample: SampleCardType) => void;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];
const formatDate = (d: string | null) => (d ? new Date(d).toLocaleDateString() : "—");

function resolveEffectiveTestStatus(
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
    if (test.workflowState === "COUNT_INCUBATING" || test.workflowState === "INCUBATING" || test.workflowState === "RUNNING") {
      return { label: test.workflowStateDisplay || "Incubation In Progress", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "AWAITING_RESULTS") {
      return { label: test.workflowStateDisplay || "Ready for Result Entry", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
    }
    if (test.workflowState === "RESULTS_RECORDED") {
      return { label: "Result Recorded — Pending Review", icon: <FiberManualRecordIcon sx={{ fontSize: 12, color: infoColor }} />, color: infoColor };
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
  onViewAuditHistory,
  onVoid
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
        p: 1.75,
        border: "1.5px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 1.25,
        overflowY: "auto"
      }}
    >
      {/* 1. Unified Top Header Bar */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 1.5, flexWrap: { xs: "wrap", md: "nowrap" } }}>
        {/* Left: Title, Type Badge, Status Badge, Reference Subtitle */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", minWidth: 0, flexShrink: 1 }}>
          <Typography sx={{ fontSize: "0.95rem", fontWeight: 700, color: theme.palette.primary.main, lineHeight: 1.2, whiteSpace: "nowrap" }}>
            {sample.displayName}
          </Typography>
          <CategoryBadge category={sample.category} />
          <SampleLifecycleBadge
            status={sample.status}
            role={role}
            onClick={() => onLifecycleBadgeClick(sample.sampleId)}
          />
          <Typography sx={{ fontSize: "0.72rem", color: "text.secondary", fontWeight: 600, whiteSpace: "nowrap" }}>
            Ref: {sample.referenceNumber} · #{sample.sampleId}
          </Typography>
        </Box>

        {/* Right: Action Buttons (height: ~25px, padding: 2px 8px, font-size: 0.72rem) */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexShrink: 0, flexWrap: "wrap" }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<DescriptionOutlinedIcon sx={{ fontSize: 13 }} />}
            onClick={() => onLifecycleBadgeClick(sample.sampleId)}
            sx={{
              height: 25,
              px: 1,
              py: 0.25,
              fontSize: "0.72rem",
              fontWeight: 600,
              textTransform: "none",
              minWidth: "auto",
              borderColor: theme.custom.status.purple.border,
              color: theme.palette.primary.main,
              bgcolor: theme.custom.status.purple.bg,
              "&:hover": { bgcolor: theme.custom.status.purple.border, borderColor: theme.palette.primary.main }
            }}
          >
            Sample Summary
          </Button>

          <Button
            component={Link}
            to={`/samples/${sample.sampleId}/report`}
            target="_blank"
            rel="noopener"
            size="small"
            variant="outlined"
            startIcon={<PictureAsPdfOutlinedIcon sx={{ fontSize: 13 }} />}
            sx={{
              height: 25,
              px: 1,
              py: 0.25,
              fontSize: "0.72rem",
              fontWeight: 600,
              textTransform: "none",
              minWidth: "auto",
              borderColor: theme.custom.status.info.border,
              color: theme.custom.status.info.text,
              bgcolor: theme.custom.status.info.bg,
              "&:hover": { bgcolor: theme.custom.status.info.border, borderColor: theme.custom.status.info.text }
            }}
          >
            View Full Report
          </Button>

          <Button
            size="small"
            variant="outlined"
            startIcon={<HistoryOutlinedIcon sx={{ fontSize: 13 }} />}
            onClick={() => onViewAuditHistory(sample.sampleId)}
            sx={{
              height: 25,
              px: 1,
              py: 0.25,
              fontSize: "0.72rem",
              fontWeight: 600,
              textTransform: "none",
              minWidth: "auto",
              borderColor: "divider",
              color: "text.secondary",
              bgcolor: "background.paper",
              "&:hover": { bgcolor: "background.default" }
            }}
          >
            Audit History
          </Button>

          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<BlockOutlinedIcon sx={{ fontSize: 13 }} />}
            onClick={() => (onVoid ? onVoid(sample) : onLifecycleBadgeClick(sample.sampleId))}
            sx={{
              height: 25,
              px: 1,
              py: 0.25,
              fontSize: "0.72rem",
              fontWeight: 600,
              textTransform: "none",
              minWidth: "auto",
              borderColor: theme.custom.status.detected.border,
              color: theme.custom.status.detected.text,
              bgcolor: theme.custom.status.detected.bg,
              "&:hover": { bgcolor: theme.custom.status.detected.border, borderColor: theme.custom.status.detected.text }
            }}
          >
            Void Sample
          </Button>

          {needsPreparation && (
            <Button
              size="small"
              variant="contained"
              startIcon={<ScienceOutlinedIcon sx={{ fontSize: 13 }} />}
              onClick={() => onNeedsPreparationClick(sample)}
              sx={{
                height: 25,
                px: 1,
                py: 0.25,
                fontSize: "0.72rem",
                fontWeight: 700,
                textTransform: "none",
                minWidth: "auto",
                bgcolor: theme.custom.status.action.text,
                color: "#ffffff",
                "&:hover": { bgcolor: theme.custom.status.action.text, opacity: 0.85 }
              }}
            >
              Prepare Sample
            </Button>
          )}

          <Button
            size="small"
            variant="outlined"
            onClick={onClose}
            startIcon={<CloseIcon sx={{ fontSize: 13 }} />}
            sx={{
              height: 25,
              px: 1,
              py: 0.25,
              fontSize: "0.72rem",
              fontWeight: 600,
              textTransform: "none",
              minWidth: "auto",
              borderColor: "divider",
              color: "text.secondary",
              whiteSpace: "nowrap",
              "&:hover": { borderColor: "text.secondary", bgcolor: "background.default" }
            }}
          >
            Deselect
          </Button>
        </Box>
      </Box>

      {/* 2. Compact 5-Column Metadata Grid */}
      <Box
        className="meta-grid"
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "repeat(2, 1fr)", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" },
          gap: "6px 12px",
          p: 1,
          borderRadius: 1.5,
          bgcolor: "background.default"
        }}
      >
        {/* 1. ASSIGNED TO */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            ASSIGNED TO
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 700, color: theme.palette.primary.main, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.assignedAnalystName || sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName || "Unassigned"}
          </Typography>
        </Box>

        {/* 2. PREPARATION STATUS */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            PREP STATUS
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: needsPreparation ? theme.custom.status.action.text : theme.custom.status.notDetected.text, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.preparationStatus || "—"}
          </Typography>
        </Box>

        {/* 3. CAUSE OF TESTING */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            CAUSE OF TESTING
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.causeOfTesting || "—"}
          </Typography>
        </Box>

        {/* 4. BATCH NUMBER */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            BATCH NUMBER
          </Typography>
          <Box sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary" }}>
            {isProductLike ? (
              <EditableCell
                value={sample.batchNumber ?? ""}
                editable={!sample.incubationStarted}
                onSave={(v) => correct("batchNumber", v)}
              />
            ) : sample.category === "AfterCleaning" ? (
              <EditableCell
                value={sample.previousProductBatchNumber || sample.batchNumber || ""}
                editable={!sample.incubationStarted}
                onSave={(v) => correct("batchNumber", v)}
              />
            ) : (
              <span>{sample.batchNumber || "—"}</span>
            )}
          </Box>
        </Box>

        {/* 5. CONTROL NUMBER */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            CONTROL NUMBER
          </Typography>
          <Box sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary" }}>
            <EditableCell
              value={sample.controlNumber}
              editable={!sample.incubationStarted}
              onSave={(v) => correct("controlNumber", v)}
            />
          </Box>
        </Box>

        {/* 6. RECEIVED AT */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            RECEIVED AT
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {new Date(sample.receivedAt).toLocaleString("en-GB", {
              day: "2-digit",
              month: "short",
              hour: "2-digit",
              minute: "2-digit"
            })}
          </Typography>
        </Box>

        {/* 7. SAMPLED BY */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            SAMPLED BY
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.sampledBy || "—"}
          </Typography>
        </Box>

        {/* 8. SAMPLE QUANTITY */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            SAMPLE QTY
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.sampleQuantity || "—"}
          </Typography>
        </Box>

        {/* 9. CATEGORY-SPECIFIC CONTEXT (Stage / Sampling Point / Prev Product) */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            {sample.category === "FinishedProduct"
              ? "STAGE"
              : isWater
              ? "SAMPLING POINT"
              : sample.category === "AfterCleaning"
              ? "PREV PRODUCT"
              : "MFG DATE"}
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {sample.category === "FinishedProduct"
              ? sample.productionStage || "—"
              : isWater
              ? (sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode} — ${sample.waterSamplingPointLocation || ""}` : "—")
              : sample.category === "AfterCleaning"
              ? sample.previousProductName || "—"
              : formatDate(sample.mfgDate)}
          </Typography>
        </Box>

        {/* 10. EXP DATE / STORAGE CONDITION */}
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: "0.64rem", lineHeight: 1.1, textTransform: "uppercase", color: "text.secondary", fontWeight: 600, letterSpacing: "0.03em", mb: 0.25 }}>
            {isWater || sample.storageCondition
              ? "STORAGE"
              : isProductLike
              ? "EXP DATE"
              : "CATEGORY"}
          </Typography>
          <Typography sx={{ fontSize: "0.72rem", fontWeight: 600, color: "text.primary", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {isWater
              ? (sample.storageCondition === "Refrigerator" ? `Fridge (${sample.storageTimeHours ?? "?"}h)` : sample.storageCondition || "Ambient")
              : isProductLike
              ? formatDate(sample.expDate)
              : sample.storageCondition || sample.category}
          </Typography>
        </Box>
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
              const stepInfo = resolveEffectiveTestStatus(test, theme);
              return (
                <AssignedTestCard
                  key={test.testOrderId}
                  test={test}
                  sample={sample}
                  stepInfo={stepInfo}
                  onTestClick={onTestClick}
                  onActionComplete={onCorrected}
                />
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
