import { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Stack,
  Paper,
  Button,
  CircularProgress,
  Alert,
  AlertTitle,
  Snackbar,
  useTheme,
} from "@mui/material";
import DescriptionIcon from "@mui/icons-material/Description";
import VisibilityIcon from "@mui/icons-material/Visibility";
import DownloadIcon from "@mui/icons-material/Download";
import { Trans } from "react-i18next";
import { useTranslation } from "../i18n";
import { Ltr } from "./Ltr";
import {
  ItemStmService,
  ItemStmDto,
} from "../modules/laboratoryConfiguration/items/services/ItemStmService";
import { documentControlService } from "../modules/documentControl/services/documentControlService";
import { formatLabCalendarDate } from "../utils/formatDate";

interface ItemStmPanelProps {
  itemId: number;
}

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return "—";
  return formatLabCalendarDate(dateStr);
}

export function ItemStmPanel({ itemId }: ItemStmPanelProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  const [stm, setStm] = useState<ItemStmDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<"view" | "download" | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    if (itemId) {
      setLoading(true);
      setFetchError(null);
      ItemStmService.getForItem(itemId)
        .then((data) => {
          if (!cancelled) setStm(data);
        })
        .catch(() => {
          if (!cancelled) {
            setStm(null);
            setFetchError("load");
          }
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }
    return () => {
      cancelled = true;
    };
  }, [itemId]);

  const handleView = async () => {
    if (!stm?.controlledPdfFileId) return;
    setActionLoading("view");
    setActionError(null);
    try {
      const { url } = await documentControlService.viewFileBlob(stm.controlledPdfFileId);
      window.open(url, "_blank");
    } catch {
      setActionError(t("components.itemStmPanel.openFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  const handleDownload = async () => {
    if (!stm?.controlledPdfFileId) return;
    setActionLoading("download");
    setActionError(null);
    try {
      const fileName =
        stm.controlledPdfFileName ||
        `${stm.companyDocumentCode || "STM"}_rev${stm.revisionNumber || "01"}.pdf`;
      await documentControlService.downloadFile(stm.controlledPdfFileId, fileName);
    } catch {
      setActionError(t("components.itemStmPanel.downloadFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (fetchError) {
    return <Alert severity="error">{t("components.itemStmPanel.loadFailed")}</Alert>;
  }

  if (!stm) {
    return null;
  }

  if (stm.status === "Effective") {
    return (
      <>
        <Paper
          sx={{
            p: 1.5,
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 1.5,
            bgcolor: "background.paper",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 1.5,
          }}
        >
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography
              variant="subtitle2"
              component="div"
              sx={{
                fontWeight: 700,
                fontSize: 13,
                color: "text.primary",
                display: "flex",
                alignItems: "center",
                gap: 0.75,
              }}
            >
              <DescriptionIcon
                fontSize="small"
                sx={{ color: theme.custom?.status?.purple?.text ?? "primary.main" }}
              />
              {stm.documentTypeCode} {stm.companyDocumentCode}
            </Typography>
            {stm.title && (
              <Typography
                variant="body2"
                sx={{
                  fontWeight: 600,
                  fontSize: 13,
                  color: "text.primary",
                  mt: 0.25,
                }}
              >
                {stm.title}
              </Typography>
            )}
            <Typography
              variant="caption"
              component="div"
              sx={{
                color: "text.secondary",
                fontSize: 12,
                mt: 0.25,
              }}
            >
              <Trans
                i18nKey="components.itemStmPanel.revisionEffective"
                ns="common"
                values={{ revision: stm.revisionNumber, date: formatDate(stm.effectiveDate) }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>

          <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
            <Button
              size="small"
              variant="outlined"
              disabled={actionLoading !== null}
              startIcon={
                actionLoading === "view" ? (
                  <CircularProgress size={12} />
                ) : (
                  <VisibilityIcon fontSize="small" />
                )
              }
              onClick={handleView}
              sx={{ textTransform: "none", fontSize: 12, minHeight: 32 }}
              aria-label={t("components.itemStmPanel.viewAria", { code: stm.companyDocumentCode ?? "STM" })}
            >
              {t("components.itemStmPanel.view")}
            </Button>
            {stm.canDownload && (
              <Button
                size="small"
                variant="contained"
                color="primary"
                disabled={actionLoading !== null}
                startIcon={
                  actionLoading === "download" ? (
                    <CircularProgress size={12} />
                  ) : (
                    <DownloadIcon fontSize="small" />
                  )
                }
                onClick={handleDownload}
                sx={{ textTransform: "none", fontSize: 12, minHeight: 32 }}
                aria-label={t("components.itemStmPanel.downloadAria", { code: stm.companyDocumentCode ?? "STM" })}
              >
                {t("components.itemStmPanel.download")}
              </Button>
            )}
          </Stack>
        </Paper>

        <Snackbar
          open={Boolean(actionError)}
          autoHideDuration={5000}
          onClose={() => setActionError(null)}
          anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        >
          <Alert severity="error" onClose={() => setActionError(null)} sx={{ borderRadius: 1.5 }}>
            {actionError}
          </Alert>
        </Snackbar>
      </>
    );
  }

  let warningMessage = "";
  if (stm.status === "NotLinked") {
    warningMessage = t("components.itemStmPanel.notLinked");
  } else if (stm.status === "NotFound") {
    warningMessage = t("components.itemStmPanel.notFound", { sop: stm.sopNumber ?? "" });
  } else if (stm.status === "NoEffectiveRevision") {
    warningMessage = t("components.itemStmPanel.noEffectiveRevision", { code: stm.companyDocumentCode ?? "" });
  }

  return (
    <Alert severity="warning" sx={{ borderRadius: 1.5 }}>
      <AlertTitle sx={{ fontWeight: 700 }}>{t("components.itemStmPanel.noEffectiveTitle")}</AlertTitle>
      {warningMessage}
    </Alert>
  );
}
