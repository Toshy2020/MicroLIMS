import React, { useState } from "react";
import {
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TablePagination,
  Box,
  Typography
} from "@mui/material";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { CategoryBadge, CauseBadge, StatusBadge } from "../../../components/StatusBadge";
import { TestStatusSummaryCell } from "./TestStatusSummaryCell";
import { SampleActionMenu } from "./SampleActionMenu";
import { brandColors } from "../../../theme";

interface Props {
  samples: SampleRecord[];
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewSummary: (sample: SampleRecord) => void;
  onEdit: (sample: SampleRecord) => void;
  onViewReport: (sample: SampleRecord) => void;
  onViewAuditHistory: (sample: SampleRecord) => void;
  onPrepareSample: (sample: SampleRecord) => void;
}

const SAMPLE_STATUS_MAP: Record<string, { label: string; bg: string; color: string }> = {
  Received: { label: "Received", bg: "#f3f4f6", color: "#4b5563" },
  InTesting: { label: "Under Testing", bg: "#dbeafe", color: "#1e40af" },
  UnderReview: { label: "Pending Review", bg: "#fef3c7", color: "#92400e" },
  UnderApproval: { label: "Pending Review", bg: "#fef3c7", color: "#92400e" },
  PendingReview: { label: "Pending Review", bg: "#fef3c7", color: "#92400e" },
  Approved: { label: "Approved", bg: "#dcfce7", color: "#166534" },
  Rejected: { label: "Rejected", bg: "#fee2e2", color: "#991b1b" },
  RetestRequested: { label: "Cancelled / Voided", bg: "#f1f5f9", color: "#475569" },
  Cancelled: { label: "Cancelled / Voided", bg: "#f1f5f9", color: "#475569" },
  Voided: { label: "Cancelled / Voided", bg: "#f1f5f9", color: "#475569" }
};

function OverallSampleStatusBadge({ status }: { status: string }) {
  const config = SAMPLE_STATUS_MAP[status] || {
    label: status,
    bg: "#f3f4f6",
    color: "#4b5563"
  };

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
        bgcolor: config.bg,
        color: config.color,
        border: `1px solid ${config.bg === "#f3f4f6" ? "#e5e7eb" : "transparent"}`
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
        border: "1px solid #e5e7eb",
        borderRadius: 2,
        overflow: "hidden",
        bgcolor: "#ffffff"
      }}
    >
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small" sx={{ minWidth: 960 }}>
          <TableHead sx={{ bgcolor: "#fafafa" }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", width: 50 }}>#</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 180 }}>
                Item / Reference
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", width: 110 }}>
                Item Type
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 120 }}>
                Cause
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 110 }}>
                Sampled By
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 140 }}>
                Batch / Control No.
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 140 }}>
                Received At
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", width: 140 }}>
                Sample Status
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, color: "#374151", minWidth: 170 }}>
                Test Status Summary
              </TableCell>
              <TableCell align="center" sx={{ fontWeight: 700, fontSize: 12, color: "#374151", width: 110 }}>
                Actions
              </TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {paginatedSamples.length === 0 ? (
              <TableRow>
                <TableCell colSpan={10} align="center" sx={{ py: 6 }}>
                  <Typography sx={{ color: "#9ca3af", fontSize: 14 }}>
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
                    "&:hover": { bgcolor: "#fbf8fc" }
                  }}
                >
                  {/* # ID */}
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "#6b7280" }}>
                    #{sample.sampleId}
                  </TableCell>

                  {/* Item / Reference */}
                  <TableCell>
                    <Typography sx={{ fontWeight: 700, fontSize: 13, color: "#111827", lineHeight: 1.2 }}>
                      {sample.displayName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "#6b7280", mt: 0.25 }}>
                      {sample.referenceNumber}
                    </Typography>
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
                  <TableCell sx={{ fontSize: 12, color: "#374151" }}>
                    {sample.sampledBy || "—"}
                  </TableCell>

                  {/* Batch / Control No. */}
                  <TableCell>
                    {sample.batchNumber ? (
                      <Box sx={{ fontSize: 12 }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "#1f2937" }}>
                          B: {sample.batchNumber}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "#6b7280" }}>
                          C: {sample.controlNumber || "—"}
                        </Typography>
                      </Box>
                    ) : (
                      <Typography sx={{ fontSize: 12, color: "#374151" }}>
                        C: {sample.controlNumber || "—"}
                      </Typography>
                    )}
                  </TableCell>

                  {/* Received At */}
                  <TableCell sx={{ fontSize: 12, color: "#4b5563", whiteSpace: "nowrap" }}>
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
            borderTop: "1px solid #e5e7eb",
            "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
              fontSize: 12
            }
          }}
        />
      )}
    </Paper>
  );
}
