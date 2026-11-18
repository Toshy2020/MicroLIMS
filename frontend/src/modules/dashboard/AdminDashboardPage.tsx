import { useTranslation } from "../../i18n";
import { useEffect, useState } from "react";
import { Box, Button, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import RefreshIcon from "@mui/icons-material/Refresh";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import PendingActionsOutlinedIcon from "@mui/icons-material/PendingActionsOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import TaskAltOutlinedIcon from "@mui/icons-material/TaskAltOutlined";
import TuneOutlinedIcon from "@mui/icons-material/TuneOutlined";
import ScheduleOutlinedIcon from "@mui/icons-material/ScheduleOutlined";
import { SummaryTiles, type SummaryTile } from "../../components/configHierarchy/SummaryTiles";
import { PageCard } from "../../components/PageCard";
import { useMenuGroups } from "../../hooks/useMenuGroups";
import type { MenuItem } from "../../routes/menuConfig";
import { DashboardStateGate } from "./components/DashboardStateGate";
import { DashboardService } from "./services/DashboardService";
import { DashboardSummary, KpiDeltas } from "./types/dashboard";
import { useDashboardLab } from "./DashboardLabContext";

// Kept as a key so the translated text is looked up at render, not stored in the effect.
const SERVICE_ERROR_KEY = "admin.serviceError";

export function AdminDashboardPage() {
  const { t } = useTranslation("dashboard");
  const lab = useDashboardLab();

  const menuItems = useMenuGroups().flatMap((g) => g.items);
  const configPath = lab.isPhyschem ? "/sections/physicochemical-configuration" : "/sections/microbiology-configuration";
  const tools = ["/admin/users", "/admin/roles", "/admin/audit", "/reports", configPath]
    .map((path) => menuItems.find((item) => item.path === path))
    .filter((item): item is MenuItem => !!item);

  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [kpis, setKpis] = useState<KpiDeltas | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const reload = () => setReloadKey((k) => k + 1);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    // The summary IS this page, so its failure surfaces as an error with a
    // retry. The KPI deltas are supplementary - a failure there degrades that
    // one panel rather than taking the whole dashboard down.
    Promise.all([DashboardService.getSummary(lab.code), DashboardService.getKpiDeltas(lab.code).catch(() => null)])
      .then(([sumData, kpiData]) => {
        if (cancelled) return;
        setSummary(sumData);
        setKpis(kpiData);
      })
      .catch(() => {
        if (!cancelled) setError(SERVICE_ERROR_KEY);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [reloadKey, lab.code]);

  if (!summary) {
    return (
      <DashboardStateGate loading={loading} error={error === SERVICE_ERROR_KEY ? t(SERVICE_ERROR_KEY) : error} hasData={false} onRetry={reload}>
        {null}
      </DashboardStateGate>
    );
  }

  const tiles: SummaryTile[] = [
    { label: t("admin.tiles.totalSamples"), value: kpis?.totalSamples ?? "—", icon: <Inventory2OutlinedIcon /> },
    { label: t("admin.tiles.totalTests"), value: kpis?.totalTests ?? "—", icon: <ScienceOutlinedIcon /> },
    { label: t("admin.tiles.pendingTests"), value: summary.pendingTests, icon: <PendingActionsOutlinedIcon />, tone: "info", to: lab.workspace("?status=Active") },
    { label: t("admin.tiles.reviewQueue"), value: summary.reviewerQueue, caption: t("common.samples"), icon: <RateReviewOutlinedIcon />, tone: "action", to: lab.workspace("?status=UnderReview") },
    { label: t("admin.tiles.approvalQueue"), value: summary.approvalQueue, caption: t("common.samples"), icon: <TaskAltOutlinedIcon />, tone: "notDetected", to: lab.workspace("?status=UnderApproval") },
    ...(summary.pendingPreparationConfigApproval > 0
      ? [{ label: t("admin.tiles.prepConfigs"), value: summary.pendingPreparationConfigApproval, icon: <TuneOutlinedIcon />, tone: "inconclusive" as const, to: "/laboratory-configuration/items" }]
      : []),
    { label: t("admin.tiles.overdue"), value: summary.delayedTests, icon: <ScheduleOutlinedIcon />, tone: "detected", to: lab.workspace("?urgency=overdue") }
  ];

  return (
    <>
      <PageHeader
        title={t("admin.title")}
        subtitle={lab.code
          ? t("admin.subtitleLab", { lab: t(`labName.${lab.code}`) })
          : t("admin.subtitle")}
      >
        <Button
          variant="outlined"
          onClick={reload}
          disabled={loading}
          startIcon={<RefreshIcon />}
        >
          {t("common.refresh")}
        </Button>
      </PageHeader>

      {/* The numbers lead: each queue opens the workspace filtered to it. */}
      <SummaryTiles tiles={tiles} />

      {/* Administration: the admin's own tools and this lab's configuration,
          read from the permission-filtered menu so no card leads to a 403. */}
      {tools.length > 0 && (
        <>
          <Typography component="h2" sx={{ fontSize: 16, fontWeight: 700, mb: 1.5 }}>
            {t("admin.administration")}
          </Typography>
          <Box
            component="ul"
            sx={{ listStyle: "none", m: 0, p: 0, display: "grid", gap: 2, gridTemplateColumns: "repeat(auto-fill, minmax(min(100%, 250px), 1fr))" }}
          >
            {tools.map((tool) => (
              <Box component="li" key={tool.path} sx={{ display: "flex" }}>
                <PageCard page={tool} />
              </Box>
            ))}
          </Box>
        </>
      )}
    </>
  );
}
