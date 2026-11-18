import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  Box,
  Tabs,
  Tab,
  Button,
  Tooltip,
  CircularProgress,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { Ltr } from "../../components/Ltr";
import { LabPage, KpiStrip } from "../../components/lab";
import { useAuth } from "../../contexts/AuthContext";
import { useTranslation } from "../../i18n";
import { PERMISSIONS } from "../../routes/routes";
import { serverMessage } from "../../utils/errorMessage";
import { WorkingStandardService } from "./services/WorkingStandardService";
import { WorkingStandardLotsTable } from "./lots/WorkingStandardLotsTable";
import { QualificationsTable } from "./qualifications/QualificationsTable";
import { NewQualificationDialog } from "./qualifications/NewQualificationDialog";
import { QualificationDetailDrawer } from "./qualifications/QualificationDetailDrawer";
import type {
  WorkingStandardLotDto,
  WorkingStandardQualificationDto
} from "./types";

export function WorkingStandardsPage() {
  const { t } = useTranslation("workingStandards");
  const { permissions } = useAuth();
  const canQualify = permissions.includes(PERMISSIONS.WORKING_STANDARDS_QUALIFY);

  const [activeTab, setActiveTab] = useState<0 | 1>(0);
  const [lots, setLots] = useState<WorkingStandardLotDto[]>([]);
  const [qualifications, setQualifications] = useState<WorkingStandardQualificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [targetLot, setTargetLot] = useState<WorkingStandardLotDto | null>(null);
  const [selectedQualificationId, setSelectedQualificationId] = useState<number | null>(null);

  const loadData = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    setError(null);
    try {
      const [lotsData, qualsData] = await Promise.all([
        WorkingStandardService.getLots(),
        WorkingStandardService.getQualifications()
      ]);
      setLots(lotsData);
      setQualifications(qualsData);
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? t("page.loadError");
      setError(msg);
      toast.error(msg);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleOpenNewInitial = () => {
    setTargetLot(null);
    setDialogOpen(true);
  };

  const handleRequalify = (lot: WorkingStandardLotDto) => {
    setTargetLot(lot);
    setDialogOpen(true);
  };

  const handleQualificationCreated = (created: WorkingStandardQualificationDto) => {
    setDialogOpen(false);
    setTargetLot(null);
    loadData();
    setSelectedQualificationId(created.id);
  };

  const handleQualificationUpdated = () => {
    loadData(true);
  };

  const kpis = useMemo(() => {
    let validCount = 0;
    let dueSoonCount = 0;
    let expiredOrDepletedCount = 0;

    lots.forEach((lot) => {
      const st = String(lot.status).toLowerCase();
      if (st === "valid") validCount++;
      else if (st === "duesoon") dueSoonCount++;
      else if (st === "expired" || st === "depleted") expiredOrDepletedCount++;
    });

    const pendingCount = qualifications.filter(
      (q) => q.status === "Draft" || q.status === "Assayed" || q.status === "Reviewed"
    ).length;

    return [
      { label: t("kpis.activeLots"), value: lots.length },
      { label: t("kpis.valid"), value: validCount, tone: "notDetected" as const },
      { label: t("kpis.dueSoon"), value: dueSoonCount, tone: dueSoonCount > 0 ? ("inconclusive" as const) : undefined },
      { label: t("kpis.expiredDepleted"), value: expiredOrDepletedCount, tone: expiredOrDepletedCount > 0 ? ("detected" as const) : undefined },
      { label: t("kpis.pendingQuals"), value: pendingCount, tone: pendingCount > 0 ? ("info" as const) : undefined }
    ];
  }, [lots, qualifications, t]);

  return (
    <LabPage
      title={t("page.title")}
      subtitle={t("page.subtitle")}
      actions={
        <Box sx={{ display: "flex", gap: 1 }}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<RefreshIcon fontSize="small" />}
            onClick={() => loadData()}
            disabled={loading}
          >
            {t("actions.refresh")}
          </Button>

          <Tooltip
            title={
              canQualify
                ? t("actions.newQualificationTooltip")
                : t("actions.permissionRequired", { permission: "WorkingStandards.Qualify" })
            }
          >
            <Box component="span">
              <Button
                variant="contained"
                size="small"
                startIcon={<AddIcon fontSize="small" />}
                onClick={handleOpenNewInitial}
                disabled={!canQualify}
              >
                {t("actions.newQualification")}
              </Button>
            </Box>
          </Tooltip>
        </Box>
      }
      kpis={<KpiStrip loading={loading} tiles={kpis} />}
    >
      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
          <Tab label={<Trans i18nKey="tabs.lots" ns="workingStandards" values={{ count: lots.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />} />
          <Tab label={<Trans i18nKey="tabs.qualifications" ns="workingStandards" values={{ count: qualifications.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />} />
        </Tabs>
      </Box>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
          <CircularProgress size={36} />
        </Box>
      ) : activeTab === 0 ? (
        <WorkingStandardLotsTable
          lots={lots}
          canQualify={canQualify}
          onRequalify={handleRequalify}
        />
      ) : (
        <QualificationsTable
          qualifications={qualifications}
          onSelectQualification={(q) => setSelectedQualificationId(q.id)}
        />
      )}

      <NewQualificationDialog
        open={dialogOpen}
        targetLot={targetLot}
        onClose={() => {
          setDialogOpen(false);
          setTargetLot(null);
        }}
        onSuccess={handleQualificationCreated}
      />

      <QualificationDetailDrawer
        open={Boolean(selectedQualificationId)}
        qualificationId={selectedQualificationId}
        onClose={() => setSelectedQualificationId(null)}
        onUpdated={handleQualificationUpdated}
      />
    </LabPage>
  );
}
