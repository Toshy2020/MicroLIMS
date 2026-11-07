import { useEffect, useState } from "react";
import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import {
  StructuredFrequencyField,
  FrequencyValue,
  emptyFrequency,
  frequencyFrom,
  frequencyLabel,
  frequencyRule,
  LimitFields,
  ChemistryLimitFields,
  LimitPills,
  LimitValues,
  PanelSection,
  SidePanel,
  emptyLimits
} from "../../../../components/configHierarchy";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
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
  editableCodes?: Set<string>;
}

const getInitialLimits = (testCode: string, existing?: SamplingConfig, isCount?: boolean): LimitValues => {
  if (existing) {
    return {
      alertLimit: existing.alertLimit ?? "",
      actionLimit: existing.actionLimit ?? "",
      specLimit: existing.specLimit ?? "",
      unit: existing.unit ?? "",
      limitType: existing.limitType ?? (isCount ? null : "Range"),
      lowerLimit: existing.lowerLimit ?? null,
      upperLimit: existing.upperLimit ?? null,
      lowerInclusive: existing.lowerInclusive ?? true,
      upperInclusive: existing.upperInclusive ?? true,
      expectedResultText: existing.expectedResultText ?? ""
    };
  }
  return {
    alertLimit: "",
    actionLimit: "",
    specLimit: "",
    unit: "",
    limitType: isCount ? null : "Range",
    lowerLimit: null,
    upperLimit: null,
    lowerInclusive: true,
    upperInclusive: true,
    expectedResultText: ""
  };
};

const sameLimits = (a: LimitValues, c: SamplingConfig, isCount: boolean) => {
  if (isCount) {
    return (
      a.alertLimit === (c.alertLimit ?? "") &&
      a.actionLimit === (c.actionLimit ?? "") &&
      a.specLimit === (c.specLimit ?? "") &&
      a.unit === (c.unit ?? "")
    );
  }
  return (
    (a.limitType ?? null) === (c.limitType ?? null) &&
    (a.lowerLimit ?? null) === (c.lowerLimit ?? null) &&
    (a.upperLimit ?? null) === (c.upperLimit ?? null) &&
    (a.lowerInclusive ?? true) === (c.lowerInclusive ?? true) &&
    (a.upperInclusive ?? true) === (c.upperInclusive ?? true) &&
    (a.expectedResultText ?? "") === (c.expectedResultText ?? "") &&
    a.unit === (c.unit ?? "")
  );
};

const hasAny = (v: LimitValues, isCount: boolean) => {
  if (isCount) {
    return [v.alertLimit, v.actionLimit, v.specLimit].some((x) => (x ?? "").trim() !== "");
  }
  if (!v.limitType) return false;
  if (v.limitType === "Range") return v.lowerLimit != null || v.upperLimit != null;
  if (v.limitType === "NotMoreThan") return v.upperLimit != null;
  if (v.limitType === "NotLessThan") return v.lowerLimit != null;
  if (v.limitType === "Qualitative") return Boolean(v.expectedResultText && v.expectedResultText.trim() !== "");
  return false;
};

const formatSpecLimit = (v: LimitValues): string => {
  if (v.limitType === "Range") {
    if (v.lowerLimit != null && v.upperLimit != null) return `${v.lowerLimit}-${v.upperLimit}`;
    if (v.lowerLimit != null) return `NLT ${v.lowerLimit}`;
    if (v.upperLimit != null) return `NMT ${v.upperLimit}`;
    return "";
  }
  if (v.limitType === "NotMoreThan") return v.upperLimit != null ? `NMT ${v.upperLimit}` : "";
  if (v.limitType === "NotLessThan") return v.lowerLimit != null ? `NLT ${v.lowerLimit}` : "";
  if (v.limitType === "Qualitative") return v.expectedResultText ?? "";
  return v.specLimit ?? "";
};

