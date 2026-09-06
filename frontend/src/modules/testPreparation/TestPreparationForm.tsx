import { useState } from "react";
import { Box, Select, MenuItem, TextField, Button, Alert } from "@mui/material";
import { SamplePreparationService } from "./services/SamplePreparationService";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useAuth } from "../../contexts/AuthContext";

interface Props {
  sample: {
    sampleId: number;
    category: string;
    itemName?: string | null;
    assignedAnalystId?: number | null;
    assignedAnalystName?: string | null;
  };
  onSaved: () => void;
}

// The Test Preparation form - shared by the standalone TestPreparationPage
// (pick-a-sample-from-a-dropdown flow) and the "Needs Preparation" dialog
// opened directly from a Testing Workspace card. Once per Sample; renamed
// "Start Testing" since completing it also auto-assigns the preparer as
// analyst on every Waiting test order for this sample.
export function TestPreparationForm({ sample, onSaved }: Props) {
  const { userId, role } = useAuth();
  const [form, setForm] = useState<Record<string, any>>({ technique: "PourPlate" });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [signing, setSigning] = useState(false);

  const isAssignedToOther =
    Boolean(sample.assignedAnalystId) &&
    sample.assignedAnalystId !== userId &&
    role !== "SectionHead" &&
    role !== "SystemAdministrator";

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const canSubmit = Boolean(form.diluent?.trim()) && Boolean(form.neutralizer?.trim());

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const save = async (password: string) => {
    setMessage(null);
    await SamplePreparationService.prepare({
      sampleId: sample.sampleId, amount: Number(form.amount), technique: form.technique,
      filtrationVolume: form.filtrationVolume ? Number(form.filtrationVolume) : undefined,
      washingVolume: form.washingVolume ? Number(form.washingVolume) : undefined,
      diluent: form.diluent.trim(), neutralizer: form.neutralizer.trim(),
      password
    });
    setSigning(false);
    setMessage({ text: "Preparation saved.", ok: true });
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

      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
        <TextField label="Sample Amount" value={form.amount ?? ""} onChange={(e) => setField("amount", e.target.value)} disabled={isAssignedToOther} />
        <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)} disabled={isAssignedToOther}>
          <MenuItem value="PourPlate">Pour Plate</MenuItem>
          <MenuItem value="Filtration">Filtration</MenuItem>
        </Select>
      </Box>

      {form.technique === "Filtration" && (
        <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
          <TextField label="Filtration Volume (ml)" value={form.filtrationVolume ?? ""} onChange={(e) => setField("filtrationVolume", e.target.value)} disabled={isAssignedToOther} />
          <TextField label="Washing Volume (ml)" value={form.washingVolume ?? ""} onChange={(e) => setField("washingVolume", e.target.value)} disabled={isAssignedToOther} />
        </Box>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
        <TextField label="Diluent" value={form.diluent ?? ""} onChange={(e) => setField("diluent", e.target.value)} disabled={isAssignedToOther} required />
        <TextField label="Neutralizer" value={form.neutralizer ?? ""} onChange={(e) => setField("neutralizer", e.target.value)} disabled={isAssignedToOther} required />
      </Box>

      <Alert severity="info" sx={{ mt: 2, fontSize: 12 }}>
        <strong>{sample.itemName ?? "This item"}</strong> has no preparation configuration yet. What you enter
        here becomes its standing configuration for future samples, and is sent to the Section Head for
        approval - testing is not held up waiting for it.
      </Alert>

      <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
        <Button variant="contained" onClick={() => setSigning(true)} disabled={isAssignedToOther || !canSubmit}>
          Start Testing
        </Button>
      </Box>

      <SignatureDialog
        open={signing}
        meaningStatement="I confirm the preparation steps entered above are the steps performed for this sample, and are correct for this item."
        onCancel={() => setSigning(false)}
        onConfirm={save}
      />
    </Box>
  );
}
