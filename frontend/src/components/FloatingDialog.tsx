import { Dialog, DialogTitle, DialogContent, DialogActions, IconButton, DialogProps, SxProps, Theme, Tooltip, useMediaQuery, useTheme } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { ReactNode, SyntheticEvent } from "react";
import { useTranslation } from "../i18n";

interface FloatingDialogProps {
  open: boolean;
  title: ReactNode;
  onClose: () => void;
  children: ReactNode;
  actions?: ReactNode;
  // Defaults to "md" - the size every existing consumer needs. Only pass
  // this when a dialog genuinely needs more room (e.g. a multi-step
  // session wizard hosting a wide matrix panel).
  maxWidth?: DialogProps["maxWidth"];
  // Escape hatch for a dialog needing a branded/colored title bar (e.g. a
  // multi-step session workspace) instead of the default plain title row.
  // The built-in close button inherits titleSx's color, so setting a light
  // text color here (for a dark/branded bar) carries over to it too.
  titleSx?: SxProps<Theme>;
  // Escape hatch for a dialog needing a taller/fixed-height Paper (e.g. a
  // wizard that shouldn't reflow height as it moves between steps).
  paperSx?: SxProps<Theme>;
  // Fixed content rendered between the title bar and the scrollable body -
  // e.g. a step indicator that shouldn't scroll away with the content.
  subHeader?: ReactNode;
  // In bench mode (a shared touch tablet) the dialog takes the whole screen.
  // Opt-in so existing dialogs keep their size.
  benchFullScreen?: boolean;
  // Runs once the dialog has finished opening. A destructive dialog uses it to put focus on Cancel.
  onEntered?: () => void;
}

// Every laboratory process opens as a modal/floating page - "No
// navigation between pages. Analyst focuses only on one task."
export function FloatingDialog({ open, title, onClose, children, actions, maxWidth = "md", titleSx, paperSx, subHeader, benchFullScreen, onEntered }: FloatingDialogProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  // A laboratory task on a phone gets the whole screen: a floating card with
  // margins left too little room for result grids and pushed the actions
  // below the fold.
  const fullScreen = useMediaQuery(theme.breakpoints.down("sm")) || (Boolean(theme.custom?.bench) && Boolean(benchFullScreen));
  const handleClose = (e?: SyntheticEvent | Event | object) => {
    if (e && "stopPropagation" in e && typeof e.stopPropagation === "function") {
      e.stopPropagation();
    }
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth={maxWidth}
      fullWidth
      fullScreen={fullScreen}
      onClick={(e) => e.stopPropagation()}
      slotProps={{
        paper: paperSx ? { sx: paperSx } : undefined,
        transition: onEntered ? { onEntered } : undefined
      }}
    >
      <DialogTitle component="div" sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", ...titleSx }}>
        {title}
        <Tooltip title={t("dialog.close")}>
          <IconButton
            onClick={(e) => {
              e.stopPropagation();
              onClose();
            }}
            size="small"
            aria-label={t("dialog.closeDialog")}
            sx={{ color: "inherit", marginInlineStart: "auto", mt: -0.25, marginInlineEnd: -0.75, flexShrink: 0 }}
          >
            <CloseIcon />
          </IconButton>
        </Tooltip>
      </DialogTitle>
      {subHeader}
      <DialogContent dividers>{children}</DialogContent>
      {actions && <DialogActions>{actions}</DialogActions>}
    </Dialog>
  );
}
