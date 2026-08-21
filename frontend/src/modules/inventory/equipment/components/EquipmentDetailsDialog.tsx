import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Divider,
  Alert,
  IconButton,
  CircularProgress,
  Stack,
  Tooltip,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import HistoryIcon from "@mui/icons-material/History";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatLabDate, formatLabDateTime } from "../../../../utils/formatDate";
import { brandColors } from "../../../../theme";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type {
  EquipmentItem,
  EquipmentStatusHistoryItem
} from "../types/equipmentTypes";
import {
  EQUIPMENT_STATUS_LABELS
} from "../types/equipmentTypes";
import {
  isEquipmentCalibrationDueSoon,
  isEquipmentCalibrationOverdue
} from "./EquipmentKpiCards";
import { EquipmentDocumentList } from "./EquipmentDocumentList";
import { UploadEquipmentDocumentDialog } from "./UploadEquipmentDocumentDialog";

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
  const [statusHistory, setStatusHistory] = useState<EquipmentStatusHistoryItem[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [docRefreshKey, setDocRefreshKey] = useState(0);
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

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
      loadStatusHistory(equipment.id);
      setMessage(null);
    }
  }, [equipment, open]);

  if (!equipment) return null;

  const isOverdue = isEquipmentCalibrationOverdue(equipment);
  const isDueSoon = isEquipmentCalibrationDueSoon(equipment, 30);

  return (
    <>
      <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
        <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pb: 1.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
              {equipment.code}
            </Typography>
            <Typography sx={{ fontSize: 14, color: "text.secondary" }}>
              · {equipment.instrumentType}
            </Typography>
            <StatusBadge status={equipment.status} />
          </Box>
          <IconButton size="small" onClick={onClose}>
            <CloseIcon fontSize="small" />
          </IconButton>
        </DialogTitle>

        <DialogContent dividers sx={{ p: 3 }}>
          {message && (
            <Alert
              severity={message.ok ? "success" : "error"}
              onClose={() => setMessage(null)}
              sx={{ mb: 2 }}
            >
              {message.text}
            </Alert>
          )}

          {/* SECTION 1 — Equipment Specifications & Status */}
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
              Instrument Information
            </Typography>
            <Tooltip title="Edit Equipment Specifications & Status">
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
                Edit / Change Status
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
              <MetaRow label="Instrument Type" value={equipment.instrumentType} />
              <MetaRow label="Manufacturer" value={equipment.manufacturerName || "—"} />
              <MetaRow label="Serial Number" value={equipment.serialNumber || "—"} />
              <MetaRow label="Firmware Version" value={equipment.firmwareVersion || "—"} />
            </Box>
            <Box>
              <MetaRow label="Equipment Code" value={equipment.code} />
              <MetaRow label="Location" value={equipment.location} />
              <MetaRow
                label="Calibration Due"
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
                      {equipment.calibrationDueDate ? formatLabDate(equipment.calibrationDueDate) : "—"}
                    </span>
                    {isOverdue && <StatusBadge status="Overdue" />}
                    {isDueSoon && <StatusBadge status="Due Soon" />}
                  </Box>
                }
              />
              <MetaRow
                label="Operational Status"
                value={<StatusBadge status={equipment.status} />}
              />
            </Box>
          </Box>

          <Divider sx={{ my: 2.5 }} />

          {/* SECTION 2 — Status History */}
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <HistoryIcon sx={{ fontSize: 18, color: "text.secondary" }} />
              <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                Operational Status History
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
                No operational status changes have been recorded for this equipment.
              </Typography>
            ) : (
              <Stack divider={<Divider sx={{ my: 1 }} />} spacing={1}>
                {statusHistory.map((h) => (
                  <Box key={h.id} sx={{ fontSize: 12 }}>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 0.5 }}>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <StatusBadge status={h.previousStatus} />
                        <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>
                          →
                        </Typography>
                        <StatusBadge status={h.newStatus} />
                      </Box>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        {formatLabDateTime(h.changedAt)}
                      </Typography>
                    </Box>
                    <Typography sx={{ fontSize: 12, fontWeight: 500, color: "text.primary", mt: 0.5 }}>
                      {h.comment}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
                      Changed by: <strong>{h.changedByName}</strong>
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
                Calibration Certificates
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
              Upload Certificate
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
        </DialogContent>

        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={onClose} color="inherit">
            Close
          </Button>
        </DialogActions>
      </Dialog>

      {/* Upload Certificate Dialog */}
      <UploadEquipmentDocumentDialog
        open={isUploadOpen}
        equipmentId={equipment.id}
        onClose={() => setIsUploadOpen(false)}
        onSuccess={() => {
          setIsUploadOpen(false);
          setDocRefreshKey((k) => k + 1);
          setMessage({ text: "Calibration certificate uploaded successfully.", ok: true });
          onEquipmentUpdated();
        }}
      />
    </>
  );
}