const validateChemistry = (testCode: string, v: LimitValues): string | null => {
  const type = v.limitType ?? "Range";
  if (type === "Range") {
    if (v.lowerLimit == null && v.upperLimit == null) {
      return `${testCode}: Range limit requires a lower limit, an upper limit, or both.`;
    }
    if (v.lowerLimit != null && v.upperLimit != null && v.lowerLimit > v.upperLimit) {
      return `${testCode}: Lower limit cannot be greater than upper limit.`;
    }
  } else if (type === "NotMoreThan") {
    if (v.upperLimit == null) {
      return `${testCode}: Upper limit is required for Not More Than.`;
    }
  } else if (type === "NotLessThan") {
    if (v.lowerLimit == null) {
      return `${testCode}: Lower limit is required for Not Less Than.`;
    }
  } else if (type === "Qualitative") {
    if (!v.expectedResultText || v.expectedResultText.trim() === "") {
      return `${testCode}: Expected result text is required for Qualitative.`;
    }
  }
  return null;
};

// One panel for a sample location: its details, its assigned tests, and
// limits for assigned count tests or chemistry tests (one row per test, created on
// save) - so a location can be fully configured in one place.
export function WaterLocationPanel({ open, point, defaultDepartmentId, departments, configs, isCountTest, onClose, onSaved, editableCodes }: Props) {
  const { options } = useTestDefinitions();
  const [code, setCode] = useState("");
  const [location, setLocation] = useState("");
  const [frequency, setFrequency] = useState<FrequencyValue>(emptyFrequency);
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
    setFrequency(frequencyFrom(point));
    setDepartmentId(point?.waterDepartmentId ?? defaultDepartmentId ?? "");
    const assigned = point?.assignedTestCodes ?? [];
    setTestCodes(assigned);

    const initialLimits: Record<string, LimitValues> = {};
    for (const c of configs) {
      const isCount = isCountTest(c.testCode) || options.find((o) => o.code === c.testCode)?.workflowType === "CountTest";
      initialLimits[c.testCode] = getInitialLimits(c.testCode, c, isCount);
    }
    for (const tCode of assigned) {
      if (!initialLimits[tCode]) {
        const isCount = isCountTest(tCode) || options.find((o) => o.code === tCode)?.workflowType === "CountTest";
        initialLimits[tCode] = getInitialLimits(tCode, undefined, isCount);
      }
    }
    setLimits(initialLimits);
    setSavedPointId(point?.id ?? null);
    setPointVersion(point?.version);
    setError(null);
  }, [open, point, defaultDepartmentId, configs, options, isCountTest]);

  const labName = (testCode: string) => options.find((o) => o.code === testCode)?.section?.name || "Other laboratory";

  const save = async () => {
    if (!code.trim() || departmentId === "") {
      setError("Point code and water system are required.");
      return;
    }
    if (frequency.type === "Weekly" && !frequency.weekday) {
      setError("A weekly frequency needs a sampling weekday.");
      return;
    }

    // Validate non-count test limits before saving
    for (const testCode of testCodes) {
      if (editableCodes !== undefined && !editableCodes.has(testCode)) continue;
      const isCount = isCountTest(testCode) || options.find((o) => o.code === testCode)?.workflowType === "CountTest";
      if (!isCount) {
        const existing = configs.find((c) => c.testCode === testCode);
        const value = limits[testCode] ?? getInitialLimits(testCode, existing, false);
        if (existing || hasAny(value, false)) {
          const err = validateChemistry(testCode, value);
          if (err) {
            setError(err);
            return;
          }
        }
      }
    }

    setSaving(true);
    setError(null);
    let pointId = savedPointId;
    try {
      if (pointId != null) {
        const updated = await WaterConfigService.updateSamplingPoint(pointId, code.trim(), location, frequencyLabel(frequency), testCodes, Number(departmentId), pointVersion, frequencyRule(frequency));
        setPointVersion(updated?.version);
      } else {
        const created = await WaterConfigService.createSamplingPoint(code.trim(), location, frequencyLabel(frequency), testCodes, Number(departmentId), frequencyRule(frequency));
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
    for (const testCode of testCodes) {
      // Do not touch locked tests that the user cannot edit
      if (editableCodes !== undefined && !editableCodes.has(testCode)) continue;
      const isCount = isCountTest(testCode) || options.find((o) => o.code === testCode)?.workflowType === "CountTest";
      const existing = configs.find((c) => c.testCode === testCode);
      const value = limits[testCode] ?? getInitialLimits(testCode, existing, isCount);

      try {
        if (isCount) {
          if (existing) {
            if (!sameLimits(value, existing, true)) {
              await WaterConfigService.updateSamplingConfiguration(
                existing.id,
                testCode,
                value.alertLimit,
                value.actionLimit,
                value.specLimit,
                value.unit,
                existing.version
              );
            }
          } else if (hasAny(value, true)) {
            await WaterConfigService.createSamplingConfiguration(
              pointId!,
              testCode,
              value.alertLimit,
              value.actionLimit,
              value.specLimit,
              value.unit
            );
          }
        } else {
          const specLimit = formatSpecLimit(value);
          if (existing) {
            if (!sameLimits(value, existing, false)) {
              await WaterConfigService.updateSamplingConfiguration(
                existing.id,
                testCode,
                null,
                null,
                specLimit,
                value.unit,
                existing.version,
                value.limitType ?? "Range",
                value.lowerLimit,
                value.upperLimit,
                value.lowerInclusive ?? true,
                value.upperInclusive ?? true,
                value.expectedResultText
              );
            }
          } else if (hasAny(value, false)) {
            await WaterConfigService.createSamplingConfiguration(
              pointId!,
              testCode,
              null,
              null,
              specLimit,
              value.unit,
              value.limitType ?? "Range",
              value.lowerLimit,
              value.upperLimit,
              value.lowerInclusive ?? true,
              value.upperInclusive ?? true,
              value.expectedResultText
            );
          }
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
          <StructuredFrequencyField idPrefix="water-point-frequency" value={frequency} onChange={setFrequency} weekdayRequired />
        </Stack>
      </PanelSection>

      <PanelSection title="2 · Assigned tests" hint="Every test here is ordered when a sample from this location is received.">
        <TestCodePickerMulti
          value={testCodes}
          onChange={setTestCodes}
          label="Assigned tests"
          editableCodes={editableCodes ?? new Set<string>()}
        />
      </PanelSection>

      <PanelSection title="3 · Limits for assigned tests" hint="Configure limits for count tests and chemistry tests.">
        {testCodes.length === 0 ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Assign tests to set limits.</Typography>
        ) : (
          <Stack spacing={2}>
            {testCodes.map((testCode) => {
              const opt = options.find((o) => o.code === testCode);
              const isCount = isCountTest(testCode) || opt?.workflowType === "CountTest";
              const isEditable = editableCodes === undefined || editableCodes.has(testCode);
              const existing = configs.find((c) => c.testCode === testCode);
              const val = limits[testCode] ?? getInitialLimits(testCode, existing, isCount);

              return isEditable ? (
                <Stack key={testCode} spacing={1} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{testCode}</Typography>
                  {isCount ? (
                    <LimitFields
                      idPrefix={`water-limit-${testCode}`}
                      value={val}
                      onChange={(v) => setLimits((prev) => ({ ...prev, [testCode]: v }))}
                      unitPlaceholder="e.g. CFU/mL"
                    />
                  ) : (
                    <ChemistryLimitFields
                      idPrefix={`water-limit-${testCode}`}
                      value={val}
                      onChange={(v) => setLimits((prev) => ({ ...prev, [testCode]: v }))}
                      unitPlaceholder="e.g. pH, µS/cm"
                    />
                  )}
                </Stack>
              ) : (
                <Stack key={testCode} spacing={1} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "action.hover" }}>
                  <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
                    <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{testCode}</Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {labName(testCode)} · Read-only
                    </Typography>
                  </Stack>
                  <LimitPills {...val} />
                </Stack>
              );
            })}
          </Stack>
        )}
      </PanelSection>
    </SidePanel>
  );
}
