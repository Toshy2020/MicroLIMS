import { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import {
  Box, Paper, Typography, Button, Alert, CircularProgress, Divider, Chip
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import EditIcon from "@mui/icons-material/Edit";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import DeleteOutlineOutlinedIcon from "@mui/icons-material/DeleteOutlineOutlined";
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { PageHeader } from "../../components/PageHeader";
import { useTranslation } from "../../i18n";
import { getErrorMessage } from "../../utils/errorMessage";
import { ReasonDialog } from "../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { PreparationStatusBadge } from "./components/PreparationStatusBadge";
import { PreparationComponentsTable } from "./record/PreparationComponentsTable";
import { PreparationHistoryTable } from "./record/PreparationHistoryTable";
import { PreparationWhereUsedPanel } from "../testPreparation/components/PreparationWhereUsedPanel";
import { CurrentFactorCard } from "./record/CurrentFactorCard";
import { StandardizationHistoryTable } from "./record/StandardizationHistoryTable";
import { useTitrantStandardization } from "./record/useTitrantStandardization";
import { TitrantStandardizationDialog } from "./components/TitrantStandardizationDialog";
import { ReleasePreparationDialog } from "./components/ReleasePreparationDialog";
import { releaseStateText } from "./releaseState";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { solutionTypeLabel } from "./types";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import type { SolutionPreparationResponse, RecipeSnapshot } from "./types";
import type { LabelTarget } from "../../services/labelService";
import { PrintLabelButton } from "../../components/labels/PrintLabelButton";
import { PrintLabelsDialog } from "../../components/labels/PrintLabelsDialog";
import { usePostCreateLabelPrint } from "../../components/labels/usePostCreateLabelPrint";
import { toast } from "sonner";
import { useTrackRecent } from "../navigation/useTrackRecent";
import { useBreadcrumb } from "../../components/breadcrumbContext";

export function PreparationRecordPage() {
  const { t } = useTranslation("solutionPreparation");
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { permissions } = useAuth();
  const canRelease = permissions.includes(PERMISSIONS.SOLUTIONS_RELEASE);
  const [releaseOpen, setReleaseOpen] = useState(false);
  const {
    dialogOpen: postCreateDialogOpen,
    targets: postCreateTargets,
    offerPrint,
    closeDialog: closePostCreateDialog
  } = usePostCreateLabelPrint();
  const consumedOfferRef = useRef<string | null>(null);

  const [preparation, setPreparation] = useState<SolutionPreparationResponse | null>(null);
  useTrackRecent("solution", preparation?.id);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  useBreadcrumb(preparation ? [preparation.code, preparation.solutionMasterName].filter(Boolean).join(" ") : error ? t("record.unavailable") : null);

  // Reason dialog state for Cancel / Discard
  const [actionType, setActionType] = useState<"cancel" | "discard" | null>(null);
  const [reason, setReason] = useState("");
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const loadPreparation = useCallback(async (prepId: number, silent = false) => {
    if (!silent) setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getById(prepId);
      setPreparation(data);
    } catch (err: unknown) {
      setError(getErrorMessage(err, t("record.loadError")));
    } finally {
      if (!silent) setLoading(false);
    }
  }, [t]);

  const refreshPreparation = useCallback(async (prepId: number) => {
    await loadPreparation(prepId, true);
  }, [loadPreparation]);

  const {
    standardizations,
    loadingStandardizations,
    canStandardize,
    dialogOpen,
    setDialogOpen,
    handleSuccess
  } = useTitrantStandardization(preparation, refreshPreparation);

  useEffect(() => {
    if (id) {
      loadPreparation(Number(id));
    }
  }, [id, loadPreparation]);

  useEffect(() => {
    const state = location.state as { offerPrintTarget?: LabelTarget } | null;
    const target = state?.offerPrintTarget;
    if (!target) return;
    const key = `${target.entityType}:${target.entityId}:${target.code}`;
    if (consumedOfferRef.current === key) return;
    consumedOfferRef.current = key;
    offerPrint([target]);
    navigate(`${location.pathname}${location.search}`, { replace: true, state: null });
  }, [location.state, location.pathname, location.search, navigate, offerPrint]);

  const recipe = useMemo<RecipeSnapshot | null>(() => {
    if (!preparation?.recipeSnapshotJson) return null;
    try {
      return JSON.parse(preparation.recipeSnapshotJson) as RecipeSnapshot;
    } catch {
      return null;
    }
  }, [preparation?.recipeSnapshotJson]);

  const handleOpenAction = (type: "cancel" | "discard") => {
    setActionType(type);
    setReason("");
    setActionError(null);
  };

  const handleConfirmAction = async () => {
    if (!preparation || !actionType) return;
    if (!reason.trim()) {
      setActionError(t("record.reasonRequired"));
      return;
    }

    setActionLoading(true);
    setActionError(null);
    try {
      if (actionType === "cancel") {
        await SolutionPreparationService.cancel(preparation.id, reason.trim());
        toast.success(t("record.cancelledToast"));
      } else {
        await SolutionPreparationService.discard(preparation.id, reason.trim());
        toast.success(t("record.discardedToast"));
      }
      setActionType(null);
      await loadPreparation(preparation.id);
    } catch (err: unknown) {
      setActionError(
        getErrorMessage(err, actionType === "cancel" ? t("record.cancelFailed") : t("record.discardFailed"))
      );
    } finally {
      setActionLoading(false);
    }
  };

  const postCreateDialog = (
    <PrintLabelsDialog
      open={postCreateDialogOpen}
      targets={postCreateTargets}
      onClose={closePostCreateDialog}
    />
  );

  if (loading) {
    return (
      <>
        <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center" }}>
          <CircularProgress />
        </Box>
        {postCreateDialog}
      </>
    );
  }

  if (error || !preparation) {
    return (
      <>
        <Box sx={{ p: 3 }}>
          <Alert severity="error" sx={{ mb: 2 }}>
            {error ?? t("record.notFound")}
          </Alert>
          <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/preparation")}>
            {t("record.backToList")}
          </Button>
        </Box>
        {postCreateDialog}
      </>
    );
  }

  const isInProgress = preparation.effectiveStatus === "InProgress";
  const isPrepared = preparation.effectiveStatus === "Prepared";
  const releaseText = releaseStateText(preparation, t);
  const awaitingRelease = preparation.approvalStatus === "PendingReview";
  const showRelease = canRelease && awaitingRelease;
  const hasAutoclave = Boolean(preparation.autoclaveEquipmentId) || recipe?.sterilizationRequired === true;
  const typeLabel = solutionTypeLabel(preparation.type, t);
  const subtitle = preparation.hplcMethodAbbreviation
    ? t("record.subtitleWithMethod", { type: typeLabel, recipe: preparation.solutionMasterName, method: preparation.hplcMethodAbbreviation })
    : t("record.subtitle", { type: typeLabel, recipe: preparation.solutionMasterName });

  return (
    <Box sx={{ p: 3, maxWidth: 1100, mx: "auto" }}>
      <PageHeader
        title={preparation.code || preparation.solutionMasterName}
        subtitle={subtitle}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
          <PreparationStatusBadge status={preparation.effectiveStatus} size="medium" />
          <PrintLabelButton
            target={{
              entityType: "SolutionPreparation",
              entityId: preparation.id,
              code: preparation.code ?? "",
              displayName: preparation.solutionMasterName || preparation.code || t("record.defaultPrintName")
            }}
          />
          <Button
            variant="outlined"
            size="small"
            startIcon={<ArrowBackIcon />}
            onClick={() => navigate("/preparation")}
          >
            {t("record.backToList")}
          </Button>
        </Box>
      </PageHeader>

      {/* Main Metadata Panel */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(4, 1fr)" }, gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("record.codeLabel")}</Typography>
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              {preparation.code ? <Ltr>{preparation.code}</Ltr> : t("record.noCodeYet")}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("record.startedByAt")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.startedByUserName || "—"}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              <Ltr>{formatDateTimeWithTimeZone(preparation.startedAt)}</Ltr>
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("record.preparedByAt")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.preparedByUserName || "—"}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {preparation.preparedAt ? <Ltr>{formatDateTimeWithTimeZone(preparation.preparedAt)}</Ltr> : t("record.notYetPrepared")}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("record.expiresAt")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 700, color: preparation.effectiveStatus === "Expired" ? "error.main" : "text.primary" }}>
              {preparation.expiresAt ? <Ltr>{formatDateTimeWithTimeZone(preparation.expiresAt)}</Ltr> : "—"}
            </Typography>
            {recipe && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                <Trans
                  i18nKey="record.shelfLife"
                  ns="solutionPreparation"
                  values={{ value: recipe.shelfLifeValue, unit: recipe.shelfLifeUnit }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            )}
          </Box>
        </Box>

        <Divider sx={{ mb: 2 }} />

        {/* Quantities & Parameters */}
        <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap", mb: 2 }}>
          <Chip
            label={
              <Trans
                i18nKey="record.finalVolumeChip"
                ns="solutionPreparation"
                values={{ value: preparation.finalVolumeMl ?? "—" }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            }
            color="primary"
            variant="outlined"
            size="small"
            sx={{ fontWeight: 600 }}
          />
          {preparation.measuredPh != null && (
            <Chip
              label={
                recipe?.phTarget != null ? (
                  <Trans
                    i18nKey="record.measuredPhTargetChip"
                    ns="solutionPreparation"
                    values={{ value: preparation.measuredPh, target: recipe.phTarget, tolerance: recipe.phTolerance ?? 0 }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                ) : (
                  <Trans
                    i18nKey="record.measuredPhChip"
                    ns="solutionPreparation"
                    values={{ value: preparation.measuredPh }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                )
              }
              size="small"
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
          {recipe?.storageCondition && (
            <Chip
              label={t("record.storageChip", { condition: recipe.storageCondition })}
              size="small"
              variant="outlined"
              sx={{ fontWeight: 500 }}
            />
          )}
        </Box>

        {releaseText && (
          <Alert
            severity={preparation.approvalStatus === "Approved" ? "success" : preparation.approvalStatus === "Rejected" ? "error" : "warning"}
            sx={{ mt: 1 }}
          >
            {releaseText}
          </Alert>
        )}

        {recipe?.instructions && (
          <Box sx={{ p: 1.5, bgcolor: "action.hover", borderRadius: 1, mt: 1 }}>
            <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
              {t("record.recipeInstructions")}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary", whiteSpace: "pre-wrap" }}>
              {recipe.instructions}
            </Typography>
          </Box>
        )}
      </Paper>

      {/* Current Factor Card (Titrant only) */}
      {preparation.type === "Titrant" && (
        <CurrentFactorCard
          currentFactor={preparation.currentFactor}
          canStandardize={canStandardize}
          onStandardize={() => setDialogOpen(true)}
        />
      )}

      {/* Autoclave record (recipes that need sterilization) */}
      {hasAutoclave && (
        <Paper variant="outlined" sx={{ p: 3, mb: 3 }} data-testid="autoclave-record">
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
            {t("record.autoclaveHeading")}
          </Typography>
          {preparation.autoclaveEquipmentId ? (
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)" }, gap: 2 }}>
              {[
                { label: t("record.autoclaveFields.autoclave"), value: preparation.autoclaveEquipmentName ?? `#${preparation.autoclaveEquipmentId}` },
                { label: t("record.autoclaveFields.program"), value: preparation.autoclaveProgram ?? "—" },
                { label: t("record.autoclaveFields.loadType"), value: preparation.loadType ?? "—" },
                {
                  label: t("record.autoclaveFields.temperature"),
                  value: preparation.autoclaveTemperature != null ? <Ltr>{t("units.celsius", { value: preparation.autoclaveTemperature })}</Ltr> : "—"
                },
                {
                  label: t("record.autoclaveFields.cycleTime"),
                  value: preparation.autoclaveCycleTime != null ? <Ltr>{t("units.minutes", { value: preparation.autoclaveCycleTime })}</Ltr> : "—"
                },
                {
                  label: t("record.autoclaveFields.cycleNumber"),
                  value: preparation.autoclaveCycleNumber != null ? <Ltr>{String(preparation.autoclaveCycleNumber)}</Ltr> : "—"
                }
              ].map((item) => (
                <Box key={item.label}>
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>{item.label}</Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{item.value}</Typography>
                </Box>
              ))}
            </Box>
          ) : (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("record.autoclaveNotEntered")}
            </Typography>
          )}
        </Paper>
      )}

      {/* Components Table */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
          {t("record.componentsHeading")}
        </Typography>
        <PreparationComponentsTable components={preparation.components} />
      </Paper>

      {/* Standardization History (Titrant only) */}
      {preparation.type === "Titrant" && (
        <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
            {t("record.standardizationHistoryHeading")}
          </Typography>
          <StandardizationHistoryTable
            standardizations={standardizations}
            loading={loadingStandardizations}
          />
        </Paper>
      )}

      {/* Where used: test preparations that picked this lot as diluent / neutralizer */}
      {["Diluent", "Reagent", "Neutralizer"].includes(preparation.type) && (
        <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
          <PreparationWhereUsedPanel lot={{ solutionPreparationId: preparation.id }} />
        </Paper>
      )}

      {/* Status History */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
          {t("record.statusHistoryHeading")}
        </Typography>
        <PreparationHistoryTable history={preparation.statusHistory} />
      </Paper>

      {/* Action Footer */}
      {(isInProgress || isPrepared || showRelease) && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 2 }}>
          {showRelease && (
            <Button
              variant="contained"
              onClick={() => setReleaseOpen(true)}
              sx={{ textTransform: "none", fontWeight: 700 }}
            >
              {t("record.releaseAction")}
            </Button>
          )}
          {isInProgress && (
            <>
              <Button
                variant="outlined"
                color="error"
                startIcon={<CancelOutlinedIcon />}
                onClick={() => handleOpenAction("cancel")}
              >
                {t("record.cancelAction")}
              </Button>
              <Button
                variant="contained"
                startIcon={<EditIcon />}
                onClick={() => navigate(`/preparation/${preparation.id}/edit`)}
                sx={{ textTransform: "none", fontWeight: 700 }}
              >
                {t("record.resumeAction")}
              </Button>
            </>
          )}

          {isPrepared && (
            <Button
              variant="outlined"
              color="error"
              startIcon={<DeleteOutlineOutlinedIcon />}
              onClick={() => handleOpenAction("discard")}
            >
              {t("record.discardAction")}
            </Button>
          )}
        </Box>
      )}

      <ReleasePreparationDialog
        open={releaseOpen}
        preparationId={preparation.id}
        code={preparation.code}
        solutionName={preparation.solutionMasterName}
        onClose={() => setReleaseOpen(false)}
        onSuccess={(message) => {
          toast.success(message);
          void refreshPreparation(preparation.id);
        }}
      />

      {/* Cancel / Discard Reason Dialog */}
      <ReasonDialog
        open={Boolean(actionType)}
        title={actionType === "cancel" ? t("record.cancelDialogTitle") : t("record.discardDialogTitle")}
        onClose={() => { if (!actionLoading) setActionType(null); }}
        onConfirm={handleConfirmAction}
        confirmText={actionType === "cancel" ? t("record.cancelDialogTitle") : t("record.discardDialogTitle")}
        confirmColor="error"
        loading={actionLoading}
        loadingText={t("record.submitting")}
        error={actionError}
        reason={reason}
        onReasonChange={setReason}
        label={t("record.reasonLabel")}
        placeholder={actionType === "cancel" ? t("record.cancelPlaceholder") : t("record.discardPlaceholder")}
      >
        <Alert severity="warning" sx={{ fontSize: 13 }}>
          {actionType === "cancel" ? t("record.cancelWarning") : t("record.discardWarning")}
        </Alert>
      </ReasonDialog>

      {/* Titrant Standardization Dialog */}
      {preparation.type === "Titrant" && (
        <TitrantStandardizationDialog
          open={dialogOpen}
          preparation={preparation}
          onClose={() => setDialogOpen(false)}
          onSuccess={handleSuccess}
        />
      )}

      {postCreateDialog}
    </Box>
  );
}
