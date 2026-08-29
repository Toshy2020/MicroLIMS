import React, { useState } from "react";
import {
  Box,
  Typography,
  Popover,
  Paper,
  Divider,
  Button,
  List,
  ListItemButton,
  ListItemText,
  useTheme
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import FiberManualRecordIcon from "@mui/icons-material/FiberManualRecord";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { StatusBadge, statusColor } from "../../../components/StatusBadge";
import { StatusTone } from "../../../theme/statusTokens";
import { brandColors } from "../../../theme";

interface Props {
  sample: SampleRecord;
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewAllTests: (sample: SampleRecord) => void;
  onPrepareSample?: (sample: SampleRecord) => void;
}

function getSummaryText(tests: TestOrderSummary[], preparationStatus: string, sampleStatus: string): { text: string; tone: StatusTone; isDirectAction: boolean } {
  if (preparationStatus === "NeedsPreparation") {
    return { text: "Needs Preparation", tone: "inconclusive", isDirectAction: true };
  }

  if (sampleStatus === "UnderReview" || sampleStatus === "UnderApproval") {
    return { text: sampleStatus === "UnderApproval" ? "Under Approval" : "Under Review", tone: "purple", isDirectAction: true };
  }

  const total = tests.length;
  if (total === 0) {
    return { text: "—", tone: "pending", isDirectAction: false };
  }

  const approved = tests.filter((t) => t.status === "Approved").length;
  const inProgress = tests.filter(
    (t) => t.status === "InProgress" || t.status === "Running" || t.status === "Incubating"
  ).length;
  const underReview = tests.filter(
    (t) => t.status === "UnderReview" || t.status === "Reviewed"
  ).length;
  const resultEntered = tests.filter((t) => t.status === "ResultEntered").length;
  const rejected = tests.filter((t) => t.status === "Rejected").length;
  const waiting = tests.filter((t) => t.status === "Waiting" || t.status === "NotStarted").length;

  if (approved === total) {
    return { text: `${total} / ${total} Approved`, tone: "notDetected", isDirectAction: false };
  }

  if (waiting === total) {
    return { text: "Not Started", tone: "pending", isDirectAction: false };
  }

  if (underReview === total) {
    return { text: "Under Review", tone: "purple", isDirectAction: true };
  }

  const parts: string[] = [];
  if (underReview > 0) parts.push(`${underReview} Under Review`);
  if (inProgress > 0) parts.push(`${inProgress} In Progress`);
  if (resultEntered > 0) parts.push(`${resultEntered} Result Entered`);
  if (approved > 0 && parts.length === 0) parts.push(`${approved} / ${total} Approved`);
  if (rejected > 0) parts.push(`${rejected} Rejected`);
  if (waiting > 0 && parts.length === 0) parts.push(`${waiting} Waiting`);

  const summary = parts.slice(0, 2).join(", ") || `${total} Tests`;
  return { text: summary, tone: "purple", isDirectAction: false };
}

export function TestStatusSummaryCell({ sample, onTestClick, onViewAllTests, onPrepareSample }: Props) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const theme = useTheme();

  const tests = sample.assignedTests || [];
  const summaryInfo = getSummaryText(tests, sample.preparationStatus, sample.status);
  const summaryTone = theme.custom.status[summaryInfo.tone];
  const summary = { text: summaryInfo.text, bg: summaryTone.bg, textColor: summaryTone.text, border: summaryTone.border };
  const isOpen = Boolean(anchorEl);

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    event.stopPropagation(); // Stop propagation to row selection

    // 1. Stage: Needs Preparation -> Direct shortcut to Preparation Dialog
    if (sample.preparationStatus === "NeedsPreparation") {
      onPrepareSample?.(sample);
      return;
    }

    // 2. Stage: Under Review / Under Approval -> Direct shortcut to Review / Approval Dialog
    if (
      sample.status === "UnderReview" ||
      sample.status === "UnderApproval" ||
      (tests.length > 0 && tests.every((t) => t.status === "UnderReview" || t.status === "Reviewed"))
    ) {
      onViewAllTests(sample);
      return;
    }

    // 3. Other stages -> Popover list of tests
    if (tests.length > 0) {
      setAnchorEl(event.currentTarget);
    }
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const showDropdownIcon = tests.length > 0 && !summaryInfo.isDirectAction;

  return (
    <>
      <Box
        component="button"
        onClick={handleClick}
        disabled={tests.length === 0 && sample.preparationStatus !== "NeedsPreparation"}
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 0.5,
          px: 1.25,
          py: 0.5,
          borderRadius: 1.5,
          border: `1px solid ${summary.border}`,
          bgcolor: summary.bg,
          color: summary.textColor,
          fontSize: 12,
          fontWeight: 600,
          cursor: (tests.length > 0 || sample.preparationStatus === "NeedsPreparation") ? "pointer" : "default",
          transition: "all 0.15s ease",
          "&:hover": (tests.length > 0 || sample.preparationStatus === "NeedsPreparation")
            ? { filter: "brightness(0.95)", transform: "scale(1.01)" }
            : undefined
        }}
      >
        <span>{summary.text}</span>
        {showDropdownIcon && (
          <ExpandMoreIcon
            sx={{
              fontSize: 16,
              transform: isOpen ? "rotate(180deg)" : "none",
              transition: "transform 0.2s ease"
            }}
          />
        )}
      </Box>

      {tests.length > 0 && (
        <Popover
          open={isOpen}
          anchorEl={anchorEl}
          onClose={handleClose}
          anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
          transformOrigin={{ vertical: "top", horizontal: "left" }}
          PaperProps={{
            sx: {
              width: 290,
              p: 1.5,
              borderRadius: 2,
              boxShadow: "0 4px 20px rgba(0,0,0,0.12)",
              border: "1px solid",
              borderColor: "divider"
            }
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
              Assigned Tests ({tests.length})
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              {sample.referenceNumber}
            </Typography>
          </Box>

          <Divider sx={{ mb: 1 }} />

          <List dense disablePadding sx={{ maxHeight: 220, overflowY: "auto" }}>
            {tests.map((test) => {
              const effectiveStatus = test.workflowStatus ?? test.status;
              const isApproved = effectiveStatus === "Approved" || effectiveStatus === "Completed";
              const unit = sample.category === "EnvironmentalMonitoring" ? "rooms" : "parts";
              const locCount = test.locationCount && test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";
              const color = statusColor(effectiveStatus, theme);

              return (
                <ListItemButton
                  key={test.testOrderId}
                  onClick={() => {
                    handleClose();
                    onTestClick(test, sample);
                  }}
                  sx={{
                    px: 1,
                    py: 0.75,
                    borderRadius: 1,
                    mb: 0.5,
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    bgcolor: "background.default",
                    "&:hover": { bgcolor: theme.custom.status.purple.bg }
                  }}
                >
                  <ListItemText
                    primary={
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                        {test.testCode}{locCount}
                      </Typography>
                    }
                  />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    {isApproved ? (
                      <CheckCircleIcon sx={{ fontSize: 14, color: theme.custom.status.notDetected.text }} />
                    ) : (
                      <FiberManualRecordIcon sx={{ fontSize: 10, color }} />
                    )}
                    <Typography sx={{ fontSize: 11, fontWeight: 600, color }}>
                      <StatusBadge status={effectiveStatus} />
                    </Typography>
                  </Box>
                </ListItemButton>
              );
            })}
          </List>

          <Divider sx={{ my: 1 }} />

          <Button
            fullWidth
            size="small"
            variant="contained"
            color="primary"
            onClick={() => {
              handleClose();
              onViewAllTests(sample);
            }}
            endIcon={<OpenInNewIcon sx={{ fontSize: 14 }} />}
            sx={{
              fontSize: 12,
              fontWeight: 600,
              py: 0.5
            }}
          >
            View All Tests ({tests.length})
          </Button>
        </Popover>
      )}
    </>
  );
}
