import { useEffect, useMemo, useState } from "react";
import { isAxiosError } from "axios";
import type { TFunction } from "i18next";
import { useTranslation } from "../../../../i18n";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
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
  SidePanel
} from "../../../../components/configHierarchy";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { RecordChangedBanner } from "../../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../../hooks/useRecordChanged";
import { useReloadedRecord } from "../../../../hooks/useReloadedRecord";
import { isTestCodeInLab } from "../WaterPreparationForm";
import { WaterConfigService } from "../services/WaterConfigService";
import { SamplingConfig, SamplingPoint, WaterDept, WaterPointLabFrequency } from "../waterConfigTypes";

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

const validateChemistry = (testCode: string, v: LimitValues, t: TFunction<"labConfig">): string | null => {
  const type = v.limitType ?? "Range";
  if (type === "Range") {
    if (v.lowerLimit == null && v.upperLimit == null) {
      return t("water.panel.rangeNeedsOne", { code: testCode });
    }
    if (v.lowerLimit != null && v.upperLimit != null && v.lowerLimit > v.upperLimit) {
      return t("water.panel.lowerGtUpper", { code: testCode });
    }
  } else if (type === "NotMoreThan") {
    if (v.upperLimit == null) {
      return t("water.panel.upperRequired", { code: testCode });
    }
  } else if (type === "NotLessThan") {
    if (v.lowerLimit == null) {
      return t("water.panel.lowerRequired", { code: testCode });
    }
  } else if (type === "Qualitative") {
    if (!v.expectedResultText || v.expectedResultText.trim() === "") {
      return t("water.panel.expectedRequired", { code: testCode });
    }
  }
  return null;
};

