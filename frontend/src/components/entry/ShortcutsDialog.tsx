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
import { useTranslation } from "../../i18n";
import { Ltr } from "../Ltr";
import { monospaceFontFamily } from "../../theme/palette";

interface ShortcutsDialogProps {
  open: boolean;
  onClose: () => void;
  onExited?: () => void;
}

interface ShortcutRow {
  keys: string;
  actionKey: string;
}

const GRID_SHORTCUTS: ShortcutRow[] = [
  { keys: "Enter / Down", actionKey: "components.shortcutsDialog.enterDown" },
  { keys: "Up", actionKey: "components.shortcutsDialog.up" },
  { keys: "Left / Right", actionKey: "components.shortcutsDialog.leftRight" },
  { keys: "Tab / Shift+Tab", actionKey: "components.shortcutsDialog.tabShiftTab" },
  { keys: "Ctrl+D", actionKey: "components.shortcutsDialog.ctrlD" },
  { keys: "Ctrl+Enter", actionKey: "components.shortcutsDialog.ctrlEnter" },
  { keys: "Escape", actionKey: "components.shortcutsDialog.escape" }
];

const GLOBAL_SHORTCUTS: ShortcutRow[] = [
  { keys: "Ctrl+K", actionKey: "components.shortcutsDialog.ctrlK" },
  { keys: "?", actionKey: "components.shortcutsDialog.questionMark" }
];

function ShortcutTable({ heading, rows }: { heading: string; rows: ShortcutRow[] }) {
  const { t } = useTranslation();
  return (
    <Box sx={{ mb: 2, "&:last-of-type": { mb: 0 } }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.75 }}>
        {heading}
      </Typography>
      <Table size="small" aria-label={heading}>
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 600, width: "40%" }}>{t("components.shortcutsDialog.keyColumn")}</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>{t("components.shortcutsDialog.actionColumn")}</TableCell>
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
                  <Ltr>{row.keys}</Ltr>
                </Box>
              </TableCell>
              <TableCell>{t(row.actionKey)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

export function ShortcutsDialog({ open, onClose, onExited }: ShortcutsDialogProps) {
  const { t } = useTranslation();
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
        <Typography component="span" variant="h6">{t("components.shortcutsDialog.title")}</Typography>
      </DialogTitle>
      <DialogContent dividers>
        <ShortcutTable heading={t("components.shortcutsDialog.gridHeading")} rows={GRID_SHORTCUTS} />
        <ShortcutTable heading={t("components.shortcutsDialog.globalHeading")} rows={GLOBAL_SHORTCUTS} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} autoFocus>{t("actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
}
