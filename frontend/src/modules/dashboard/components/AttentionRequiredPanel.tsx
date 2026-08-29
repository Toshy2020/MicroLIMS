import { useState, useEffect } from "react";
import { Paper, Box, Typography, Stack, useTheme, Chip } from "@mui/material";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import HourglassBottomOutlinedIcon from "@mui/icons-material/HourglassBottomOutlined";
import { Link } from "react-router-dom";
import { NotificationItem, MediaExpiryLot } from "../types/dashboard";
import { SectionTitle } from "../../../components/SectionTitle";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { DashboardService } from "../services/DashboardService";

function timeAgo(timestamp: string): string {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(timestamp).getTime()) / 60_000));
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hr${hours > 1 ? "s" : ""} ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

interface AttentionRequiredPanelProps {
  notifications?: NotificationItem[];
  expiringMedia?: MediaExpiryLot[];
  loading?: boolean;
}

export function AttentionRequiredPanel({ notifications: propNotifications, expiringMedia: propExpiringMedia, loading }: AttentionRequiredPanelProps) {
  const theme = useTheme();

  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [expiringMedia, setExpiringMedia] = useState<MediaExpiryLot[]>([]);

  useEffect(() => {
    if (propNotifications) {
      setNotifications(propNotifications.filter((n) => n.type !== "ReviewWaiting" && n.type !== "ApprovalWaiting"));
    } else {
      DashboardService.getNotifications()
        .then((items) => setNotifications(items.filter((n) => n.type !== "ReviewWaiting" && n.type !== "ApprovalWaiting")))
        .catch(() => setNotifications([]));
    }
  }, [propNotifications]);

  useEffect(() => {
    if (propExpiringMedia) {
      setExpiringMedia(propExpiringMedia);
    } else {
      DashboardService.getMediaExpiry(5)
        .then(setExpiringMedia)
        .catch(() => setExpiringMedia([]));
    }
  }, [propExpiringMedia]);

  return (
    <Paper sx={{ p: 2.5, height: "100%", display: "flex", flexDirection: "column" }}>
      <SectionTitle>Attention Required</SectionTitle>

      {loading ? (
        <LoadingSpinner />
      ) : (
        <Stack spacing={1.5} sx={{ flex: 1 }}>
          {/* Expiring media alerts */}
          {expiringMedia.map((lot) => (
            <Box
              key={lot.mediaId}
              component={Link}
              to="/laboratory-configuration/media"
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.25,
                p: 1.25,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: lot.daysRemaining <= 2 ? theme.custom.status.detected.border : theme.custom.status.action.border,
                bgcolor: lot.daysRemaining <= 2 ? theme.custom.status.detected.bg : theme.custom.status.action.bg,
                cursor: "pointer",
                textDecoration: "none",
                color: "inherit"
              }}
            >
              <HourglassBottomOutlinedIcon
                fontSize="small"
                sx={{ color: lot.daysRemaining <= 2 ? theme.custom.status.detected.text : theme.custom.status.action.text }}
              />
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                  {lot.mediaTypeName} · Lot {lot.lotNumber}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  Expires in {lot.daysRemaining} day{lot.daysRemaining === 1 ? "" : "s"}
                </Typography>
              </Box>
              <Chip size="small" label="Expiring" sx={{ height: 20, fontSize: 10, fontWeight: 700 }} />
            </Box>
          ))}

          {/* Actionable notifications */}
          {notifications.slice(0, 5).map((n, i) => (
            <Box
              key={n.id ?? i}
              sx={{
                display: "flex",
                gap: 1.25,
                alignItems: "flex-start",
                p: 1.25,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: theme.palette.divider,
                opacity: n.isRead ? 0.7 : 1
              }}
            >
              {n.severity === "error" ? (
                <ErrorOutlineIcon fontSize="small" sx={{ color: theme.custom.status.detected.text, mt: 0.25 }} />
              ) : n.severity === "warning" ? (
                <WarningAmberOutlinedIcon fontSize="small" sx={{ color: theme.custom.status.action.text, mt: 0.25 }} />
              ) : (
                <InfoOutlinedIcon fontSize="small" sx={{ color: theme.custom.status.info.text, mt: 0.25 }} />
              )}
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: n.isRead ? 500 : 700 }}>
                  {n.message}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {timeAgo(n.timestamp)}
                </Typography>
              </Box>
            </Box>
          ))}

          {expiringMedia.length === 0 && notifications.length === 0 && (
            <Box sx={{ py: 3, textAlign: "center" }}>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                No active attention alerts. All conditions normal.
              </Typography>
            </Box>
          )}
        </Stack>
      )}
    </Paper>
  );
}
