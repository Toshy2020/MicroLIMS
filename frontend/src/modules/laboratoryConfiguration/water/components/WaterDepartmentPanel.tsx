import { useEffect, useState } from "react";
import { TextField } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import { SidePanel } from "../../../../components/configHierarchy";
import { RecordChangedBanner } from "../../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../../hooks/useRecordChanged";
import { useReloadedRecord } from "../../../../hooks/useReloadedRecord";
import { WaterConfigService } from "../services/WaterConfigService";
import { WaterDept } from "../waterConfigTypes";

interface Props {
  open: boolean;
  // null = add a new water system
  dept: WaterDept | null;
  onClose: () => void;
  onSaved: (message: string, id: number) => void;
}

export function WaterDepartmentPanel({ open, dept: openedDept, onClose, onSaved }: Props) {
  const { t } = useTranslation("labConfig");
  const { current: dept, reload } = useReloadedRecord<WaterDept>(openedDept, async (d) =>
    ((await WaterConfigService.getWaterDepartments()) as WaterDept[]).find((x) => x.id === d.id)
  );
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
      setError(t("water.deptPanel.nameRequired"));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (dept) {
        await WaterConfigService.updateWaterDepartment(dept.id, name.trim(), dept.version);
        noteOwnSave("waterDepartment", dept.id);
        onSaved(t("water.deptPanel.updated", { name: name.trim() }), dept.id);
      } else {
        const created = await WaterConfigService.createWaterDepartment(name.trim());
        onSaved(t("water.deptPanel.added", { name: name.trim() }), created?.id);
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? t("water.deptPanel.saveError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={t("water.deptPanel.overline")}
      title={dept ? t("water.deptPanel.rename", { name: dept.name }) : t("water.deptPanel.add")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={dept ? t("water.deptPanel.saveChanges") : t("water.deptPanel.add")}
      error={error}
    >
      {open && dept && (
        <RecordChangedBanner type="waterDepartment" id={dept.id} dirty={name !== dept.name} onReload={() => void reload()} />
      )}
      <TextField
        autoFocus
        size="small"
        label={t("water.deptPanel.nameLabel")}
        required
        placeholder={t("water.deptPanel.namePlaceholder")}
        value={name}
        onChange={(e) => setName(e.target.value)}
      />
    </SidePanel>
  );
}
