import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import { monospaceFontFamily } from "../../theme/palette";

interface ShortcutsDialogProps {
  open: boolean;
  onClose: () => void;
  onExited?: () => void;
}

interface ShortcutRow {
  keys: string;
  action: string;
}

const GRID_SHORTCUTS: ShortcutRow[] = [
  { keys: "Enter / Down", action: "Move to the same column in the next row" },
  { keys: "Up", action: "Move to the same column in the previous row" },
  { keys: "Left / Right", action: "Move to the previous / next cell when the caret is at the edge of the text" },
  { keys: "Tab / Shift+Tab", action: "Move to the next / previous cell in reading order" },
  { keys: "Ctrl+D", action: "Copy the value from the cell above" },
  { keys: "Ctrl+Enter", action: "Save the form" },
  { keys: "Escape", action: "Cancel edit or close the dialog" }
];

const GLOBAL_SHORTCUTS: ShortcutRow[] = [
  { keys: "Ctrl+K", action: "Search MicroLIMS" },
  { keys: "?", action: "Open keyboard shortcuts (when not typing in a field)" }
];

function ShortcutTable({ heading, rows }: { heading: string; rows: ShortcutRow[] }) {
  return (
    <Box sx={{ mb: 2, "&:last-of-type": { mb: 0 } }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.75 }}>
        {heading}
      </Typography>
      <Table size="small" aria-label={heading}>
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 600, width: "40%" }}>Key</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Action</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.keys}>
              <TableCell>
                <Box
                  component="kbd"
                  sx={{
                    fontFamily: monospaceFontFamily,
                    fontSize: 12,
                    px: 0.75,
                    py: 0.25,
                    borderRadius: 1,
                    border: "1px solid",
                    borderColor: "divider",
                    bgcolor: "action.hover",
                    whiteSpace: "nowrap"
                  }}
                >
                  {row.keys}
                </Box>
              </TableCell>
              <TableCell>{row.action}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

export function ShortcutsDialog({ open, onClose, onExited }: ShortcutsDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="shortcuts-dialog-title"
      disableRestoreFocus={Boolean(onExited)}
      slotProps={{ transition: { onExited } }}
    >
      <DialogTitle id="shortcuts-dialog-title">
        <Typography component="span" variant="h6">Keyboard shortcuts</Typography>
      </DialogTitle>
      <DialogContent dividers>
        <ShortcutTable heading="Data entry grids" rows={GRID_SHORTCUTS} />
        <ShortcutTable heading="Global" rows={GLOBAL_SHORTCUTS} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} autoFocus>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
