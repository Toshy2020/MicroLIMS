import { Dialog, DialogTitle, DialogContent, DialogActions, IconButton, DialogProps, SxProps, Theme } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { ReactNode } from "react";

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
}

// Every laboratory process opens as a modal/floating page - "No
// navigation between pages. Analyst focuses only on one task."
export function FloatingDialog({ open, title, onClose, children, actions, maxWidth = "md", titleSx, paperSx, subHeader }: FloatingDialogProps) {
  const handleClose = (e?: React.SyntheticEvent | Event | {}) => {
    if (e && "stopPropagation" in e && typeof (e as any).stopPropagation === "function") {
      (e as any).stopPropagation();
    }
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth={maxWidth}
      fullWidth
      onClick={(e) => e.stopPropagation()}
      slotProps={{
        paper: paperSx ? { sx: paperSx } : undefined
      }}
    >
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", ...titleSx }}>
        {title}
        <IconButton
          onClick={(e) => {
            e.stopPropagation();
            onClose();
          }}
          size="small"
          sx={{ color: "inherit" }}
        >
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      {subHeader}
      <DialogContent dividers>{children}</DialogContent>
      {actions && <DialogActions>{actions}</DialogActions>}
    </Dialog>
  );
}
