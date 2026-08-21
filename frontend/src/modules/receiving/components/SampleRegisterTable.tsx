import React, { useState, useEffect } from "react";
import {
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TablePagination,
  Box,
  Typography,
  useTheme
} from "@mui/material";
import DescriptionIcon from "@mui/icons-material/Description";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { CategoryBadge, CauseBadge, StatusBadge } from "../../../components/StatusBadge";
import { StatusTone } from "../../../theme/statusTokens";
import { TestStatusSummaryCell } from "./TestStatusSummaryCell";
import { SampleActionMenu } from "./SampleActionMenu";
import { brandColors } from "../../../theme";
import { ReadOnlyItemDocumentsDialog } from "../../../components/ReadOnlyItemDocumentsDialog";
import { ItemDocumentService } from "../../laboratoryConfiguration/items/services/ItemDocumentService";

interface Props {
  samples: SampleRecord[];
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewSummary: (sample: SampleRecord) => void;
  onEdit: (sample: SampleRecord) => void;
  onViewReport: (sample: SampleRecord) => void;
  onViewAuditHistory: (sample: SampleRecord) => void;
  onPrepareSample: (sample: SampleRecord) => void;
}

// Same statuses StatusBadge.tsx's central token map covers, but collapsed
// to this table's own coarser labels (e.g. RetestRequested/Cancelled/Voided
// all read "Cancelled / Voided" here) - so this stays a local label+tone
// map rather than reusing StatusBadge directly, while still sourcing its
// colors from the one shared theme.custom.status table (not its own hex).
const SAMPLE_STATUS_MAP: Record<string, { label: string; tone: StatusTone }> = {
  Received: { label: "Received", tone: "pending" },
  InTesting: { label: "Under Testing", tone: "info" },
  UnderReview: { label: "Pending Review", tone: "action" },
  UnderApproval: { label: "Pending Review", tone: "action" },
  PendingReview: { label: "Pending Review", tone: "action" },
  Approved: { label: "Approved", tone: "notDetected" },
  Rejected: { label: "Rejected", tone: "detected" },
  RetestRequested: { label: "Cancelled / Voided", tone: "pending" },
  Cancelled: { label: "Cancelled / Voided", tone: "pending" },
  Voided: { label: "Cancelled / Voided", tone: "pending" }
};

function OverallSampleStatusBadge({ status }: { status: string }) {
  const theme = useTheme();
  const config = SAMPLE_STATUS_MAP[status] || { label: status, tone: "pending" as StatusTone };
  const tokens = theme.custom.status[config.tone];

  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        px: 1.25,
        py: 0.35,
        borderRadius: 5,
        fontSize: 11,
        fontWeight: 700,
        bgcolor: tokens.bg,
        color: tokens.text,
        border: `1px solid ${tokens.border}`
      }}
    >
      {config.label}
    </Box>
  );
}

