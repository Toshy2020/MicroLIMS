import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Paper, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TextField, Tooltip, Typography, useTheme
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import axios from "axios";
import { Link as RouterLink, useParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState, FormDialog, LabPage, RegisterTable, type RegisterColumn } from "../../components/lab";
import { LoadErrorAlert } from "../../components/LoadErrorAlert";
import { SectionTitle } from "../../components/SectionTitle";
import { SignatureDialog } from "../../components/SignatureDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { TableSkeleton } from "../../components/TableSkeleton";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import {
  auditTrailReviewService,
  type AuditTrailReviewDto,
  type AuditTrailReviewFindingDto
} from "../../services/auditTrailReviewService";
import { tableHeadSx } from "../../theme";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import type { AuditLogItem } from "../auditSearch/types/auditTypes";
import { FindingDialog } from "./FindingDialog";
import { formatDateTimeWithZone, formatReviewScope } from "./formatAuditDate";
import { useBreadcrumb } from "../../components/breadcrumbContext";

const ENTRIES_PAGE_SIZE_OPTIONS = [25, 50, 100];
const MAX_COMMENT_LENGTH = 2000;

export function AuditTrailReviewDetailPage() {
  const { id } = useParams<{ id: string }>();
  const reviewId = Number(id);
  const theme = useTheme();
  const { permissions, userId } = useAuth();
  const canReview = permissions.includes(PERMISSIONS.AUDIT_REVIEW);

  const [review, setReview] = useState<AuditTrailReviewDto | null>(null);
  const [reviewLoading, setReviewLoading] = useState(true);
  const [reviewError, setReviewError] = useState<string | null>(null);
  useBreadcrumb(review?.title ?? (reviewError ? "Review unavailable" : null));

  const [entries, setEntries] = useState<AuditLogItem[]>([]);
  const [entriesTotalCount, setEntriesTotalCount] = useState(0);
  const [entriesPage, setEntriesPage] = useState(1);
  const [entriesPageSize, setEntriesPageSize] = useState(25);
  const [entriesLoading, setEntriesLoading] = useState(true);
  const [entriesError, setEntriesError] = useState<string | null>(null);

  const [statusMessage, setStatusMessage] = useState<string>("");
  const [reloadReviewTick, setReloadReviewTick] = useState(0);
  const [reloadEntriesTick, setReloadEntriesTick] = useState(0);

  const [findingEntry, setFindingEntry] = useState<AuditLogItem | null>(null);
  const [closingFinding, setClosingFinding] = useState<AuditTrailReviewFindingDto | null>(null);
  const [closeComment, setCloseComment] = useState("");
  const [closeFieldError, setCloseFieldError] = useState<string | null>(null);
  const [closeSubmitError, setCloseSubmitError] = useState<string | null>(null);
  const [closingSubmitting, setClosingSubmitting] = useState(false);

  const [signingOpen, setSigningOpen] = useState(false);
  const [conclusion, setConclusion] = useState("");
  const [exportingPdf, setExportingPdf] = useState(false);

  const reloadReview = useCallback(() => setReloadReviewTick((t) => t + 1), []);
  const reloadEntries = useCallback(() => setReloadEntriesTick((t) => t + 1), []);

  useEffect(() => {
    if (!Number.isFinite(reviewId) || reviewId <= 0) {
      setReviewError("Invalid audit trail review identifier.");
      setReviewLoading(false);
      return;
    }

    const controller = new AbortController();
    setReviewLoading(true);
    setReviewError(null);

    auditTrailReviewService
      .get(reviewId, controller.signal)
      .then((data) => {
        setReview(data);
      })
      .catch((err: unknown) => {
        if (axios.isCancel(err) || (err instanceof DOMException && err.name === "AbortError")) return;
        const msg = serverMessage(err) ?? getErrorMessage(err, "Could not load the audit trail review.");
        setReviewError(msg);
        toast.error(msg);
      })
      .finally(() => {
        if (!controller.signal.aborted) setReviewLoading(false);
      });

    return () => controller.abort();
  }, [reviewId, reloadReviewTick]);

  useEffect(() => {
    if (!Number.isFinite(reviewId) || reviewId <= 0) {
      setEntriesLoading(false);
      return;
    }

    const controller = new AbortController();
    setEntriesLoading(true);
    setEntriesError(null);

    auditTrailReviewService
      .getEntries(reviewId, entriesPage, entriesPageSize, controller.signal)
      .then((res) => {
        setEntries(res.items);
        setEntriesTotalCount(res.totalCount);
      })
      .catch((err: unknown) => {
        if (axios.isCancel(err) || (err instanceof DOMException && err.name === "AbortError")) return;
        const msg = serverMessage(err) ?? getErrorMessage(err, "Could not load audit trail entries.");
        setEntriesError(msg);
        toast.error(msg);
      })
      .finally(() => {
        if (!controller.signal.aborted) setEntriesLoading(false);
      });

    return () => controller.abort();
  }, [reviewId, entriesPage, entriesPageSize, reloadEntriesTick]);

  const findingsByAuditLogId = useMemo(() => {
    const map = new Map<number, AuditTrailReviewFindingDto>();
    for (const f of review?.findings ?? []) {
      map.set(f.auditLogId, f);
    }
    return map;
  }, [review]);

  const isSigned = review?.status === "Signed";
  const isStarter = review ? (userId === null || review.startedByUserId === userId) : false;
  const canSign = Boolean(review && canReview && !isSigned && isStarter);

  const handleAddFinding = async (note: string) => {
    if (!review || !findingEntry) return;
    await auditTrailReviewService.addFinding(review.id, {
      auditLogId: findingEntry.id,
      note
    });
    toast.success("Finding recorded.");
    setStatusMessage(`Finding recorded for audit entry #${findingEntry.id}.`);
    setFindingEntry(null);
    reloadReview();
  };

  const handleCloseFinding = async () => {
    if (!closingFinding) return;
    setCloseSubmitError(null);
    const trimmed = closeComment.trim();
    if (!trimmed) {
      setCloseFieldError("Closing comment is required.");
      return;
    }
    if (trimmed.length > MAX_COMMENT_LENGTH) {
      setCloseFieldError(`Closing comment must be ${MAX_COMMENT_LENGTH} characters or fewer.`);
      return;
    }
    setCloseFieldError(null);
    setClosingSubmitting(true);
    try {
      await auditTrailReviewService.closeFinding(closingFinding.id, { comment: trimmed });
      toast.success("Finding closed.");
      setStatusMessage(`Finding #${closingFinding.id} closed.`);
      setClosingFinding(null);
      setCloseComment("");
      reloadReview();
    } catch (err: unknown) {
      setCloseSubmitError(serverMessage(err) ?? getErrorMessage(err, "Could not close the finding."));
    } finally {
      setClosingSubmitting(false);
    }
  };

  const handleSign = async (password: string) => {
    if (!review) return;
    const trimmedConclusion = conclusion.trim();
    if (trimmedConclusion.length > MAX_COMMENT_LENGTH) {
      throw { response: { data: { message: `Conclusion must be ${MAX_COMMENT_LENGTH} characters or fewer.` } } };
    }
    const signed = await auditTrailReviewService.sign(
      review.id,
      { password, conclusion: trimmedConclusion },
      review.version
    );
    setReview(signed);
    setSigningOpen(false);
    setConclusion("");
    toast.success("Audit trail review signed.");
    setStatusMessage("Audit trail review signed.");
  };

  const handleExportPdf = async () => {
    if (!review) return;
    setExportingPdf(true);
    try {
      await auditTrailReviewService.exportPdf(review.id);
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not export the audit trail review PDF."));
    } finally {
      setExportingPdf(false);
    }
  };

  const entryColumns: RegisterColumn<AuditLogItem>[] = [
    {
      key: "id",
      label: "Entry ID",
      numeric: true,
      nowrap: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}>
          #{row.id}
        </Typography>
      )
    },
    {
      key: "timestamp",
      label: "Timestamp",
      nowrap: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {formatDateTimeWithZone(row.timestamp)}
        </Typography>
      )
    },
    {
      key: "userName",
      label: "User",
      render: (row) => (
        <Box>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>{row.userName}</Typography>
          {row.userUsername && (
            <Typography variant="caption" color="text.secondary" sx={{ fontFamily: monospaceFontFamily }}>
              {row.userUsername}
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "entityName",
      label: "Record",
      render: (row) => (
        <Box>
          <Typography variant="body2">{row.entityName}</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ fontFamily: monospaceFontFamily }}>
            {row.entityId}
          </Typography>
        </Box>
      )
    },
    {
      key: "action",
      label: "Action",
      nowrap: true
    },
    {
      key: "reason",
      label: "Reason",
      render: (row) => {
        const text = row.reason ?? "—";
        return (
          <Tooltip title={text}>
            <Typography
              variant="body2"
              sx={{ maxWidth: 260, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
            >
              {text}
            </Typography>
          </Tooltip>
        );
      }
    },
    {
      key: "finding",
      label: "Finding",
      render: (row) => {
        const existing = findingsByAuditLogId.get(row.id);
        return existing ? <StatusBadge status={existing.status} /> : "—";
      }
    },
    ...(canReview && !isSigned
      ? [
          {
            key: "actions",
            label: "Actions",
            align: "right" as const,
            render: (row: AuditLogItem) => {
              const alreadyMarked = findingsByAuditLogId.has(row.id);
              return (
                <Button
                  size="small"
                  variant="outlined"
                  disabled={alreadyMarked}
                  aria-label={`Mark audit entry ${row.id} as finding`}
                  onClick={() => setFindingEntry(row)}
                >
                  Mark as finding
                </Button>
              );
            }
          }
        ]
      : [])
  ];

  const showServerPagination = entriesTotalCount > 25 && entries.length <= 25;

  return (
    <LabPage
      title={review ? review.title : "Audit Trail Review"}
      subtitle={
        review
          ? `Started by ${review.startedByName ?? `User #${review.startedByUserId}`} on ${formatDateTimeWithZone(review.startedAtUtc)}`
          : undefined
      }
      actions={
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
          <Button
            component={RouterLink}
            to="/audit-trail-reviews"
            variant="outlined"
            startIcon={<ArrowBackOutlinedIcon />}
          >
            All reviews
          </Button>
          {review && (
            <Button
              variant="outlined"
              startIcon={<PictureAsPdfOutlinedIcon />}
              disabled={exportingPdf}
              onClick={() => void handleExportPdf()}
            >
              {exportingPdf ? "Exporting..." : "Export PDF"}
            </Button>
          )}
          {canSign && (
            <Button
              variant="contained"
              startIcon={<VerifiedUserOutlinedIcon />}
              onClick={() => setSigningOpen(true)}
            >
              Sign review
            </Button>
          )}
        </Stack>
      }
    >
      <Box role="status" aria-live="polite" sx={{ position: "absolute", left: -9999 }}>
        {statusMessage}
      </Box>

      {reviewError && <LoadErrorAlert message={reviewError} onRetry={reloadReview} />}
      {reviewLoading && !review && <TableSkeleton rows={4} cols={4} />}

      {review && (
        <>
          <Paper
            variant="outlined"
            sx={{ p: 2, borderRadius: 2 }}
          >
            <Stack
              direction={{ xs: "column", md: "row" }}
              spacing={2}
              sx={{ justifyContent: "space-between", alignItems: { md: "center" }, mb: 1.5 }}
            >
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                <StatusBadge status={review.status} />
                {review.outcome && <StatusBadge status={review.outcome} />}
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}
                >
                  ATR-{review.id} · v{review.version}
                </Typography>
              </Stack>
            </Stack>

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" },
                gap: 2
              }}
            >
              <Box>
                <Typography variant="caption" color="text.secondary">Scope</Typography>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: 600,
                    fontFamily: review.scopeKind === "All" ? undefined : monospaceFontFamily
                  }}
                >
                  {formatReviewScope(review.scopeKind, review.scopeEntityName, review.scopeRecordId)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Period</Typography>
                <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                  {formatDateTimeWithZone(review.periodFromUtc)} – {formatDateTimeWithZone(review.periodToUtc)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Cut-off audit ID</Typography>
                <Typography
                  variant="body2"
                  sx={{ fontWeight: 600, fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}
                >
                  #{review.cutoffAuditLogId}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Entries in scope</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {review.entryCount}
                </Typography>
              </Box>
            </Box>

            {review.newerEntriesCount > 0 && (
              <Alert
                severity="info"
                role="status"
                sx={{
                  mt: 2,
                  bgcolor: theme.custom.status.info.bg,
                  color: theme.custom.status.info.text,
                  border: `1px solid ${theme.custom.status.info.border}`
                }}
              >
                {review.newerEntriesCount} newer entries exist after this review&apos;s cut-off
              </Alert>
            )}

            {canReview && !isSigned && !isStarter && (
              <Alert severity="info" sx={{ mt: 2 }}>
                Only the user who started this review ({review.startedByName ?? `User #${review.startedByUserId}`}) may sign it.
              </Alert>
            )}
          </Paper>

          {isSigned && (
            <Paper
              variant="outlined"
              sx={{
                p: 2,
                borderRadius: 2,
                bgcolor: theme.custom.status.notDetected.bg,
                borderColor: theme.custom.status.notDetected.border
              }}
            >
              <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 700, mb: 1 }}>
                Electronic Signature
              </Typography>
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
                  gap: 2,
                  mb: 1.5
                }}
              >
                <Box>
                  <Typography variant="caption" color="text.secondary">Signed by</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {review.signerFullName ?? "—"}
                    {review.signerUsername ? ` (${review.signerUsername})` : ""}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Signed at</Typography>
                  <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {formatDateTimeWithZone(review.signedAtUtc)}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Meaning</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {review.signatureMeaningText ?? "—"}
                  </Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Conclusion</Typography>
                <Typography variant="body2">{review.conclusion ?? "—"}</Typography>
              </Box>
            </Paper>
          )}

          <Box>
            <SectionTitle>Audit Entries in Scope</SectionTitle>
            {entriesError && <LoadErrorAlert message={entriesError} onRetry={reloadEntries} sx={{ mb: 1.5 }} />}
            <RegisterTable
              columns={entryColumns}
              rows={entries}
              getRowId={(r) => r.id}
              loading={entriesLoading}
              pageSize={25}
              empty={{
                title: "No audit entries in this review scope",
                description: "No audit log rows match the fixed period, scope, and cut-off ID."
              }}
            />
            {!entriesLoading && showServerPagination && (
              <TablePagination
                component="div"
                count={entriesTotalCount}
                page={entriesPage - 1}
                onPageChange={(_, nextPage) => setEntriesPage(nextPage + 1)}
                rowsPerPage={entriesPageSize}
                rowsPerPageOptions={ENTRIES_PAGE_SIZE_OPTIONS}
                onRowsPerPageChange={(e) => {
                  setEntriesPageSize(Number(e.target.value));
                  setEntriesPage(1);
                }}
              />
            )}
          </Box>

          <Box>
            <SectionTitle>Findings</SectionTitle>
            {review.findings.length === 0 ? (
              <Paper variant="outlined" sx={{ borderRadius: 2 }}>
                <EmptyState
                  title="No findings recorded"
                  description={
                    isSigned
                      ? "This review was signed with no findings recorded."
                      : "No findings have been recorded for this review yet."
                  }
                />
              </Paper>
            ) : (
              <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
                <TableContainer>
                  <Table size="small" aria-label="Audit trail review findings">
                    <TableHead>
                      <TableRow sx={tableHeadSx(theme)}>
                        <TableCell>Entry ID</TableCell>
                        <TableCell>Note</TableCell>
                        <TableCell>Recorded by</TableCell>
                        <TableCell>Status</TableCell>
                        <TableCell>Closure</TableCell>
                        {canReview && !isSigned && <TableCell align="right">Actions</TableCell>}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {review.findings.map((f) => (
                        <TableRow key={f.id}>
                          <TableCell sx={{ fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}>
                            #{f.auditLogId}
                          </TableCell>
                          <TableCell>
                            <Tooltip title={f.note}>
                              <Typography variant="body2" sx={{ maxWidth: 360 }}>
                                {f.note}
                              </Typography>
                            </Tooltip>
                          </TableCell>
                          <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>
                            <Typography variant="body2">{f.recordedByName ?? `User #${f.recordedByUserId}`}</Typography>
                            <Typography variant="caption" color="text.secondary">
                              {formatDateTimeWithZone(f.recordedAtUtc)}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={f.status} />
                          </TableCell>
                          <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>
                            {f.status === "Closed" ? (
                              <Box>
                                <Typography variant="body2">{f.closedComment ?? "—"}</Typography>
                                <Typography variant="caption" color="text.secondary">
                                  {f.closedByName ?? `User #${f.closedByUserId}`} · {formatDateTimeWithZone(f.closedAtUtc)}
                                </Typography>
                              </Box>
                            ) : (
                              "—"
                            )}
                          </TableCell>
                          {canReview && !isSigned && (
                            <TableCell align="right">
                              {f.status === "Open" && (
                                <Button
                                  size="small"
                                  variant="outlined"
                                  aria-label={`Close finding ${f.id}`}
                                  onClick={() => {
                                    setClosingFinding(f);
                                    setCloseComment("");
                                    setCloseFieldError(null);
                                    setCloseSubmitError(null);
                                  }}
                                >
                                  Close finding
                                </Button>
                              )}
                            </TableCell>
                          )}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Paper>
            )}
          </Box>
        </>
      )}

      <FindingDialog
        open={Boolean(findingEntry)}
        entry={findingEntry}
        onClose={() => setFindingEntry(null)}
        onSubmit={handleAddFinding}
      />

      <FormDialog
        open={Boolean(closingFinding)}
        title={`Close finding #${closingFinding?.id ?? ""}`}
        onClose={() => {
          if (!closingSubmitting) setClosingFinding(null);
        }}
        onSubmit={handleCloseFinding}
        submitLabel="Close finding"
        submitting={closingSubmitting}
        error={closeSubmitError}
      >
        <TextField
          label="Closing comment"
          size="small"
          required
          multiline
          minRows={3}
          fullWidth
          value={closeComment}
          onChange={(e) => {
            setCloseComment(e.target.value);
            if (closeFieldError) setCloseFieldError(null);
          }}
          error={Boolean(closeFieldError)}
          helperText={closeFieldError ?? `${closeComment.trim().length}/${MAX_COMMENT_LENGTH}`}
          slotProps={{ htmlInput: { maxLength: MAX_COMMENT_LENGTH } }}
        />
      </FormDialog>

      <SignatureDialog
        open={signingOpen}
        meaningStatement="I have reviewed the audit trail entries in scope and attest to the conclusion below."
        showComment
        commentLabel="Conclusion"
        commentRequired
        comment={conclusion}
        onCommentChange={setConclusion}
        onCancel={() => setSigningOpen(false)}
        onConfirm={handleSign}
      />
    </LabPage>
  );
}
