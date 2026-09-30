import { useEffect, useState } from "react";
import { TextField } from "@mui/material";
import { SidePanel } from "../../../../components/configHierarchy";
import { WaterConfigService } from "../services/WaterConfigService";
import { WaterDept } from "../waterConfigTypes";

interface Props {
  open: boolean;
  // null = add a new water system
  dept: WaterDept | null;
  onClose: () => void;
  onSaved: (message: string, id: number) => void;
}

export function WaterDepartmentPanel({ open, dept, onClose, onSaved }: Props) {
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(dept?.name ?? "");
    setError(null);
  }, [open, dept]);

  const save = async () => {
    if (!name.trim()) {
      setError("Name is required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (dept) {
        await WaterConfigService.updateWaterDepartment(dept.id, name.trim(), dept.version);
        onSaved(`"${name.trim()}" updated.`, dept.id);
      } else {
        const created = await WaterConfigService.createWaterDepartment(name.trim());
        onSaved(`"${name.trim()}" added.`, created?.id);
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this water system.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline="Water system"
      title={dept ? `Rename ${dept.name}` : "Add water system"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={dept ? "Save changes" : "Add water system"}
      error={error}
    >
      <TextField
        autoFocus
        size="small"
        label="Name"
        required
        placeholder="e.g. Purified Water System"
        value={name}
        onChange={(e) => setName(e.target.value)}
      />
    </SidePanel>
  );
}
