import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Alert, Box, Button, Paper, Skeleton, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import { useTranslation } from "../../i18n";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { PageHeader } from "../../components/PageHeader";
import { MetaBadge, StatusBadge, ToneBadge } from "../../components/StatusBadge";
import { PartialOutageBanner } from "../../components/lab/PartialOutageBanner";
import { SignedTimeline } from "../../components/SignedTimeline";
import { useBreadcrumb } from "../../components/breadcrumbContext";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import {
  qualityService,
  type QualityActionDetailDto,
  type QualityCommentDto,
  type QualityExtensionDto,
  type QualityLinkDto,
  type QualityRecordDetailDto
} from "../../services/qualityService";
import { saveBlob } from "./saveBlob";
import { qualityTypeActionsFor } from "./typeActions";
import { ActionsPanel } from "./components/ActionsPanel";
import { AttachmentsPanel } from "./components/AttachmentsPanel";
import { CommentsPanel } from "./components/CommentsPanel";
import { ExtensionDialog, type ExtensionDialogMode } from "./components/ExtensionDialog";
import { ExtensionsPanel } from "./components/ExtensionsPanel";
import { LinksPanel } from "./components/LinksPanel";
import { PathStepper } from "./components/PathStepper";
import { TypeDetails } from "./components/TypeDetails";

type SecondaryKey = "links" | "comments" | "extensions";
type SecondaryState = "loading" | "ready" | "failed";

const SECONDARY_KEYS: readonly SecondaryKey[] = ["links", "comments", "extensions"];
// The server clamps list sizes to 100.
const SECONDARY_PAGE_SIZE = 100;

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
        {label}
      </Typography>
      <Typography component="div" variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
        {children}
      </Typography>
    </Box>
  );
}

