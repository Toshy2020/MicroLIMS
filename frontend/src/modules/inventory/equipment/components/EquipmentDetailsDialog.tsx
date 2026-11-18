import { useEffect, useRef, useState } from "react";
import {
  Button,
  Box,
  Typography,
  Divider,
  Alert,
  CircularProgress,
  Stack,
  Tooltip,
  Tabs,
  Tab,
  Snackbar,
  useTheme
} from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import HistoryIcon from "@mui/icons-material/History";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../../utils/formatDate";
import { brandColors } from "../../../../theme";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type {
  EquipmentItem,
  EquipmentStatusHistoryItem
} from "../types/equipmentTypes";

import {
  isEquipmentCalibrationDueSoon,
  isEquipmentCalibrationOverdue
} from "./EquipmentKpiCards";
import { EquipmentDocumentList } from "./EquipmentDocumentList";
import { UploadEquipmentDocumentDialog } from "./UploadEquipmentDocumentDialog";
import { EquipmentScheduleSection } from "./EquipmentScheduleSection";
import { RecordCalibrationDialog } from "./RecordCalibrationDialog";
import { MaintenanceLogSection } from "./MaintenanceLogSection";
import { EquipmentHistoryPrint } from "./EquipmentHistoryPrint";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { PrintLabelButton } from "../../../../components/labels/PrintLabelButton";
import { Ltr } from "../../../../components/Ltr";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { useTrackRecent } from "../../../navigation/useTrackRecent";

interface Props {
  open: boolean;
  equipment: EquipmentItem | null;
  onClose: () => void;
  onEditEquipment: (equipment: EquipmentItem) => void;
  onEquipmentUpdated: () => void;
}

function MetaRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", py: 0.5 }}>
      <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", fontWeight: 500 }}>
        {label}
      </Typography>
      <Typography component="span" sx={{ fontSize: 12, fontWeight: 600, textAlign: "right" }}>
        {value ?? "—"}
      </Typography>
    </Box>
  );
}

