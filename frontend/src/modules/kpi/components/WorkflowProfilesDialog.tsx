import { Button } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { WorkflowProfilesPanel } from "./WorkflowProfilesPanel";
import { useTranslation } from "../../../i18n";

interface WorkflowProfilesDialogProps {
  open: boolean;
  onClose: () => void;
}

// Hosts the panel on the existing KPI tab. The panel loads only while the dialog is open.
export function WorkflowProfilesDialog({ open, onClose }: WorkflowProfilesDialogProps) {
  const { t } = useTranslation("kpi");
  return (
    <FloatingDialog
      open={open}
      title={t("filters.profiles")}
      onClose={onClose}
      maxWidth="lg"
      actions={<Button onClick={onClose}>{t("dialog.close")}</Button>}
    >
      <WorkflowProfilesPanel />
    </FloatingDialog>
  );
}
