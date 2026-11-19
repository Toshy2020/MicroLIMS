import { useCallback, useEffect, useState } from "react";
import { Box, Button, Link as MuiLink, Stack, Tooltip, Typography } from "@mui/material";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import axios from "axios";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { LabPage, RegisterTable, type RegisterColumn } from "../../components/lab";
import { LoadErrorAlert } from "../../components/LoadErrorAlert";
import { StatusBadge } from "../../components/StatusBadge";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import {
  auditTrailReviewService,
  type AuditTrailReviewListItemDto,
  type StartAuditTrailReviewRequest
} from "../../services/auditTrailReviewService";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import { formatDateTimeWithZone, formatReviewScope } from "./formatAuditDate";
import { StartReviewDialog } from "./StartReviewDialog";
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string): string => i18n.t(key, { ns: "auditReview" });


const PAGE_SIZE = 25;

export function AuditTrailReviewsPage() {
  const { t } = useTranslation("auditReview");
  const navigate = useNavigate();
  const { permissions } = useAuth();
  const canReview = permissions.includes(PERMISSIONS.AUDIT_REVIEW);

  const [reviews, setReviews] = useState<AuditTrailReviewListItemDto[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadTick, setReloadTick] = useState(0);
  const [startOpen, setStartOpen] = useState(false);

  const reload = useCallback(() => setReloadTick((n) => n + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);

    auditTrailReviewService
      .list(page, PAGE_SIZE, controller.signal)
      .then((data) => {
        setReviews(data);
      })
      .catch((err: unknown) => {
        if (axios.isCancel(err) || (err instanceof DOMException && err.name === "AbortError")) return;
        const msg = serverMessage(err) ?? getErrorMessage(err, tl("list.loadError"));
        setLoadError(msg);
        toast.error(msg);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [page, reloadTick]);

  const handleStart = async (request: StartAuditTrailReviewRequest) => {
    const created = await auditTrailReviewService.start(request);
    toast.success(t("list.started"));
    setStartOpen(false);
    navigate(`/audit-trail-reviews/${created.id}`);
  };

  const columns: RegisterColumn<AuditTrailReviewListItemDto>[] = [
    {
      key: "title",
      label: t("list.title_col"),
      sortable: true,
      render: (row) => (
        <Box sx={{ minWidth: 0 }}>
          <Tooltip title={row.title}>
            <MuiLink
              component={RouterLink}
              to={`/audit-trail-reviews/${row.id}`}
              onClick={(e) => e.stopPropagation()}
              underline="hover"
              sx={{
                fontWeight: 600,
                display: "block",
                maxWidth: 280,
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap"
              }}
            >
              {row.title}
            </MuiLink>
          </Tooltip>
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{ fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}
          >
            {t("list.cutoff", { id: row.id, cutoff: row.cutoffAuditLogId })}
          </Typography>
        </Box>
      )
    },
    {
      key: "scopeKind",
      label: t("common.scope"),
      render: (row) => {
        const text = formatReviewScope(row.scopeKind, row.scopeEntityName, row.scopeRecordId, t);
        return (
          <Tooltip title={text}>
            <Typography
              variant="body2"
              sx={{
                maxWidth: 200,
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
                fontFamily: row.scopeKind === "All" ? undefined : monospaceFontFamily
              }}
            >
              {text}
            </Typography>
          </Tooltip>
        );
      }
    },
    {
      key: "periodFromUtc",
      label: t("common.period"),
      sortable: true,
      render: (row) => (
        <Box sx={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>
          <Typography variant="body2">{formatDateTimeWithZone(row.periodFromUtc)}</Typography>
          <Typography variant="caption" color="text.secondary">
            {t("list.periodTo", { time: formatDateTimeWithZone(row.periodToUtc) })}
          </Typography>
        </Box>
      )
    },
    {
      key: "entryCount",
      label: t("list.entries"),
      numeric: true,
      sortable: true,
      render: (row) => (
        <Box sx={{ fontVariantNumeric: "tabular-nums" }}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {row.entryCount}
          </Typography>
          {row.newerEntriesCount > 0 && (
            <Typography variant="caption" color="text.secondary">
              {t("list.newer", { n: row.newerEntriesCount })}
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "findingCount",
      label: t("common.findings"),
      numeric: true,
      sortable: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {t("list.findingsOpen", { n: row.findingCount, open: row.openFindingCount })}
        </Typography>
      )
    },
    {
      key: "status",
      label: t("common.status"),
      sortable: true,
      render: (row) => (
        <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap" }}>
          <StatusBadge status={row.status} />
          {row.outcome && <StatusBadge status={row.outcome} />}
        </Stack>
      )
    },
    {
      key: "startedAtUtc",
      label: t("list.startedSigned"),
      sortable: true,
      render: (row) => (
        <Box sx={{ fontVariantNumeric: "tabular-nums" }}>
          <Typography variant="body2">
            {row.status === "Signed"
              ? `${row.signerFullName ?? t("list.signerFallback")} · ${formatDateTimeWithZone(row.signedAtUtc)}`
              : `${row.startedByName ?? t("common.userNum", { id: row.startedByUserId })} · ${formatDateTimeWithZone(row.startedAtUtc)}`}
          </Typography>
        </Box>
      )
    }
  ];

  const hasServerPagination = page > 1 || reviews.length === PAGE_SIZE;

  return (
    <LabPage
      title={t("list.title")}
      subtitle={t("list.subtitle")}
      actions={
        canReview ? (
          <Button variant="contained" startIcon={<AddOutlinedIcon />} onClick={() => setStartOpen(true)}>
            {t("list.startReview")}
          </Button>
        ) : undefined
      }
    >
      {loadError && (
        <LoadErrorAlert message={loadError} onRetry={reload} />
      )}

      <RegisterTable
        columns={columns}
        rows={reviews}
        getRowId={(r) => r.id}
        loading={loading}
        pageSize={PAGE_SIZE}
        onRowClick={(r) => navigate(`/audit-trail-reviews/${r.id}`)}
        empty={{
          title: t("list.emptyTitle"),
          description: canReview
            ? t("list.emptyReviewer")
            : t("list.emptyViewer"),
          action: canReview ? (
            <Button variant="contained" onClick={() => setStartOpen(true)}>
              {t("list.startReview")}
            </Button>
          ) : undefined
        }}
      />

      {!loading && hasServerPagination && (
        <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end", alignItems: "center" }}>
          <Button size="small" variant="outlined" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
            {t("list.previousPage")}
          </Button>
          <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {t("list.page", { n: page })}
          </Typography>
          <Button size="small" variant="outlined" disabled={reviews.length < PAGE_SIZE} onClick={() => setPage((p) => p + 1)}>
            {t("list.nextPage")}
          </Button>
        </Stack>
      )}

      {canReview && (
        <StartReviewDialog
          open={startOpen}
          onClose={() => setStartOpen(false)}
          onSubmit={handleStart}
        />
      )}
    </LabPage>
  );
}
