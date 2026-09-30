import { useEffect, useMemo, useState } from "react";
import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import {
  FrequencyField,
  LimitFields,
  LimitValues,
  PanelSection,
  SidePanel,
  emptyLimits
} from "../../../../components/configHierarchy";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { WaterConfigService } from "../services/WaterConfigService";
import { SamplingConfig, SamplingPoint, WaterDept } from "../waterConfigTypes";

interface Props {
  open: boolean;
  // null = add a new location to `defaultDepartmentId`
  point: SamplingPoint | null;
  defaultDepartmentId: number | null;
  departments: WaterDept[];
  configs: SamplingConfig[];
  isCountTest: (code: string) => boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
}

const sameLimits = (a: LimitValues, c: SamplingConfig) =>
  a.alertLimit === (c.alertLimit ?? "") && a.actionLimit === (c.actionLimit ?? "") &&
  a.specLimit === (c.specLimit ?? "") && a.unit === (c.unit ?? "");

const hasAny = (v: LimitValues) => [v.alertLimit, v.actionLimit, v.specLimit].some((x) => x.trim() !== "");

// One panel for a sample location: its details, its assigned tests, and
// the limits of each assigned count test (one row per test, created on
// save) - so a location can be fully configured in one place.
export function WaterLocationPanel({ open, point, defaultDepartmentId, departments, configs, isCountTest, onClose, onSaved }: Props) {
  const [code, setCode] = useState("");
  const [location, setLocation] = useState("");
  const [frequency, setFrequency] = useState("");
  const [departmentId, setDepartmentId] = useState<number | "">("");
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [limits, setLimits] = useState<Record<string, LimitValues>>({});
  // Set once the location itself is saved, so a retry after a failed
  // limits save updates it instead of creating a duplicate.
  const [savedPointId, setSavedPointId] = useState<number | null>(null);
  const [pointVersion, setPointVersion] = useState<number | undefined>(undefined);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setCode(point?.code ?? "");
    setLocation(point?.location ?? "");
    setFrequency(point?.testingFrequency ?? "");
    setDepartmentId(point?.waterDepartmentId ?? defaultDepartmentId ?? "");
    setTestCodes(point?.assignedTestCodes ?? []);
    setLimits(
      Object.fromEntries(
        configs.map((c) => [c.testCode, { alertLimit: c.alertLimit ?? "", actionLimit: c.actionLimit ?? "", specLimit: c.specLimit ?? "", unit: c.unit ?? "" }])
      )
    );
    setSavedPointId(point?.id ?? null);
    setPointVersion(point?.version);
    setError(null);
  }, [open, point, defaultDepartmentId, configs]);

  const countCodes = useMemo(() => testCodes.filter(isCountTest), [testCodes, isCountTest]);

  const save = async () => {
    if (!code.trim() || departmentId === "") {
      setError("Point code and water system are required.");
      return;
    }
    setSaving(true);
    setError(null);
    let pointId = savedPointId;
    try {
      if (pointId != null) {
        const updated = await WaterConfigService.updateSamplingPoint(pointId, code.trim(), location, frequency, testCodes, Number(departmentId), pointVersion);
        setPointVersion(updated?.version);
      } else {
        const created = await WaterConfigService.createSamplingPoint(code.trim(), location, frequency, testCodes, Number(departmentId));
        pointId = created.id;
        setSavedPointId(pointId);
        setPointVersion(created?.version);
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this sample location.");
      setSaving(false);
      return;
    }

    const failed: string[] = [];
    for (const testCode of countCodes) {
      const value = limits[testCode] ?? emptyLimits;
      const existing = configs.find((c) => c.testCode === testCode);
      try {
        if (existing) {
          if (!sameLimits(value, existing)) {
            await WaterConfigService.updateSamplingConfiguration(existing.id, testCode, value.alertLimit, value.actionLimit, value.specLimit, value.unit, existing.version);
          }
        } else if (hasAny(value)) {
          await WaterConfigService.createSamplingConfiguration(pointId!, testCode, value.alertLimit, value.actionLimit, value.specLimit, value.unit);
        }
      } catch (e: any) {
        failed.push(`${testCode}: ${e?.response?.data?.message ?? "could not be saved"}`);
      }
    }
    setSaving(false);

    if (failed.length > 0) {
      setError(`The location was saved, but some limits were not. ${failed.join(" ")}`);
      return;
    }
    onSaved(point ? `Sample location ${code.trim()} updated.` : `Sample location ${code.trim()} added.`);
  };

  const deptName = departments.find((d) => d.id === departmentId)?.name;

  return (
    <SidePanel
      open={open}
      overline={deptName}
      title={point ? `Edit sample location ${point.code}` : "Add sample location"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={point ? "Save changes" : "Add sample location"}
      error={error}
    >
      <PanelSection title="1 · Location details">
        <Stack spacing={1.5}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
            <TextField size="small" label="Point code" required value={code} onChange={(e) => setCode(e.target.value)} sx={{ flex: 1 }} />
            <TextField
              select
              size="small"
              label="Water system"
              required
              value={departmentId}
              onChange={(e) => setDepartmentId(Number(e.target.value))}
              sx={{ flex: 1 }}
            >
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <TextField size="small" label="Point name" placeholder="e.g. Manufacturing hall tap" value={location} onChange={(e) => setLocation(e.target.value)} />
          <FrequencyField id="water-point-frequency" value={frequency} onChange={setFrequency} />
        </Stack>
      </PanelSection>

      <PanelSection title="2 · Assigned tests" hint="Every test here is ordered when a sample from this location is received.">
        <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned tests" />
      </PanelSection>

      <PanelSection title="3 · Limits for count tests" hint="One row per assigned count test. Presence/absence tests need no limits.">
        {countCodes.length === 0 ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Assign a count test (e.g. TAMC-Water) to set limits.</Typography>
        ) : (
          <Stack spacing={2}>
            {countCodes.map((testCode) => (
              <Stack key={testCode} spacing={1} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{testCode}</Typography>
                <LimitFields
                  idPrefix={`water-limit-${testCode}`}
                  value={limits[testCode] ?? emptyLimits}
                  onChange={(v) => setLimits((prev) => ({ ...prev, [testCode]: v }))}
                  unitPlaceholder="e.g. CFU/mL"
                />
              </Stack>
            ))}
          </Stack>
        )}
      </PanelSection>
    </SidePanel>
  );
}
