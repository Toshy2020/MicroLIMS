import React, { useState } from "react";
import {
  Box,
  Typography,
  Popover,
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
import type { TFunction } from "i18next";
import { Trans } from "react-i18next";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { StatusBadge, statusColor } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { StatusTone } from "../../../theme/statusTokens";

interface Props {
  sample: SampleRecord;
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewAllTests: (sample: SampleRecord) => void;
  onPrepareSample?: (sample: SampleRecord) => void;
  // Periodic retest awaiting its tests: opens the test-selection dialog.
  onSelectTests?: (sample: SampleRecord) => void;
}

// Closed samples: the summary states how the record closed, ahead of any
// preparation or test-progress wording that would suggest work remains.
const CLOSED_SAMPLE_SUMMARY: Record<string, { key: string; tone: StatusTone }> = {
  Rejected: { key: "testSummary.rejected", tone: "detected" },
  Voided: { key: "testSummary.voided", tone: "detected" },
  Cancelled: { key: "testSummary.cancelled", tone: "detected" },
  RetestRequested: { key: "testSummary.onHoldRetest", tone: "inconclusive" }
};

function getSummaryText(
  t: TFunction<"receiving">,
  tests: TestOrderSummary[],
  preparationStatus: string,
  sampleStatus: string,
  awaitingTestSelection: boolean
): { text: string; tone: StatusTone; isDirectAction: boolean } {
  const closed = CLOSED_SAMPLE_SUMMARY[sampleStatus];
  if (closed) {
    return { text: t(closed.key), tone: closed.tone, isDirectAction: false };
  }

  if (awaitingTestSelection) {
    return { text: t("testSummary.awaitingTestSelection"), tone: "inconclusive", isDirectAction: true };
  }

  if (preparationStatus === "NeedsPreparation") {
    return { text: t("testSummary.needsPreparation"), tone: "inconclusive", isDirectAction: true };
  }

  if (sampleStatus === "UnderReview" || sampleStatus === "UnderApproval") {
    return {
      text: sampleStatus === "UnderApproval" ? t("testSummary.underApproval") : t("testSummary.underReview"),
      tone: "purple",
      isDirectAction: true
    };
  }

  const total = tests.length;
  if (total === 0) {
    return { text: "—", tone: "pending", isDirectAction: false };
  }

  const approved = tests.filter((test) => test.status === "Approved").length;
  const inProgress = tests.filter(
    (test) => test.status === "InProgress" || test.status === "Running" || test.status === "Incubating"
  ).length;
  const underReview = tests.filter((test) => test.status === "UnderReview").length;
  const underApproval = tests.filter((test) => test.status === "Reviewed").length;
  const resultEntered = tests.filter((test) => test.status === "ResultEntered").length;
  const rejected = tests.filter((test) => test.status === "Rejected").length;
  const waiting = tests.filter((test) => test.status === "Waiting" || test.status === "NotStarted").length;

  if (approved === total) {
    return { text: t("testSummary.approvedOfTotal", { approved: total, total }), tone: "notDetected", isDirectAction: false };
  }

  if (waiting === total) {
    return { text: t("testSummary.notStarted"), tone: "pending", isDirectAction: false };
  }

  if (underApproval === total) {
    return { text: t("testSummary.underApproval"), tone: "purple", isDirectAction: true };
  }

  if (underReview === total) {
    return { text: t("testSummary.underReview"), tone: "purple", isDirectAction: true };
  }

  const parts: string[] = [];
  if (underReview > 0) parts.push(t("testSummary.countUnderReview", { count: underReview }));
  if (underApproval > 0) parts.push(t("testSummary.countUnderApproval", { count: underApproval }));
  if (inProgress > 0) parts.push(t("testSummary.countInProgress", { count: inProgress }));
  if (resultEntered > 0) parts.push(t("testSummary.countResultEntered", { count: resultEntered }));
  if (approved > 0 && parts.length === 0) parts.push(t("testSummary.approvedOfTotal", { approved, total }));
  if (rejected > 0) parts.push(t("testSummary.countRejected", { count: rejected }));
  if (waiting > 0 && parts.length === 0) parts.push(t("testSummary.countWaiting", { count: waiting }));

  const summary = parts.slice(0, 2).join(", ") || t("testSummary.countTests", { count: total });
  return { text: summary, tone: "purple", isDirectAction: false };
}

export function TestStatusSummaryCell({ sample, onTestClick, onViewAllTests, onPrepareSample, onSelectTests }: Props) {
  const { t } = useTranslation("receiving");
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const theme = useTheme();

  const tests = sample.assignedTests || [];
  // Another laboratory of the sample may still be pending while this one has tests.
  const pendingSelection = Boolean(sample.awaitingTestSelection);
  const awaiting = pendingSelection && tests.length === 0;
  const summaryInfo = getSummaryText(t, tests, sample.preparationStatus, sample.status, awaiting);

  const summaryTone = theme.custom.status[summaryInfo.tone];
  const summary = { text: summaryInfo.text, bg: summaryTone.bg, textColor: summaryTone.text, border: summaryTone.border };
  const isOpen = Boolean(anchorEl);

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    event.stopPropagation(); // Stop propagation to row selection

    // 0. Periodic retest with no tests yet -> the test-selection dialog
    if (awaiting) {
      onSelectTests?.(sample);
      return;
    }

    // 1. Stage: Needs Preparation -> Direct shortcut to Preparation Dialog
    // (never for a closed sample - there is no preparation left to do)
    if (sample.preparationStatus === "NeedsPreparation" && !CLOSED_SAMPLE_SUMMARY[sample.status]) {
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

  const handleClose = (e?: React.SyntheticEvent | Event | object) => {
    if (e && "stopPropagation" in e && typeof (e as { stopPropagation?: unknown }).stopPropagation === "function") {
      (e as { stopPropagation: () => void }).stopPropagation();
    }
    setAnchorEl(null);
  };

  const showDropdownIcon = tests.length > 0 && !summaryInfo.isDirectAction;

  return (
    <>
      <Box
        component="button"
        type="button"
        data-no-row-click="true"
        onClick={handleClick}
        disabled={!awaiting && tests.length === 0 && sample.preparationStatus !== "NeedsPreparation"}
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
          cursor: (awaiting || tests.length > 0 || sample.preparationStatus === "NeedsPreparation") ? "pointer" : "default",
          transition: "all 0.15s ease",
          "&:hover": (awaiting || tests.length > 0 || sample.preparationStatus === "NeedsPreparation")
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

      {pendingSelection && tests.length > 0 && (
        <Typography component="span" sx={{ display: "block", fontSize: 11, color: "text.secondary", mt: 0.25 }}>
          {t("testSummary.awaitingOtherLab")}
        </Typography>
      )}

      {tests.length > 0 && (
        <Popover
          open={isOpen}
          anchorEl={anchorEl}
          onClose={handleClose}
          onClick={(e) => e.stopPropagation()}
          anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
          transformOrigin={{ vertical: "top", horizontal: "left" }}
          slotProps={{
            paper: {
              sx: {
                width: 290,
                p: 1.5,
                borderRadius: 2,
                boxShadow: "0 4px 20px rgba(0,0,0,0.12)",
                border: "1px solid",
                borderColor: "divider"
              }
            }
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
              <Trans i18nKey="testSummary.assignedTestsTitle" ns="receiving" values={{ count: tests.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Ltr>{sample.referenceNumber}</Ltr>
            </Typography>
          </Box>

          <Divider sx={{ mb: 1 }} />

          <List dense disablePadding sx={{ maxHeight: 220, overflowY: "auto" }}>
            {tests.map((test) => {
              const effectiveStatus = test.workflowStatus ?? test.status;
              const isApproved = effectiveStatus === "Approved" || effectiveStatus === "Completed";
              const unit = sample.category === "EnvironmentalMonitoring" ? t("testSummary.rooms") : t("testSummary.parts");
              const locCount = test.locationCount && test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";
              const color = statusColor(effectiveStatus, theme);

              return (
                <ListItemButton
                  key={test.testOrderId}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleClose(e);
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
                        <Ltr>{test.testCode}</Ltr>{locCount}
                      </Typography>
                    }
                  />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    {isApproved ? (
                      <CheckCircleIcon sx={{ fontSize: 14, color: theme.custom.status.notDetected.text }} />
                    ) : (
                      <FiberManualRecordIcon sx={{ fontSize: 11, color }} />
                    )}
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color }}>
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
            onClick={(e) => {
              e.stopPropagation();
              handleClose(e);
              onViewAllTests(sample);
            }}
            endIcon={<OpenInNewIcon sx={{ fontSize: 14 }} />}
            sx={{
              fontSize: 12,
              fontWeight: 600,
              py: 0.5
            }}
          >
            <Trans i18nKey="testSummary.viewAllTests" ns="receiving" values={{ count: tests.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
          </Button>
        </Popover>
      )}
    </>
  );
}
