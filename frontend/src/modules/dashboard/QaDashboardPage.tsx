import { useTranslation } from "../../i18n";
import { useEffect, useState } from "react";
import { Button } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import AssignmentTurnedInOutlinedIcon from "@mui/icons-material/AssignmentTurnedInOutlined";
import GavelOutlinedIcon from "@mui/icons-material/GavelOutlined";
import AssignmentLateOutlinedIcon from "@mui/icons-material/AssignmentLateOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import ListAltOutlinedIcon from "@mui/icons-material/ListAltOutlined";
import axios from "axios";
import { KpiStrip, LabPage, PartialOutageBanner } from "../../components/lab";
import type { SummaryTile } from "../../components/configHierarchy/SummaryTiles";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import {
  auditTrailReviewService,
  type AuditTrailReviewListItemDto
} from "../../services/auditTrailReviewService";
import {
  qualityDashboardService,
  type QualityDashboardDto
} from "../../services/qualityDashboardService";
import { documentApprovalService } from "../documentControl/services/documentApprovalService";
import type { DocumentApprovalTaskDto } from "../documentControl/types/documentControlTypes";

function isAbortError(err: unknown): boolean {
  return axios.isCancel(err) || (err instanceof DOMException && err.name === "AbortError");
}

export function QaDashboardPage() {
  const { t } = useTranslation("dashboard");
  const { userId, permissions } = useAuth();
  const canViewQuality = permissions.includes(PERMISSIONS.QUALITY_VIEW);

  const [reviews, setReviews] = useState<AuditTrailReviewListItemDto[] | null>(null);
  const [pendingApprovals, setPendingApprovals] = useState<DocumentApprovalTaskDto[] | null>(null);
  const [qualityDash, setQualityDash] = useState<QualityDashboardDto | null>(null);
  const [failedSources, setFailedSources] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = () => setReloadKey((k) => k + 1);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailedSources([]);

    const reviewsPromise = auditTrailReviewService
      .list(1, 100, controller.signal)
      .then((data) => ({ ok: true as const, data }))
      .catch((err: unknown) => {
        if (isAbortError(err)) return { ok: false as const, aborted: true as const };
        return { ok: false as const, aborted: false as const, error: err };
      });

    const approvalsPromise = (
      userId != null
        ? documentApprovalService.getApprovalTasks(
            { approverUserId: userId, status: "Pending" },
            controller.signal
          )
        : Promise.resolve([] as DocumentApprovalTaskDto[])
    )
      .then((data) => ({ ok: true as const, data }))
      .catch((err: unknown) => {
        if (isAbortError(err)) return { ok: false as const, aborted: true as const };
        return { ok: false as const, aborted: false as const, error: err };
      });

    const qualityPromise = (
      canViewQuality
        ? qualityDashboardService.getDashboard(controller.signal)
        : Promise.resolve(null)
    )
      .then((data) => ({ ok: true as const, data }))
      .catch((err: unknown) => {
        if (isAbortError(err)) return { ok: false as const, aborted: true as const };
        return { ok: false as const, aborted: false as const, error: err };
      });

    Promise.all([reviewsPromise, approvalsPromise, qualityPromise]).then(
      ([reviewsResult, approvalsResult, qualityResult]) => {
        if (controller.signal.aborted) return;

        const failed: string[] = [];

        if (reviewsResult.ok) {
          setReviews(reviewsResult.data);
        } else if (!reviewsResult.aborted) {
          setReviews(null);
          failed.push("qa.sources.auditReviews");
        }

        if (approvalsResult.ok) {
          setPendingApprovals(approvalsResult.data);
        } else if (!approvalsResult.aborted) {
          setPendingApprovals(null);
          failed.push("qa.sources.documentApprovals");
        }

        if (qualityResult.ok) {
          setQualityDash(qualityResult.data);
        } else if (!qualityResult.aborted) {
          setQualityDash(null);
          failed.push("qualityTiles.source");
        }

        setFailedSources(failed);
        setLoading(false);
      }
    );

    return () => controller.abort();
  }, [reloadKey, userId, canViewQuality]);

  const inProgressCount = reviews
    ? reviews.filter((r) => r.status === "InProgress").length
    : null;
  const openFindingsCount = reviews
    ? reviews.reduce((sum, r) => sum + r.openFindingCount, 0)
    : null;
  const pendingApprovalsCount = pendingApprovals ? pendingApprovals.length : null;

  const tiles: SummaryTile[] = [
    {
      label: t("qa.tiles.auditInProgress"),
      value: inProgressCount ?? "—",
      tone: "info",
      icon: <FactCheckOutlinedIcon />,
      hint: t("qa.tiles.auditInProgressHint"),
      to: "/audit-trail-reviews"
    },
    {
      label: t("qa.tiles.openFindings"),
      value: openFindingsCount ?? "—",
      tone: openFindingsCount === null ? undefined : openFindingsCount > 0 ? "detected" : "notDetected",
      icon: <WarningAmberOutlinedIcon />,
      hint: t("qa.tiles.openFindingsHint"),
      to: "/audit-trail-reviews"
    },
    {
      label: t("qa.tiles.documentsWaiting"),
      value: pendingApprovalsCount ?? "—",
      tone: pendingApprovalsCount && pendingApprovalsCount > 0 ? "action" : undefined,
      icon: <AssignmentTurnedInOutlinedIcon />,
      hint: t("qa.tiles.documentsWaitingHint"),
      to: "/document-control/library"
    },
    ...(canViewQuality
      ? [
          {
            label: t("qualityTiles.waitingForDecision"),
            value: qualityDash ? qualityDash.waitingForMyDecisionTotalCount : "—",
            tone:
              qualityDash && qualityDash.waitingForMyDecisionTotalCount > 0
                ? ("action" as const)
                : undefined,
            icon: <GavelOutlinedIcon />,
            hint: t("qualityTiles.waitingForDecisionHint"),
            to: "/quality"
          },
          {
            label: t("qualityTiles.myOpen"),
            value: qualityDash
              ? qualityDash.myOpenRecordsTotalCount + qualityDash.myOpenActionsTotalCount
              : "—",
            tone: "info" as const,
            icon: <ListAltOutlinedIcon />,
            hint: t("qualityTiles.myOpenHint"),
            to: "/quality?owner=me"
          },
          {
            label: t("qualityTiles.overdue"),
            value: qualityDash
              ? qualityDash.overdueRecordsTotalCount + qualityDash.overdueActionsTotalCount
              : "—",
            tone:
              qualityDash &&
              qualityDash.overdueRecordsTotalCount + qualityDash.overdueActionsTotalCount > 0
                ? ("detected" as const)
                : ("notDetected" as const),
            icon: <AssignmentLateOutlinedIcon />,
            hint: t("qualityTiles.overdueHint"),
            to: "/quality?overdue=true"
          },
          {
            label: t("qualityTiles.effectivenessDue"),
            value: qualityDash ? qualityDash.effectivenessChecksDueTotalCount : "—",
            tone:
              qualityDash && qualityDash.effectivenessChecksDueTotalCount > 0
                ? ("action" as const)
                : undefined,
            icon: <VerifiedOutlinedIcon />,
            hint: t("qualityTiles.effectivenessDueHint"),
            to: "/quality?type=Capa"
          }
        ]
      : [])
  ];

  return (
    <LabPage
      title={t("qa.title")}
      subtitle={t("qa.subtitle")}
      actions={
        <Button
          variant="outlined"
          onClick={reload}
          disabled={loading}
          startIcon={<RefreshIcon />}
        >
          {t("common.refresh")}
        </Button>
      }
      kpis={
        <>
          <PartialOutageBanner sources={failedSources.map((k) => t(k))} onRetry={reload} />
          <KpiStrip tiles={tiles} loading={loading} />
        </>
      }
    >
      {null}
    </LabPage>
  );
}
