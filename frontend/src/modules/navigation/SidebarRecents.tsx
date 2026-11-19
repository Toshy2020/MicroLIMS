import { useId, useState } from "react";
import { Link } from "react-router-dom";
import { Box, Collapse, List, ListItemButton, ListItemText, Tooltip, Typography } from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { ConfirmationDialog } from "../../components/ConfirmationDialog";
import { useUiState } from "../../hooks/useUiState";
import { inAppRoute } from "./recentRecordRoutes";
import { MAX_SIDEBAR_RECENTS, useRecentRecords } from "./useRecentRecords";
import type { RecentRecordDto } from "./services/recentRecordsService";

export const RECENTS_OPEN_KEY = "nav.recentsOpen";

interface SidebarRecentsProps {
  collapsed: boolean;
  onNavigate: () => void;
}

const headerButtonSx = {
  bgcolor: "transparent",
  border: 0,
  p: 0.25,
  color: "rgba(255, 255, 255, 0.85)",
  fontSize: 11,
  cursor: "pointer",
  textDecoration: "underline",
  "&:hover": { color: "common.white" }
} as const;

interface Row {
  r: RecentRecordDto;
  to: string;
}

// The five most recently opened records, newest first. The section is a display
// aid: when the list cannot be loaded, or is empty, it is not shown at all.
export function SidebarRecents({ collapsed, onNavigate }: SidebarRecentsProps) {
  const { t } = useTranslation("navigation");
  const { items, failed, clear } = useRecentRecords();
  const openState = useUiState(RECENTS_OPEN_KEY);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [clearing, setClearing] = useState(false);
  const listId = useId();

  const rows: Row[] = [];
  for (const r of items) {
    const to = inAppRoute(r.route);
    if (to !== null) rows.push({ r, to });
    if (rows.length === MAX_SIDEBAR_RECENTS) break;
  }
  if (failed || rows.length === 0) return null;

  const open = openState.value !== "0";
  const toggle = () => {
    openState.set(open ? "0" : "1").catch(() => {
      // The state is only a convenience; the section stays as it is.
    });
  };
  const visible = collapsed || open;

  return (
    <Box component="section" aria-label={t("sidebar.recentlyOpened")}>
      {!collapsed && (
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", px: 2.5, pt: 0.5 }}>
          <Box
            component="button"
            type="button"
            onClick={toggle}
            aria-expanded={open}
            aria-controls={listId}
            sx={{
              ...headerButtonSx,
              display: "flex",
              alignItems: "center",
              gap: 0.25,
              textDecoration: "none",
              fontWeight: 600,
              letterSpacing: "0.08em",
              textTransform: "uppercase"
            }}
          >
            <ExpandMoreIcon aria-hidden sx={{ fontSize: 14, transform: open ? "none" : "rotate(-90deg)" }} />
            {t("sidebar.recent")}
          </Box>
          {open && (
            <Box component="button" type="button" onClick={() => setConfirmOpen(true)} sx={headerButtonSx}>
              {t("sidebar.clear")}
            </Box>
          )}
        </Box>
      )}
      <Collapse in={visible} unmountOnExit>
        <List disablePadding id={listId} aria-label={t("sidebar.recentRecords")}>
          {rows.map(({ r, to }) => {
            const label = r.title ? `${r.identifier} ${r.title}` : r.identifier;
            return (
              <Box key={`${r.recordType}:${r.recordId}`} component="li" sx={{ listStyle: "none" }}>
                <Tooltip title={label} placement="right">
                  <ListItemButton
                    component={Link}
                    to={to}
                    aria-label={label}
                    onClick={onNavigate}
                    sx={{
                      minHeight: 34,
                      py: 0.5,
                      px: collapsed ? 2.25 : 2,
                      mx: 1,
                      my: 0.25,
                      borderRadius: 1,
                      color: "rgba(255, 255, 255, 0.85)",
                      justifyContent: collapsed ? "center" : "flex-start",
                      "&:hover": { bgcolor: "rgba(255, 255, 255, 0.1)", color: "common.white" }
                    }}
                  >
                    <HistoryIcon
                      aria-hidden
                      sx={{ fontSize: 16, mr: collapsed ? 0 : 1.25, color: "rgba(255, 255, 255, 0.75)" }}
                    />
                    {!collapsed && (
                      <ListItemText
                        primary={
                          <>
                            <Typography component="span" sx={{ fontSize: 13, fontFamily: "monospace", mr: 0.75 }}>
                              <Ltr>{r.identifier}</Ltr>
                            </Typography>
                            {r.title && (
                              <>
                                <Typography aria-hidden component="span" sx={{ fontSize: 12, opacity: 0.6, mr: 0.75 }}>
                                  {"·"}
                                </Typography>
                                <Typography component="span" sx={{ fontSize: 12, opacity: 0.8 }}>
                                  {r.title}
                                </Typography>
                              </>
                            )}
                          </>
                        }
                        slotProps={{ primary: { noWrap: true } }}
                      />
                    )}
                  </ListItemButton>
                </Tooltip>
              </Box>
            );
          })}
        </List>
      </Collapse>
      <ConfirmationDialog
        open={confirmOpen}
        destructive
        loading={clearing}
        title="Clear recent records"
        message="Remove all records from your recently opened list? The records themselves are not changed."
        confirmText="Clear"
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => {
          setClearing(true);
          clear().then(() => {
            setClearing(false);
            setConfirmOpen(false);
          });
        }}
      />
    </Box>
  );
}
