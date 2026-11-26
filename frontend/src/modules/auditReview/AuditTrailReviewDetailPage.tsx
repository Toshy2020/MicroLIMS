import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Paper, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TextField, Tooltip, Typography, useTheme
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import axios from "axios";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState, FormDialog, LabPage, RegisterTable, type RegisterColumn } from "../../components/lab";
import { LoadErrorAlert } from "../../components/LoadErrorAlert";
import { SectionTitle } from "../../components/SectionTitle";
import { SignatureDialog } from "../../components/SignatureDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { TableSkeleton } from "../../components/TableSkeleton";
import { useAuth } from "../../contexts/AuthContext";
import { APP_ROUTES, PERMISSIONS } from "../../routes/routes";
import {
  auditTrailReviewService,
  type AuditTrailReviewDto,
  type AuditTrailReviewFindingDto
} from "../../services/auditTrailReviewService";
import { tableHeadSx } from "../../theme";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import type { AuditLogItem } from "../auditSearch/types/auditTypes";
import { RaiseDeviationButton } from "../quality/components/RaiseDeviationButton";
import { FindingDialog } from "./FindingDialog";
import { formatDateTimeWithZone, formatReviewScope } from "./formatAuditDate";
import { useBreadcrumb } from "../../components/breadcrumbContext";
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string): string => i18n.t(key, { ns: "auditReview" });


const ENTRIES_PAGE_SIZE_OPTIONS = [25, 50, 100];
const MAX_COMMENT_LENGTH = 2000;

