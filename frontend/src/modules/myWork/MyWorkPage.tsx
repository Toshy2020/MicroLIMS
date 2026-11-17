import { useEffect, useMemo, useRef, useState } from "react";
import type { FocusEvent } from "react";
import { Alert, Box, Button, InputAdornment, Skeleton, Stack, TextField, Typography } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import SearchIcon from "@mui/icons-material/Search";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { Trans } from "react-i18next";
import { useHasPermission } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { ConfirmationDialog } from "../../components/ConfirmationDialog";
import { Ltr } from "../../components/Ltr";
import type { SummaryTile } from "../../components/configHierarchy/SummaryTiles";
import { EmptyState, KpiStrip, LabPage, PartialOutageBanner } from "../../components/lab";
import { useTranslation } from "../../i18n";
import { getErrorMessage } from "../../utils/errorMessage";
import { MyWorkGroup } from "./components/MyWorkGroup";
import { useMyWork } from "./hooks/useMyWork";
import type { MyWorkItem, MyWorkUrgency } from "./myWorkTypes";
import { myWorkService } from "./services/myWorkService";

const FILTERABLE_URGENCIES: ReadonlySet<string> = new Set<MyWorkUrgency>([
  "Overdue",
  "Now",
  "Today",
  "Later"
]);

const MY_WORK_EMPTY_REDIRECT_KEY = "microlims_mywork_empty_redirected";

function hasRedirectedToDashboard(): boolean {
  try {
    return sessionStorage.getItem(MY_WORK_EMPTY_REDIRECT_KEY) === "1";
  } catch {
    return false;
  }
}

function markRedirectedToDashboard(): void {
  try {
    sessionStorage.setItem(MY_WORK_EMPTY_REDIRECT_KEY, "1");
  } catch {
    // Storage may be unavailable in restricted contexts.
  }
}

