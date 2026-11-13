import { useRef, useState } from "react";
import { IconButton, ListItemText, Menu, MenuItem, Tooltip } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlined";
import { AboutDialog } from "./AboutDialog";

// The Help entries other than About are built in later tasks of the help
// package; until then they are shown as unavailable rather than faked.
const PENDING_ITEMS = [
  "Help for this page",
  "Full guide",
  "Keyboard shortcuts",
  "What's new",
  "Replay tour"
] as const;

const MENU_ID = "header-help-menu";

export function HelpMenu() {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [aboutOpen, setAboutOpen] = useState(false);

  const closeMenu = () => setAnchor(null);

  return (
    <>
      <Tooltip title="Help">
        <IconButton
          ref={buttonRef}
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
        {PENDING_ITEMS.map((label) => (
          <MenuItem key={label} disabled>
            <ListItemText primary={label} secondary="Not available yet" />
          </MenuItem>
        ))}
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
    </>
  );
}
