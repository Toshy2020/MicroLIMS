import { useState, useEffect, useMemo, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Paper,
  Typography,
  Button,
  Alert,
  CircularProgress,
  Divider,
  Chip
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import EditIcon from "@mui/icons-material/Edit";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import DeleteOutlineOutlinedIcon from "@mui/icons-material/DeleteOutlineOutlined";
import { PageHeader } from "../../components/PageHeader";
import { ReasonDialog } from "../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { PreparationStatusBadge } from "./components/PreparationStatusBadge";
import { PreparationComponentsTable } from "./record/PreparationComponentsTable";
import { PreparationHistoryTable } from "./record/PreparationHistoryTable";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { formatLabDate, formatLabDateTime } from "../../utils/formatDate";
import { toast } from "sonner";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot
} from "./types";

export function PreparationRecordPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [preparation, setPreparation] = useState<SolutionPreparationResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Reason dialog state for Cancel / Discard
  const [actionType, setActionType] = useState<"cancel" | "discard" | null>(null);
  const [reason, setReason] = useState("");
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const loadPreparation = useCallback(async (prepId: number) => {
    setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getById(prepId);
      setPreparation(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load preparation record.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (id) {
      loadPreparation(Number(id));
    }
  }, [id, loadPreparation]);

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
      setActionError("Reason is required.");
      return;
    }

    setActionLoading(true);
    setActionError(null);
    try {
      if (actionType === "cancel") {
        await SolutionPreparationService.cancel(preparation.id, reason.trim());
        toast.success("Preparation cancelled.");
      } else {
        await SolutionPreparationService.discard(preparation.id, reason.trim());
        toast.success("Solution discarded.");
      }
      setActionType(null);
      await loadPreparation(preparation.id);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setActionError(e.response?.data?.message ?? e.message ?? `Failed to ${actionType} preparation.`);
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center" }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !preparation) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error ?? "Preparation not found."}
        </Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/preparation")}>
          Back to List
        </Button>
      </Box>
    );
  }

  const isInProgress = preparation.effectiveStatus === "InProgress";
  const isPrepared = preparation.effectiveStatus === "Prepared";

  return (
    <Box sx={{ p: 3, maxWidth: 1100, mx: "auto" }}>
      <PageHeader
        title={preparation.code || preparation.solutionMasterName}
        subtitle={`${preparation.type} · Recipe: ${preparation.solutionMasterName}${preparation.hplcMethodAbbreviation ? ` · Method: ${preparation.hplcMethodAbbreviation}` : ""}`}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
          <PreparationStatusBadge status={preparation.effectiveStatus} size="medium" />
          <Button
            variant="outlined"
            size="small"
            startIcon={<ArrowBackIcon />}
            onClick={() => navigate("/preparation")}
            sx={{ textTransform: "none" }}
          >
            Back to List
          </Button>
        </Box>
      </PageHeader>

      {/* Main Metadata Panel */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(4, 1fr)" }, gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Code</Typography>
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              {preparation.code || "— (In Progress)"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Started By / At</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.startedByUserName || "—"}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {formatLabDateTime(preparation.startedAt)}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Prepared By / At</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.preparedByUserName || "—"}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {preparation.preparedAt ? formatLabDateTime(preparation.preparedAt) : "Not yet prepared"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Expires At</Typography>
            <Typography variant="body2" sx={{ fontWeight: 700, color: preparation.effectiveStatus === "Expired" ? "error.main" : "text.primary" }}>
              {preparation.expiresAt ? formatLabDate(preparation.expiresAt) : "—"}
            </Typography>
            {recipe && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Shelf life: {recipe.shelfLifeValue} {recipe.shelfLifeUnit}
              </Typography>
            )}
          </Box>
        </Box>

        <Divider sx={{ mb: 2 }} />

        {/* Quantities & Parameters */}
        <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap", mb: 2 }}>
          <Chip
            label={`Final Volume: ${preparation.finalVolumeMl ?? "—"} mL`}
            color="primary"
            variant="outlined"
            size="small"
            sx={{ fontWeight: 600 }}
          />
          {preparation.measuredPh != null && (
            <Chip
              label={`Measured pH: ${preparation.measuredPh}${recipe?.phTarget != null ? ` (Target: ${recipe.phTarget} ± ${recipe.phTolerance ?? 0})` : ""}`}
              size="small"
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
          {recipe?.storageCondition && (
            <Chip
              label={`Storage: ${recipe.storageCondition}`}
              size="small"
              variant="outlined"
              sx={{ fontWeight: 500 }}
            />
          )}
        </Box>

        {recipe?.instructions && (
          <Box sx={{ p: 1.5, bgcolor: "action.hover", borderRadius: 1, mt: 1 }}>
            <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
              Recipe Instructions:
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary", whiteSpace: "pre-wrap" }}>
              {recipe.instructions}
            </Typography>
          </Box>
        )}
      </Paper>

      {/* Components Table */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
          Recipe Components & Consumed Lots
        </Typography>
        <PreparationComponentsTable components={preparation.components} />
      </Paper>

      {/* Status History */}
      <Paper variant="outlined" sx={{ p: 3, mb: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
          Status History
        </Typography>
        <PreparationHistoryTable history={preparation.statusHistory} />
      </Paper>

      {/* Action Footer */}
      {(isInProgress || isPrepared) && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 2 }}>
          {isInProgress && (
            <>
              <Button
                variant="outlined"
                color="error"
                startIcon={<CancelOutlinedIcon />}
                onClick={() => handleOpenAction("cancel")}
                sx={{ textTransform: "none" }}
              >
                Cancel Preparation
              </Button>
              <Button
                variant="contained"
                startIcon={<EditIcon />}
                onClick={() => navigate(`/preparation/${preparation.id}/edit`)}
                sx={{ textTransform: "none", fontWeight: 700 }}
              >
                Resume Preparation
              </Button>
            </>
          )}

          {isPrepared && (
            <Button
              variant="outlined"
              color="error"
              startIcon={<DeleteOutlineOutlinedIcon />}
              onClick={() => handleOpenAction("discard")}
              sx={{ textTransform: "none" }}
            >
              Discard Solution
            </Button>
          )}
        </Box>
      )}

      {/* Cancel / Discard Reason Dialog */}
      <ReasonDialog
        open={Boolean(actionType)}
        title={actionType === "cancel" ? "Cancel Preparation" : "Discard Solution"}
        onClose={() => { if (!actionLoading) setActionType(null); }}
        onConfirm={handleConfirmAction}
        confirmText={actionType === "cancel" ? "Cancel Preparation" : "Discard Solution"}
        confirmColor="error"
        loading={actionLoading}
        loadingText="Submitting..."
        error={actionError}
        reason={reason}
        onReasonChange={setReason}
        label="Reason *"
        placeholder={actionType === "cancel" ? "State the operational reason for cancelling this preparation..." : "State the reason for discarding this prepared solution..."}
      >
        <Alert severity="warning" sx={{ fontSize: 13 }}>
          {actionType === "cancel"
            ? "Cancelling this preparation will close it permanently. No stock will be consumed."
            : "Discarding this solution will mark it as discarded. Stock consumed at preparation time will not be restored."}
        </Alert>
      </ReasonDialog>
    </Box>
  );
}
