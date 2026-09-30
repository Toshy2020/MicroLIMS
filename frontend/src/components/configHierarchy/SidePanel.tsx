import { ReactNode } from "react";
import { Alert, Box, Button, Drawer, IconButton, Stack, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";

interface Props {
  open: boolean;
  overline?: string;
  title: string;
  onClose: () => void;
  onSave: () => void;
  saving?: boolean;
  saveLabel?: string;
  error?: string | null;
  children: ReactNode;
}

// Right-hand drawer used for every add/edit form on the configuration
// pages, so editing happens next to the record instead of in a form at
// the top of the page.
export function SidePanel({ open, overline, title, onClose, onSave, saving, saveLabel = "Save", error, children }: Props) {
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={saving ? undefined : onClose}
      slotProps={{ paper: { sx: { width: { xs: "100%", sm: 520 }, display: "flex", flexDirection: "column" } } }}
    >
      <Stack direction="row" sx={{ px: 3, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "flex-start", gap: 2 }}>
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          {overline && (
            <Typography sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary" }}>
              {overline}
            </Typography>
          )}
          <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700, overflowWrap: "anywhere" }}>
            {title}
          </Typography>
        </Box>
        <IconButton aria-label="Close panel" onClick={onClose} disabled={saving}>
          <CloseIcon />
        </IconButton>
      </Stack>
      <Box
        component="form"
        id="config-side-panel-form"
        onSubmit={(e) => {
          e.preventDefault();
          onSave();
        }}
        sx={{ flexGrow: 1, overflowY: "auto", px: 3, py: 2.5, display: "flex", flexDirection: "column", gap: 3 }}
      >
        {error && <Alert severity="error">{error}</Alert>}
        {children}
      </Box>
      <Stack direction="row" spacing={1.25} sx={{ px: 3, py: 2, borderTop: "1px solid", borderColor: "divider", justifyContent: "flex-end" }}>
        <Button onClick={onClose} disabled={saving} sx={{ textTransform: "none" }}>
          Cancel
        </Button>
        <Button type="submit" form="config-side-panel-form" variant="contained" disabled={saving} sx={{ textTransform: "none", fontWeight: 700 }}>
          {saving ? "Saving..." : saveLabel}
        </Button>
      </Stack>
    </Drawer>
  );
}

// Section heading inside a SidePanel ("1 · Location details").
export function PanelSection({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <Box component="section" sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
      <Box>
        <Typography component="h3" sx={{ fontSize: 14, fontWeight: 700 }}>
          {title}
        </Typography>
        {hint && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{hint}</Typography>}
      </Box>
      {children}
    </Box>
  );
}
