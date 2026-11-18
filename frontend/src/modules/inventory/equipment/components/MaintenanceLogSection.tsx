import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Button,
  Chip,
  IconButton,
  Tooltip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  CircularProgress,
  Alert,
  TextField,
  Stack
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import BlockIcon from "@mui/icons-material/Block";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { AddLogEntryDialog } from "./AddLogEntryDialog";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../../utils/formatDate";
import type {
  MaintenanceEntryType,
  MaintenanceLogEntryResponse
} from "../types/equipmentTypes";
import { useTranslation } from "../../../../i18n";

interface Props {
  equipmentId: number;
  equipmentCode?: string;
  onLogChanged?: () => void;
}

const TYPE_COLORS: Record<MaintenanceEntryType, "primary" | "warning" | "default"> = {
  PreventiveMaintenance: "primary",
  BreakdownRepair: "warning",
  Other: "default"
};

export function MaintenanceLogSection({ equipmentId, equipmentCode = "", onLogChanged }: Props) {
  const { t, i18n } = useTranslation("inventory");
  const [entries, setEntries] = useState<MaintenanceLogEntryResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isAddOpen, setIsAddOpen] = useState(false);

  // Voiding state
  const [voidTarget, setVoidTarget] = useState<MaintenanceLogEntryResponse | null>(null);
  const [voidReason, setVoidReason] = useState("");
  const [voidSigning, setVoidSigning] = useState(false);
  const [voidError, setVoidError] = useState<string | null>(null);

  const loadEntries = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await EquipmentPlanService.getMaintenanceLog(equipmentId);
      setEntries(data);
    } catch {
      setError(t("equipment.maintenance.loadError"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEntries();
  }, [equipmentId]);

  const handleVoidSign = async (password: string) => {
    if (!voidTarget) return;
    await EquipmentPlanService.voidLogEntry(voidTarget.id, {
      reason: voidReason.trim(),
      password
    });
    setVoidSigning(false);
    setVoidTarget(null);
    setVoidReason("");
    await loadEntries();
    onLogChanged?.();
  };

  const handleVoidCancel = () => {
    setVoidTarget(null);
    setVoidReason("");
    setVoidError(null);
    setVoidSigning(false);
  };

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="h6" sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
            {t("equipment.maintenance.title")}
          </Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mt: 0.25 }}>
            {t("equipment.maintenance.subtitle")}
          </Typography>
        </Box>
        <Button
          id="add-log-entry-btn"
          variant="contained"
          size="small"
          startIcon={<AddIcon fontSize="small" />}
          onClick={() => setIsAddOpen(true)}
          sx={{ textTransform: "none", fontSize: 12 }}
        >
          {t("equipment.maintenance.addButton")}
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={24} />
        </Box>
      ) : entries.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{
            p: 3,
            textAlign: "center",
            bgcolor: "background.default",
            color: "text.secondary"
          }}
        >
          <Typography sx={{ fontSize: 13, fontStyle: "italic" }}>
            {t("equipment.maintenance.empty")}
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: "background.default" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.date")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.type")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.workDone")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.partsReplaced")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.findings")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.performedBy")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("equipment.maintenance.columns.status")}</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("equipment.maintenance.columns.actions")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {entries.map((entry) => {
                const isVoided = Boolean(entry.voidedAt);
                return (
                  <TableRow
                    key={entry.id}
                    sx={{
                      opacity: isVoided ? 0.6 : 1,
                      bgcolor: isVoided ? "action.hover" : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                      {formatLabCalendarDate(entry.performedOn)}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      <Chip
                        label={t(`equipment.maintenance.entryTypes.${entry.entryType}`)}
                        color={TYPE_COLORS[entry.entryType] ?? "default"}
                        size="small"
                        sx={{ fontSize: 11, height: 20 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, maxWidth: 220 }}>
                      <Typography sx={{ fontSize: 12, fontWeight: 500 }}>
                        {entry.workDone}
                      </Typography>
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.partsReplaced || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.findings || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.performedByUserName || t("equipment.maintenance.userFallback", { id: entry.performedByUserId })}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {isVoided ? (
                        <Tooltip
                          title={t("equipment.maintenance.voidedTooltip", {
                            userId: entry.voidedByUserId,
                            reason: entry.voidReason,
                            at: entry.voidedAt ? formatDateTimeWithTimeZone(entry.voidedAt) : ""
                          })}
                        >
                          <Chip
                            label={t("equipment.maintenance.voided")}
                            color="error"
                            size="small"
                            sx={{ fontSize: 11, height: 20 }}
                          />
                        </Tooltip>
                      ) : (
                        <Chip
                          label={t("equipment.maintenance.recorded")}
                          color="success"
                          variant="outlined"
                          size="small"
                          sx={{ fontSize: 11, height: 20 }}
                        />
                      )}
                    </TableCell>
                    <TableCell sx={{ textAlign: "right", whiteSpace: "nowrap" }}>
                      {!isVoided && (
                        <Tooltip title={t("equipment.maintenance.voidActionTooltip")}>
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => {
                              setVoidTarget(entry);
                              setVoidReason("");
                              setVoidSigning(false);
                              setVoidError(null);
                            }}
                          >
                            <BlockIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Add Log Entry Dialog */}
      <AddLogEntryDialog
        open={isAddOpen}
        equipmentId={equipmentId}
        equipmentCode={equipmentCode}
        onClose={() => setIsAddOpen(false)}
        onSuccess={() => {
          setIsAddOpen(false);
          loadEntries();
          onLogChanged?.();
        }}
      />

      {/* Void Entry Dialog */}
      <FloatingDialog
        open={Boolean(voidTarget) && !voidSigning}
        title={t("equipment.maintenance.voidDialogTitle")}
        onClose={handleVoidCancel}
        maxWidth="xs"
        actions={
          <>
            <Button
              onClick={handleVoidCancel}
              color="inherit"
            >
              {t("common.cancel")}
            </Button>
            <Button
              variant="contained"
              color="error"
              onClick={() => {
                if (!voidReason.trim()) {
                  setVoidError(t("equipment.maintenance.voidReasonRequired"));
                  return;
                }
                setVoidSigning(true);
              }}
              disabled={!voidReason.trim()}
            >
              {t("equipment.maintenance.continueToSign")}
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          {t("equipment.maintenance.voidDialogBody")}
        </Typography>

        {voidError && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setVoidError(null)}>
            {voidError}
          </Alert>
        )}

        {voidTarget && (
          <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.default", borderRadius: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
              {formatLabCalendarDate(voidTarget.performedOn)} · {t(`equipment.maintenance.entryTypes.${voidTarget.entryType}`)}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
              {voidTarget.workDone}
            </Typography>
          </Box>
        )}

        <Stack spacing={2}>
          <TextField
            id="void-log-reason"
            label={t("equipment.maintenance.voidReasonLabel")}
            multiline
            rows={2}
            size="small"
            required
            fullWidth
            value={voidReason}
            onChange={(e) => setVoidReason(e.target.value)}
            placeholder={t("equipment.maintenance.voidReasonPlaceholder")}
          />
        </Stack>
      </FloatingDialog>

      {voidTarget && (
        <SignatureDialog
          open={voidSigning}
          meaningStatement={i18n.getFixedT("en", "inventory")("equipment.maintenance.voidMeaningStatement", {
            id: voidTarget.id,
            code: equipmentCode,
            reason: voidReason.trim()
          })}
          meaningStatementTranslation={t("equipment.maintenance.voidMeaningStatement", {
            id: voidTarget.id,
            code: equipmentCode,
            reason: voidReason.trim()
          })}
          onCancel={() => setVoidSigning(false)}
          onConfirm={handleVoidSign}
        />
      )}
    </Box>
  );
}