export function SampleRegisterTable({
  samples,
  onTestClick,
  onViewSummary,
  onEdit,
  onViewReport,
  onViewAuditHistory,
  onPrepareSample
}: Props) {
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [activeDocSample, setActiveDocSample] = useState<SampleRecord | null>(null);

  const handleChangePage = (_: unknown, newPage: number) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  const paginatedSamples = samples.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  const formatReceivedDate = (d: string) => {
    try {
      const date = new Date(d);
      return date.toLocaleDateString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      });
    } catch {
      return d;
    }
  };

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        overflow: "hidden",
        bgcolor: "background.paper"
      }}
    >
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small" sx={{ minWidth: 960 }}>
          <TableHead sx={{ bgcolor: "background.default" }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", width: 50 }}>#</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 180 }}>
                Item / Reference
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", width: 110 }}>
                Item Type
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 120 }}>
                Cause
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 110 }}>
                Sampled By
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 140 }}>
                Batch / Control No.
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 140 }}>
                Received At
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", width: 140 }}>
                Sample Status
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", minWidth: 170 }}>
                Test Status Summary
              </TableCell>
              <TableCell align="center" sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", width: 110 }}>
                Actions
              </TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {paginatedSamples.length === 0 ? (
              <TableRow>
                <TableCell colSpan={10} align="center" sx={{ py: 6 }}>
                  <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                    No samples found matching the filter criteria.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              paginatedSamples.map((sample) => (
                <TableRow
                  key={sample.sampleId}
                  hover
                  sx={{
                    "&:last-child td, &:last-child th": { border: 0 },
                    "&:hover": { bgcolor: "action.hover" }
                  }}
                >
                  {/* # ID */}
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
                    #{sample.sampleId}
                  </TableCell>

                  {/* Item / Reference */}
                  <TableCell>
                    <Typography sx={{ fontWeight: 700, fontSize: 13, color: "text.primary", lineHeight: 1.2 }}>
                      {sample.displayName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
                      {sample.referenceNumber}
                    </Typography>
                    {sample.itemId && (
                      <SampleDocIndicator
                        sample={sample}
                        onOpenDocs={(s) => setActiveDocSample(s)}
                      />
                    )}
                  </TableCell>

                  {/* Item Type */}
                  <TableCell>
                    <CategoryBadge category={sample.category} />
                  </TableCell>

                  {/* Cause of Testing */}
                  <TableCell>
                    <CauseBadge label={sample.causeOfTesting || "—"} />
                  </TableCell>

                  {/* Sampled By */}
                  <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                    {sample.sampledBy || "—"}
                  </TableCell>

                  {/* Batch / Control No. */}
                  <TableCell>
                    {sample.batchNumber ? (
                      <Box sx={{ fontSize: 12 }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                          B: {sample.batchNumber}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                          C: {sample.controlNumber || "—"}
                        </Typography>
                      </Box>
                    ) : (
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        C: {sample.controlNumber || "—"}
                      </Typography>
                    )}
                  </TableCell>

                  {/* Received At */}
                  <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
                    {formatReceivedDate(sample.receivedAt)}
                  </TableCell>

                  {/* Sample Status */}
                  <TableCell>
                    <OverallSampleStatusBadge status={sample.status} />
                  </TableCell>

                  {/* Test Status Summary */}
                  <TableCell>
                    <TestStatusSummaryCell
                      sample={sample}
                      onTestClick={onTestClick}
                      onViewAllTests={onViewSummary}
                    />
                  </TableCell>

                  {/* Actions */}
                  <TableCell align="center">
                    <SampleActionMenu
                      sample={sample}
                      onViewSummary={onViewSummary}
                      onEdit={onEdit}
                      onViewReport={onViewReport}
                      onViewAuditHistory={onViewAuditHistory}
                      onPrepareSample={onPrepareSample}
                    />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Box>

      {samples.length > 0 && (
        <TablePagination
          rowsPerPageOptions={[10, 25, 50, 100]}
          component="div"
          count={samples.length}
          rowsPerPage={rowsPerPage}
          page={page}
          onPageChange={handleChangePage}
          onRowsPerPageChange={handleChangeRowsPerPage}
          sx={{
            borderTop: "1px solid",
            borderColor: "divider",
            "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
              fontSize: 12
            }
          }}
        />
      )}

      {/* Read-Only Controlled Item Documents Dialog */}
      <ReadOnlyItemDocumentsDialog
        open={Boolean(activeDocSample)}
        itemId={activeDocSample?.itemId ?? null}
        itemName={activeDocSample?.displayName ?? ""}
        category={activeDocSample?.category}
        onClose={() => setActiveDocSample(null)}
      />
    </Paper>
  );
}

function SampleDocIndicator({
  sample,
  onOpenDocs,
}: {
  sample: SampleRecord;
  onOpenDocs: (sample: SampleRecord) => void;
}) {
  const [docCount, setDocCount] = useState<number | null>(null);

  useEffect(() => {
    if (sample.itemId) {
      ItemDocumentService.getDocumentsForItem(sample.itemId)
        .then((docs) => setDocCount(docs.length))
        .catch(() => setDocCount(0));
    }
  }, [sample.itemId]);

  if (!sample.itemId || docCount === null) return null;

  return (
    <Box
      component="span"
      onClick={(e) => {
        e.stopPropagation();
        onOpenDocs(sample);
      }}
      sx={{
        display: "inline-flex",
        alignItems: "center",
        gap: 0.5,
        mt: 0.5,
        px: 0.75,
        py: 0.2,
        borderRadius: 1,
        fontSize: 10,
        fontWeight: 600,
        bgcolor: docCount > 0 ? "action.hover" : "transparent",
        color: docCount > 0 ? "primary.main" : "text.secondary",
        border: "1px solid",
        borderColor: docCount > 0 ? "primary.light" : "divider",
        cursor: "pointer",
        "&:hover": {
          bgcolor: "action.selected",
        },
      }}
      title="View Controlled Item Documents (SOP & Verification Report)"
    >
      <DescriptionIcon style={{ fontSize: 11 }} />
      {docCount > 0 ? `${docCount} Docs` : "0 Docs"}
    </Box>
  );
}

