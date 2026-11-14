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
import { HELP_GUIDANCE, useHelpPanel, type HelpTarget } from "../HelpPanelContext";
import type { RoleType } from "../tour/tourSteps";
import { monospaceFontFamily } from "../../../theme/palette";
import { itemsForRole, RELEASE_NOTES, type ReleaseNote, type ReleaseNoteItem } from "./releaseNotes";

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
  notes = RELEASE_NOTES
}: WhatsNewDialogProps) {
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
    const source = mode === "all" ? notes : notes.slice(0, 1);
    const filtered: FilteredReleaseNote[] = [];
    for (const note of source) {
      const items = itemsForRole(note, role);
      if (items.length > 0) {
        filtered.push({ version: note.version, date: note.date, items });
      }
    }
    return filtered;
  }, [mode, notes, role]);

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
          What&apos;s new
        </Typography>
      </DialogTitle>
      <DialogContent dividers>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1.5 }}>
          {HELP_GUIDANCE}
        </Typography>
        {sections.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No release notes are available for your role.
          </Typography>
        ) : (
          sections.map((section, idx) => (
            <Box key={section.version} sx={{ mt: idx === 0 ? 0 : 2.5 }}>
              {idx > 0 && <Divider sx={{ mb: 2 }} />}
              <Box sx={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 1, mb: 1 }}>
                <Typography
                  component="h3"
                  variant="subtitle1"
                  sx={{ fontWeight: 700, fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}
                >
                  {`Version ${section.version}`}
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
                          aria-label={`Read guide: ${item.text}`}
                          sx={{ mt: 0.25, px: 0, minWidth: 0, textTransform: "none" }}
                        >
                          Read guide
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
          Close
        </Button>
      </DialogActions>
    </Dialog>
  );
}
