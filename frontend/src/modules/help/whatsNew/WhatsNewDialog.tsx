import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  List,
  ListItem,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../i18n";
import { useHelpPanel, type HelpTarget } from "../HelpPanelContext";
import type { RoleType } from "../tour/tourSteps";
import { getReleaseNotes, itemsForRole, type ReleaseNote, type ReleaseNoteItem } from "./releaseNotes";

export interface WhatsNewDialogProps {
  open: boolean;
  onClose: () => void;
  /** Called when the closing transition has finished; used by menu openers to restore focus. */
  onExited?: () => void;
  role: RoleType | null;
  /** "latest" shows only the newest release note (default); "all" shows every version. */
  mode?: "latest" | "all";
  /** Optional release notes override; defaults to RELEASE_NOTES. */
  notes?: readonly ReleaseNote[];
}

interface FilteredReleaseNote {
  version: string;
  date: string;
  items: ReleaseNoteItem[];
}

export function WhatsNewDialog({
  open,
  onClose,
  onExited,
  role,
  mode = "latest",
  notes
}: WhatsNewDialogProps) {
  const { t } = useTranslation("help");
  const { openPanel } = useHelpPanel();
  const titleId = useId();
  const [suppressRestoreFocus, setSuppressRestoreFocus] = useState(false);
  const openedGuideRef = useRef(false);

  useEffect(() => {
    if (open) {
      openedGuideRef.current = false;
      setSuppressRestoreFocus(false);
    }
  }, [open]);

  const sections = useMemo<FilteredReleaseNote[]>(() => {
    const resolvedNotes = notes ?? getReleaseNotes(t);
    const source = mode === "all" ? resolvedNotes : resolvedNotes.slice(0, 1);
    const filtered: FilteredReleaseNote[] = [];
    for (const note of source) {
      const items = itemsForRole(note, role);
      if (items.length > 0) {
        filtered.push({ version: note.version, date: note.date, items });
      }
    }
    return filtered;
  }, [mode, notes, role, t]);

  const handleOpenGuide = (target: HelpTarget) => {
    openedGuideRef.current = true;
    setSuppressRestoreFocus(true);
    onClose();
    openPanel(target);
  };

  const handleExited = () => {
    if (!openedGuideRef.current) {
      onExited?.();
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby={titleId}
      disableRestoreFocus={Boolean(onExited) || suppressRestoreFocus}
      slotProps={{ transition: { onExited: handleExited } }}
    >
      <DialogTitle id={titleId}>
        <Typography component="span" variant="h6">
          {t("whatsNew.title")}
        </Typography>
      </DialogTitle>
      <DialogContent dividers>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1.5 }}>
          {t("shared.guidance")}
        </Typography>
        {sections.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("whatsNew.none")}
          </Typography>
        ) : (
          sections.map((section, idx) => (
            <Box key={section.version} sx={{ mt: idx === 0 ? 0 : 2.5 }}>
              {idx > 0 && <Divider sx={{ mb: 2 }} />}
              <Box sx={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 1, mb: 1 }}>
                <Typography
                  component="h3"
                  variant="subtitle1"
                  sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
                >
                  {t("whatsNew.version", { version: section.version })}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ fontVariantNumeric: "tabular-nums" }}
                >
                  {section.date}
                </Typography>
              </Box>
              <List dense disablePadding>
                {section.items.map((item) => {
                  const guide = item.guide;
                  return (
                    <ListItem
                      key={item.text}
                      disableGutters
                      sx={{ display: "block", py: 0.5 }}
                    >
                      <Typography variant="body2">{item.text}</Typography>
                      {guide && (
                        <Button
                          size="small"
                          variant="text"
                          onClick={() => handleOpenGuide(guide)}
                          aria-label={t("whatsNew.readGuideAria", { text: item.text })}
                          sx={{ mt: 0.25, px: 0, minWidth: 0, textTransform: "none" }}
                        >
                          {t("whatsNew.readGuide")}
                        </Button>
                      )}
                    </ListItem>
                  );
                })}
              </List>
            </Box>
          ))
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} autoFocus>
          {t("shared.close")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
