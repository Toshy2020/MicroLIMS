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
import { toast } from "sonner";
import { LabPage, KpiStrip } from "../../components/lab";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
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

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [lotsData, qualsData] = await Promise.all([
        WorkingStandardService.getLots(),
        WorkingStandardService.getQualifications()
      ]);
      setLots(lotsData);
      setQualifications(qualsData);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? "Failed to load working standards data";
      setError(msg);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, []);

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
    loadData();
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
      { label: "Active Lots", value: lots.length },
      { label: "Valid", value: validCount, tone: "notDetected" as const },
      { label: "Due Soon", value: dueSoonCount, tone: dueSoonCount > 0 ? ("inconclusive" as const) : undefined },
      { label: "Expired / Depleted", value: expiredOrDepletedCount, tone: expiredOrDepletedCount > 0 ? ("detected" as const) : undefined },
      { label: "Pending Quals", value: pendingCount, tone: pendingCount > 0 ? ("info" as const) : undefined }
    ];
  }, [lots, qualifications]);

  return (
    <LabPage
      title="Working Standards"
      subtitle="Working standard lots, qualifications, moisture verification and sign-off"
      actions={
        <Box sx={{ display: "flex", gap: 1 }}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<RefreshIcon fontSize="small" />}
            onClick={loadData}
            disabled={loading}
          >
            Refresh
          </Button>

          <Tooltip title={canQualify ? "Create initial qualification" : "Permission required: WorkingStandards.Qualify"}>
            <Box component="span">
              <Button
                variant="contained"
                size="small"
                startIcon={<AddIcon fontSize="small" />}
                onClick={handleOpenNewInitial}
                disabled={!canQualify}
              >
                New qualification
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
          <Tab label={`Lots (${lots.length})`} />
          <Tab label={`Qualifications (${qualifications.length})`} />
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
