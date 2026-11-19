import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, Box, Button, IconButton, Popover, Tooltip, Typography } from "@mui/material";
import EditNoteIcon from "@mui/icons-material/EditNote";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { ConfirmationDialog } from "../ConfirmationDialog";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";
import type { DraftOverview } from "../../hooks/useDraftOverview";
import type { Draft } from "../../services/drafts/draftStore";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

interface DraftsMenuProps {
  overview: DraftOverview;
}

// "2 h ago", "5 min ago": the age of the last change on the tablet's own clock.
export function draftAge(updatedAtDevice: string, now: Date, t: TFunction = i18n.getFixedT("en", "common")): string {
  const minutes = Math.max(0, Math.floor((now.getTime() - Date.parse(updatedAtDevice)) / 60_000));
  if (Number.isNaN(minutes)) return t("components.draftsMenu.unknownAge");
  if (minutes < 1) return t("components.draftsMenu.justNow");
  if (minutes < 60) return t("components.draftsMenu.minutesAgo", { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return t("components.draftsMenu.hoursAgo", { count: hours });
  return t("components.draftsMenu.daysAgo", { count: Math.floor(hours / 24) });
}

// The signed-in user's unsaved entries on this tablet. Drafts are not records:
// opening one only goes to its screen, where the analyst chooses to restore it.
export function DraftsMenu({ overview }: DraftsMenuProps) {
  const { t } = useTranslation();
  const { drafts, otherUsersCount, discard } = overview;
  const navigate = useNavigate();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [toDiscard, setToDiscard] = useState<Draft<unknown> | null>(null);
  const [busy, setBusy] = useState(false);

  if (drafts.length === 0 && otherUsersCount === 0) return null;
  const now = new Date();

  return (
    <>
      <Tooltip title={t("components.draftsMenu.tooltip")}>
        <IconButton
          color="inherit"
          onClick={(e) => setAnchor(e.currentTarget)}
          aria-label={t("components.draftsMenu.buttonAria", { count: drafts.length })}
          aria-haspopup="dialog"
          aria-expanded={Boolean(anchor)}
        >
          <Badge badgeContent={drafts.length} color="warning" showZero={false}>
            <EditNoteIcon />
          </Badge>
        </IconButton>
      </Tooltip>

      <Popover
        open={Boolean(anchor)}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ paper: { role: "dialog", "aria-label": t("components.draftsMenu.tooltip"), sx: { minWidth: 300, maxWidth: 420, p: 1.5 } } }}
      >
        <Typography variant="subtitle2" component="p" sx={{ mb: 0.5 }}>
          {t("components.draftsMenu.tooltip")}
        </Typography>
        <Typography variant="caption" color="text.secondary" component="p" sx={{ mb: 1 }}>
          {t("components.draftsMenu.subtitle")}
        </Typography>

        {drafts.map((draft) => (
          <Box key={draft.id} sx={{ display: "flex", alignItems: "center", gap: 0.5, py: 0.5 }}>
            <Button
              aria-label={t("components.draftsMenu.openAria", { title: draft.title })}
              onClick={() => {
                setAnchor(null);
                navigate(draft.route);
              }}
              sx={{ flex: 1, justifyContent: "flex-start", textAlign: "left", textTransform: "none", color: "text.primary" }}
            >
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{draft.title}</Typography>
                <Typography variant="caption" color="text.secondary" component="span" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                  {t("components.draftsMenu.startedAt", {
                    age: draftAge(draft.updatedAtDevice, now, t),
                    date: formatDateTimeWithTimeZone(draft.startedAtDevice)
                  })}
                </Typography>
                {draft.state === "rejected" && (
                  <Typography variant="caption" color="error" component="span" sx={{ display: "block" }}>
                    {t("components.draftsMenu.rejectedPrefix", {
                      reason: draft.rejectReason ?? t("components.draftsMenu.serverRefused")
                    })}
                  </Typography>
                )}
              </Box>
            </Button>
            <IconButton aria-label={t("components.draftsMenu.discardAria", { title: draft.title })} onClick={() => setToDiscard(draft)}>
              <DeleteOutlinedIcon />
            </IconButton>
          </Box>
        ))}

        {otherUsersCount > 0 && (
          <Typography variant="body2" role="note" sx={{ mt: 1 }}>
            {t("components.draftsMenu.otherUsersNote")}
          </Typography>
        )}
      </Popover>

      <ConfirmationDialog
        open={toDiscard !== null}
        destructive
        title={t("components.draftsMenu.discardTitle")}
        message={t("components.draftsMenu.discardMessage", { title: toDiscard?.title ?? "" })}
        confirmText={t("components.draftsMenu.discardConfirm")}
        loading={busy}
        onCancel={() => setToDiscard(null)}
        onConfirm={async () => {
          if (!toDiscard) return;
          setBusy(true);
          try {
            await discard(toDiscard.id);
          } finally {
            setBusy(false);
            setToDiscard(null);
          }
        }}
      />
    </>
  );
}
