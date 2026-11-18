import React, { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  CircularProgress,
  Paper,
  Divider,
  FormControlLabel,
  Checkbox,
  LinearProgress,
  Chip
} from "@mui/material";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import InfoIcon from "@mui/icons-material/Info";
import type { AcknowledgementPresentationDto } from "../types/acknowledgementTypes";
import { monospaceFontFamily } from "../../../theme/palette";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

interface DocumentAcknowledgementDialogProps {
  open: boolean;
  context: AcknowledgementPresentationDto | null;
  loading: boolean;
  readingProgress?: number;
  onClose: () => void;
  onConfirmAcknowledgement: (confirmed: boolean, comments?: string) => Promise<void>;
}

export const DocumentAcknowledgementDialog: React.FC<DocumentAcknowledgementDialogProps> = ({
  open,
  context,
  loading: _loading,
  readingProgress = 100,
  onClose,
  onConfirmAcknowledgement
}) => {
  const { t } = useTranslation("documentControl");
  const [confirmedStatement, setConfirmedStatement] = useState(false);
  const [comments, setComments] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!confirmedStatement) {
      setError(t("acknowledgement.mustConfirmError"));
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      await onConfirmAcknowledgement(confirmedStatement, comments.trim() || undefined);
      setConfirmedStatement(false);
      setComments("");
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("acknowledgement.submitError")));
    } finally {
      setSubmitting(false);
    }
  };

  if (!context) {
    return null;
  }

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1.5 }}>
        <VerifiedUserIcon color="primary" />
        <Typography variant="h6" component="span" sx={{
          fontWeight: "bold"
        }}>
          {t("acknowledgement.title")}
        </Typography>
      </DialogTitle>

      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {error && <Alert severity="error">{error}</Alert>}

          {!context.canAcknowledge && (
            <Alert severity="warning">
              {context.validationMessage || t("acknowledgement.cannotAcknowledgeDefault")}
            </Alert>
          )}

          {/* Document & Revision Details */}
          <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.default" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
              <div>
                <Typography variant="subtitle2" sx={{
                  color: "text.secondary"
                }}>
                  {t("acknowledgement.codeAndRevision")}
                </Typography>
                <Typography variant="h6" sx={{
                  fontWeight: "bold"
                }}>
                  {t("acknowledgement.codeAndRevValue", { code: context.companyDocumentCode, rev: context.revisionNumber })}
                </Typography>
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>
                  {context.documentTitle}
                </Typography>
              </div>
              <Chip
                label={t("acknowledgement.statusChip", { status: context.assignmentStatus })}
                color={context.assignmentStatus === "Acknowledged" ? "success" : "primary"}
                size="small"
              />
            </Box>

            <Divider sx={{ my: 1.5 }} />

            <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 1.5 }}>
              <div>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("acknowledgement.assignedUser")}</Typography>
                <Typography variant="body2" sx={{
                  fontWeight: "medium"
                }}>{context.assignedUserName}</Typography>
              </div>
              <div>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>{t("acknowledgement.dueDate")}</Typography>
                <Typography variant="body2">
                  {context.dueDateUtc ? formatLabCalendarDate(context.dueDateUtc) : t("acknowledgement.noDueDate")}
                </Typography>
              </div>
              {context.controlledFileName && (
                <div style={{ gridColumn: "span 2" }}>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>{t("acknowledgement.controlledFileArtifact")}</Typography>
                  <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.8rem" }}>
                    {context.controlledFileName}
                  </Typography>
                </div>
              )}
            </Box>
          </Paper>

          {/* Reading Progress Indicator (Informational Only - DC-URS-190) */}
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
              <MenuBookIcon color="action" fontSize="small" />
              <Typography variant="subtitle2" sx={{
                fontWeight: "bold"
              }}>
                {t("acknowledgement.readingProgressTitle")}
              </Typography>
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                  ml: "auto"
                }}>
                {t("acknowledgement.viewedPercent", { percent: readingProgress })}
              </Typography>
            </Box>
            {/* Mandated label (ML-DC-FRS-1C-001 §2:133). Must sit on the
                progress indicator itself, not only in the helper text below. */}
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, mb: 1 }}>
              <Typography
                variant="caption"
                sx={{
                  fontWeight: "bold",
                  color: "text.secondary"
                }}>
                {t("acknowledgement.informationalOnly")}
              </Typography>
            </Box>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, Math.max(0, readingProgress))}
              sx={{ height: 8, borderRadius: 1 }}
              color={readingProgress >= 100 ? "success" : "primary"}
            />
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, mt: 1 }}>
              <InfoIcon fontSize="inherit" color="action" />
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("acknowledgement.progressHelper")}
              </Typography>
            </Box>
          </Paper>

          {/* Legal Evidentiary Statement (DC-URS-191, DC-URS-189) */}
          <Paper
            variant="outlined"
            sx={{
              p: 2,
              bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "background.default",
              borderColor: "primary.main",
              borderLeftWidth: 4
            }}
          >
            <Typography variant="subtitle2" color="primary" gutterBottom sx={{
              fontWeight: "bold"
            }}>
              {t("acknowledgement.legalTitle")}
            </Typography>
            <Typography
              variant="body2"
              sx={{
                fontStyle: "italic",
                lineHeight: 1.6,
                color: "text.primary"
              }}
            >
              "{context.legalStatementText}"
            </Typography>

            <Divider sx={{ my: 1.5 }} />

            <FormControlLabel
              control={
                <Checkbox
                  checked={confirmedStatement}
                  onChange={(e) => setConfirmedStatement(e.target.checked)}
                  color="primary"
                  disabled={!context.canAcknowledge || submitting}
                />
              }
              label={
                <Typography variant="body2" sx={{
                  fontWeight: "bold"
                }}>
                  {t("acknowledgement.confirmCheckbox")}
                </Typography>
              }
            />
          </Paper>

          {/* Optional Comments */}
          <TextField
            label={t("acknowledgement.remarksLabel")}
            multiline
            rows={2}
            fullWidth
            size="small"
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            disabled={!context.canAcknowledge || submitting}
            placeholder={t("acknowledgement.remarksPlaceholder")}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting} color="inherit">
          {t("common.cancel")}
        </Button>
        <Button
          onClick={() => handleSubmit()}
          variant="contained"
          color="primary"
          disabled={!context.canAcknowledge || !confirmedStatement || submitting}
          startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <VerifiedUserIcon />}
        >
          {submitting ? t("acknowledgement.submitting") : t("acknowledgement.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
