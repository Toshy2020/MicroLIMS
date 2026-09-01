import { useState } from "react";
import { Box, Button, Alert, Typography, Chip } from "@mui/material";
import { SamplePreparationService } from "./services/SamplePreparationService";
import { PreparationStepsSummary } from "./PreparationStepsSummary";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useAuth } from "../../contexts/AuthContext";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";

interface Props {
  sample: {
    sampleId: number;
    assignedAnalystId?: number | null;
    assignedAnalystName?: string | null;
  };
  config: ItemPreparationConfiguration;
  onSaved: () => void;
}

// Confirm-only. The configured steps are shown as they stand and signed
// for as-is - correcting them is a Section Head configuration change under
// Laboratory Configuration, not an analyst-side override here.
export function ConfirmPreparationForm({ sample, config, onSaved }: Props) {
  const { userId, role } = useAuth();
  const [signing, setSigning] = useState(false);

  const isAssignedToOther =
    Boolean(sample.assignedAnalystId) &&
    sample.assignedAnalystId !== userId &&
    role !== "SectionHead" &&
    role !== "SystemAdministrator";

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const confirm = async (password: string) => {
    await SamplePreparationService.confirm({ sampleId: sample.sampleId, password });
    setSigning(false);
    onSaved();
  };

  return (
    <Box>
      {isAssignedToOther && (
        <Alert severity="warning" sx={{ mb: 2, fontSize: 13 }}>
          <strong>Sample Assignment Rule:</strong> This sample is currently assigned to{" "}
          <strong>{sample.assignedAnalystName || `User #${sample.assignedAnalystId}`}</strong>.
          Only the designated analyst may prepare this sample, unless reassigned by an authorized Section Head.
        </Alert>
      )}

      {config.approvalStatus === "PendingReview" && (
        <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
          This item's preparation configuration is still awaiting Section Head approval. It is in effect and
          can be confirmed now - approval is a separate review and does not hold up testing.
        </Alert>
      )}

      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Configured Preparation Steps
        </Typography>
        <Chip
          size="small"
          label={config.approvalStatus === "Approved" ? "Approved" : "Pending Approval"}
          color={config.approvalStatus === "Approved" ? "success" : "warning"}
          sx={{ height: 20, fontSize: 11 }}
        />
      </Box>

      <PreparationStepsSummary config={config} />

      <Typography variant="caption" sx={{ display: "block", mt: 2, color: "text.secondary" }}>
        Confirming records these exact values against this sample. Later changes to the item's configuration
        will not alter this record.
      </Typography>

      <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
        <Button variant="contained" onClick={() => setSigning(true)} disabled={isAssignedToOther}>
          Confirm & Start Testing
        </Button>
      </Box>

      <SignatureDialog
        open={signing}
        meaningStatement="I confirm the preparation steps shown above are the steps performed for this sample."
        onCancel={() => setSigning(false)}
        onConfirm={confirm}
      />
    </Box>
  );
}