export function QualityRecordPage() {
  const { id } = useParams<{ id: string }>();
  const recordId = Number(id);
  const { t } = useTranslation("quality");
  const { permissions, userId } = useAuth();

  const can = {
    raise: permissions.includes(PERMISSIONS.QUALITY_RAISE),
    investigate: permissions.includes(PERMISSIONS.QUALITY_INVESTIGATE),
    labConclude: permissions.includes(PERMISSIONS.QUALITY_LAB_CONCLUDE),
    approve: permissions.includes(PERMISSIONS.QUALITY_APPROVE)
  };
  const readOnly = !can.raise && !can.investigate && !can.labConclude && !can.approve;

  const [detail, setDetail] = useState<QualityRecordDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [links, setLinks] = useState<QualityLinkDto[] | null>(null);
  const [linksTotalCount, setLinksTotalCount] = useState<number | null>(null);
  const [comments, setComments] = useState<QualityCommentDto[] | null>(null);
  const [extensions, setExtensions] = useState<QualityExtensionDto[] | null>(null);
  const [secondary, setSecondary] = useState<Record<SecondaryKey, SecondaryState>>({
    links: "loading",
    comments: "loading",
    extensions: "loading"
  });
  const [extensionMode, setExtensionMode] = useState<ExtensionDialogMode | null>(null);
  const [exportingPdf, setExportingPdf] = useState(false);

  useBreadcrumb(detail?.number ?? (error ? t("record.loadError") : null));

  const loadDetail = useCallback(
    async (signal: AbortSignal, initial: boolean) => {
      if (!Number.isFinite(recordId)) {
        setError(t("record.loadError"));
        setLoading(false);
        return;
      }
      try {
        const result = await qualityService.getRecordDetail(recordId, signal);
        if (signal.aborted) return;
        setDetail(result);
        setError(null);
      } catch (e: unknown) {
        if (signal.aborted) return;
        const message = getErrorMessage(e, t("record.loadError"));
        if (initial) setError(message);
        else toast.error(message);
      } finally {
        if (!signal.aborted) setLoading(false);
      }
    },
    [recordId, t]
  );

  const loadSecondary = useCallback(
    async (key: SecondaryKey, signal: AbortSignal) => {
      setSecondary((state) => ({ ...state, [key]: state[key] === "ready" ? "ready" : "loading" }));
      try {
        if (key === "links") {
          const result = await qualityService.listLinks(recordId, 0, SECONDARY_PAGE_SIZE, signal);
          if (!signal.aborted) {
            setLinks(result.items);
            setLinksTotalCount(result.totalCount);
          }
        } else if (key === "comments") {
          const result = await qualityService.listComments(recordId, 0, SECONDARY_PAGE_SIZE, signal);
          if (!signal.aborted) setComments(result);
        } else {
          const result = await qualityService.listExtensions(recordId, 1, SECONDARY_PAGE_SIZE, signal);
          if (!signal.aborted) setExtensions(result);
        }
        if (!signal.aborted) setSecondary((state) => ({ ...state, [key]: "ready" }));
      } catch {
        if (signal.aborted) return;
        if (key === "links") {
          setLinks(null);
          setLinksTotalCount(null);
        } else if (key === "comments") setComments(null);
        else setExtensions(null);
        setSecondary((state) => ({ ...state, [key]: "failed" }));
      }
    },
    [recordId]
  );

  const controllerRef = useRef<AbortController | null>(null);
  const retryRef = useRef<AbortController | null>(null);

  const loadAll = useCallback(
    async (initial: boolean) => {
      controllerRef.current?.abort();
      retryRef.current?.abort();
      const controller = new AbortController();
      controllerRef.current = controller;
      await Promise.all([
        loadDetail(controller.signal, initial),
        ...SECONDARY_KEYS.map((key) => loadSecondary(key, controller.signal))
      ]);
    },
    [loadDetail, loadSecondary]
  );

  useEffect(() => {
    setLoading(true);
    setDetail(null);
    void loadAll(true);
    return () => {
      controllerRef.current?.abort();
      retryRef.current?.abort();
    };
  }, [loadAll]);

  const reload = useCallback(() => loadAll(false), [loadAll]);

  const failedKeys = SECONDARY_KEYS.filter((key) => secondary[key] === "failed");
  const retryFailed = () => {
    retryRef.current?.abort();
    const controller = new AbortController();
    retryRef.current = controller;
    for (const key of failedKeys) void loadSecondary(key, controller.signal);
  };

  const exportPdf = async () => {
    if (!detail) return;
    setExportingPdf(true);
    try {
      saveBlob(await qualityService.downloadPdf(detail.id), `${detail.number}.pdf`);
    } catch (e: unknown) {
      toast.error(getErrorMessage(e, t("record.pdfError")));
    } finally {
      setExportingPdf(false);
    }
  };

  if (loading && !detail) {
    return (
      <Stack spacing={2} aria-busy="true" aria-label={t("record.loading")} role="group">
        <Skeleton variant="rounded" height={64} />
        <Skeleton variant="rounded" height={160} />
        <Skeleton variant="rounded" height={240} />
      </Stack>
    );
  }

  if (error || !detail) {
    return (
      <Stack spacing={2}>
        <Button component={Link} to="/quality" startIcon={<ArrowBackIcon />} sx={{ alignSelf: "flex-start" }}>
          {t("record.backToRegister")}
        </Button>
        <Alert severity="error" role="alert">
          {error ?? t("record.loadError")}
        </Alert>
      </Stack>
    );
  }

  const isOwner = userId !== null && detail.ownerUserId === userId;
  // The server lets only the owner or a Quality.Investigate holder void an attachment or remove a link.
  const canRemove = isOwner || can.investigate;
  const TypeActions = qualityTypeActionsFor(detail.type);

  const openActionExtension = (action: QualityActionDetailDto) =>
    setExtensionMode({ kind: "request", actionId: action.id, actionDescription: action.description, currentDueDate: action.dueDate });

  // A panel whose request has not answered yet shows a placeholder, not an "unavailable" message.
  const placeholder = <Skeleton variant="rounded" height={96} aria-busy="true" />;

  return (
    <Stack spacing={2.5}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1.5 }}>
        <Button component={Link} to="/quality" startIcon={<ArrowBackIcon />}>
          {t("record.backToRegister")}
        </Button>
        <Button variant="outlined" startIcon={<PictureAsPdfOutlinedIcon />} disabled={exportingPdf} onClick={() => void exportPdf()}>
          {t("record.downloadPdf")}
        </Button>
      </Box>

      <PageHeader
        title={
          <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>
            {detail.number}
          </Box>
        }
        subtitle={detail.title}
      />

      {readOnly && <Alert severity="info">{t("record.readOnlyNotice")}</Alert>}
      {detail.isCancelled && (
        <Alert severity="warning">{t("record.cancelledNotice", { reason: detail.cancelReason ?? "" })}</Alert>
      )}
      {failedKeys.length > 0 && (
        <PartialOutageBanner sources={failedKeys.map((key) => t(`record.outageSources.${key}`))} onRetry={retryFailed} />
      )}

      <Paper variant="outlined" component="section" aria-label={t("record.summaryLabel")} sx={{ p: 2.5, borderRadius: 1.5 }}>
        <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap", rowGap: 1 }}>
          <MetaBadge label={t(`types.${detail.type}`)} />
          <StatusBadge status={detail.isCancelled ? "Cancelled" : detail.status} />
          {detail.classificationName && <MetaBadge label={detail.classificationName} />}
          {detail.isOverdue && <ToneBadge tone="detected" label={t("record.overdue")} />}
        </Stack>
        <Stack direction={{ xs: "column", md: "row" }} spacing={3} sx={{ flexWrap: "wrap", rowGap: 2 }}>
          <Fact label={t("record.header.section")}>{detail.sectionName ?? t("record.header.none")}</Fact>
          <Fact label={t("record.header.raisedBy")}>
            {t("record.header.raisedLine", {
              name: detail.raisedByName ?? t("record.header.systemProcess"),
              date: formatDateTimeWithTimeZone(detail.raisedAtUtc)
            })}
          </Fact>
          <Fact label={t("record.header.owner")}>{detail.ownerName ?? t("record.header.none")}</Fact>
          <Fact label={t("record.header.dueDate")}>{detail.dueDate ?? t("record.header.none")}</Fact>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 2 }}>
          {t("record.description")}
        </Typography>
        <Typography variant="body2" sx={{ whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
          {detail.description}
        </Typography>
      </Paper>

      <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
        <PathStepper steps={detail.pathSteps} />
      </Paper>

      {TypeActions && (
        <Box component="section" aria-label={t("record.typeActionsLabel")}>
          <TypeActions
            record={detail}
            permissions={can}
            currentUserId={userId}
            onChanged={reload}
          />
        </Box>
      )}

      <TypeDetails record={detail} />

      <ActionsPanel
        record={{ id: detail.id, type: detail.type, version: detail.version, isCancelled: detail.isCancelled }}
        actions={detail.actions}
        links={links}
        currentUserId={userId}
        onChanged={reload}
        onRequestExtension={openActionExtension}
      />

      {secondary.extensions === "loading" ? (
        placeholder
      ) : (
        <ExtensionsPanel
          extensions={extensions}
          actions={detail.actions}
          canRequest={isOwner && !detail.isCancelled}
          canDecide={can.approve && !detail.isCancelled}
          onRequest={() =>
            setExtensionMode({ kind: "request", actionId: null, actionDescription: null, currentDueDate: detail.dueDate })
          }
          onDecide={(extension) => setExtensionMode({ kind: "decide", extension })}
        />
      )}

      {secondary.links === "loading" ? (
        placeholder
      ) : (
        <LinksPanel
          recordId={detail.id}
          recordVersion={detail.version}
          links={links}
          totalCount={linksTotalCount}
          canEdit={can.raise}
          canRemove={canRemove}
          onChanged={reload}
        />
      )}

      <AttachmentsPanel
        recordId={detail.id}
        recordVersion={detail.version}
        attachments={detail.attachments}
        canEdit={can.raise}
        canRemove={canRemove}
        onChanged={reload}
      />

      {secondary.comments === "loading" ? (
        placeholder
      ) : (
        <CommentsPanel
          recordId={detail.id}
          recordVersion={detail.version}
          comments={comments}
          currentUserId={userId}
          canEdit={can.raise}
          onChanged={reload}
        />
      )}

      <SignedTimeline entries={detail.timeline} emptyText={t("record.timelineEmpty")} />

      <ExtensionDialog
        mode={extensionMode}
        recordId={detail.id}
        recordNumber={detail.number}
        recordVersion={detail.version}
        onClose={() => setExtensionMode(null)}
        onDone={reload}
      />
    </Stack>
  );
}
