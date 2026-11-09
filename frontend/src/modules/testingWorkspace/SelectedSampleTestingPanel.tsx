import React from "react";
import {
  Box,
  Paper,
  Typography,
  Stack,
  Button,
  IconButton,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  Divider,
  Tooltip,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
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
  onEdit?: (sample: SampleCardType) => void;
  onVoid?: (sample: SampleCardType) => void;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];
const formatDate = (d: string | null) => (d ? new Date(d).toLocaleDateString() : "—");

export function SelectedSampleTestingPanel({
  sample,
  onTestClick,
  onClose,
  onNeedsPreparationClick,
  onLifecycleBadgeClick,
  onCorrected,
  onViewAuditHistory,
  onEdit,
  onVoid
}: Props) {
  const { permissions } = useAuth();
  const theme = useTheme();
  const [openPathogenWorkflow, setOpenPathogenWorkflow] = React.useState(false);
  const [actionsMenuAnchor, setActionsMenuAnchor] = React.useState<HTMLElement | null>(null);
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";
  // Pathogen tests are the ones with a TSB / broth-enrichment step; a sample
  // without one (e.g. Finished Product chemistry tests) has no pathogen workflow.
  const hasPathogenTest = sample.assignedTests.some((t) => t.usesSharedTsb);
  const isProductLike = PRODUCT_LIKE.includes(sample.category);
  const isWater = sample.category === "Water";
  // Mirrors SampleController: Analysts cannot correct or void (the backend enforces it).
  const canManageSample = permissions.includes(PERMISSIONS.SAMPLES_CORRECT);
  const completedTestsCount = sample.assignedTests.filter(
    (t) => t.status === "Approved" || t.status === "Completed" || t.status === "Reviewed"
  ).length;

  const assignedAnalystDisplay =
    sample.assignedAnalystName ||
    sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName ||
    "Unassigned";
  const batchDisplay =
    (sample.category === "AfterCleaning" ? sample.previousProductBatchNumber || sample.batchNumber : sample.batchNumber) || "—";
  const receivedAtDisplay = new Date(sample.receivedAt).toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit"
  });
  const contextLabel =
    sample.category === "FinishedProduct"
      ? "STAGE"
      : isWater
      ? "SAMPLING POINT"
      : sample.category === "AfterCleaning"
      ? "PREV PRODUCT"
      : "MFG DATE";
  const contextValue =
    sample.category === "FinishedProduct"
      ? sample.productionStage || "—"
      : isWater
      ? (sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode}, ${sample.waterSamplingPointLocation || ""}` : "—")
      : sample.category === "AfterCleaning"
      ? sample.previousProductName || "—"
      : formatDate(sample.mfgDate);
  const storageOrExpLabel =
    isWater || sample.storageCondition
      ? "STORAGE"
      : isProductLike
      ? "EXP DATE"
      : "CATEGORY";
  const storageOrExpValue =
    isWater
      ? (sample.storageCondition === "Refrigerator" ? `Fridge (${sample.storageTimeHours ?? "?"}h)` : sample.storageCondition || "Ambient")
      : isProductLike
      ? formatDate(sample.expDate)
      : sample.storageCondition || sample.category;

  const metaCells: { label: string; value: string; color?: string; fontWeight?: number }[] = [
    { label: "ASSIGNED TO", value: assignedAnalystDisplay, color: theme.palette.primary.main, fontWeight: 700 },
    {
      label: "PREP STATUS",
      value: sample.preparationStatus || "—",
      color: needsPreparation ? theme.custom.status.action.text : theme.custom.status.notDetected.text
    },
    { label: "CAUSE OF TESTING", value: sample.causeOfTesting || "—" },
    { label: "BATCH NUMBER", value: batchDisplay },
    { label: "CONTROL NUMBER", value: sample.controlNumber || "—" },
    { label: "RECEIVED AT", value: receivedAtDisplay },
    { label: "SAMPLED BY", value: sample.sampledBy || "—" },
    { label: "SAMPLE QTY", value: sample.sampleQuantity || "—" },
    { label: contextLabel, value: contextValue },
    { label: storageOrExpLabel, value: storageOrExpValue }
  ];

  return (
    <Paper
      elevation={0}
      sx={{
        p: 1.75,
        border: "1.5px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper",
        display: "flex",
        flexDirection: "column",
        gap: 1.25
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
            onClick={() => onLifecycleBadgeClick(sample.sampleId)}
          />
          <Typography sx={{ fontSize: "0.75rem", color: "text.secondary", fontWeight: 600, whiteSpace: "nowrap" }}>
            Ref: {sample.referenceNumber} · #{sample.sampleId}
          </Typography>
        </Box>

        {/* Right: Primary & Secondary Actions with Secondary/Destructive Overflow Menu */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexShrink: 0, flexWrap: "wrap" }}>
          {needsPreparation && (
            <Button
              size="small"
              variant="contained"
              startIcon={<ScienceOutlinedIcon sx={{ fontSize: 15 }} />}
              onClick={() => onNeedsPreparationClick(sample)}
              sx={{
                minHeight: 32,
                px: 1.25,
                py: 0.5,
                fontSize: "0.75rem",
                fontWeight: 700,
                textTransform: "none",
                bgcolor: theme.custom.status.action.text,
                color: "common.white",
                "&:hover": { bgcolor: theme.custom.status.action.text, opacity: 0.85 }
              }}
            >
              Prepare Sample
            </Button>
          )}

          <Button
            size="small"
            variant="outlined"
            startIcon={<DescriptionOutlinedIcon sx={{ fontSize: 15 }} />}
            onClick={() => onLifecycleBadgeClick(sample.sampleId)}
            sx={{
              minHeight: 32,
              px: 1.25,
              py: 0.5,
              fontSize: "0.75rem",
              fontWeight: 600,
              textTransform: "none",
              borderColor: "divider",
              color: "text.primary",
              bgcolor: "background.paper",
              "&:hover": { bgcolor: "background.default", borderColor: "primary.main" }
            }}
          >
            Sample Summary
          </Button>

          <Tooltip title="More sample actions">
            <IconButton
              size="small"
              aria-label="More sample actions"
              aria-controls={actionsMenuAnchor ? "selected-sample-actions-menu" : undefined}
              aria-haspopup="true"
              aria-expanded={Boolean(actionsMenuAnchor)}
              onClick={(e) => setActionsMenuAnchor(e.currentTarget)}
              sx={{
                width: 32,
                height: 32,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1.5,
                color: "text.secondary",
                "&:hover": { bgcolor: "background.default", color: "text.primary" }
              }}
            >
              <MoreVertIcon fontSize="small" />
            </IconButton>
          </Tooltip>

          <Menu
            id="selected-sample-actions-menu"
            anchorEl={actionsMenuAnchor}
            open={Boolean(actionsMenuAnchor)}
            onClose={() => setActionsMenuAnchor(null)}
            anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
            transformOrigin={{ vertical: "top", horizontal: "right" }}
            slotProps={{ paper: { sx: { minWidth: 190, mt: 0.5 } } }}
          >
            <MenuItem
              onClick={() => {
                setActionsMenuAnchor(null);
                onViewAuditHistory(sample.sampleId);
              }}
            >
              <ListItemIcon>
                <HistoryOutlinedIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Audit History" slotProps={{ primary: { sx: { fontSize: 13 } } }} />
            </MenuItem>

            {canManageSample && onEdit && (
              <MenuItem
                disabled={!sample.canEditDetails}
                title={sample.canEditDetails ? "Correct sample details (signed)" : "Locked: the sample has been submitted for review"}
                onClick={() => {
                  setActionsMenuAnchor(null);
                  onEdit(sample);
                }}
              >
                <ListItemIcon>
                  <EditOutlinedIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText
                  primary="Edit Details"
                  secondary={!sample.canEditDetails ? "Locked under review" : undefined}
                  slotProps={{
                    primary: { sx: { fontSize: 13 } },
                    secondary: { sx: { fontSize: 11 } }
                  }}
                />
              </MenuItem>
            )}

            {canManageSample && sample.status !== "Voided" && <Divider />}

            {canManageSample && sample.status !== "Voided" && (
              <MenuItem
                onClick={() => {
                  setActionsMenuAnchor(null);
                  if (onVoid) {
                    onVoid(sample);
                  } else {
                    onLifecycleBadgeClick(sample.sampleId);
                  }
                }}
                sx={{ color: "error.main" }}
              >
                <ListItemIcon sx={{ color: "error.main" }}>
                  <BlockOutlinedIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText
                  primary="Void Sample..."
                  slotProps={{ primary: { sx: { fontSize: 13, fontWeight: 600 } } }}
                />
              </MenuItem>
            )}
          </Menu>

          <Button
            size="small"
            variant="outlined"
            onClick={onClose}
            startIcon={<CloseIcon sx={{ fontSize: 15 }} />}
            sx={{
              minHeight: 32,
              px: 1.25,
              py: 0.5,
              fontSize: "0.75rem",
              fontWeight: 600,
              textTransform: "none",
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

      {/* 2. Compact 5-Column Metadata Grid with Full-Value Tooltips */}
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
        {metaCells.map((cell) => (
          <Box key={cell.label} sx={{ minWidth: 0 }}>
            <Typography
              sx={{
                fontSize: "0.75rem",
                lineHeight: 1.1,
                textTransform: "uppercase",
                color: "text.secondary",
                fontWeight: 600,
                letterSpacing: "0.03em",
                mb: 0.25
              }}
            >
              {cell.label}
            </Typography>
            <Tooltip title={cell.value !== "—" ? cell.value : ""} placement="top" arrow disableInteractive>
              <Typography
                sx={{
                  fontSize: "0.75rem",
                  fontWeight: cell.fontWeight ?? 600,
                  color: cell.color ?? "text.primary",
                  whiteSpace: "nowrap",
                  overflow: "hidden",
                  textOverflow: "ellipsis"
                }}
              >
                {cell.value}
              </Typography>
            </Tooltip>
          </Box>
        ))}
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
              {sample.assignedTests.length > 0
                ? `${completedTestsCount} of ${sample.assignedTests.length} test workflows completed · Select a test card to record or review results`
                : "No laboratory test workflows assigned"}
            </Typography>
          </Box>

          {hasPathogenTest && (
          <Button
            variant="contained"
            size="small"
            startIcon={<ScienceOutlinedIcon />}
            onClick={() => setOpenPathogenWorkflow(true)}
            sx={{
              bgcolor: brandColors.sectionTitle,
              color: "common.white",
              fontWeight: 700,
              fontSize: 12,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            Open Pathogen Workflow
          </Button>
          )}
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
                  {isProductLike
                    ? "Record the sample preparation and sign for it before starting laboratory tests."
                    : "Test locations and configuration must be completed before starting laboratory tests."}
                </Typography>
              </Box>
            </Box>

            <Button
              variant="contained"
              size="small"
              onClick={() => onNeedsPreparationClick(sample)}
              sx={{
                bgcolor: theme.custom.status.action.text,
                color: "common.white",
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
            {sample.assignedTests.map((test) => (
              <AssignedTestCard
                key={test.testOrderId}
                test={test}
                sample={sample}
                onTestClick={onTestClick}
                onActionComplete={onCorrected}
              />
            ))}
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
