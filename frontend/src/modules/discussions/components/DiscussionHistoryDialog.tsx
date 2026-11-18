import { useEffect, useState } from "react";
import { Trans } from "react-i18next";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  CircularProgress,
  Divider,
  Paper
} from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import { discussionService } from "../services/discussionService";
import { DiscussionVersion } from "../types/discussionTypes";
import { DiscussionCategoryBadge } from "./DiscussionCategoryBadge";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";

interface Props {
  open: boolean;
  postId: number;
  postTitle: string;
  onClose: () => void;
}

export function DiscussionHistoryDialog({ open, postId, postTitle, onClose }: Props) {
  const { t } = useTranslation("discussions");
  const [history, setHistory] = useState<DiscussionVersion[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  // A failed load used to read "No prior edits recorded".
  const [loadFailed, setLoadFailed] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (open) {
      setLoading(true);
      setLoadFailed(false);
      discussionService
        .getHistory(postId)
        .then((res) => setHistory(res))
        .catch(() => {
          setHistory([]);
          setLoadFailed(true);
        })
        .finally(() => setLoading(false));
    }
  }, [open, postId, reloadKey]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700, pb: 1 }}>
        <HistoryIcon color="primary" />
        {t("history.title", { title: postTitle })}
      </DialogTitle>
      <Divider />
      <DialogContent sx={{ py: 2 }}>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : loadFailed ? (
          <LoadErrorAlert
            message={t("history.loadError")}
            onRetry={() => setReloadKey((k) => k + 1)}
          />
        ) : history.length === 0 ? (
          <Typography sx={{ color: "text.secondary", textAlign: "center", py: 4 }}>
            {t("history.empty")}
          </Typography>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("history.showing", { count: history.length })}
            </Typography>
            {history.map((ver) => (
              <Paper
                key={ver.id}
                variant="outlined"
                sx={{
                  p: 2,
                  borderRadius: 2,
                  bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.01)")
                }}
              >
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 13, bgcolor: "primary.main", color: "common.white", px: 1, py: 0.25, borderRadius: 1 }}>
                      {t("history.versionLabel", { n: ver.versionNumber })}
                    </Typography>
                    <DiscussionCategoryBadge category={ver.category} categoryName={ver.categoryName} />
                  </Box>
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    <Trans ns="discussions" i18nKey="history.archivedBy" values={{ date: formatDateTimeWithTimeZone(ver.changedAt), name: ver.changedByName }} components={{ strong: <strong /> }} />
                  </Typography>
                </Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                  {ver.title}
                </Typography>
                <Typography
                  variant="body2"
                  sx={{
                    whiteSpace: "pre-wrap",
                    color: "text.primary",
                    p: 1.5,
                    bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(0,0,0,0.2)" : "rgba(0,0,0,0.03)"),
                    borderRadius: 1,
                    fontSize: 13
                  }}
                >
                  {ver.content}
                </Typography>
              </Paper>
            ))}
          </Box>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 1.5 }}>
        <Button onClick={onClose} variant="outlined" color="inherit">
          {t("actions.close")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