export function AuditTrailReviewDetailPage() {
  const { t } = useTranslation("auditReview");
  const tEn = i18n.getFixedT("en", "auditReview");
  const { id } = useParams<{ id: string }>();
  const reviewId = Number(id);
  const theme = useTheme();
  const { permissions, userId } = useAuth();
  const canReview = permissions.includes(PERMISSIONS.AUDIT_REVIEW);
  // The server also requires Quality.Approve to raise a deviation from a finding; this only decides whether to offer it.
  const canRaiseDeviation = permissions.includes(PERMISSIONS.QUALITY_APPROVE);
  const navigate = useNavigate();

  const [review, setReview] = useState<AuditTrailReviewDto | null>(null);
  const [reviewLoading, setReviewLoading] = useState(true);
  const [reviewError, setReviewError] = useState<string | null>(null);
  useBreadcrumb(review?.title ?? (reviewError ? t("detail.unavailable") : null));

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

  const reloadReview = useCallback(() => setReloadReviewTick((n) => n + 1), []);
  const reloadEntries = useCallback(() => setReloadEntriesTick((n) => n + 1), []);

  useEffect(() => {
    if (!Number.isFinite(reviewId) || reviewId <= 0) {
      setReviewError(tl("detail.invalidId"));
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
        const msg = serverMessage(err) ?? getErrorMessage(err, tl("detail.loadError"));
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
        const msg = serverMessage(err) ?? getErrorMessage(err, tl("detail.entriesError"));
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
    toast.success(t("common.findingRecorded"));
    setStatusMessage(t("detail.findingStatus", { id: findingEntry.id }));
    setFindingEntry(null);
    reloadReview();
  };

  const handleCloseFinding = async () => {
    if (!closingFinding) return;
    setCloseSubmitError(null);
    const trimmed = closeComment.trim();
    if (!trimmed) {
      setCloseFieldError(t("detail.closeCommentRequired"));
      return;
    }
    if (trimmed.length > MAX_COMMENT_LENGTH) {
      setCloseFieldError(t("detail.closeCommentTooLong", { max: MAX_COMMENT_LENGTH }));
      return;
    }
    setCloseFieldError(null);
    setClosingSubmitting(true);
    try {
      await auditTrailReviewService.closeFinding(closingFinding.id, { comment: trimmed });
      toast.success(t("detail.findingClosed"));
      setStatusMessage(t("detail.findingClosedStatus", { id: closingFinding.id }));
      setClosingFinding(null);
      setCloseComment("");
      reloadReview();
    } catch (err: unknown) {
      setCloseSubmitError(serverMessage(err) ?? getErrorMessage(err, t("detail.closeError")));
    } finally {
      setClosingSubmitting(false);
    }
  };

  const handleSign = async (password: string) => {
    if (!review) return;
    const trimmedConclusion = conclusion.trim();
    if (trimmedConclusion.length > MAX_COMMENT_LENGTH) {
      throw { response: { data: { message: t("detail.conclusionTooLong", { max: MAX_COMMENT_LENGTH }) } } };
    }
    const signed = await auditTrailReviewService.sign(
      review.id,
      { password, conclusion: trimmedConclusion },
      review.version
    );
    setReview(signed);
    setSigningOpen(false);
    setConclusion("");
    toast.success(t("detail.signed"));
    setStatusMessage(t("detail.signed"));
  };

  const handleExportPdf = async () => {
    if (!review) return;
    setExportingPdf(true);
    try {
      await auditTrailReviewService.exportPdf(review.id);
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("detail.exportError")));
    } finally {
      setExportingPdf(false);
    }
  };

  const entryColumns: RegisterColumn<AuditLogItem>[] = [
    {
      key: "id",
      label: t("common.entryId"),
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
      label: t("detail.timestamp"),
      nowrap: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {formatDateTimeWithZone(row.timestamp)}
        </Typography>
      )
    },
    {
      key: "userName",
      label: t("detail.user"),
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
      label: t("detail.record"),
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
      label: t("detail.action"),
      nowrap: true
    },
    {
      key: "reason",
      label: t("detail.reason"),
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
      label: t("detail.finding"),
      render: (row) => {
        const existing = findingsByAuditLogId.get(row.id);
        return existing ? <StatusBadge status={existing.status} /> : "—";
      }
    },
    ...(canReview && !isSigned
      ? [
          {
            key: "actions",
            label: t("common.actions"),
            align: "right" as const,
            render: (row: AuditLogItem) => {
              const alreadyMarked = findingsByAuditLogId.has(row.id);
              return (
                <Button
                  size="small"
                  variant="outlined"
                  disabled={alreadyMarked}
                  aria-label={t("detail.markFindingAria", { id: row.id })}
                  onClick={() => setFindingEntry(row)}
                >
                  {t("detail.markFinding")}
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
      title={review ? review.title : t("detail.title")}
      subtitle={
        review
          ? t("detail.startedBy", { name: review.startedByName ?? t("common.userNum", { id: review.startedByUserId }), time: formatDateTimeWithZone(review.startedAtUtc) })
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
            {t("detail.allReviews")}
          </Button>
          {review && (
            <Button
              variant="outlined"
              startIcon={<PictureAsPdfOutlinedIcon />}
              disabled={exportingPdf}
              onClick={() => void handleExportPdf()}
            >
              {exportingPdf ? t("detail.exporting") : t("detail.exportPdf")}
            </Button>
          )}
          {canSign && (
            <Button
              variant="contained"
              startIcon={<VerifiedUserOutlinedIcon />}
              onClick={() => setSigningOpen(true)}
            >
              {t("detail.signReview")}
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
                <Typography variant="caption" color="text.secondary">{t("common.scope")}</Typography>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: 600,
                    fontFamily: review.scopeKind === "All" ? undefined : monospaceFontFamily
                  }}
                >
                  {formatReviewScope(review.scopeKind, review.scopeEntityName, review.scopeRecordId, t)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">{t("common.period")}</Typography>
                <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                  {formatDateTimeWithZone(review.periodFromUtc)} – {formatDateTimeWithZone(review.periodToUtc)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">{t("detail.cutoffId")}</Typography>
                <Typography
                  variant="body2"
                  sx={{ fontWeight: 600, fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}
                >
                  #{review.cutoffAuditLogId}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">{t("detail.entriesInScope")}</Typography>
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
                {t("detail.newerEntries", { n: review.newerEntriesCount })}
              </Alert>
            )}

            {canReview && !isSigned && !isStarter && (
              <Alert severity="info" sx={{ mt: 2 }}>
                {t("detail.onlyStarter", { name: review.startedByName ?? t("common.userNum", { id: review.startedByUserId }) })}
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
                {t("detail.eSignature")}
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
                  <Typography variant="caption" color="text.secondary">{t("detail.signedBy")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {review.signerFullName ?? "—"}
                    {review.signerUsername ? ` (${review.signerUsername})` : ""}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">{t("detail.signedAt")}</Typography>
                  <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {formatDateTimeWithZone(review.signedAtUtc)}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">{t("detail.meaning")}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {review.signatureMeaningText ?? "—"}
                  </Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">{t("common.conclusion")}</Typography>
                <Typography variant="body2">{review.conclusion ?? "—"}</Typography>
              </Box>
            </Paper>
          )}

          <Box>
            <SectionTitle>{t("detail.entriesHeading")}</SectionTitle>
            {entriesError && <LoadErrorAlert message={entriesError} onRetry={reloadEntries} sx={{ mb: 1.5 }} />}
            <RegisterTable
              columns={entryColumns}
              rows={entries}
              getRowId={(r) => r.id}
              loading={entriesLoading}
              pageSize={25}
              empty={{
                title: t("detail.noEntriesTitle"),
                description: t("detail.noEntriesDescription")
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
            <SectionTitle>{t("common.findings")}</SectionTitle>
            {review.findings.length === 0 ? (
              <Paper variant="outlined" sx={{ borderRadius: 2 }}>
                <EmptyState
                  title={t("detail.noFindingsTitle")}
                  description={
                    isSigned
                      ? t("detail.noFindingsSigned")
                      : t("detail.noFindingsYet")
                  }
                />
              </Paper>
            ) : (
              <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
                <TableContainer>
                  <Table size="small" aria-label={t("detail.findingsTable")}>
                    <TableHead>
                      <TableRow sx={tableHeadSx(theme)}>
                        <TableCell>{t("common.entryId")}</TableCell>
                        <TableCell>{t("detail.note")}</TableCell>
                        <TableCell>{t("detail.recordedBy")}</TableCell>
                        <TableCell>{t("common.status")}</TableCell>
                        <TableCell>{t("detail.closure")}</TableCell>
                        {((canReview && !isSigned) || canRaiseDeviation) && <TableCell align="right">{t("common.actions")}</TableCell>}
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
                            <Typography variant="body2">{f.recordedByName ?? t("common.userNum", { id: f.recordedByUserId })}</Typography>
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
                                  {f.closedByName ?? t("common.userNum", { id: f.closedByUserId })} · {formatDateTimeWithZone(f.closedAtUtc)}
                                </Typography>
                              </Box>
                            ) : (
                              "—"
                            )}
                          </TableCell>
                          {((canReview && !isSigned) || canRaiseDeviation) && (
                            <TableCell align="right">
                              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end", flexWrap: "wrap" }}>
                              {canRaiseDeviation && (
                                <RaiseDeviationButton
                                  sourceKind="auditFinding"
                                  sourceId={f.id}
                                  sourceVersion={f.version}
                                  initialTitle={t("detail.raiseDeviationTitle", { id: f.id })}
                                  initialDescription={f.note}
                                  ariaLabel={t("detail.raiseDeviationAria", { id: f.id })}
                                  onRaised={(deviation) => navigate(APP_ROUTES.QUALITY_DETAIL(deviation.id))}
                                />
                              )}
                              {canReview && !isSigned && f.status === "Open" && (
                                <Button
                                  size="small"
                                  variant="outlined"
                                  aria-label={t("detail.closeFindingAria", { id: f.id })}
                                  onClick={() => {
                                    setClosingFinding(f);
                                    setCloseComment("");
                                    setCloseFieldError(null);
                                    setCloseSubmitError(null);
                                  }}
                                >
                                  {t("detail.closeFinding")}
                                </Button>
                              )}
                              </Stack>
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
        title={t("detail.closeTitle", { id: closingFinding?.id ?? "" })}
        onClose={() => {
          if (!closingSubmitting) setClosingFinding(null);
        }}
        onSubmit={handleCloseFinding}
        submitLabel={t("detail.closeFinding")}
        submitting={closingSubmitting}
        error={closeSubmitError}
      >
        <TextField
          label={t("detail.closingComment")}
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
        meaningStatement={tEn("detail.signatureMeaning")}
        meaningStatementTranslation={t("detail.signatureMeaning")}
        showComment
        commentLabel={t("common.conclusion")}
        commentRequired
        comment={conclusion}
        onCommentChange={setConclusion}
        onCancel={() => setSigningOpen(false)}
        onConfirm={handleSign}
      />
    </LabPage>
  );
}
