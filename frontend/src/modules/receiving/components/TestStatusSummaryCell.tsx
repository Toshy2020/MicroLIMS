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
  ListItemText
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import FiberManualRecordIcon from "@mui/icons-material/FiberManualRecord";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { StatusBadge, statusColor } from "../../../theme/../components/StatusBadge";
import { brandColors } from "../../../theme";

interface Props {
  sample: SampleRecord;
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewAllTests: (sample: SampleRecord) => void;
}

function getSummaryText(tests: TestOrderSummary[], preparationStatus: string): { text: string; bg: string; color: string } {
  if (preparationStatus === "NeedsPreparation") {
    return { text: "Needs Preparation", bg: "#fef3c7", color: "#92400e" };
  }

  const total = tests.length;
  if (total === 0) {
    return { text: "—", bg: "#f3f4f6", color: "#6b7280" };
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
    return { text: `${total} / ${total} Approved`, bg: "#dcfce7", color: "#166534" };
  }

  if (waiting === total) {
    return { text: "Not Started", bg: "#f3f4f6", color: "#4b5563" };
  }

  const parts: string[] = [];
  if (underReview > 0) parts.push(`${underReview} Under Review`);
  if (inProgress > 0) parts.push(`${inProgress} In Progress`);
  if (resultEntered > 0) parts.push(`${resultEntered} Result Entered`);
  if (approved > 0 && parts.length === 0) parts.push(`${approved} / ${total} Approved`);
  if (rejected > 0) parts.push(`${rejected} Rejected`);
  if (waiting > 0 && parts.length === 0) parts.push(`${waiting} Waiting`);

  const summary = parts.slice(0, 2).join(", ") || `${total} Tests`;
  return { text: summary, bg: "#f3e8ff", color: brandColors.sectionTitle };
}

export function TestStatusSummaryCell({ sample, onTestClick, onViewAllTests }: Props) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);

  const tests = sample.assignedTests || [];
  const summary = getSummaryText(tests, sample.preparationStatus);
  const isOpen = Boolean(anchorEl);

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    if (tests.length > 0) {
      setAnchorEl(event.currentTarget);
    }
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  return (
    <>
      <Box
        component="button"
        onClick={handleClick}
        disabled={tests.length === 0}
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 0.5,
          px: 1.25,
          py: 0.5,
          borderRadius: 1.5,
          border: `1px solid ${summary.bg === "#f3e8ff" ? "#d8b4fe" : "#e5e7eb"}`,
          bgcolor: summary.bg,
          color: summary.color,
          fontSize: 12,
          fontWeight: 600,
          cursor: tests.length > 0 ? "pointer" : "default",
          transition: "all 0.15s ease",
          "&:hover": tests.length > 0 ? { filter: "brightness(0.95)", transform: "scale(1.01)" } : undefined
        }}
      >
        <span>{summary.text}</span>
        {tests.length > 0 && (
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
              border: "1px solid #e5e7eb"
            }
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: brandColors.pageTitle }}>
              Assigned Tests ({tests.length})
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              {sample.referenceNumber}
            </Typography>
          </Box>

          <Divider sx={{ mb: 1 }} />

          <List dense disablePadding sx={{ maxHeight: 220, overflowY: "auto" }}>
            {tests.map((test) => {
              const isApproved = test.status === "Approved";
              const unit = sample.category === "EnvironmentalMonitoring" ? "rooms" : "parts";
              const locCount = test.locationCount && test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";
              const color = statusColor(test.status);

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
                    bgcolor: "#fafafa",
                    "&:hover": { bgcolor: "#f3e8ff" }
                  }}
                >
                  <ListItemText
                    primary={
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                        {test.testCode}{locCount}
                      </Typography>
                    }
                  />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    {isApproved ? (
                      <CheckCircleIcon sx={{ fontSize: 14, color: "#16a34a" }} />
                    ) : (
                      <FiberManualRecordIcon sx={{ fontSize: 10, color }} />
                    )}
                    <Typography sx={{ fontSize: 11, fontWeight: 600, color }}>
                      {test.status}
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
            onClick={() => {
              handleClose();
              onViewAllTests(sample);
            }}
            endIcon={<OpenInNewIcon sx={{ fontSize: 14 }} />}
            sx={{
              bgcolor: brandColors.sectionTitle,
              fontSize: 12,
              fontWeight: 600,
              py: 0.5,
              "&:hover": { bgcolor: "#631f74" }
            }}
          >
            View All Tests ({tests.length})
          </Button>
        </Popover>
      )}
    </>
  );
}
