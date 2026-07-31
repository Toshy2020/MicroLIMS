import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Typography } from "@mui/material";

interface ConfirmationDialogProps {
  open: boolean;
  message: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmationDialog({ open, message, onConfirm, onCancel }: ConfirmationDialogProps) {
  return (
    <Dialog open={open} onClose={onCancel}>
      <DialogTitle>Confirm</DialogTitle>
      <DialogContent><Typography>{message}</Typography></DialogContent>
      <DialogActions>
        <Button onClick={onCancel}>Cancel</Button>
        <Button onClick={onConfirm} variant="contained" color="primary">Confirm</Button>
      </DialogActions>
    </Dialog>
  );
}
