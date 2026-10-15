import { Button } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { WorkflowProfilesPanel } from "./WorkflowProfilesPanel";

interface WorkflowProfilesDialogProps {
  open: boolean;
  onClose: () => void;
}

// Hosts the panel on the existing KPI tab. The panel loads only while the dialog is open.
export function WorkflowProfilesDialog({ open, onClose }: WorkflowProfilesDialogProps) {
  return (
    <FloatingDialog
      open={open}
      title="Workflow Profiles"
      onClose={onClose}
      maxWidth="lg"
      actions={<Button onClick={onClose}>Close</Button>}
    >
      <WorkflowProfilesPanel />
    </FloatingDialog>
  );
}
