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
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { ItemDocumentsCard } from "./ItemDocumentsCard";
import { AssignedTestCard } from "./components/AssignedTestCard";
import { RaiseDeviationButton } from "../quality/components/RaiseDeviationButton";
import { QualityRecordsPanel } from "../quality/components/QualityRecordsPanel";
import { formatLabCalendarDate, formatDateTimeWithTimeZone } from "../../utils/formatDate";

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
const formatDate = (d: string | null) => (d ? formatLabCalendarDate(d) : "—");

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
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const [openPathogenWorkflow, setOpenPathogenWorkflow] = React.useState(false);
  const [actionsMenuAnchor, setActionsMenuAnchor] = React.useState<HTMLElement | null>(null);
  const [qualityRefreshKey, setQualityRefreshKey] = React.useState(0);
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
    t("selectedSample.unassigned");
  const isAssignedName = Boolean(
    sample.assignedAnalystName || sample.assignedTests.some((t) => t.assignedAnalystName)
  );
  const batchDisplay =
    (sample.category === "AfterCleaning" ? sample.previousProductBatchNumber || sample.batchNumber : sample.batchNumber) || "—";
  const receivedAtDisplay = formatDateTimeWithTimeZone(sample.receivedAt);
  const contextLabel =
    sample.category === "FinishedProduct"
      ? t("selectedSample.label.stage")
      : isWater
      ? t("selectedSample.label.samplingPoint")
      : sample.category === "AfterCleaning"
      ? t("selectedSample.label.prevProduct")
      : t("selectedSample.label.mfgDate");
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
      ? t("selectedSample.label.storage")
      : isProductLike
      ? t("selectedSample.label.expDate")
      : t("selectedSample.label.category");
  const storageOrExpValue: string =
    isWater
      ? (sample.storageCondition === "Refrigerator"
          ? t("selectedSample.fridgeHours", { hours: sample.storageTimeHours ?? "?" }).replace(/<\/?ltr>/g, "")
          : sample.storageCondition || t("selectedSample.ambient"))
      : isProductLike
      ? formatDate(sample.expDate)
      : sample.storageCondition || sample.category;

  const metaCells: { key: string; label: string; value: string; ltr?: boolean; color?: string; fontWeight?: number }[] = [
    { key: "assignedTo", label: t("selectedSample.label.assignedTo"), value: assignedAnalystDisplay, ltr: isAssignedName, color: theme.palette.primary.main, fontWeight: 700 },
    {
      key: "prepStatus",
      label: t("selectedSample.label.prepStatus"),
      value: sample.preparationStatus || "—",
      color: needsPreparation ? theme.custom.status.action.text : theme.custom.status.notDetected.text
    },
    { key: "causeOfTesting", label: t("selectedSample.label.causeOfTesting"), value: sample.causeOfTesting || "—" },
    { key: "batchNumber", label: t("selectedSample.label.batchNumber"), value: batchDisplay, ltr: batchDisplay !== "—" },
    { key: "controlNumber", label: t("selectedSample.label.controlNumber"), value: sample.controlNumber || "—", ltr: Boolean(sample.controlNumber) },
    { key: "receivedAt", label: t("selectedSample.label.receivedAt"), value: receivedAtDisplay, ltr: true },
    { key: "sampledBy", label: t("selectedSample.label.sampledBy"), value: sample.sampledBy || "—", ltr: Boolean(sample.sampledBy) },
    { key: "sampleQty", label: t("selectedSample.label.sampleQty"), value: sample.sampleQuantity || "—", ltr: Boolean(sample.sampleQuantity) },
    { key: "context", label: contextLabel, value: contextValue, ltr: contextValue !== "—" && !isWater && sample.category !== "FinishedProduct" && sample.category !== "AfterCleaning" },
    { key: "storageOrExp", label: storageOrExpLabel, value: storageOrExpValue, ltr: isProductLike && storageOrExpValue !== "—" }
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
            <Trans
              i18nKey="selectedSample.refSubtitle"
              ns="testingWorkspace"
              values={{ ref: sample.referenceNumber, id: sample.sampleId }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
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
              {t("selectedSample.prepareSample")}
            </Button>
          )}

          <RaiseDeviationButton
            targetType="Sample"
            targetId={sample.sampleId}
            sectionId={sample.labSectionId ?? null}
            onRaised={() => setQualityRefreshKey((k) => k + 1)}
          />

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
            {t("selectedSample.sampleSummary")}
          </Button>

          <Tooltip title={t("selectedSample.moreSampleActions")}>
            <IconButton
              size="small"
              aria-label={t("selectedSample.moreSampleActions")}
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
              <ListItemText primary={t("selectedSample.auditHistory")} slotProps={{ primary: { sx: { fontSize: 13 } } }} />
            </MenuItem>

            {canManageSample && onEdit && (
              <MenuItem
                disabled={!sample.canEditDetails}
                title={sample.canEditDetails ? t("selectedSample.editDetailsAllowedTitle") : t("selectedSample.editDetailsLockedTitle")}
                onClick={() => {
                  setActionsMenuAnchor(null);
                  onEdit(sample);
                }}
              >
                <ListItemIcon>
                  <EditOutlinedIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText
                  primary={t("selectedSample.editDetails")}
                  secondary={!sample.canEditDetails ? t("selectedSample.lockedUnderReview") : undefined}
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
                  primary={t("selectedSample.voidSample")}
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
            {t("selectedSample.deselect")}
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
          <Box key={cell.key} sx={{ minWidth: 0 }}>
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
                {cell.ltr ? <Ltr>{cell.value}</Ltr> : cell.value}
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
              <Trans
                i18nKey="selectedSample.assignedTestsHeading"
                ns="testingWorkspace"
                values={{ count: sample.assignedTests.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {sample.assignedTests.length > 0
                ? t("selectedSample.completedProgress", { completed: completedTestsCount, total: sample.assignedTests.length })
                : t("selectedSample.noTestWorkflowsAssigned")}
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
            {t("selectedSample.openPathogenWorkflow")}
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
                  {t("selectedSample.sampleNeedsPreparation")}
                </Typography>
                <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text }}>
                  {isProductLike
                    ? t("selectedSample.prepInstructionsProduct")
                    : t("selectedSample.prepInstructionsLocation")}
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
              {t("selectedSample.startPreparation")}
            </Button>
          </Paper>
        )}

        {sample.assignedTests.length === 0 ? (
          <Typography sx={{ color: "text.secondary", fontSize: 13, py: 3, textAlign: "center" }}>
            {t("selectedSample.noTestsAssigned")}
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

        <Box sx={{ mt: 2 }}>
          <QualityRecordsPanel
            targetType="Sample"
            targetId={sample.sampleId}
            refreshKey={qualityRefreshKey}
            compact
          />
        </Box>
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