// One panel for a sample location: its details, its assigned tests, and
// limits for assigned count tests or chemistry tests (one row per test, created on
// save) - so a location can be fully configured in one place.
export function WaterLocationPanel({ open, point: openedPoint, defaultDepartmentId, departments, configs, isCountTest, onClose, onSaved, editableCodes }: Props) {
  const { t } = useTranslation("labConfig");
  const { current: point, reload } = useReloadedRecord<SamplingPoint>(openedPoint, async (p) =>
    ((await WaterConfigService.getSamplingPoints()) as SamplingPoint[]).find((x) => x.id === p.id)
  );
  // Set when the user changes a field, cleared whenever the form is (re)loaded from the record.
  const [touched, setTouched] = useState(false);
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

  const [labFrequencies, setLabFrequencies] = useState<WaterPointLabFrequency[]>([]);
  const [editingLabId, setEditingLabId] = useState<number | null>(null);
  const [editingFrequency, setEditingFrequency] = useState<FrequencyValue>(emptyFrequency);
  const [labSavingId, setLabSavingId] = useState<number | null>(null);
  const [labError, setLabError] = useState<string | null>(null);

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
    setLabFrequencies(point?.labFrequencies ?? []);
    setEditingLabId(null);
    setLabSavingId(null);
    setLabError(null);
    setTouched(false);
    setError(null);
  }, [open, point, defaultDepartmentId, configs, options, isCountTest]);

  const labSections = useMemo(() => {
    const map = new Map<number, { id: number; code: string; name: string; testCodes: string[] }>();
    for (const code of testCodes) {
      const def = options.find((o) => o.code === code);
      let sectionId: number | undefined = def?.section?.id ?? def?.sectionId;
      let sectionCode: string | undefined = def?.section?.code;
      let sectionName: string | undefined = def?.section?.name;

      if (sectionId == null) {
        const microOpt = options.find((o) => o.section?.code?.toUpperCase() === "MICRO");
        if (microOpt) {
          sectionId = microOpt.section?.id ?? microOpt.sectionId;
          sectionCode = microOpt.section?.code;
          sectionName = microOpt.section?.name;
        }
      }

      if (sectionId != null) {
        if (!sectionCode) {
          sectionCode = sectionId === 1 ? "MICRO" : sectionId === 2 ? "FP" : `Section ${sectionId}`;
        }
        if (!sectionName) {
          sectionName = sectionCode;
        }
        if (!map.has(sectionId)) {
          map.set(sectionId, { id: sectionId, code: sectionCode, name: sectionName, testCodes: [code] });
        } else {
          map.get(sectionId)!.testCodes.push(code);
        }
      }
    }
    return Array.from(map.values()).sort((a, b) => a.code.localeCompare(b.code));
  }, [testCodes, options]);

  const canConfigureLab = (group: { id: number; testCodes: string[] }) => {
    if (editableCodes === undefined) return true;
    return (
      group.testCodes.some((code) => editableCodes.has(code)) ||
      Array.from(editableCodes).some((code) => isTestCodeInLab(code, group.id, options))
    );
  };

  const startEditLab = (secId: number) => {
    setLabError(null);
    setEditingLabId(secId);
    const existing = labFrequencies.find((f) => f.sectionId === secId && f.frequencyType);
    if (existing) {
      setEditingFrequency(frequencyFrom(existing));
    } else {
      setEditingFrequency(frequencyFrom(point));
    }
  };

  const saveLabFrequency = async (secId: number) => {
    const currentPointId = savedPointId ?? point?.id;
    if (!currentPointId) {
      setLabError(t("water.panel.saveLocationFirst"));
      return;
    }
    if (editingFrequency.type === "Weekly" && !editingFrequency.weekday) {
      setLabError(t("water.panel.weeklyNeedsDay"));
      return;
    }
    setLabSavingId(secId);
    setLabError(null);
    try {
      const updated = await WaterConfigService.setPointLabFrequency(currentPointId, secId, frequencyRule(editingFrequency));
      noteOwnSave("waterPointLabFrequency", updated.id);
      setLabFrequencies((prev) => {
        const rest = prev.filter((f) => f.sectionId !== secId);
        return [...rest, updated];
      });
      setEditingLabId(null);
    } catch (e: unknown) {
      setLabError((isAxiosError<{ message?: string }>(e) && e.response?.data?.message) || t("water.panel.labSaveError"));
    } finally {
      setLabSavingId(null);
    }
  };

  const resetLabFrequency = async (secId: number) => {
    const currentPointId = savedPointId ?? point?.id;
    if (!currentPointId) return;
    setLabSavingId(secId);
    setLabError(null);
    try {
      await WaterConfigService.deletePointLabFrequency(currentPointId, secId);
      noteOwnSave("waterPointLabFrequency", labFrequencies.find((f) => f.sectionId === secId)?.id);
      setLabFrequencies((prev) => prev.filter((f) => f.sectionId !== secId));
      if (editingLabId === secId) setEditingLabId(null);
    } catch (e: unknown) {
      setLabError((isAxiosError<{ message?: string }>(e) && e.response?.data?.message) || t("water.panel.labResetError"));
    } finally {
      setLabSavingId(null);
    }
  };

  const reloadLabFrequency = async (pointId: number, sectionId: number) => {
    setLabError(null);
    try {
      const points: SamplingPoint[] = await WaterConfigService.getSamplingPoints();
      const fresh = points.find((p) => p.id === pointId)?.labFrequencies?.find((f) => f.sectionId === sectionId);
      setLabFrequencies((previous) => [
        ...previous.filter((f) => f.sectionId !== sectionId),
        ...(fresh ? [fresh] : [])
      ]);
      setEditingLabId((previous) => previous === sectionId ? null : previous);
    } catch (e: unknown) {
      setLabError((isAxiosError<{ message?: string }>(e) && e.response?.data?.message) ||
        t("components.configWizard.reloadLatestFailed", { ns: "common" }));
    }
  };

  const labName = (testCode: string) => options.find((o) => o.code === testCode)?.section?.name || t("water.panel.otherLab");

  const save = async () => {
    if (!code.trim() || departmentId === "") {
      setError(t("water.panel.codeAndSystemRequired"));
      return;
    }
    if (frequency.type === "Weekly" && !frequency.weekday) {
      setError(t("water.panel.weeklyNeedsDay"));
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
          const err = validateChemistry(testCode, value, t);
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
        noteOwnSave("waterSamplingPoint", pointId);
      } else {
        const created = await WaterConfigService.createSamplingPoint(code.trim(), location, frequencyLabel(frequency), testCodes, Number(departmentId), frequencyRule(frequency));
        pointId = created.id;
        setSavedPointId(pointId);
        setPointVersion(created?.version);
      }
    } catch (e: unknown) {
      setError((isAxiosError<{ message?: string }>(e) && e.response?.data?.message) || t("water.panel.saveError"));
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
      } catch (e: unknown) {
        const message = (isAxiosError<{ message?: string }>(e) && e.response?.data?.message) || t("water.panel.couldNotBeSaved");
        failed.push(`${testCode}: ${message}`);
      }
    }
    setSaving(false);

    if (failed.length > 0) {
      setError(t("water.panel.limitsNotSaved", { failures: failed.join(" ") }));
      return;
    }
    onSaved(point ? t("water.panel.updated", { code: code.trim() }) : t("water.panel.added", { code: code.trim() }));
  };

  const deptName = departments.find((d) => d.id === departmentId)?.name;

  return (
    <SidePanel
      open={open}
      overline={deptName}
      title={point ? t("water.panel.editTitle", { code: point.code }) : t("water.panel.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={point ? t("water.panel.saveChanges") : t("water.panel.addTitle")}
      error={error}
    >
      {open && point && <RecordChangedBanner type="waterSamplingPoint" id={point.id} dirty={touched || editingLabId !== null} onReload={() => void reload()} />}
      <PanelSection title={t("water.panel.section1")}>
        <Stack spacing={1.5}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
            <TextField size="small" label={t("water.panel.pointCode")} required value={code} onChange={(e) => { setTouched(true); setCode(e.target.value); }} sx={{ flex: 1 }} />
            <TextField
              select
              size="small"
              label={t("water.panel.waterSystem")}
              required
              value={departmentId}
              onChange={(e) => { setTouched(true); setDepartmentId(Number(e.target.value)); }}
              sx={{ flex: 1 }}
            >
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <TextField size="small" label={t("water.panel.pointName")} placeholder={t("water.panel.pointNamePlaceholder")} value={location} onChange={(e) => { setTouched(true); setLocation(e.target.value); }} />
          <StructuredFrequencyField idPrefix="water-point-frequency" value={frequency} onChange={(v) => { setTouched(true); setFrequency(v); }} weekdayRequired />
        </Stack>
      </PanelSection>

      <PanelSection title={t("water.panel.section2")} hint={t("water.panel.section2Hint")}>
        <TestCodePickerMulti
          value={testCodes}
          onChange={(codes) => { setTouched(true); setTestCodes(codes); }}
          label={t("water.panel.assignedTests")}
          editableCodes={editableCodes ?? new Set<string>()}
        />
      </PanelSection>

      <PanelSection title={t("water.panel.section3")} hint={t("water.panel.section3Hint")}>
        {labError && <Alert severity="error" sx={{ mb: 1.5 }}>{labError}</Alert>}
        {!(point || savedPointId) ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {t("water.panel.saveFirstNote")}
          </Typography>
        ) : labSections.length === 0 ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {t("water.panel.assignFirstNote")}
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {labSections.map((sec) => {
              const override = labFrequencies.find((f) => f.sectionId === sec.id && f.frequencyType);
              const desc = override ? (override.description || frequencyLabel(override)) : t("water.panel.pointDefault");
              const editable = canConfigureLab(sec);
              const isEditing = editingLabId === sec.id;
              const isBusy = labSavingId === sec.id;

              return (
                <Box
                  key={sec.id}
                  data-testid={`lab-freq-row-${sec.code}`}
                  sx={{
                    p: 1.5,
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 1.5,
                    bgcolor: editable ? "background.paper" : "action.hover"
                  }}
                >
                  {open && point && override?.id != null && (
                    <RecordChangedBanner
                      type="waterPointLabFrequency"
                      id={override.id}
                      dirty={isEditing}
                      onReload={() => void reloadLabFrequency(point.id, sec.id)}
                    />
                  )}
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", justifyContent: "space-between", flexWrap: "wrap" }}>
                    <Box>
                      <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{sec.code}</Typography>
                      <Typography sx={{ fontSize: 13, color: override ? "text.primary" : "text.secondary" }}>
                        {desc}
                      </Typography>
                    </Box>
                    {editable ? (
                      !isEditing && (
                        <Stack direction="row" spacing={1}>
                          <Button size="small" onClick={() => startEditLab(sec.id)} disabled={isBusy}>
                            {t("water.panel.edit")}
                          </Button>
                          {override && (
                            <Button
                              size="small"
                              color="error"
                              onClick={() => resetLabFrequency(sec.id)}
                              disabled={isBusy}
                            >
                              {t("water.panel.resetDefault")}
                            </Button>
                          )}
                        </Stack>
                      )
                    ) : (
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {t("water.panel.readOnly")}
                      </Typography>
                    )}
                  </Stack>

                  {isEditing && (
                    <Box sx={{ mt: 1.5, pt: 1.5, borderTop: "1px solid", borderColor: "divider" }}>
                      <StructuredFrequencyField
                        idPrefix={`water-lab-frequency-${sec.id}`}
                        value={editingFrequency}
                        onChange={setEditingFrequency}
                        weekdayRequired
                        inheritedHint={t("water.panel.inheritedHint", { value: frequencyLabel(frequency) || t("water.panel.noFrequency") })}
                      />
                      <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
                        <Button
                          size="small"
                          variant="contained"
                          onClick={() => saveLabFrequency(sec.id)}
                          disabled={isBusy}
                        >
                          {t("water.panel.save")}
                        </Button>
                        <Button
                          size="small"
                          onClick={() => setEditingLabId(null)}
                          disabled={isBusy}
                        >
                          {t("water.panel.cancel")}
                        </Button>
                      </Stack>
                    </Box>
                  )}
                </Box>
              );
            })}
          </Stack>
        )}
      </PanelSection>

      <PanelSection title={t("water.panel.section4")} hint={t("water.panel.section4Hint")}>
        {testCodes.length === 0 ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("water.panel.assignToSetLimits")}</Typography>
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
                      onChange={(v) => { setTouched(true); setLimits((prev) => ({ ...prev, [testCode]: v })); }}
                      unitPlaceholder={t("water.panel.unitCount")}
                    />
                  ) : (
                    <ChemistryLimitFields
                      idPrefix={`water-limit-${testCode}`}
                      value={val}
                      onChange={(v) => { setTouched(true); setLimits((prev) => ({ ...prev, [testCode]: v })); }}
                      unitPlaceholder={t("water.panel.unitChem")}
                    />
                  )}
                </Stack>
              ) : (
                <Stack key={testCode} spacing={1} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "action.hover" }}>
                  <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
                    <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{testCode}</Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {t("water.panel.labReadOnly", { lab: labName(testCode) })}
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
