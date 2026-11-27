import { useState } from "react";
import { Box, Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { useAuth, type Role } from "../../contexts/AuthContext";
import { APP_ROUTES, PERMISSIONS } from "../../routes/routes";
import type { QualityCreatedDto } from "../../services/qualityService";
import { ProposeChangeControlDialog } from "./changeControl/ProposeChangeControlDialog";
import { ReceiveComplaintDialog } from "./complaint/ReceiveComplaintDialog";

// Mirrors the server's change-control proposer roles; the server stays authoritative.
const CHANGE_CONTROL_PROPOSER_ROLES: readonly Role[] = ["SectionHead", "QcManager", "Qa"];

// The two ways to start a quality record from the register: a change control is proposed (Quality.Raise) and a
// complaint is received (Quality.Approve, which is QA). The server also limits proposing to a section head, QC
// manager or QA role (QualityRecordCore.IsChangeControlProposerRoleAsync), so the button is shown only to those. A deviation is raised from the record it concerns, and a
// CAPA from a deviation or from the CAPA it follows. The new record opens on its own page.
export function QualityCreateActions() {
  const { t } = useTranslation("quality");
  const { permissions, role } = useAuth();
  const navigate = useNavigate();
  const [dialog, setDialog] = useState<"change" | "complaint" | null>(null);

  const canPropose =
    permissions.includes(PERMISSIONS.QUALITY_RAISE) && role !== null && CHANGE_CONTROL_PROPOSER_ROLES.includes(role);
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
