import { useState } from "react";
import { Box, Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { useAuth } from "../../contexts/AuthContext";
import { APP_ROUTES, PERMISSIONS } from "../../routes/routes";
import type { QualityCreatedDto } from "../../services/qualityService";
import { ProposeChangeControlDialog } from "./changeControl/ProposeChangeControlDialog";
import { ReceiveComplaintDialog } from "./complaint/ReceiveComplaintDialog";

// The two ways to start a quality record from the register: a change control is proposed (Quality.Raise) and a
// complaint is received (Quality.Approve, which is QA). A deviation is raised from the record it concerns, and a
// CAPA from a deviation or from the CAPA it follows. The new record opens on its own page.
export function QualityCreateActions() {
  const { t } = useTranslation("quality");
  const { permissions } = useAuth();
  const navigate = useNavigate();
  const [dialog, setDialog] = useState<"change" | "complaint" | null>(null);

  const canPropose = permissions.includes(PERMISSIONS.QUALITY_RAISE);
  const canReceive = permissions.includes(PERMISSIONS.QUALITY_APPROVE);
  if (!canPropose && !canReceive) return null;

  const open = (created: QualityCreatedDto) => {
    setDialog(null);
    navigate(APP_ROUTES.QUALITY_DETAIL(created.id));
  };

  return (
    <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
      {canPropose && (
        <Button variant="contained" onClick={() => setDialog("change")}>
          {t("steps.create.proposeChange")}
        </Button>
      )}
      {canReceive && (
        <Button variant="outlined" onClick={() => setDialog("complaint")}>
          {t("steps.create.receiveComplaint")}
        </Button>
      )}
      <ProposeChangeControlDialog open={dialog === "change"} onClose={() => setDialog(null)} onCreated={open} />
      <ReceiveComplaintDialog open={dialog === "complaint"} onClose={() => setDialog(null)} onCreated={open} />
    </Box>
  );
}
