import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { IconButton, ListItemText, Menu, MenuItem, Tooltip } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlined";
import { AboutDialog } from "./AboutDialog";
import { useHelpPanel } from "./HelpPanelContext";
import { ShortcutsDialog } from "../../components/entry/ShortcutsDialog";
import { useOptionalAuth } from "../../contexts/AuthContext";
import { TOUR_STEPS, type RoleType } from "./tour/tourSteps";
import { WhatsNewDialog } from "./whatsNew/WhatsNewDialog";
import type { ReleaseNote } from "./whatsNew/releaseNotes";

const MENU_ID = "header-help-menu";

function toRoleType(value: string | null | undefined): RoleType | null {
  return value != null && Object.prototype.hasOwnProperty.call(TOUR_STEPS, value) ? (value as RoleType) : null;
}

export function HelpMenu({ whatsNewNotes }: { whatsNewNotes?: readonly ReleaseNote[] } = {}) {
  const { helpButtonRef: buttonRef, openPanel, startTour, whatsNewNotes: contextNotes } = useHelpPanel();
  const auth = useOptionalAuth();
  const validRole = toRoleType(auth?.role);
  const navigate = useNavigate();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [aboutOpen, setAboutOpen] = useState(false);
  const [whatsNewOpen, setWhatsNewOpen] = useState(false);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [shortcutsReturnFocus, setShortcutsReturnFocus] = useState(false);

  const closeMenu = () => setAnchor(null);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key !== "?" || e.ctrlKey || e.metaKey || e.altKey) return;
      const target = e.target as HTMLElement | null;
      if (target) {
        const tag = target.tagName;
        if (
          tag === "INPUT" ||
          tag === "TEXTAREA" ||
          tag === "SELECT" ||
          target.isContentEditable ||
          target.getAttribute("contenteditable") === "true" ||
          Boolean(target.closest("[contenteditable='true']"))
        ) {
          return;
        }
      }
      e.preventDefault();
      setShortcutsReturnFocus(false);
      setShortcutsOpen(true);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  return (
    <>
      <Tooltip title="Help">
        <IconButton
          ref={buttonRef}
          data-tour="help-menu"
          onClick={(e) => setAnchor(e.currentTarget)}
          sx={{ color: "inherit" }}
          aria-label="Help"
          aria-controls={anchor ? MENU_ID : undefined}
          aria-haspopup="menu"
          aria-expanded={Boolean(anchor)}
        >
          <HelpOutlineIcon />
        </IconButton>
      </Tooltip>
      <Menu
        id={MENU_ID}
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={closeMenu}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <MenuItem
          onClick={() => {
            closeMenu();
            openPanel();
          }}
        >
          <ListItemText primary="Help for this page" />
        </MenuItem>
        <MenuItem
          onClick={() => {
            closeMenu();
            navigate("/help");
          }}
        >
          <ListItemText primary="Full guide" />
        </MenuItem>
        <MenuItem
          onClick={() => {
            closeMenu();
            setShortcutsReturnFocus(true);
            setShortcutsOpen(true);
          }}
        >
          <ListItemText primary="Keyboard shortcuts" />
        </MenuItem>
        <MenuItem
          onClick={() => {
            closeMenu();
            setWhatsNewOpen(true);
          }}
        >
          <ListItemText primary="What's new" />
        </MenuItem>
        <MenuItem
          onClick={() => {
            closeMenu();
            startTour();
          }}
        >
          <ListItemText primary="Replay tour" />
        </MenuItem>
        <MenuItem
          onClick={() => {
            closeMenu();
            setAboutOpen(true);
          }}
        >
          <ListItemText primary="About" />
        </MenuItem>
      </Menu>
      <AboutDialog
        open={aboutOpen}
        onClose={() => setAboutOpen(false)}
        onExited={() => buttonRef.current?.focus()}
      />
      <WhatsNewDialog
        open={whatsNewOpen}
        onClose={() => setWhatsNewOpen(false)}
        onExited={() => buttonRef.current?.focus()}
        role={validRole}
        mode="all"
        notes={whatsNewNotes ?? contextNotes}
      />
      <ShortcutsDialog
        open={shortcutsOpen}
        onClose={() => setShortcutsOpen(false)}
        onExited={shortcutsReturnFocus ? () => buttonRef.current?.focus() : undefined}
      />
    </>
  );
}
