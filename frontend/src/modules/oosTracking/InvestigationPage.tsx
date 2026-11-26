import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import { useTranslation } from "../../i18n";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { StatusBadge } from "../../components/StatusBadge";
import { PartialOutageBanner } from "../../components/lab/PartialOutageBanner";
import { useBreadcrumb } from "../../components/breadcrumbContext";
import { monospaceFontFamily } from "../../theme/palette";
import { tableHeadSx } from "../../theme";
import { serverMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import {
  oosInvestigationService,
  type OosCategoryDto,
  type OosChecklistFactsDto,
  type OosInvestigationDetailDto
} from "../../services/oosInvestigationService";
import { UserService, type UserRecord } from "../users/services/UserService";
import { PhaseOneSection } from "./components/PhaseOneSection";
import { RetestPlanSection } from "./components/RetestPlanSection";
import { PhaseTwoSection } from "./components/PhaseTwoSection";
import { ConclusionSection } from "./components/ConclusionSection";
import { DeviationRequestsPanel } from "./components/DeviationRequestsPanel";
import { QualityRecordsPanel } from "../quality/components/QualityRecordsPanel";
import { SignedTimeline } from "../../components/SignedTimeline";

export function InvestigationPage() {
  const { id } = useParams<{ id: string }>();
  const investigationId = Number(id);
  const { t } = useTranslation("oosTracking");
  const { permissions } = useAuth();

  const canContribute = permissions.includes(PERMISSIONS.OOS_CONTRIBUTE);
  const canManage = permissions.includes(PERMISSIONS.OOS_MANAGE);
  const canRequestDeviation = permissions.includes(PERMISSIONS.OOS_REQUEST_DEVIATION);
  const isReadOnly = !canContribute && !canManage && !canRequestDeviation;

  const [detail, setDetail] = useState<OosInvestigationDetailDto | null>(null);
  const [facts, setFacts] = useState<OosChecklistFactsDto | null>(null);
  const [categories, setCategories] = useState<OosCategoryDto[]>([]);
  const [analysts, setAnalysts] = useState<UserRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [factsLoading, setFactsLoading] = useState(true);
  const [factsFailed, setFactsFailed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportingPdf, setExportingPdf] = useState(false);

  useBreadcrumb(detail?.number ?? (error ? t("investigation.loadError") : null));

  const loadDetail = useCallback(
    async (signal?: AbortSignal) => {
      if (!Number.isFinite(investigationId)) return;
      setLoading(true);
      setError(null);
      try {
        const res = await oosInvestigationService.getById(investigationId, signal);
        if (!signal?.aborted) {
          setDetail(res);
        }
      } catch (e: unknown) {
        if (!signal?.aborted) {
          setError(serverMessage(e) ?? t("investigation.loadError"));
        }
      } finally {
        if (!signal?.aborted) setLoading(false);
      }
    },
    [investigationId, t]
  );

  const loadFacts = useCallback(
    async (signal?: AbortSignal) => {
      if (!Number.isFinite(investigationId)) return;
      setFactsLoading(true);
      setFactsFailed(false);
      try {
        const res = await oosInvestigationService.getFacts(investigationId, signal);
        if (!signal?.aborted) {
          setFacts(res);
        }
      } catch {
        if (!signal?.aborted) {
          setFacts(null);
          setFactsFailed(true);
        }
      } finally {
        if (!signal?.aborted) setFactsLoading(false);
      }
    },
    [investigationId]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadDetail(controller.signal);
    void loadFacts(controller.signal);

    if (canManage) {
      oosInvestigationService
        .listCategories(1, 100, controller.signal)
        .then((res) => {
          if (!controller.signal.aborted) setCategories(res.items);
        })
        .catch(() => {
          if (!controller.signal.aborted) setCategories([]);
        });

      UserService.getEligibleAnalysts()
        .then((res) => {
          if (!controller.signal.aborted) setAnalysts(res);
        })
        .catch(() => {
          if (!controller.signal.aborted) setAnalysts([]);
        });
    }

    return () => controller.abort();
  }, [loadDetail, loadFacts, canManage]);

  const reloadControllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    return () => {
      reloadControllerRef.current?.abort();
    };
  }, []);

  const reloadAll = useCallback(async () => {
    reloadControllerRef.current?.abort();
    const controller = new AbortController();
    reloadControllerRef.current = controller;
    await Promise.all([
      loadDetail(controller.signal),
      loadFacts(controller.signal)
    ]);
  }, [loadDetail, loadFacts]);

  const runMutation = async (fn: () => Promise<void>): Promise<string | null> => {
    try {
      await fn();
      await reloadAll();
      return null;
    } catch (e: unknown) {
      const msg = serverMessage(e) ?? t("investigation.loadError");
      toast.error(msg);
      return msg;
    }
  };

  const handleExportPdf = async () => {
    if (!detail) return;
    setExportingPdf(true);
    try {
      const blob = await oosInvestigationService.downloadPdf(detail.id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${detail.number}.pdf`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (e: unknown) {
      toast.error(serverMessage(e) ?? t("investigation.pdfError"));
    } finally {
      setExportingPdf(false);
    }
  };

  if (loading && !detail) {
    return (
      <Stack spacing={2}>
        <Skeleton variant="rounded" height={64} />
        <Skeleton variant="rounded" height={240} />
      </Stack>
    );
  }

  if (error || !detail) {
    return (
      <Stack spacing={2}>
        <Button
          component={Link}
          to="/oos-tracking"
          startIcon={<ArrowBackIcon />}
          sx={{ alignSelf: "flex-start" }}
        >
          {t("investigation.backToRegister")}
        </Button>
        <Alert severity="error">{error ?? t("investigation.loadError")}</Alert>
      </Stack>
    );
  }

  return (
    <Stack spacing={2.5}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: 1.5
        }}
      >
        <Button component={Link} to="/oos-tracking" startIcon={<ArrowBackIcon />}>
          {t("investigation.backToRegister")}
        </Button>
        <Button
          variant="outlined"
          startIcon={<PictureAsPdfOutlinedIcon />}
          disabled={exportingPdf}
          onClick={() => void handleExportPdf()}
        >
          {t("investigation.downloadPdf")}
        </Button>
      </Box>

      <PageHeader
        title={
          <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>
            {detail.number}
          </Box>
        }
        subtitle={`${detail.sampleDisplayName} (${detail.sectionName})`}
      />

      {isReadOnly && (
        <Alert severity="info">{t("investigation.readOnlyNotice")}</Alert>
      )}

      {factsFailed && (
        <PartialOutageBanner
          sources={[t("facts.sourceName")]}
          onRetry={() => void loadFacts()}
        />
      )}

      <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
        <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
          <StatusBadge status={detail.kind} />
          <StatusBadge status={detail.status} />
          {detail.conclusion && <StatusBadge status={detail.conclusion} />}
          {detail.isOverdue && (
            <Chip size="small" color="error" label={t("register.overdueChip")} />
          )}
        </Stack>

        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={3}
          sx={{ mb: 2, flexWrap: "wrap" }}
        >
          <Box>
            <Typography variant="caption" color="text.secondary">
              {t("investigation.header.sample")}
            </Typography>
            <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
              {detail.sampleReferenceNumber}
            </Typography>
          </Box>
          {detail.batchNumber && (
            <Box>
              <Typography variant="caption" color="text.secondary">
                {t("investigation.header.batch")}
              </Typography>
              <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily }}>
                {detail.batchNumber}
              </Typography>
            </Box>
          )}
          <Box>
            <Typography variant="caption" color="text.secondary">
              {t("investigation.header.section")}
            </Typography>
            <Typography variant="body2">{detail.sectionName}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">
              {t("investigation.header.openedBy")}
            </Typography>
            <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
              {detail.openedByName} · {formatDateTimeWithTimeZone(detail.openedAtUtc)}
            </Typography>
          </Box>
          {detail.phaseOneDueDate && (
            <Box>
              <Typography variant="caption" color="text.secondary">
                {t("investigation.header.phaseOneDue")}
              </Typography>
              <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                {detail.phaseOneDueDate}
              </Typography>
            </Box>
          )}
          {detail.dueDate && (
            <Box>
              <Typography variant="caption" color="text.secondary">
                {t("investigation.header.dueDate")}
              </Typography>
              <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                {detail.dueDate}
              </Typography>
            </Box>
          )}
        </Stack>

        <Box sx={{ mb: 1 }}>
          <SectionTitle>
            {t("investigation.header.resultsTitle")}
          </SectionTitle>
        </Box>
        <TableContainer sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx}>
              <TableRow>
                <TableCell>{t("investigation.header.testCode")}</TableCell>
                <TableCell>{t("investigation.header.outcome")}</TableCell>
                <TableCell>{t("investigation.header.summary")}</TableCell>
                <TableCell>{t("investigation.header.invalidated")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.results.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
                      {r.testCode}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {r.testDisplayName}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={r.outcomeAtOpening} />
                  </TableCell>
                  <TableCell>{r.resultSummaryAtOpening}</TableCell>
                  <TableCell>
                    {r.isInvalidated ? (
                      <StatusBadge status="Invalidated" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <PhaseOneSection
        detail={detail}
        facts={facts}
        factsLoading={factsLoading}
        categories={categories}
        canContribute={canContribute}
        canManage={canManage}
        onAddStatement={(text, version) =>
          runMutation(() =>
            oosInvestigationService.addAnalystStatement(detail.id, text, version)
          )
        }
        onAnswerChecklist={(itemKey, answer, comment, version) =>
          runMutation(() =>
            oosInvestigationService.answerChecklist(
              detail.id,
              1,
              itemKey,
              answer,
              comment,
              version
            )
          )
        }
        onSignPhaseOne={async (request, version) => {
          await oosInvestigationService.signPhaseOne(detail.id, request, version);
          await reloadAll();
        }}
      />

      {detail.kind === "Oos" &&
        (detail.status !== "PhaseOne" || detail.retestPlans.length > 0) && (
          <RetestPlanSection
            detail={detail}
            analysts={analysts}
            canManage={canManage}
            onSaveDraft={(request, version) =>
              runMutation(async () => {
                await oosInvestigationService.saveRetestPlan(detail.id, request, version);
              })
            }
            onSignPlan={async (planId, password, version) => {
              await oosInvestigationService.signRetestPlan(
                planId,
                password,
                version
              );
              await reloadAll();
            }}
          />
        )}

      {detail.kind === "Oos" &&
        (detail.status === "PhaseTwo" ||
          (detail.status === "Concluded" &&
            detail.entries.some((e) => e.phase === 2))) && (
          <PhaseTwoSection
            detail={detail}
            facts={facts}
            factsLoading={factsLoading}
            canContributeOrManage={canContribute || canManage}
            onAddEntry={(kind, text, parentEntryId, version) =>
              runMutation(() =>
                oosInvestigationService.addEntry(
                  detail.id,
                  kind,
                  text,
                  parentEntryId,
                  version
                )
              )
            }
            onAnswerChecklist={(itemKey, answer, comment, version) =>
              runMutation(() =>
                oosInvestigationService.answerChecklist(
                  detail.id,
                  2,
                  itemKey,
                  answer,
                  comment,
                  version
                )
              )
            }
          />
        )}

      {(detail.kind === "Excursion" || detail.status !== "PhaseOne") && (
        <ConclusionSection
          detail={detail}
          categories={categories}
          canManage={canManage}
          onConclude={async (request, version) => {
            await oosInvestigationService.conclude(detail.id, request, version);
            await reloadAll();
          }}
          onReopen={async (reason, password, version) => {
            await oosInvestigationService.reopen(
              detail.id,
              reason,
              password,
              version
            );
            await reloadAll();
          }}
          onCloseExcursion={async (request, version) => {
            await oosInvestigationService.closeExcursion(
              detail.id,
              request,
              version
            );
            await reloadAll();
          }}
        />
      )}

      <DeviationRequestsPanel
        requests={detail.deviationRequests}
        investigationVersion={detail.version}
        canRequest={canRequestDeviation}
        canAddress={canManage}
        onRequest={(reason, version) =>
          runMutation(async () => {
            await oosInvestigationService.requestDeviation(
              detail.id,
              reason,
              version
            );
          })
        }
        onAddress={(requestId, comment, version) =>
          runMutation(() =>
            oosInvestigationService.addressDeviationRequest(
              requestId,
              comment,
              version
            )
          )
        }
        onDeviationRaised={() => void reloadAll()}
      />

      <QualityRecordsPanel
        targetType="OosInvestigation"
        targetId={detail.id}
      />

      <SignedTimeline entries={detail.timeline} />
    </Stack>
  );
}
