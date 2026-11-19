import { useEffect, useState } from "react";
import { useTranslation } from "../../../../i18n";
import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import { LimitFields, ChemistryLimitFields, LimitValues, PanelSection, SidePanel, emptyLimits } from "../../../../components/configHierarchy";
import { TestCodePicker } from "../../../../components/TestCodePicker";
import { useEditableTestCodes } from "../../../../hooks/useEditableTestCodes";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { RecordChangedBanner } from "../../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../../hooks/useRecordChanged";
import { useReloadedRecord } from "../../../../hooks/useReloadedRecord";
import { AfterCleaningConfigService } from "../services/AfterCleaningConfigService";
import { AC_COUNT_TYPES, Machine, MachinePart, PATHOGEN_TEST_TYPE, PartConfig } from "../acConfigTypes";
import { cleaningTechniqueOf } from "../cleaningTechniques";
import { PathogenPicker } from "./PathogenPicker";

const errorText = (e: any, fallback: string) => e?.response?.data?.message ?? fallback;

interface MachineProps {
  open: boolean;
  machine: Machine | null;
  onClose: () => void;
  onSaved: (message: string, id?: number) => void;
}

export function AcMachinePanel({ open, machine: openedMachine, onClose, onSaved }: MachineProps) {
  const { t } = useTranslation("labConfig");
  const { current: machine, reload } = useReloadedRecord<Machine>(openedMachine, async (m) =>
    ((await AfterCleaningConfigService.getMachines()) as Machine[]).find((x) => x.id === m.id)
  );
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(machine?.name ?? "");
    setError(null);
  }, [open, machine]);

  const save = async () => {
    if (!name.trim()) return setError(t("afterCleaning.machine.nameRequired"));
    setSaving(true);
    setError(null);
    try {
      if (machine) {
        await AfterCleaningConfigService.updateMachine(machine.id, name.trim(), machine.version);
        noteOwnSave("machine", machine.id);
        onSaved(t("afterCleaning.machine.updated", { name: name.trim() }), machine.id);
      } else {
        const created = await AfterCleaningConfigService.createMachine(name.trim());
        onSaved(t("afterCleaning.machine.added", { name: name.trim() }), created?.id);
      }
    } catch (e) {
      setError(errorText(e, t("afterCleaning.machine.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={t("afterCleaning.machine.overline")}
      title={machine ? t("afterCleaning.machine.renameTitle", { name: machine.name }) : t("afterCleaning.machine.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={machine ? t("afterCleaning.machine.saveChanges") : t("afterCleaning.machine.addTitle")}
      error={error}
    >
      {open && machine && (
        <RecordChangedBanner type="machine" id={machine.id} dirty={name !== machine.name} onReload={() => void reload()} />
      )}
      <TextField autoFocus size="small" label={t("afterCleaning.machine.nameLabel")} required placeholder={t("afterCleaning.machine.namePlaceholder")} value={name} onChange={(e) => setName(e.target.value)} />
    </SidePanel>
  );
}

interface PartProps {
  open: boolean;
  part: MachinePart | null;
  defaultMachineId: number | null;
  machines: Machine[];
  onClose: () => void;
  onSaved: (message: string, machineId?: number) => void;
}

export function AcPartPanel({ open, part: openedPart, defaultMachineId, machines, onClose, onSaved }: PartProps) {
  const { t } = useTranslation("labConfig");
  const { current: part, reload } = useReloadedRecord<MachinePart>(openedPart, async (p) =>
    ((await AfterCleaningConfigService.getMachines()) as Machine[])
      .flatMap((m) => m.parts)
      .find((x) => x.id === p.id)
  );
  const [name, setName] = useState("");
  const [machineId, setMachineId] = useState<number | "">("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(part?.name ?? "");
    setMachineId(part?.machineId ?? defaultMachineId ?? "");
    setError(null);
  }, [open, part, defaultMachineId]);

  const save = async () => {
    if (!name.trim() || machineId === "") return setError(t("afterCleaning.part.required"));
    setSaving(true);
    setError(null);
    try {
      if (part) {
        await AfterCleaningConfigService.updateMachinePart(part.id, name.trim(), Number(machineId), part.version);
        noteOwnSave("machinePart", part.id);
      } else await AfterCleaningConfigService.createMachinePart(name.trim(), Number(machineId));
      onSaved(part ? t("afterCleaning.part.updated", { name: name.trim() }) : t("afterCleaning.part.added", { name: name.trim() }), Number(machineId));
    } catch (e) {
      setError(errorText(e, t("afterCleaning.part.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={machines.find((m) => m.id === machineId)?.name ?? t("afterCleaning.part.overline")}
      title={part ? t("afterCleaning.part.editTitle", { name: part.name }) : t("afterCleaning.part.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={part ? t("afterCleaning.part.saveChanges") : t("afterCleaning.part.addPart")}
      error={error}
    >
      <Stack spacing={1.5}>
        {open && part && (
          <RecordChangedBanner
            type="machinePart"
            id={part.id}
            dirty={name !== part.name || machineId !== part.machineId}
            onReload={() => void reload()}
          />
        )}
        <TextField autoFocus size="small" label={t("afterCleaning.part.partName")} required placeholder={t("afterCleaning.part.partPlaceholder")} value={name} onChange={(e) => setName(e.target.value)} />
        <TextField select size="small" label={t("afterCleaning.part.machine")} required value={machineId} onChange={(e) => setMachineId(Number(e.target.value))}>
          {machines.map((m) => (
            <MenuItem key={m.id} value={m.id}>
              {m.name}
            </MenuItem>
          ))}
        </TextField>
      </Stack>
    </SidePanel>
  );
}

interface CountProps {
  open: boolean;
  part: MachinePart | null;
  config: PartConfig | null;
  onClose: () => void;
  onSaved: (message: string) => void;
  editableCodes?: Set<string>;
}

// A swab or rinse count test or chemistry test for a part, with its limits.
export function AcCountTestPanel({ open, part, config: openedConfig, onClose, onSaved, editableCodes }: CountProps) {
  const { t } = useTranslation("labConfig");
  const { current: config, reload } = useReloadedRecord<PartConfig>(openedConfig, async (c) =>
    ((await AfterCleaningConfigService.getPartConfigurations(c.machinePartId)) as PartConfig[]).find((x) => x.id === c.id)
  );
  // Set when the user changes a field, cleared whenever the form is (re)loaded from the record.
  const [touched, setTouched] = useState(false);
  const { editableCodes: hookEditable } = useEditableTestCodes();
  const effectiveEditable = editableCodes !== undefined ? editableCodes : hookEditable;
  const { options: testDefinitions } = useTestDefinitions();

  const [testType, setTestType] = useState(AC_COUNT_TYPES[0].value);
  const [testCode, setTestCode] = useState("");
  const [limits, setLimits] = useState<LimitValues>(emptyLimits);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeDef = testDefinitions.find((td) => td.code === testCode);
  const isHplc = activeDef?.workflowType === "HplcMethodAssay";
  const isIcp = activeDef?.workflowType === "IcpMethodAssay";
  const isMethodLimit = isHplc || isIcp;
  const isPhyschem = activeDef
    ? activeDef.section?.code !== "MICRO" && activeDef.workflowType !== "CountTest"
    : config?.limitType != null;
  const technique = activeDef ? cleaningTechniqueOf(activeDef.workflowType) : (config?.limitType === "Qualitative" ? "UV" : "HPLC");
  const isUv = technique === "UV";

  useEffect(() => {
    if (!open) return;
    setTestType(config?.testType ?? AC_COUNT_TYPES[0].value);
    setTestCode(config?.testCode ?? "");
    if (config) {
      const def = testDefinitions.find((td) => td.code === config.testCode);
      const isMethod = def?.workflowType === "HplcMethodAssay" || def?.workflowType === "IcpMethodAssay";
      if (isMethod) {
        setLimits(emptyLimits);
      } else {
        const isChem = def ? def.section?.code !== "MICRO" && def.workflowType !== "CountTest" : config.limitType != null;
        const tech = def ? cleaningTechniqueOf(def.workflowType) : (config.limitType === "Qualitative" ? "UV" : "HPLC");
        setLimits({
          alertLimit: config.alertLimit ?? "",
          actionLimit: config.actionLimit ?? "",
          specLimit: config.specLimit ?? "",
          unit: config.unit ?? "",
          limitType: config.limitType ?? (isChem ? (tech === "UV" ? "Qualitative" : "Range") : "Range"),
          lowerLimit: config.lowerLimit ?? null,
          upperLimit: config.upperLimit ?? null,
          lowerInclusive: config.lowerInclusive ?? true,
          upperInclusive: config.upperInclusive ?? true,
          expectedResultText: config.expectedResultText ?? (tech === "UV" ? "No peaks" : "")
        });
      }
    } else {
      setLimits(emptyLimits);
    }
    setTouched(false);
    setError(null);
  }, [open, config, testDefinitions]);

  const handleTestCodeChange = (newCode: string) => {
    setTouched(true);
    setTestCode(newCode);
    const def = testDefinitions.find((td) => td.code === newCode);
    const isMethod = def?.workflowType === "HplcMethodAssay" || def?.workflowType === "IcpMethodAssay";
    if (isMethod) {
      setLimits(emptyLimits);
      return;
    }
    const isChem = def ? def.section?.code !== "MICRO" && def.workflowType !== "CountTest" : false;
    if (isChem) {
      const tech = cleaningTechniqueOf(def?.workflowType);
      if (tech === "UV") {
        setLimits((prev) => ({
          ...prev,
          limitType: "Qualitative",
          expectedResultText: prev.expectedResultText || "No peaks"
        }));
      } else {
        setLimits((prev) => ({
          ...prev,
          limitType: prev.limitType === "Qualitative" ? "Range" : (prev.limitType ?? "Range")
        }));
      }
    }
  };

  const save = async () => {
    if (!part) return;
    if (!testCode) return setError(t("afterCleaning.count.chooseTest"));

    if (isPhyschem && !isMethodLimit) {
      const type = limits.limitType ?? (isUv ? "Qualitative" : "Range");
      if (type === "Range") {
        if (limits.lowerLimit == null && limits.upperLimit == null) {
          return setError(t("afterCleaning.count.rangeNeedsOne"));
        }
        if (limits.lowerLimit != null && limits.upperLimit != null && limits.lowerLimit > limits.upperLimit) {
          return setError(t("afterCleaning.count.lowerGtUpper"));
        }
      } else if (type === "NotMoreThan") {
        if (limits.upperLimit == null) {
          return setError(t("afterCleaning.count.upperRequired"));
        }
      } else if (type === "NotLessThan") {
        if (limits.lowerLimit == null) {
          return setError(t("afterCleaning.count.lowerRequired"));
        }
      } else if (type === "Qualitative") {
        if (!limits.expectedResultText || limits.expectedResultText.trim() === "") {
          return setError(t("afterCleaning.count.expectedRequired"));
        }
      }
    }

    setSaving(true);
    setError(null);

    let spec = limits.specLimit;
    if (isPhyschem && !isMethodLimit) {
      const type = limits.limitType ?? (isUv ? "Qualitative" : "Range");
      if (type === "Range") {
        if (limits.lowerLimit != null && limits.upperLimit != null) spec = `${limits.lowerLimit}-${limits.upperLimit}`;
        else if (limits.lowerLimit != null) spec = `NLT ${limits.lowerLimit}`;
        else if (limits.upperLimit != null) spec = `NMT ${limits.upperLimit}`;
      } else if (type === "NotMoreThan") {
        spec = limits.upperLimit != null ? `NMT ${limits.upperLimit}` : "";
      } else if (type === "NotLessThan") {
        spec = limits.lowerLimit != null ? `NLT ${limits.lowerLimit}` : "";
      } else if (type === "Qualitative") {
        spec = limits.expectedResultText ?? "";
      }
    }

    const extra = isPhyschem && !isMethodLimit
      ? {
          limitType: limits.limitType ?? (isUv ? "Qualitative" : "Range"),
          lowerLimit: limits.lowerLimit ?? null,
          upperLimit: limits.upperLimit ?? null,
          lowerInclusive: limits.lowerInclusive ?? true,
          upperInclusive: limits.upperInclusive ?? true,
          expectedResultText: limits.expectedResultText ?? null
        }
      : undefined;

    const alertLimit = isMethodLimit ? "" : limits.alertLimit;
    const actionLimit = isMethodLimit ? "" : limits.actionLimit;
    const specLimit = isMethodLimit ? "" : (isPhyschem ? spec : limits.specLimit);
    const unit = isMethodLimit ? "" : limits.unit;

    try {
      if (config) {
        await AfterCleaningConfigService.updatePartConfiguration(
          config.id,
          testType,
          testCode,
          alertLimit,
          actionLimit,
          specLimit,
          false,
          unit,
          config.version,
          extra
        );
        noteOwnSave("machinePartConfiguration", config.id);
      } else {
        await AfterCleaningConfigService.createPartConfiguration(
          part.id,
          testType,
          testCode,
          alertLimit,
          actionLimit,
          specLimit,
          false,
          unit,
          extra
        );
      }
      onSaved(config ? t("afterCleaning.count.updated", { part: part.name }) : t("afterCleaning.count.added", { part: part.name }));
    } catch (e) {
      setError(errorText(e, t("afterCleaning.count.saveError")));
    } finally {
      setSaving(false);
    }
  };

  const unitHint = AC_COUNT_TYPES.find((ct) => ct.value === testType)?.unitHint;

  return (
    <SidePanel
      open={open}
      overline={part?.name}
      title={config ? (isPhyschem ? t("afterCleaning.count.editTest") : t("afterCleaning.count.editCount")) : (isPhyschem ? t("afterCleaning.count.addTest") : t("afterCleaning.count.addSwabRinse"))}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={config ? t("afterCleaning.count.saveChanges") : t("afterCleaning.count.addTest")}
      error={error}
    >
      {open && config && (
        <RecordChangedBanner type="machinePartConfiguration" id={config.id} dirty={touched} onReload={() => void reload()} />
      )}
      <PanelSection title={t("afterCleaning.count.section1")}>
        <TextField select size="small" label={t("afterCleaning.count.sampleType")} value={testType} onChange={(e) => { setTouched(true); setTestType(e.target.value); }}>
          {AC_COUNT_TYPES.map((ct) => (
            <MenuItem key={ct.value} value={ct.value}>
              {t(`afterCleaning.sampleTypes.${ct.value}`, { defaultValue: ct.label })}
            </MenuItem>
          ))}
        </TextField>
        <TestCodePicker value={testCode} onChange={handleTestCodeChange} label={t("afterCleaning.count.test")} editableCodes={effectiveEditable ?? new Set<string>()} />
      </PanelSection>
      <PanelSection
        title={t("afterCleaning.count.section2")}
        hint={
          isMethodLimit
            ? undefined
            : isPhyschem
            ? (isUv ? t("afterCleaning.count.hintUv") : t("afterCleaning.count.hintChem"))
            : t("afterCleaning.count.hintCount", { type: t(`afterCleaning.sampleTypes.${testType}`, { defaultValue: testType }).toLowerCase(), unit: unitHint })
        }
      >
        {isMethodLimit ? (
          <Typography variant="body2" sx={{ color: "text.secondary", py: 1 }}>
            {isHplc ? t("afterCleaning.count.limitFromMethod") : t("afterCleaning.count.limitFromIcp")}
          </Typography>
        ) : isPhyschem ? (
          <ChemistryLimitFields idPrefix="ac-limit" value={limits} onChange={(v) => { setTouched(true); setLimits(v); }} unitPlaceholder={t("afterCleaning.count.unitChem")} />
        ) : (
          <LimitFields idPrefix="ac-limit" value={limits} onChange={(v) => { setTouched(true); setLimits(v); }} unitPlaceholder={unitHint} />
        )}
      </PanelSection>
    </SidePanel>
  );
}

interface PathogenProps {
  open: boolean;
  part: MachinePart | null;
  // The part's current pathogen configurations.
  configs: PartConfig[];
  onClose: () => void;
  onSaved: (message: string) => void;
  editableCodes?: Set<string>;
}

// Every pathogen test for a part in one place: pick any number of
// pathogens; saving adds the new ones and removes the unticked ones
// (each is its own configuration row, so each gets its own TestOrder).
export function AcPathogenPanel({ open, part, configs, onClose, onSaved, editableCodes }: PathogenProps) {
  const { t } = useTranslation("labConfig");
  const { editableCodes: hookEditable } = useEditableTestCodes();
  const effectiveEditable = editableCodes !== undefined ? editableCodes : hookEditable;

  const [codes, setCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setCodes(configs.map((c) => c.testCode));
    setError(null);
  }, [open, configs]);

  const added = codes.filter((code) => !configs.some((c) => c.testCode === code));
  const removed = configs.filter((c) => !codes.includes(c.testCode));

  const save = async () => {
    if (!part) return;
    setSaving(true);
    setError(null);
    const failed: string[] = [];
    for (const code of added) {
      if (effectiveEditable !== undefined && !effectiveEditable.has(code)) continue;
      try {
        await AfterCleaningConfigService.createPartConfiguration(part.id, PATHOGEN_TEST_TYPE, code, "", "", "", true, "");
      } catch (e) {
        failed.push(`${code}: ${errorText(e, t("afterCleaning.pathogenPanel.couldNotAdd"))}`);
      }
    }
    const safeRemoved = removed.filter((c) => effectiveEditable === undefined || effectiveEditable.has(c.testCode));
    for (const c of safeRemoved) {
      try {
        await AfterCleaningConfigService.deletePartConfiguration(c.id);
      } catch (e) {
        failed.push(`${c.testCode}: ${errorText(e, t("afterCleaning.pathogenPanel.couldNotRemove"))}`);
      }
    }
    setSaving(false);
    if (failed.length > 0) {
      setError(t("afterCleaning.pathogenPanel.someNotSaved", { failures: failed.join(" ") }));
      return;
    }
    onSaved(t("afterCleaning.pathogenPanel.updated", { part: part.name }));
  };

  return (
    <SidePanel
      open={open}
      overline={part?.name}
      title={t("afterCleaning.pathogenPanel.title")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={t("afterCleaning.pathogenPanel.save")}
      error={error}
    >
      <PanelSection
        title={t("afterCleaning.pathogenPanel.sectionTitle")}
        hint={t("afterCleaning.pathogenPanel.sectionHint")}
      >
        <PathogenPicker value={codes} onChange={setCodes} editableCodes={effectiveEditable ?? new Set<string>()} />
        {(added.length > 0 || removed.length > 0) && (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {[added.length > 0 && t("afterCleaning.pathogenPanel.toAdd", { count: added.length }), removed.length > 0 && t("afterCleaning.pathogenPanel.toRemove", { count: removed.length })].filter(Boolean).join(", ")}.
          </Typography>
        )}
      </PanelSection>
    </SidePanel>
  );
}
