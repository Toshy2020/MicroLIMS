import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  Stack,
  TextField,
  Typography,
  useTheme
} from "@mui/material";
import { toast } from "sonner";
import { ResultRecordItem } from "../types/reportingTypes";
import { StatusBadge, CategoryBadge } from "../../../components/StatusBadge";
import { useState } from "react";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { withUnit } from "../../../utils/reportedValue";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { oosInvestigationService } from "../../../services/oosInvestigationService";
import { serverMessage } from "../../../utils/errorMessage";

interface RecordDetailDialogProps {
  open: boolean;
  onClose: () => void;
  record: ResultRecordItem | null;
}

export function RecordDetailDialog({ open, onClose, record }: RecordDetailDialogProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();
  const auth = useOptionalAuth();
  const canOpenExcursion =
    !auth ||
    auth.permissions.includes(PERMISSIONS.OOS_CONTRIBUTE) ||
    auth.permissions.includes(PERMISSIONS.OOS_MANAGE);
  const [auditOpen, setAuditOpen] = useState(false);
  const [excursionOpen, setExcursionOpen] = useState(false);
  const [excursionReason, setExcursionReason] = useState("");
  const [excursionError, setExcursionError] = useState<string | null>(null);
  const [openingExcursion, setOpeningExcursion] = useState(false);

  if (!record) return null;

  const handleOpenExcursion = async () => {
    const trimmed = excursionReason.trim();
    if (!trimmed) return;
    setOpeningExcursion(true);
    setExcursionError(null);
    try {
      const opened = await oosInvestigationService.openManualExcursion(record.testOrderId, trimmed);
      toast.success(opened.number);
      setExcursionOpen(false);
      setExcursionReason("");
    } catch (e) {
      const msg = serverMessage(e) ?? t("recordDetail.excursionError", { defaultValue: "Failed to open excursion review." });
      setExcursionError(msg);
      toast.error(msg);
    } finally {
      setOpeningExcursion(false);
    }
  };

  return (
    <>
      <FloatingDialog
        open={open}
        onClose={onClose}
        maxWidth="md"
        titleSx={{ pb: 1.5 }}
        title={
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flex: 1, minWidth: 0 }}>
            <Box>
              <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main }}>
                {t("recordDetail.title")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("recordDetail.reference")} <strong>{record.referenceNumber}</strong> {t("recordDetail.readOnlySource")}
              </Typography>
            </Box>
            <Box sx={{ display: "flex", gap: 1, mr: 1 }}>
              <CategoryBadge category={record.category} />
              <StatusBadge status={record.approvalStatus} />
            </Box>
          </Box>
        }
        actions={
          <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%", gap: 1, flexWrap: "wrap" }}>
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              <Button variant="outlined" size="small" onClick={() => setAuditOpen(true)}>
                {t("recordDetail.viewAuditTrail")}
              </Button>
              {canOpenExcursion && (
                <Button
                  variant="outlined"
                  color="warning"
                  size="small"
                  onClick={() => {
                    setExcursionReason("");
                    setExcursionError(null);
                    setExcursionOpen(true);
                  }}
                >
                  {t("recordDetail.openExcursionReview", { defaultValue: "Open excursion review" })}
                </Button>
              )}
            </Box>
            <Button variant="contained" size="small" onClick={onClose}>
              {t("recordDetail.close")}
            </Button>
          </Box>
        }
      >
          <Grid container spacing={2}>
            {/* Section 1: Sample & Identification */}
            <Grid size={12}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                {t("recordDetail.sectionSample")}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.sampleReference")}</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.referenceNumber}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.itemSubjectName")}</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.subjectName}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.itemDetailLocation")}</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.subjectDetail ?? "—"}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.batchNumber")}</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.batchNumber ?? "—"}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.controlNumber")}</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.controlNumber ?? "—"}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.category")}</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.category}</Typography>
            </Grid>

            <Grid size={12}>
              <Divider sx={{ my: 1 }} />
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                {t("recordDetail.sectionExecution")}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.testCodeName")}</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.testCode}: {record.testDisplayName}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.reportedResultValue")}</Typography>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
                {withUnit(record.reportedValue, record.unit)}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.resultLevel")}</Typography>
              <Box sx={{ mt: 0.5 }}>
                <StatusBadge status={record.resultLevel} />
              </Box>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.specificationLimit")}</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.specLimit ?? "—"}</Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.alertActionLimits")}</Typography>
              <Typography sx={{ fontSize: 13 }}>
                {t("recordDetail.alertActionValue", { alert: record.alertLimit ?? "—", action: record.actionLimit ?? "—" })}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 4
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.detectionLimitRound")}</Typography>
              <Typography sx={{ fontSize: 13 }}>
                {record.detectionLimit != null ? `${record.detectionLimit} ${record.unit ?? ""}` : "—"} {t("recordDetail.roundSuffix", { round: record.round })}
              </Typography>
            </Grid>

            <Grid size={12}>
              <Divider sx={{ my: 1 }} />
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                {t("recordDetail.sectionAttribution")}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 6
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.enteredByAnalyst")}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{record.resultEnteredByName}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {formatDateTimeWithTimeZone(record.resultEnteredAt)}
              </Typography>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 6
              }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("recordDetail.approvedBySectionHead")}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{record.approvedByName ?? t("recordDetail.pendingApproval")}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {record.approvedAt ? formatDateTimeWithTimeZone(record.approvedAt) : "—"}
              </Typography>
            </Grid>
          </Grid>
      </FloatingDialog>

      <AuditHistoryDialog
        open={auditOpen}
        onClose={() => setAuditOpen(false)}
        entityName="ResultRecord"
        entityId={record.id}
      />

      <Dialog
        open={excursionOpen}
        onClose={() => setExcursionOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle component="div">
          <Typography component="span" variant="h6">
            {t("recordDetail.openExcursionReview", { defaultValue: "Open excursion review" })}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {excursionError && (
              <Alert severity="error" role="alert">
                {excursionError}
              </Alert>
            )}
            <TextField
              fullWidth
              required
              multiline
              rows={3}
              size="small"
              label={t("recordDetail.excursionReason", { defaultValue: "Reason" })}
              value={excursionReason}
              onChange={(e) => setExcursionReason(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setExcursionOpen(false)}>
            {t("recordDetail.close")}
          </Button>
          <Button
            variant="contained"
            color="warning"
            disabled={openingExcursion || !excursionReason.trim()}
            onClick={() => void handleOpenExcursion()}
          >
            {t("recordDetail.confirmExcursionReview", { defaultValue: "Confirm Excursion Review" })}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