export function MyWorkPage() {
  const { t } = useTranslation("myWork");
  const navigate = useNavigate();
  const hasDashboardPermission = useHasPermission([
    PERMISSIONS.DASHBOARDS_LAB_OVERVIEW,
    PERMISSIONS.DASHBOARDS_REVIEW
  ]);
  const [searchParams, setSearchParams] = useSearchParams();
  const urgencyParam = searchParams.get("urgency");
  const urgencyFilter: MyWorkUrgency | null =
    urgencyParam && FILTERABLE_URGENCIES.has(urgencyParam) ? (urgencyParam as MyWorkUrgency) : null;
  const searchQuery = searchParams.get("q") ?? "";

  const { data, loading, refreshing, error, reload } = useMyWork();

  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [releaseTarget, setReleaseTarget] = useState<MyWorkItem | null>(null);
  const [releasing, setReleasing] = useState(false);

  const listContainerRef = useRef<HTMLDivElement | null>(null);
  const focusedRowKeyRef = useRef<string | null>(null);

  const handleFocusCapture = (e: FocusEvent<HTMLDivElement>) => {
    const rowEl = (e.target as HTMLElement | null)?.closest<HTMLElement>("[data-row-key]");
    focusedRowKeyRef.current = rowEl?.dataset.rowKey ?? null;
  };

  useEffect(() => {
    const rowKey = focusedRowKeyRef.current;
    if (!rowKey || !listContainerRef.current) return;
    const active = document.activeElement;
    if (active && active !== document.body && listContainerRef.current.contains(active)) return;

    const escaped = typeof CSS !== "undefined" && typeof CSS.escape === "function"
      ? CSS.escape(rowKey)
      : rowKey.replace(/"/g, '\\"');
    const rowEl = listContainerRef.current.querySelector<HTMLElement>(`[data-row-key="${escaped}"]`);
    if (!rowEl) return;
    const focusTarget = rowEl.querySelector<HTMLElement>("a, button") ?? rowEl;
    focusTarget.focus();
  }, [data]);

  const setFilterParams = (nextUrgency: MyWorkUrgency | null, nextQuery: string) => {
    const next = new URLSearchParams(searchParams);
    if (nextUrgency) {
      next.set("urgency", nextUrgency);
    } else {
      next.delete("urgency");
    }
    if (nextQuery) {
      next.set("q", nextQuery);
    } else {
      next.delete("q");
    }
    setSearchParams(next, { replace: true });
  };

  const toggleUrgency = (u: MyWorkUrgency) => {
    setFilterParams(urgencyFilter === u ? null : u, searchQuery);
  };

  const clearFilters = () => {
    setFilterParams(null, "");
  };

  const counts = useMemo(() => {
    const groups = data?.groups ?? [];
    let total = 0;
    let overdue = 0;
    let now = 0;
    let today = 0;
    for (const g of groups) {
      total += g.count;
      for (const item of g.items) {
        if (item.urgency === "Overdue") overdue += 1;
        else if (item.urgency === "Now") now += 1;
        else if (item.urgency === "Today") today += 1;
      }
    }
    return { total, overdue, now, today };
  }, [data]);

  const filteredGroups = useMemo(() => {
    const groups = data?.groups ?? [];
    const q = searchQuery.trim().toLowerCase();
    return groups
      .map((g) => {
        const items = g.items.filter((item) => {
          if (urgencyFilter && item.urgency !== urgencyFilter) return false;
          if (q && !item.identifier.toLowerCase().includes(q) && !item.title.toLowerCase().includes(q)) {
            return false;
          }
          return true;
        });
        return { ...g, items };
      })
      .filter((g) => g.items.length > 0);
  }, [data, urgencyFilter, searchQuery]);

  const visibleItemCount = useMemo(
    () => filteredGroups.reduce((acc, g) => acc + g.items.length, 0),
    [filteredGroups]
  );

  const tiles: SummaryTile[] = [
    {
      label: t("allWork"),
      value: counts.total,
      onClick: () => setFilterParams(null, searchQuery),
      active: urgencyFilter === null
    },
    {
      label: t("urgency.Overdue"),
      value: counts.overdue,
      tone: counts.overdue > 0 ? "detected" : undefined,
      onClick: () => toggleUrgency("Overdue"),
      active: urgencyFilter === "Overdue"
    },
    {
      label: t("urgency.Now"),
      value: counts.now,
      tone: counts.now > 0 ? "purple" : undefined,
      onClick: () => toggleUrgency("Now"),
      active: urgencyFilter === "Now"
    },
    {
      label: t("urgency.Today"),
      value: counts.today,
      tone: counts.today > 0 ? "action" : undefined,
      onClick: () => toggleUrgency("Today"),
      active: urgencyFilter === "Today"
    }
  ];

  const handleTake = async (item: MyWorkItem) => {
    const testOrderId = item.target.testOrderId;
    if (testOrderId == null) return;
    setBusyKey(item.key);
    try {
      await myWorkService.take(testOrderId);
      toast.success(
        <Trans i18nKey="toast.taken" ns="myWork" values={{ identifier: item.identifier }} components={{ ltr: <Ltr>{null}</Ltr> }} />
      );
      reload();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err, t("toast.takeFailed")));
    } finally {
      setBusyKey(null);
    }
  };

  const handleReleaseClick = (item: MyWorkItem) => {
    setReleaseTarget(item);
  };

  const confirmRelease = async () => {
    const target = releaseTarget;
    const testOrderId = target?.target.testOrderId;
    if (!target || testOrderId == null) return;
    setReleasing(true);
    try {
      await myWorkService.release(testOrderId);
      toast.success(
        <Trans i18nKey="toast.released" ns="myWork" values={{ identifier: target.identifier }} components={{ ltr: <Ltr>{null}</Ltr> }} />
      );
      setReleaseTarget(null);
      reload();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err, t("toast.releaseFailed")));
    } finally {
      setReleasing(false);
    }
  };

  const hasNonEmptyServerGroups = (data?.groups ?? []).some((g) => g.items.length > 0);

  useEffect(() => {
    if (loading || error || data === null || hasNonEmptyServerGroups) return;
    if (!hasDashboardPermission || hasRedirectedToDashboard()) return;
    markRedirectedToDashboard();
    navigate("/dashboard", { replace: true });
  }, [loading, error, data, hasNonEmptyServerGroups, hasDashboardPermission, navigate]);

  return (
    <LabPage
      title={t("title")}
      subtitle={t("subtitle")}
      actions={
        <Button
          variant="outlined"
          onClick={reload}
          disabled={loading || refreshing}
          startIcon={<RefreshIcon />}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {t("refresh")}
        </Button>
      }
      kpis={
        <Box data-tour="my-work-summary">
          <PartialOutageBanner sources={data?.unavailableSources ?? []} onRetry={reload} />
          <KpiStrip tiles={tiles} loading={loading && !data} />
        </Box>
      }
      filters={
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, justifyContent: "space-between" }}>
          <TextField
            size="small"
            label={t("searchLabel")}
            value={searchQuery}
            onChange={(e) => setFilterParams(urgencyFilter, e.target.value)}
            sx={{ minWidth: { xs: "100%", sm: 280 } }}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" />
                  </InputAdornment>
                )
              }
            }}
          />
          <Typography
            variant="body2"
            role="status"
            aria-live="polite"
            sx={{ color: "text.secondary", fontVariantNumeric: "tabular-nums" }}
          >
            {loading && !data ? (
              t("loading")
            ) : (
              <Trans
                i18nKey="itemsShown"
                ns="myWork"
                count={visibleItemCount}
                values={{ count: visibleItemCount }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            )}
          </Typography>
        </Stack>
      }
    >
      <Box data-tour="my-work-queue">
        {loading && !data ? (
          <Stack spacing={2} data-testid="my-work-loading">
            <Skeleton variant="rounded" height={120} />
            <Skeleton variant="rounded" height={120} />
          </Stack>
        ) : error && !data ? (
          <Alert
            severity="error"
            action={
              <Button color="inherit" size="small" onClick={reload} sx={{ textTransform: "none", fontWeight: 600 }}>
                Retry
              </Button>
            }
          >
            {getErrorMessage(error, t("loadFailed"))}
          </Alert>
        ) : !hasNonEmptyServerGroups ? (
          <EmptyState
            title={t("empty.title")}
            description={t("empty.description")}
            action={
              <Button component={Link} to="/dashboard" variant="contained" sx={{ textTransform: "none", fontWeight: 600 }}>
                {t("empty.goDashboard")}
              </Button>
            }
          />
        ) : filteredGroups.length === 0 ? (
          <EmptyState
            title={t("noMatches.title")}
            description={t("noMatches.description")}
            action={
              <Button variant="outlined" onClick={clearFilters} sx={{ textTransform: "none", fontWeight: 600 }}>
                {t("noMatches.clearFilters")}
              </Button>
            }
          />
        ) : (
          <Box ref={listContainerRef} onFocusCapture={handleFocusCapture}>
            <Stack spacing={2}>
              {filteredGroups.map((group) => (
                <MyWorkGroup
                  key={group.key}
                  group={group}
                  timeZoneId={data?.timeZoneId ?? "UTC"}
                  onTake={handleTake}
                  onRelease={handleReleaseClick}
                  busyKey={busyKey}
                />
              ))}
            </Stack>
          </Box>
        )}
      </Box>

      <ConfirmationDialog
        open={releaseTarget !== null}
        destructive
        title={t("release.title")}
        message={
          releaseTarget ? (
            <Trans
              i18nKey="release.message"
              ns="myWork"
              values={{ identifier: releaseTarget.identifier, title: releaseTarget.title }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          ) : (
            ""
          )
        }
        confirmText={t("release.confirm")}
        cancelText={t("release.cancel")}
        loading={releasing}
        onConfirm={confirmRelease}
        onCancel={() => {
          if (!releasing) setReleaseTarget(null);
        }}
      />
    </LabPage>
  );
}