export function EquipmentDetailsDialog({
  open,
  equipment,
  onClose,
  onEditEquipment,
  onEquipmentUpdated
}: Props) {
  const theme = useTheme();
  const { t } = useTranslation("inventory");
  useTrackRecent("equipment", open ? equipment?.id : null);
  const [activeTab, setActiveTab] = useState<number>(0);
  const [statusHistory, setStatusHistory] = useState<EquipmentStatusHistoryItem[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [docRefreshKey, setDocRefreshKey] = useState(0);
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [isRecordCalibrationOpen, setIsRecordCalibrationOpen] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" } | null>(null);

  const prevOpenRef = useRef(false);
  const prevIdRef = useRef<number | null>(null);

  const loadStatusHistory = async (id: number) => {
    try {
      setLoadingHistory(true);
      const history = await EquipmentInventoryService.getStatusHistory(id);
      setStatusHistory(history);
    } catch {
      setStatusHistory([]);
    } finally {
      setLoadingHistory(false);
    }
  };

  useEffect(() => {
    if (equipment && open) {
      const isNewOpen = !prevOpenRef.current;
      const isDifferentEquipment = prevIdRef.current !== equipment.id;
      if (isNewOpen || isDifferentEquipment) {
        setActiveTab(0);
        setMessage(null);
        setNotification(null);
      }
      loadStatusHistory(equipment.id);
      prevOpenRef.current = true;
      prevIdRef.current = equipment.id;
    } else if (!open) {
      prevOpenRef.current = false;
      prevIdRef.current = null;
    }
  }, [equipment, open]);

  if (!equipment) return null;

  const isOverdue = isEquipmentCalibrationOverdue(equipment);
  const isDueSoon = isEquipmentCalibrationDueSoon(equipment, 30);

  return (
    <>
      <FloatingDialog
        open={open}
        onClose={onClose}
        maxWidth="md"
        titleSx={{ pb: 1.5 }}
        title={
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
              <Ltr>{equipment.code}</Ltr>
            </Typography>
            <Typography sx={{ fontSize: 14, color: "text.secondary" }}>
              · {equipment.instrumentType}
            </Typography>
            <StatusBadge status={equipment.status} label={t(`equipment.status.${equipment.status}`)} />
          </Box>
        }
        actions={
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <PrintLabelButton
              target={{
                entityType: "EquipmentAsset",
                entityId: equipment.id,
                code: equipment.code,
                displayName: equipment.instrumentType
                  ? `${equipment.code} — ${equipment.instrumentType}`
                  : equipment.code
              }}
            />
            <Button onClick={onClose} color="inherit">
              {t("equipment.details.close")}
            </Button>
          </Stack>
        }
      >
        {message && (
          <Alert
            severity={message.ok ? "success" : "error"}
            onClose={() => setMessage(null)}
            sx={{ mb: 2 }}
          >
            {message.text}
          </Alert>
        )}

        <Tabs
          value={activeTab}
          onChange={(_, val) => setActiveTab(val)}
          sx={{ borderBottom: 1, borderColor: "divider", mb: 2.5 }}
        >
          <Tab label={t("equipment.details.tabs.details")} />
          <Tab label={t("equipment.details.tabs.schedule")} />
          <Tab label={t("equipment.details.tabs.logbook")} />
          <Tab label={t("equipment.details.tabs.history")} />
        </Tabs>

        <Box sx={{ minHeight: { xs: "auto", sm: 420 } }}>
          {activeTab === 0 && (
            <>
              {/* SECTION 1 — Equipment Specifications & Status */}
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                {t("equipment.details.instrumentInfo")}
              </Typography>
              <Tooltip title={t("equipment.details.editTooltip")}>
                <Button
                  id="equip-details-edit-btn"
                  size="small"
                  variant="outlined"
                  startIcon={<EditOutlinedIcon fontSize="small" />}
                  onClick={() => {
                    onClose();
                    onEditEquipment(equipment);
                  }}
                  sx={{ textTransform: "none", fontSize: 12 }}
                >
                  {t("equipment.details.editButton")}
                </Button>
              </Tooltip>
            </Box>

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" },
                gap: 2,
                bgcolor: "background.default",
                p: 2,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: "divider",
                mb: 3
              }}
            >
              <Box>
                <MetaRow label={t("equipment.details.instrumentType")} value={equipment.instrumentType} />
                <MetaRow label={t("equipment.details.manufacturer")} value={equipment.manufacturerName || "—"} />
                <MetaRow label={t("equipment.details.serialNumber")} value={equipment.serialNumber ? <Ltr>{equipment.serialNumber}</Ltr> : "—"} />
                <MetaRow label={t("equipment.details.firmwareVersion")} value={equipment.firmwareVersion ? <Ltr>{equipment.firmwareVersion}</Ltr> : "—"} />
              </Box>
              <Box>
                <MetaRow label={t("equipment.details.equipmentCode")} value={<Ltr>{equipment.code}</Ltr>} />
                <MetaRow label={t("equipment.details.location")} value={equipment.location} />
                <MetaRow
                  label={t("equipment.details.calibrationDue")}
                  value={
                    <Box component="span" sx={{ display: "inline-flex", alignItems: "center", gap: 0.75 }}>
                      <span
                        style={{
                          fontWeight: isOverdue || isDueSoon ? 600 : "normal",
                          color: isOverdue
                            ? theme.custom.status.detected.text
                            : isDueSoon
                            ? theme.custom.status.inconclusive.text
                            : "inherit"
                        }}
                      >
                        {equipment.calibrationDueDate ? formatLabCalendarDate(equipment.calibrationDueDate) : "—"}
                      </span>
                      {isOverdue && <StatusBadge status="Overdue" label={t("equipment.status.overdue")} />}
                      {isDueSoon && <StatusBadge status="Due Soon" label={t("equipment.status.dueSoon")} />}
                    </Box>
                  }
                />
                <MetaRow
                  label={t("equipment.details.operationalStatus")}
                  value={<StatusBadge status={equipment.status} label={t(`equipment.status.${equipment.status}`)} />}
                />
              </Box>
            </Box>

            <Divider sx={{ my: 2.5 }} />

            {/* SECTION 2 — Status History */}
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <HistoryIcon sx={{ fontSize: 18, color: "text.secondary" }} />
                <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                  {t("equipment.details.statusHistoryTitle")}
                </Typography>
              </Box>
            </Box>

            <Box sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider", borderRadius: 1.5, p: 2, mb: 3 }}>
              {loadingHistory ? (
                <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
                  <CircularProgress size={20} />
                </Box>
              ) : statusHistory.length === 0 ? (
                <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
                  {t("equipment.details.noStatusHistory")}
                </Typography>
              ) : (
                <Stack divider={<Divider sx={{ my: 1 }} />} spacing={1}>
                  {statusHistory.map((h) => (
                    <Box key={h.id} sx={{ fontSize: 12 }}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 0.5 }}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                          <StatusBadge status={h.previousStatus} label={t(`equipment.status.${h.previousStatus}`)} />
                          <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>
                            →
                          </Typography>
                          <StatusBadge status={h.newStatus} label={t(`equipment.status.${h.newStatus}`)} />
                        </Box>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                          {formatDateTimeWithTimeZone(h.changedAt)}
                        </Typography>
                      </Box>
                      <Typography sx={{ fontSize: 12, fontWeight: 500, color: "text.primary", mt: 0.5 }}>
                        {h.comment}
                      </Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
                        <Trans
                          i18nKey="equipment.details.changedBy"
                          ns="inventory"
                          values={{
                            name:
                              h.changedByName ||
                              (h.changedByUserId == null ? t("equipment.details.systemAutomatic") : t("equipment.details.unknownUser"))
                          }}
                          components={{ bold: <strong /> }}
                        />
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              )}
            </Box>

            <Divider sx={{ my: 2.5 }} />

            {/* SECTION 3 — Calibration Certificates */}
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <UploadFileIcon sx={{ fontSize: 18, color: "text.secondary" }} />
                <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                  {t("equipment.details.certificatesTitle")}
                </Typography>
              </Box>
              <Button
                id="equip-details-upload-doc-btn"
                size="small"
                variant="contained"
                startIcon={<UploadFileIcon fontSize="small" />}
                onClick={() => setIsUploadOpen(true)}
                sx={{
                  textTransform: "none",
                  fontSize: 12,
                  bgcolor: brandColors.sectionTitle,
                  "&:hover": { bgcolor: brandColors.pageTitle }
                }}
              >
                {t("equipment.details.uploadCertificate")}
              </Button>
            </Box>

            <Box sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider", borderRadius: 1.5, p: 2 }}>
              <EquipmentDocumentList
                equipmentId={equipment.id}
                refreshKey={docRefreshKey}
                onDocumentChanged={() => {
                  setDocRefreshKey((k) => k + 1);
                  onEquipmentUpdated();
                }}
              />
            </Box>
          </>
        )}

        {activeTab === 1 && (
          <EquipmentScheduleSection
            equipment={equipment}
            onSaved={() => {
              setNotification({ text: t("equipment.details.scheduleSaved"), severity: "success" });
              onEquipmentUpdated();
            }}
            onRecordCalibrationClick={() => setIsRecordCalibrationOpen(true)}
          />
        )}

        {activeTab === 2 && (
          <MaintenanceLogSection
            equipmentId={equipment.id}
            equipmentCode={equipment.code}
            onLogChanged={() => {
              onEquipmentUpdated();
            }}
          />
        )}

        {activeTab === 3 && (
          <EquipmentHistoryPrint equipmentId={equipment.id} />
        )}
        </Box>
      </FloatingDialog>

      <Snackbar
        open={Boolean(notification)}
        autoHideDuration={5000}
        onClose={() => setNotification(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        {notification ? (
          <Alert severity={notification.severity} onClose={() => setNotification(null)} sx={{ borderRadius: 1.5 }}>
            {notification.text}
          </Alert>
        ) : undefined}
      </Snackbar>

      {/* Upload Certificate Dialog */}
      <UploadEquipmentDocumentDialog
        open={isUploadOpen}
        equipmentId={equipment.id}
        onClose={() => setIsUploadOpen(false)}
        onSuccess={() => {
          setIsUploadOpen(false);
          setDocRefreshKey((k) => k + 1);
          setMessage({ text: t("equipment.details.certificateUploaded"), ok: true });
          onEquipmentUpdated();
        }}
      />

      {/* Record Calibration Dialog */}
      <RecordCalibrationDialog
        open={isRecordCalibrationOpen}
        equipmentId={equipment.id}
        equipmentCode={equipment.code}
        onClose={() => setIsRecordCalibrationOpen(false)}
        onSuccess={() => {
          setIsRecordCalibrationOpen(false);
          setDocRefreshKey((k) => k + 1);
          setMessage({ text: t("equipment.details.calibrationRecorded"), ok: true });
          onEquipmentUpdated();
        }}
      />
    </>
  );
}
